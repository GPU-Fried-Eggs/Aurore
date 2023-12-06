using System;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

[DisableAutoCreation]
[RequireMatchingQueriesForUpdate]
public partial struct AnimationProcessSystem: ISystem
{
	private EntityQuery m_AnimatedObjectQuery;
	private NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>> m_RigToSkinnedMeshRemapTables;
	private NativeList<int2> m_BonePosesOffsetsArr;

	[BurstCompile]
	public void OnCreate(ref SystemState state)
	{
		InitializeRuntimeData(ref state);

		m_BonePosesOffsetsArr = new (Allocator.Persistent);

		using var builder = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<RigDefinitionComponent, AnimationToProcessComponent>();
		m_AnimatedObjectQuery = state.GetEntityQuery(builder);
	}

	[BurstCompile]
	public void OnDestroy(ref SystemState state)
	{
		if (m_RigToSkinnedMeshRemapTables.IsCreated)
			m_RigToSkinnedMeshRemapTables.Dispose();

		if (m_BonePosesOffsetsArr.IsCreated)
			m_BonePosesOffsetsArr.Dispose();

		if (SystemAPI.TryGetSingleton<RuntimeAnimationData>(out var rad))
		{
			rad.Dispose();
			state.EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<RuntimeAnimationData>());
		}
	}

	private void InitializeRuntimeData(ref SystemState state)
	{
		var rad = RuntimeAnimationData.MakeDefault();
		state.EntityManager.CreateSingleton(rad, "Animation.RuntimeAnimationData");
	}

	private JobHandle PrepareComputationData(ref SystemState state, NativeArray<int> chunkBaseEntityIndices, ref RuntimeAnimationData runtimeData, NativeList<Entity> entitiesArr, JobHandle dependsOn)
	{
		var rigDefinitionTypeHandle = SystemAPI.GetComponentTypeHandle<RigDefinitionComponent>(true);
		
		//	Calculate bone offsets per entity
		var calcBoneOffsetsJob = new CalculateBoneOffsetsJob
		{
			ChunkBaseEntityIndices = chunkBaseEntityIndices,
			BonePosesOffsets = m_BonePosesOffsetsArr.AsArray(),
			RigDefinitionTypeHandle = rigDefinitionTypeHandle
		};

		var jh = calcBoneOffsetsJob.ScheduleParallel(m_AnimatedObjectQuery, dependsOn);

		//	Do prefix sum to calculate absolute offsets
		var prefixSumJob = new DoPrefixSumJob
		{
			BoneOffsets = m_BonePosesOffsetsArr.AsArray()
		};

		prefixSumJob.Schedule(jh).Complete();

		var boneBufferLen = m_BonePosesOffsetsArr[^1];
		runtimeData.AnimatedBonesBuffer.Resize(boneBufferLen.x, NativeArrayOptions.UninitializedMemory);
		runtimeData.BoneToEntityArr.Resize(boneBufferLen.x, NativeArrayOptions.UninitializedMemory);
		runtimeData.EntityToDataOffsetMap.Capacity = math.max(boneBufferLen.x, runtimeData.EntityToDataOffsetMap.Capacity);

		//	Clear flags by two resizes
		runtimeData.BoneTransformFlagsHolderArr.Resize(0, NativeArrayOptions.UninitializedMemory);
		runtimeData.BoneTransformFlagsHolderArr.Resize(boneBufferLen.y, NativeArrayOptions.ClearMemory);
		
		runtimeData.EntityToDataOffsetMap.Clear();

		//	Fill boneToEntityArr with proper values
		var boneToEntityArrFillJob = new CalculatePerBoneInfoJob
		{
			BonePosesOffsets = m_BonePosesOffsetsArr,
			BoneToEntityIndices = runtimeData.BoneToEntityArr,
			ChunkBaseEntityIndices = chunkBaseEntityIndices,
			RigDefinitionTypeHandle = rigDefinitionTypeHandle,
			Entities = entitiesArr,
			EntityToDataOffsetMap = runtimeData.EntityToDataOffsetMap.AsParallelWriter()
		};

		return boneToEntityArrFillJob.ScheduleParallel(m_AnimatedObjectQuery, default);
	}

	private JobHandle AnimationCalculation(ref SystemState state, NativeList<Entity> entitiesArr, in RuntimeAnimationData runtimeData, JobHandle dependsOn)
	{
		var animationToProcessBufferLookup = SystemAPI.GetBufferLookup<AnimationToProcessComponent>(true);
		var rootMotionAnimationStateBufferLookupRW = SystemAPI.GetBufferLookup<RootMotionAnimationStateComponent>();

		var rigDefsArr = m_AnimatedObjectQuery.ToComponentDataListAsync<RigDefinitionComponent>(state.WorldUpdateAllocator, out var rigDefsLookupJh);
		var dataGatherJh = JobHandle.CombineDependencies(rigDefsLookupJh, dependsOn);

		var computeAnimationsJob = new ComputeBoneAnimationJob
		{
			AnimationsToProcessLookup = animationToProcessBufferLookup,
			EntityArr = entitiesArr,
			RigDefs = rigDefsArr,
			BoneTransformFlagsArr = runtimeData.BoneTransformFlagsHolderArr,
			AnimatedBonesBuffer = runtimeData.AnimatedBonesBuffer,
			BoneToEntityArr = runtimeData.BoneToEntityArr,
			RootMotionAnimStateBufferLookup = rootMotionAnimationStateBufferLookupRW,
		};

		return computeAnimationsJob.ScheduleBatch(runtimeData.AnimatedBonesBuffer.Length, 16, dataGatherJh);
	}

	private JobHandle ProcessUserCurves(ref SystemState state, JobHandle dependsOn)
	{
		var userCurveProcessJob = new ProcessUserCurvesJob();

		return userCurveProcessJob.ScheduleParallel(dependsOn);
	}

	private JobHandle CopyEntityBonesToAnimationTransforms(ref SystemState state, ref RuntimeAnimationData runtimeData, JobHandle dependsOn)
	{
		var rigDefinitionLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true);
		var parentComponentLookup = SystemAPI.GetComponentLookup<Parent>();
			
		//	Now take available entity transforms as ref poses overrides
		var copyEntityBoneTransforms = new CopyEntityBoneTransformsToAnimationBuffer
		{
			RigDefComponentLookup = rigDefinitionLookup,
			BoneTransformFlags = runtimeData.BoneTransformFlagsHolderArr,
			EntityToDataOffsetMap = runtimeData.EntityToDataOffsetMap,
			AnimatedBoneTransforms = runtimeData.AnimatedBonesBuffer,
			ParentComponentLookup = parentComponentLookup,
		};

		return copyEntityBoneTransforms.ScheduleParallel(dependsOn);
	}

	[BurstCompile]
	public void OnUpdate(ref SystemState state)
	{
		var entityCount = m_AnimatedObjectQuery.CalculateEntityCount();
		if (entityCount == 0) return;
		
		ref var runtimeData = ref SystemAPI.GetSingletonRW<RuntimeAnimationData>().ValueRW;

		m_BonePosesOffsetsArr.Resize(entityCount + 1, NativeArrayOptions.UninitializedMemory);
		var chunkBaseEntityIndices = m_AnimatedObjectQuery.CalculateBaseEntityIndexArrayAsync(state.WorldUpdateAllocator, state.Dependency, out var baseIndexCalcJh);
		var entitiesArr = m_AnimatedObjectQuery.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var entityArrJh);

		var combinedJh = JobHandle.CombineDependencies(baseIndexCalcJh, entityArrJh);

		//	Define array with bone pose offsets for calculated bone poses
		var calcBoneOffsetsJh = PrepareComputationData(ref state, chunkBaseEntityIndices, ref runtimeData, entitiesArr, combinedJh);

		//	User curve calculus
		var userCurveProcessJobHandle = ProcessUserCurves(ref state, calcBoneOffsetsJh);

		//	Spawn jobs for animation calculation
		var computeAnimationJobHandle = AnimationCalculation(ref state, entitiesArr, runtimeData, userCurveProcessJobHandle);

		//	Copy entities poses into animation buffer for non-animated parts
		var copyEntityTransformsIntoAnimationBufferJh = CopyEntityBonesToAnimationTransforms(ref state, ref runtimeData, computeAnimationJobHandle);

		state.Dependency = copyEntityTransformsIntoAnimationBufferJh;
	}

	[BurstCompile]
	public struct ComputeBoneAnimationJob: IJobParallelForBatch
	{
		[WriteOnly, NativeDisableContainerSafetyRestriction]
		public NativeList<BoneTransform> AnimatedBonesBuffer;
		[WriteOnly, NativeDisableContainerSafetyRestriction]
		public NativeList<ulong> BoneTransformFlagsArr;
		[ReadOnly] public NativeList<int3> BoneToEntityArr;
		[ReadOnly] public BufferLookup<AnimationToProcessComponent> AnimationsToProcessLookup;
		[ReadOnly] public NativeList<RigDefinitionComponent> RigDefs;
		[ReadOnly] public NativeList<Entity> EntityArr;
		
		[NativeDisableParallelForRestriction]
		public BufferLookup<RootMotionAnimationStateComponent> RootMotionAnimStateBufferLookup;
	
		public void Execute(int startIndex, int count)
		{
			for (int i = startIndex; i < startIndex + count; ++i)
				ExecuteSingle(i);
		}

		private void ExecuteSingle(int globalBoneIndex)
		{
			var boneToEntityIndex = BoneToEntityArr[globalBoneIndex];
			var (rigBoneIndex, entityIndex) = (boneToEntityIndex.y, boneToEntityIndex.x);
			var e = EntityArr[entityIndex];
	
			var rigDef = RigDefs[entityIndex];
			var rigBlobAsset = rigDef.RigBlob;
			ref var rb = ref rigBlobAsset.Value.Bones[rigBoneIndex];
			var animationsToProcess = AnimationsToProcessLookup[e];
	
			//	Early exit if no animations
			if (animationsToProcess.IsEmpty)
				return;
	
			var transformFlags = RuntimeAnimationData.GetAnimationTransformFlagsRW(BoneToEntityArr, BoneTransformFlagsArr, globalBoneIndex, rigBlobAsset.Value.Bones.Length);
			GetHumanRotationDataForSkeletonBone(out var humanBoneInfo, ref rigBlobAsset.Value.HumanData, rigBoneIndex);
	
			Span<float> layerWeights = stackalloc float[32];
			var refPosWeight = CalculateFinalLayerWeights(layerWeights, animationsToProcess, rb.Hash, rb.HumanBodyPart);
			float3 totalWeights = refPosWeight;
	
			var blendedBonePose = BoneTransform.TransformScale(rb.RefPose, refPosWeight);
	
			var rootMotionDeltaBone = rigDef.ApplyRootMotion && rigBoneIndex == 0;
			PrepareRootMotionStateBuffers(e, animationsToProcess, out var curRootMotionState, out var newRootMotionState, rootMotionDeltaBone);
	
			for (int i = 0; i < animationsToProcess.Length; ++i)
			{
				var atp = animationsToProcess[i];
	
				var animTime = NormalizeAnimationTime(atp.Time, ref atp.Animation.Value);
	
				var layerWeight = layerWeights[atp.LayerIndex];
				if (layerWeight == 0) continue;
	
				var boneNameHash = rb.Hash;
				if (rigDef.ApplyRootMotion && (rigBlobAsset.Value.RootBoneIndex == rigBoneIndex || rigBoneIndex == 0))
					ModifyBoneHashForRootMotion(ref boneNameHash);
				
				var animationBoneIndex = GetBoneIndexByHash(ref atp.Animation.Value, boneNameHash);
	
				if (Hint.Likely(animationBoneIndex >= 0))
				{
					// Loop Pose calculus for all bones except root motion
					var calculateLoopPose = atp.Animation.Value.LoopPoseBlend && rigBoneIndex != 0;
					var additiveReferencePoseTime = math.select(-1.0f, atp.Animation.Value.AdditiveReferencePoseTime, atp.BlendMode == AnimationBlendingMode.Additive);
					
					ref var boneAnimation = ref atp.Animation.Value.Bones[animationBoneIndex];
					var (bonePose, flags) = SampleAnimation(ref boneAnimation, animTime, atp, calculateLoopPose, additiveReferencePoseTime, humanBoneInfo);
					SetTransformFlags(flags, transformFlags, rigBoneIndex);
	
					float3 modWeight = flags * atp.Weight * layerWeight;
					totalWeights += modWeight;
	
					if (rootMotionDeltaBone)
						ProcessRootMotionDeltas(ref bonePose, ref boneAnimation, atp, i, curRootMotionState, newRootMotionState);
					
					MixPoses(ref blendedBonePose, bonePose, modWeight, atp.BlendMode);
				}
			}
	
			//	Reference pose for root motion delta should be identity
			var boneRefPose = Hint.Unlikely(rootMotionDeltaBone) ? BoneTransform.Identity : rb.RefPose;
			
			BoneTransformMakePretty(ref blendedBonePose, boneRefPose, totalWeights);
			AnimatedBonesBuffer[globalBoneIndex] = blendedBonePose;
	
			if (rootMotionDeltaBone)
				SetRootMotionStateToComponentBuffer(newRootMotionState, e);
		}
	
		public static void ModifyBoneHashForRootMotion(ref Hash128 h)
		{
			h.Value.z = 0xbaad;
			h.Value.w = 0xf00d;
		}

		private int GetBoneIndexByHash(ref AnimationClipBlob acb, in Hash128 boneHash)
		{
			var queryIndex = PerfectHash<Hash128PerfectHashed>.QueryPerfectHashTable(ref acb.BonesPerfectHashSeedTable, boneHash);
			if (queryIndex >= acb.Bones.Length || queryIndex < 0)
				return -1;
			var candidateBoneHash = acb.Bones[queryIndex].Hash;
			return candidateBoneHash == boneHash ? queryIndex : -1;
		}

		private void PrepareRootMotionStateBuffers
		(
			Entity e,
			in DynamicBuffer<AnimationToProcessComponent> atps,
			out NativeArray<RootMotionAnimationStateComponent> curRootMotionState,
			out NativeArray<RootMotionAnimationStateComponent> newRootMotionState,
			bool isRootMotionBone
		)
		{
			curRootMotionState = default;
			newRootMotionState = default;
	
			if (Hint.Likely(!isRootMotionBone)) return;
	
			if (RootMotionAnimStateBufferLookup.HasBuffer(e))
				curRootMotionState = RootMotionAnimStateBufferLookup[e].AsNativeArray();
	
			newRootMotionState = new NativeArray<RootMotionAnimationStateComponent>(atps.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
		}

		private void ProcessRootMotionDeltas
		(
			ref BoneTransform bonePose,
			ref BoneClipBlob boneAnimation,
			in AnimationToProcessComponent atp,
			int animationIndex,
			in NativeArray<RootMotionAnimationStateComponent> curRootMotionState,
			NativeArray<RootMotionAnimationStateComponent> newRootMotionState
		)
		{
			//	Special care for root motion animation loops
			HandleRootMotionLoops(ref bonePose, ref boneAnimation, atp);
		
			BoneTransform rootMotionPrevPose = bonePose;
	
			// Find animation history in history buffer
			var historyBufferIndex = 0;
			for (; curRootMotionState.IsCreated && historyBufferIndex < curRootMotionState.Length && curRootMotionState[historyBufferIndex].AnimationHash != atp.Animation.Value.Hash; ++historyBufferIndex){ }
	
			var initialFrame = historyBufferIndex >= curRootMotionState.Length;
	
			if (Hint.Unlikely(!initialFrame))
			{
				rootMotionPrevPose = curRootMotionState[historyBufferIndex].AnimationState;
			}
	
			newRootMotionState[animationIndex] = new RootMotionAnimationStateComponent { AnimationHash = atp.Animation.Value.Hash, AnimationState = bonePose };
	
			var invPrevPose = BoneTransform.Inverse(rootMotionPrevPose);
			var deltaPose = BoneTransform.Multiply(invPrevPose, bonePose);
	
			bonePose = deltaPose;
		}

		private void SetRootMotionStateToComponentBuffer(in NativeArray<RootMotionAnimationStateComponent> newRootMotionData, Entity e)
		{
			RootMotionAnimStateBufferLookup[e].CopyFrom(newRootMotionData);
		}

		private void SetTransformFlags(float3 flags, in AnimationTransformFlags flagArr, int boneIndex)
		{
			if (flags.x > 0)
				flagArr.SetTranslationFlag(boneIndex);
			if (flags.y > 0)
				flagArr.SetRotationFlag(boneIndex);
			if (flags.z > 0)
				flagArr.SetScaleFlag(boneIndex);
		}

		private void GetHumanRotationDataForSkeletonBone(out HumanRotationData rv, ref BlobPtr<HumanData> hd, int rigBoneIndex)
		{
			rv = default;
			if (hd.IsValid)
			{
				rv = hd.Value.HumanRotData[rigBoneIndex];
			}
		}
	
		internal static float3 MuscleRangeToRadians(float3 minA, float3 maxA, float3 muscle)
		{
			//	Map [-1; +1] range into [minRot; maxRot]
			var negativeRange = math.min(muscle, 0);
			var positiveRange = math.max(0, muscle);
			var negativeRot = math.lerp(0, minA, -negativeRange);
			var positiveRot = math.lerp(0, maxA, +positiveRange);
	
			var rv = negativeRot + positiveRot;
			return rv;
		}

		private void MuscleValuesToQuaternion(in HumanRotationData humanBoneInfo, ref BoneTransform bt)
		{
			var r = MuscleRangeToRadians(humanBoneInfo.MinMuscleAngles, humanBoneInfo.MaxMuscleAngles, bt.Rotation.value.xyz);
			r *= humanBoneInfo.Sign;
	
			var qx = quaternion.AxisAngle(math.right(), r.x);
			var qy = quaternion.AxisAngle(math.up(), r.y);
			var qz = quaternion.AxisAngle(math.forward(), r.z);
			var qzy = math.mul(qz, qy);
			qzy.value.x = 0;
			bt.Rotation = math.mul(math.normalize(qzy), qx);
	
			ApplyHumanoidPostTransform(humanBoneInfo, ref bt);
		}
	
		public static float2 NormalizeAnimationTime(float at, ref AnimationClipBlob ac)
		{
			at += ac.CycleOffset;
			var normalizedTime = ac.Looped ? math.frac(at) : math.saturate(at);
			var rv = normalizedTime * ac.Length;
			return new (rv, normalizedTime);
		}

		private void CalculateLoopPose(ref BoneClipBlob boneAnimation, AnimationToProcessComponent atp, ref BoneTransform bonePose, in HumanRotationData hrd, float normalizedTime)
		{
			var animLen = atp.Animation.Value.Length;
			var lerpFactor = normalizedTime;
			var (rootPoseStart, _) = ProcessAnimationCurves(ref boneAnimation, hrd, 0);
			var (rootPoseEnd, _) = ProcessAnimationCurves(ref boneAnimation, hrd, animLen);
	
			var dPos = rootPoseEnd.Position - rootPoseStart.Position;
			var dRot = math.mul(math.conjugate(rootPoseEnd.Rotation), rootPoseStart.Rotation);
			bonePose.Position -= dPos * lerpFactor;
			bonePose.Rotation = math.mul(bonePose.Rotation, math.slerp(quaternion.identity, dRot, lerpFactor));
		}

		private void HandleRootMotionLoops(ref BoneTransform bonePose, ref BoneClipBlob boneAnimation, in AnimationToProcessComponent atp)
		{
			ref var animBlob = ref atp.Animation.Value;
			if (!animBlob.Looped)
				return;
	
			var numLoopCycles = (int)math.floor(atp.Time + atp.Animation.Value.CycleOffset);
			if (numLoopCycles < 1)
				return;
	
			var animLen = atp.Animation.Value.Length;
			var (endFramePose, _) = SampleAnimation(ref boneAnimation, animLen, atp, false, -1);
			var (startFramePose, _) = SampleAnimation(ref boneAnimation, 0, atp, false, -1);
	
			var deltaPose = BoneTransform.Multiply(endFramePose, BoneTransform.Inverse(startFramePose));
	
			BoneTransform accumCyclePose = BoneTransform.Identity;
			for (var c = numLoopCycles; c > 0; c >>= 1)
			{
				if ((c & 1) == 1)
					accumCyclePose = BoneTransform.Multiply(accumCyclePose, deltaPose);
				deltaPose = BoneTransform.Multiply(deltaPose, deltaPose);
			}
			bonePose = BoneTransform.Multiply(accumCyclePose, bonePose);
		}

		private void MixPoses(ref BoneTransform curPose, BoneTransform inPose, float3 weight, AnimationBlendingMode blendMode)
		{
			if (blendMode == AnimationBlendingMode.Override)
			{
				inPose.Rotation = MathUtils.ShortestRotation(curPose.Rotation, inPose.Rotation);
				var scaledPose = BoneTransform.TransformScale(inPose, weight);
	
				curPose.Position += scaledPose.Position;
				curPose.Rotation.value += scaledPose.Rotation.value;
				curPose.Scale += scaledPose.Scale;
			}
			else
			{
				curPose.Position += inPose.Position * weight.x;
				quaternion layerRot = math.normalizesafe(new float4(inPose.Rotation.value.xyz * weight.y, inPose.Rotation.value.w));
				layerRot = MathUtils.ShortestRotation(curPose.Rotation, layerRot);
				curPose.Rotation = math.mul(layerRot, curPose.Rotation);
				curPose.Scale *= (1 - weight.z) + (inPose.Scale * weight.z);
			}
		}
	
		public static float CalculateFinalLayerWeights(in Span<float> layerWeights, in DynamicBuffer<AnimationToProcessComponent> atp, in Hash128 boneHash, AvatarMaskBodyPart humanAvatarMaskBodyPart)
		{
			var layerIndex = -1;
			var w = 1.0f;
			var refPoseWeight = 1.0f;
	
			for (int i = atp.Length - 1; i >= 0; --i)
			{
				var a = atp[i];
				if (a.LayerIndex == layerIndex) continue;
	
				var inAvatarMask = IsBoneInAvatarMask(boneHash, humanAvatarMaskBodyPart, a.AvatarMask);
				var layerWeight = inAvatarMask ? a.LayerWeight : 0;
	
				var lw = w * layerWeight;
				layerWeights[a.LayerIndex] = lw;
				refPoseWeight -= lw;
				if (a.BlendMode == AnimationBlendingMode.Override)
					w = w * (1 - layerWeight);
				layerIndex = a.LayerIndex;
			}
			return atp[0].BlendMode == AnimationBlendingMode.Override ? 0 : layerWeights[0];
		}

		private void ApplyHumanoidPostTransform(HumanRotationData hrd, ref BoneTransform bt)
		{
			bt.Rotation = math.mul(math.mul(hrd.PreRot, bt.Rotation), hrd.PostRot);
		}

		private void BoneTransformMakePretty(ref BoneTransform bt, BoneTransform refPose, float3 weights)
		{
			var complWeights = math.saturate(new float3(1) - weights);
			bt.Position += refPose.Position * complWeights.x;
			var shortestRefRot = MathUtils.ShortestRotation(bt.Rotation.value, refPose.Rotation.value);
			bt.Rotation.value += shortestRefRot.value * complWeights.y;
			bt.Scale += refPose.Scale * complWeights.z;
	
			bt.Rotation = math.normalize(bt.Rotation);
		}
	
		public static bool IsBoneInAvatarMask(in Hash128 boneHash, AvatarMaskBodyPart humanAvatarMaskBodyPart, ExternalBlobPtr<AvatarMaskBlob> am)
		{
			// If no avatar mask defined or bone hash is all zeroes assume that bone included
			if (!am.IsCreated || !math.any(boneHash.Value))
				return true;
	
			return (int)humanAvatarMaskBodyPart >= 0 ?
				IsBoneInHumanAvatarMask(humanAvatarMaskBodyPart, am) :
				IsBoneInGenericAvatarMask(boneHash, am);
		}
	
		public static bool IsBoneInHumanAvatarMask(AvatarMaskBodyPart humanBoneAvatarMaskIndex, ExternalBlobPtr<AvatarMaskBlob> am)
		{
			var rv = (am.Value.HumanBodyPartsAvatarMask & 1 << (int)humanBoneAvatarMaskIndex) != 0;
			return rv;
		}
	
		public static bool IsBoneInGenericAvatarMask(in Hash128 boneHash, ExternalBlobPtr<AvatarMaskBlob> am)
		{
			for (int i = 0; i < am.Value.IncludedBoneHashes.Length; ++i)
			{
				var avatarMaskBoneHash = am.Value.IncludedBoneHashes[i];
				if (avatarMaskBoneHash == boneHash)
					return true;
			}
			return false;
		}

		private (BoneTransform, float3) SampleAnimation
		(
			ref BoneClipBlob bcb,
			float2 animTime,
			in AnimationToProcessComponent atp,
			bool calculateLoopPose, 
			float additiveReferencePoseTime,
			in HumanRotationData hrd = default
		)
		{
			var time = animTime.x;
			var timeNrm = animTime.y;
	
			var (bonePose, flags) = ProcessAnimationCurves(ref bcb, hrd, time);
			
			//	Make additive animation if requested
			if (Hint.Unlikely(additiveReferencePoseTime >= 0))
			{
				var (zeroFramePose, _) = ProcessAnimationCurves(ref bcb, hrd, additiveReferencePoseTime);
				MakeAdditiveAnimation(ref bonePose, zeroFramePose);
			}
			
			if (Hint.Unlikely(calculateLoopPose))
			{
				CalculateLoopPose(ref bcb, atp, ref bonePose, hrd, timeNrm);
			}
			
			return (bonePose, flags);
		}

		private void MakeAdditiveAnimation(ref BoneTransform rv, in BoneTransform zeroFramePose)
		{
			//	If additive layer make difference between reference pose and current animated pose
			rv.Position = rv.Position - zeroFramePose.Position;
			var conjugateZfRot = math.normalizesafe(math.conjugate(zeroFramePose.Rotation));
			conjugateZfRot = MathUtils.ShortestRotation(rv.Rotation, conjugateZfRot);
			rv.Rotation = math.mul(math.normalize(rv.Rotation), conjugateZfRot);
			rv.Scale = rv.Scale / zeroFramePose.Scale;
		}

		private (BoneTransform, float3) ProcessAnimationCurves(ref BoneClipBlob bcb, HumanRotationData hrd, float time)
		{
			var rv = BoneTransform.Identity;
	
			bool eulerToQuaternion = false;
	
			float3 flags = 0;
			for (int i = 0; i < bcb.AnimationCurves.Length; ++i)
			{
				ref var ac = ref bcb.AnimationCurves[i];
				var interpolatedCurveValue = BlobCurve.SampleAnimationCurve(ref ac.KeyFrames, time);
	
				switch (ac.BindingType)
				{
				case BindingType.Translation:
					rv.Position[ac.ChannelIndex] = interpolatedCurveValue;
					flags.x = 1;
					break;
				case BindingType.Quaternion:
					rv.Rotation.value[ac.ChannelIndex] = interpolatedCurveValue;
					flags.y = 1;
					break;
				case BindingType.EulerAngles:
					eulerToQuaternion = true;
					rv.Rotation.value[ac.ChannelIndex] = interpolatedCurveValue;
					flags.y = 1;
					break;
				case BindingType.HumanMuscle:
					rv.Rotation.value[ac.ChannelIndex] = interpolatedCurveValue;
					flags.y = 1;
					break;
				case BindingType.Scale:
					rv.Scale[ac.ChannelIndex] = interpolatedCurveValue;
					flags.z = 1;
					break;
				default:
					Debug.Assert(false, "Unknown binding type!");
					break;
				}
			}
	
			//	If we have got Euler angles instead of quaternion, convert them here
			if (eulerToQuaternion)
			{
				rv.Rotation = quaternion.Euler(math.radians(rv.Rotation.value.xyz));
			}
	
			if (bcb.IsHumanMuscleClip)
			{
				MuscleValuesToQuaternion(hrd, ref rv);
			}
	
			return (rv, flags);
		}
	}
	
	[BurstCompile]
	private partial struct ProcessUserCurvesJob: IJobEntity
	{
		private void Execute(AnimatorParametersAspect apa, in DynamicBuffer<AnimationToProcessComponent> animationsToProcess)
		{
			if (animationsToProcess.IsEmpty) return;
	
			Span<float> layerWeights = stackalloc float[32];
			var isSetByCurve = new BitField64();
			Span<float> finalParamValues = stackalloc float[apa.ParametersCount()];
			finalParamValues.Clear();
	
			ComputeBoneAnimationJob.CalculateFinalLayerWeights(layerWeights, animationsToProcess, new Hash128(), (AvatarMaskBodyPart)(-1));
	
			for (int l = 0; l < animationsToProcess.Length; ++l)
			{
				var atp = animationsToProcess[l];
				var animTime = ComputeBoneAnimationJob.NormalizeAnimationTime(atp.Time, ref atp.Animation.Value);
				var layerWeight = layerWeights[atp.LayerIndex];
				ref var curves = ref atp.Animation.Value.Curves;
				for (int k = 0; k < curves.Length; ++k)
				{
					ref var c = ref curves[k];
					var paramHash = c.Hash.Value.x;
					var paramIdx = apa.GetParameterIndex(new FastAnimatorParameter(paramHash));
					if (paramIdx < 0) continue;
	
					isSetByCurve.SetBits(paramIdx, true);
					var curveValue = SampleUserCurve(ref c.AnimationCurves[0].KeyFrames, atp, animTime.x);
	
					if (atp.Animation.Value.LoopPoseBlend)
						curveValue -= CalculateLoopPose(ref c.AnimationCurves[0].KeyFrames, atp, animTime.y);
	
					finalParamValues[paramIdx] += curveValue * atp.Weight * layerWeight;
				}
			}
	
			for (int l = 0; l < apa.ParametersCount(); ++l)
			{
				if (isSetByCurve.GetBits(l) == 0) continue;
				apa.SetParameterValueByIndex(l, finalParamValues[l]);
			}
		}

		private float SampleUserCurve(ref BlobArray<KeyFrame> curve, in AnimationToProcessComponent atp, float animTime)
		{ 
			var curveValue = BlobCurve.SampleAnimationCurve(ref curve, animTime);
			//	Make additive animation if requested
			if (atp.BlendMode == AnimationBlendingMode.Additive)
			{
				var additiveValue = BlobCurve.SampleAnimationCurve(ref curve, atp.Animation.Value.AdditiveReferencePoseTime);
				curveValue -= additiveValue;
			}
			return curveValue;
		}

		private float CalculateLoopPose(ref BlobArray<KeyFrame> curve, in AnimationToProcessComponent atp, float normalizedTime)
		{
			var startV = SampleUserCurve(ref curve, atp, 0);
			var endV = SampleUserCurve(ref curve, atp, atp.Animation.Value.Length);
	
			var rv = (endV - startV) * normalizedTime;
			return rv;
		}
	}
	
	[BurstCompile]
	private struct CalculateBoneOffsetsJob: IJobChunk
	{
		[ReadOnly]
		public ComponentTypeHandle<RigDefinitionComponent> RigDefinitionTypeHandle;
		[ReadOnly]
		public NativeArray<int> ChunkBaseEntityIndices;
		
		[WriteOnly, NativeDisableContainerSafetyRestriction]
		public NativeArray<int2> BonePosesOffsets;
	
		public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
		{
			var rigDefAccessor = chunk.GetNativeArray(ref RigDefinitionTypeHandle);
			int baseEntityIndex = ChunkBaseEntityIndices[unfilteredChunkIndex];
			int validEntitiesInChunk = 0;
	
			var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
			BonePosesOffsets[0] = 0;
	
			while (cee.NextEntityIndex(out var i))
			{
				var rigDef = rigDefAccessor[i];
	
				int entityInQueryIndex = baseEntityIndex + validEntitiesInChunk;
	            ++validEntitiesInChunk;
	
				var boneCount = rigDef.RigBlob.Value.Bones.Length;
	
				var v = new int2
				(
					//	Bone count
					boneCount,
					//	Number of ulong values that can hold bone transform flags
					(boneCount * 4 >> 6) + 1
				);
				BonePosesOffsets[entityInQueryIndex + 1] = v;
			}
		}
	}
	
	[BurstCompile]
	private struct CalculatePerBoneInfoJob: IJobChunk
	{
		[ReadOnly]
		public ComponentTypeHandle<RigDefinitionComponent> RigDefinitionTypeHandle;
		[ReadOnly]
		public NativeArray<int> ChunkBaseEntityIndices;
		[ReadOnly]
		public NativeList<int2> BonePosesOffsets;
		[ReadOnly]
		public NativeList<Entity> Entities;
		[WriteOnly, NativeDisableContainerSafetyRestriction]
		public NativeList<int3> BoneToEntityIndices;
		[WriteOnly]
		public NativeParallelHashMap<Entity, int2>.ParallelWriter EntityToDataOffsetMap;
	
		public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
		{
			var rigDefAccessor = chunk.GetNativeArray(ref RigDefinitionTypeHandle);
			int baseEntityIndex = ChunkBaseEntityIndices[unfilteredChunkIndex];
			int validEntitiesInChunk = 0;
	
			var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
	
			while (cee.NextEntityIndex(out var i))
			{
				var rigDef = rigDefAccessor[i];
				int entityInQueryIndex = baseEntityIndex + validEntitiesInChunk;
	            ++validEntitiesInChunk;
				var offset = BonePosesOffsets[entityInQueryIndex];
	
				for (int k = 0, l = rigDef.RigBlob.Value.Bones.Length; k < l; ++k)
				{
					BoneToEntityIndices[k + offset.x] = new int3(entityInQueryIndex, k, offset.y);
				}
	
				EntityToDataOffsetMap.TryAdd(Entities[entityInQueryIndex], offset);
			}
		}
	}
	
	[BurstCompile]
	private struct DoPrefixSumJob: IJob
	{
		public NativeArray<int2> BoneOffsets;
	
		public void Execute()
		{
			var sum = new int2(0);
			for (int i = 0; i < BoneOffsets.Length; ++i)
			{
				var v = BoneOffsets[i];
				sum += v;
				BoneOffsets[i] = sum;
			}
		}
	}
	
	[BurstCompile]
	private partial struct CopyEntityBoneTransformsToAnimationBuffer: IJobEntity
{
	[WriteOnly, NativeDisableContainerSafetyRestriction]
	public NativeList<BoneTransform> AnimatedBoneTransforms;
	[ReadOnly]
	public ComponentLookup<RigDefinitionComponent> RigDefComponentLookup;
	[ReadOnly]
	public ComponentLookup<Parent> ParentComponentLookup;
	[NativeDisableContainerSafetyRestriction]
	public NativeList<ulong> BoneTransformFlags;
	[ReadOnly]
	public NativeParallelHashMap<Entity, int2> EntityToDataOffsetMap;

	private void Execute(Entity e, in AnimatorEntityRefComponent aer, in LocalTransform lt)
	{
		if (!RigDefComponentLookup.TryGetComponent(aer.AnimatorEntity, out var rdc))
			return;

		var boneOffset = RuntimeAnimationData.CalculateBufferOffset(EntityToDataOffsetMap, aer.AnimatorEntity);
		if (boneOffset.x < 0)
			return;
		
		var len = rdc.RigBlob.Value.Bones.Length;

		var bonePoses = RuntimeAnimationData.GetAnimationDataForRigRW(AnimatedBoneTransforms, boneOffset.x, len);
		var transformFlags = AnimationTransformFlags.CreateFromBufferRW(BoneTransformFlags, boneOffset.y, len);
		var boneFlags = new bool3(transformFlags.IsTranslationSet(aer.BoneIndexInAnimationRig),
			transformFlags.IsRotationSet(aer.BoneIndexInAnimationRig),
			transformFlags.IsScaleSet(aer.BoneIndexInAnimationRig));

		if (!math.any(boneFlags))
		{
			var entityPose = new BoneTransform(lt);
			//	Root motion delta should be zero
			if (rdc.ApplyRootMotion && aer.BoneIndexInAnimationRig == 0)
				entityPose = BoneTransform.Identity;
			
			//	For entities without parent we indicate that bone pose is in world space
			if (!ParentComponentLookup.HasComponent(e))
				transformFlags.SetAbsoluteTransformFlag(aer.BoneIndexInAnimationRig);

			ref var bonePose = ref bonePoses[aer.BoneIndexInAnimationRig];

			if (!boneFlags.x)
				bonePose.Position = entityPose.Position;
			if (!boneFlags.y)
				bonePose.Rotation = entityPose.Rotation;
			if (!boneFlags.z)
				bonePose.Scale = entityPose.Scale;
		}
	}
}
}
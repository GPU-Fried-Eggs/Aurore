using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Deformations;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Hash128 = Unity.Entities.Hash128;
#if AURORE_DEBUG
using UnityEngine;
#endif

[DisableAutoCreation]
[RequireMatchingQueriesForUpdate]
public partial struct AnimationApplicationSystem: ISystem
{
	private EntityQuery m_BoneObjectEntitiesWithParentQuery;
	private EntityQuery m_BoneObjectEntitiesNoParentQuery;
	private NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>> m_RigToSkinnedMeshRemapTables;

	[BurstCompile]
	public void OnCreate(ref SystemState state)
	{
		using var eqb0 = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<AnimatorEntityRefComponent, Parent>()
			.WithAllRW<LocalTransform>();
		m_BoneObjectEntitiesWithParentQuery = state.GetEntityQuery(eqb0);

		using var eqb1 = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<AnimatorEntityRefComponent>()
			.WithNone<Parent>()
			.WithAllRW<LocalTransform>();
		m_BoneObjectEntitiesNoParentQuery = state.GetEntityQuery(eqb1);

		m_RigToSkinnedMeshRemapTables = new NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>>(128, Allocator.Persistent);
	}
	
	[BurstCompile]
	public void OnDestroy(ref SystemState state)
	{
		m_RigToSkinnedMeshRemapTables.Dispose();
	}

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
		ref var runtimeData = ref SystemAPI.GetSingletonRW<RuntimeAnimationData>().ValueRW;

		var fillRigtoSkinnedMeshRemapTablesJh = FillRigToSkinBonesRemapTableCache(ref state);

		//	Compute root motion
		var rootMotionJobHandle = ComputeRootMotion(ref state, runtimeData, fillRigtoSkinnedMeshRemapTablesJh);

		//	Propagate local animated transforms to the entities with parents
		var propagateTRSWithParentsJobHandle = PropagateAnimatedBonesToEntitiesTRS(ref state, runtimeData, m_BoneObjectEntitiesWithParentQuery, rootMotionJobHandle);

		//	Convert local bone transforms to absolute (root relative) transforms
		var makeAbsTransformsJobHandle = MakeAbsoluteBoneTransforms(ref state, runtimeData, propagateTRSWithParentsJobHandle);

		//	Propagate absolute animated transforms to the entities without parents
		var propagateTRNoParentsJobHandle = PropagateAnimatedBonesToEntitiesTRS(ref state, runtimeData, m_BoneObjectEntitiesNoParentQuery, makeAbsTransformsJobHandle);

		//	Make corresponding skin matrices for all skinned meshes
		var applySkinJobHandle = ApplySkinning(ref state, runtimeData, propagateTRNoParentsJobHandle);

		state.Dependency = applySkinJobHandle;
    }

    private JobHandle ComputeRootMotion(ref SystemState state, in RuntimeAnimationData runtimeData, JobHandle dependsOn)
	{
		var computeRootMotionJob = new ComputeRootMotionJob
		{
			AnimatedBonePoses = runtimeData.AnimatedBonesBuffer,
			EntityToDataOffsetMap = runtimeData.EntityToDataOffsetMap
		};

		return computeRootMotionJob.ScheduleParallel(dependsOn);
	}

	private JobHandle FillRigToSkinBonesRemapTableCache(ref SystemState state)
	{
		var rigDefinitionComponentLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true);

#if AURORE_DEBUG
		SystemAPI.TryGetSingleton<DebugConfigurationComponent>(out var dc);
#endif
		var skinnedMeshWithAnimatorQuery = SystemAPI.QueryBuilder().WithAll<SkinMatrix, AnimatedSkinnedMeshComponent>().Build();
		var skinnedMeshes = skinnedMeshWithAnimatorQuery.ToComponentDataListAsync<AnimatedSkinnedMeshComponent>(state.WorldUpdateAllocator, state.Dependency, out var skinnedMeshFromQueryJh);

		var fillRigToSkinBonesRemapTableCacheJob = new FillRigToSkinBonesRemapTableCacheJob
		{
			RigDefinitionArr = rigDefinitionComponentLookup,
			RigToSkinnedMeshRemapTables = m_RigToSkinnedMeshRemapTables,
			SkinnedMeshes = skinnedMeshes,
#if AURORE_DEBUG
			doLogging = dc.logAnimationCalculationProcesses
#endif
		};

		return fillRigToSkinBonesRemapTableCacheJob.Schedule(skinnedMeshFromQueryJh);
	}

	private JobHandle PropagateAnimatedBonesToEntitiesTRS(ref SystemState state, in RuntimeAnimationData runtimeData, EntityQuery query, JobHandle dependsOn)
	{
		var rigDefinitionComponentLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true);

		var propagateAnimationJob = new PropagateBoneTransformToEntityTRSJob
		{
			EntityToDataOffsetMap = runtimeData.EntityToDataOffsetMap,
			BoneTransforms = runtimeData.AnimatedBonesBuffer,
			RigDefLookup = rigDefinitionComponentLookup,
		};

		return propagateAnimationJob.ScheduleParallel(query, dependsOn);
	}

	private JobHandle MakeAbsoluteBoneTransforms(ref SystemState ss, in RuntimeAnimationData runtimeData, JobHandle dependsOn)
	{
		var makeAbsTransformsJob = new MakeAbsoluteTransformsJob
		{
			BoneTransforms = runtimeData.AnimatedBonesBuffer,
			EntityToDataOffsetMap = runtimeData.EntityToDataOffsetMap,
			BoneTransformFlags = runtimeData.BoneTransformFlagsHolderArr
		};

		return makeAbsTransformsJob.ScheduleParallel(dependsOn);
	}

	private JobHandle ApplySkinning(ref SystemState ss, in RuntimeAnimationData runtimeData, JobHandle dependsOn)
	{
		var rigDefinitionComponentLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true);

		var animationApplyJob = new ApplyAnimationToSkinnedMeshJob
		{
			BoneTransforms = runtimeData.AnimatedBonesBuffer,
			EntityToDataOffsetMap = runtimeData.EntityToDataOffsetMap,
			RigDefinitionLookup = rigDefinitionComponentLookup,
			RigToSkinnedMeshRemapTables = m_RigToSkinnedMeshRemapTables,
		};

		return animationApplyJob.ScheduleParallel(dependsOn);
	}

	#region Job

	[BurstCompile]
	private partial struct MakeAbsoluteTransformsJob: IJobEntity
	{
		[NativeDisableContainerSafetyRestriction]
		public NativeList<BoneTransform> BoneTransforms;
		[NativeDisableContainerSafetyRestriction]
		public NativeList<ulong> BoneTransformFlags;
		[ReadOnly]
		public NativeParallelHashMap<Entity, int2> EntityToDataOffsetMap;
		
		public void Execute(Entity rigEntity, in RigDefinitionComponent rigDef)
		{
			if (!EntityToDataOffsetMap.TryGetValue(rigEntity, out var boneDataOffset))
				return;
	
			ref var rigBones = ref rigDef.RigBlob.Value.Bones;
	
			var boneTransformsForRig = BoneTransforms.GetSpan(boneDataOffset.x, rigBones.Length);
			var boneFlags = AnimationTransformFlags.CreateFromBufferRW(BoneTransformFlags, boneDataOffset.y, rigBones.Length);
	
			// Iterate over all animated bones and calculate absolute transform in-place
			for (var animationBoneIndex = 0; animationBoneIndex < rigBones.Length; ++animationBoneIndex)
			{
				MakeAbsoluteTransform(boneFlags, animationBoneIndex, boneTransformsForRig, rigDef.RigBlob);
			}
		}

		private void MakeAbsoluteTransform(AnimationTransformFlags absTransformFlags, int boneIndex, Span<BoneTransform> boneTransforms, in BlobAssetReference<RigDefinitionBlob> rigBlob)
		{
			var resultBoneTransform = BoneTransform.Identity;
			var myBoneIndex = boneIndex;
			ref var rigBones = ref rigBlob.Value.Bones;
			bool absTransformFlag;
	
			do
			{
				var animatedBoneTransform = boneTransforms[boneIndex];
				resultBoneTransform = BoneTransform.Multiply(animatedBoneTransform, resultBoneTransform);
				absTransformFlag = absTransformFlags.IsAbsoluteTransform(boneIndex);
				
				boneIndex = rigBones[boneIndex].ParentBoneIndex;
			}
			while (boneIndex >= 0 && !absTransformFlag);
	
			boneTransforms[myBoneIndex] = resultBoneTransform;
			absTransformFlags.SetAbsoluteTransformFlag(myBoneIndex);
		}
	}
	
	[BurstCompile]
	private partial struct ApplyAnimationToSkinnedMeshJob: IJobEntity
	{
		[ReadOnly]
		public ComponentLookup<RigDefinitionComponent> RigDefinitionLookup;
		[ReadOnly]
		public NativeList<BoneTransform> BoneTransforms;
		[ReadOnly]
		public NativeParallelHashMap<Entity, int2> EntityToDataOffsetMap;
		[ReadOnly]
		public NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>> RigToSkinnedMeshRemapTables;
	
		public static Hash128 CalculateBoneRemapTableHash(in BlobAssetReference<SkinnedMeshInfoBlob> skinnedMesh, in BlobAssetReference<RigDefinitionBlob> rigDef)
		{
			var rv = new Hash128(skinnedMesh.Value.Hash.Value.x, skinnedMesh.Value.Hash.Value.y, rigDef.Value.Hash.Value.z, rigDef.Value.Hash.Value.w);
			return rv;
		}

		private ref BoneRemapTableBlob GetBoneRemapTable(in BlobAssetReference<SkinnedMeshInfoBlob> skinnedMesh, in BlobAssetReference<RigDefinitionBlob> rigDef)
		{
			var hash = CalculateBoneRemapTableHash(skinnedMesh, rigDef);
			return ref RigToSkinnedMeshRemapTables[hash].Value;
		}

		private SkinMatrix MakeSkinMatrixForBone(ref SkinnedMeshBoneInfo boneInfo, in float4x4 boneXForm, in float4x4 entityToRootBoneTransform)
		{
			var boneTransformMatrix = math.mul(entityToRootBoneTransform, boneXForm);
			boneTransformMatrix = math.mul(boneTransformMatrix, boneInfo.BindPose);
	
			var skinMatrix = new SkinMatrix { Value = new float3x4(boneTransformMatrix.c0.xyz, boneTransformMatrix.c1.xyz, boneTransformMatrix.c2.xyz, boneTransformMatrix.c3.xyz) };
			return skinMatrix;
		}

		private void Execute(in AnimatedSkinnedMeshComponent animatedSkinnedMesh, ref DynamicBuffer<SkinMatrix> outSkinMatricesBuf)
		{
			var rigEntity = animatedSkinnedMesh.AnimatedRigEntity;
	
			if (!RigDefinitionLookup.TryGetComponent(rigEntity, out var rigDef))
				return;
	
			if (!EntityToDataOffsetMap.TryGetValue(rigEntity, out var boneDataOffset))
				return;
	
			ref var boneRemapTable = ref GetBoneRemapTable(animatedSkinnedMesh.BoneInfos, rigDef.RigBlob);
	
			ref var rigBones = ref rigDef.RigBlob.Value.Bones;
	
			var skinMeshBonesInfo = animatedSkinnedMesh.BoneInfos;
			var absoluteBoneTransforms = BoneTransforms.GetReadOnlySpan(boneDataOffset.x, rigBones.Length);
	
			var rootBoneIndex = math.max(0, animatedSkinnedMesh.RootBoneIndexInRig);
			var boneObjLocalPose = absoluteBoneTransforms[rootBoneIndex];
			var entityToRootBoneTransform = math.inverse(boneObjLocalPose.ToFloat4X4());
	
			// Iterate over all animated bones and set pose for corresponding skin matrices
			for (var animationBoneIndex = 0; animationBoneIndex < rigBones.Length; ++animationBoneIndex)
			{
				var skinnedMeshBoneIndex = boneRemapTable.RigBoneToSkinnedMeshBoneRemapIndices[animationBoneIndex];
	
				//	Skip bone if it is not present in skinned mesh
				if (skinnedMeshBoneIndex < 0) continue;
	
				var absBonePose = absoluteBoneTransforms[animationBoneIndex];
				var boneXForm = absBonePose.ToFloat4X4();
	
				ref var boneInfo = ref skinMeshBonesInfo.Value.Bones[skinnedMeshBoneIndex];
				var skinMatrix = MakeSkinMatrixForBone(ref boneInfo, boneXForm, entityToRootBoneTransform);
				outSkinMatricesBuf[skinnedMeshBoneIndex] = skinMatrix;
			}
		}
	}
	
	[BurstCompile]
	private partial struct PropagateBoneTransformToEntityTRSJob: IJobEntity
	{
		[ReadOnly]
		public ComponentLookup<RigDefinitionComponent> RigDefLookup;
		[ReadOnly]
		public NativeList<BoneTransform> BoneTransforms;
		[ReadOnly]
		public NativeParallelHashMap<Entity, int2> EntityToDataOffsetMap;
	
		public void Execute(in AnimatorEntityRefComponent animatorRef, ref LocalTransform lt)
		{
			if (!RigDefLookup.TryGetComponent(animatorRef.AnimatorEntity, out var rigDef))
				return;
	
			var boneData = RuntimeAnimationData.GetAnimationDataForRigRO(BoneTransforms, EntityToDataOffsetMap, rigDef, animatorRef.AnimatorEntity);
			if (boneData.IsEmpty)
				return;
			
			lt = boneData[animatorRef.BoneIndexInAnimationRig].ToLocalTransformComponent();
		}
	}
	
	[BurstCompile]
	private partial struct ComputeRootMotionJob: IJobEntity
	{
		[NativeDisableContainerSafetyRestriction]
		public NativeList<BoneTransform> AnimatedBonePoses;
		[ReadOnly]
		public NativeParallelHashMap<Entity, int2> EntityToDataOffsetMap;

		private void Execute(Entity e, in RigDefinitionComponent rdc, LocalTransform lt)
		{
			if (!rdc.ApplyRootMotion) return;
			
			var boneData = RuntimeAnimationData.GetAnimationDataForRigRW(AnimatedBonePoses, EntityToDataOffsetMap, rdc, e);
			if (boneData.IsEmpty) return;
			
			var motionDeltaPose = boneData[0];
			var curEntityTransform = new BoneTransform(lt);
			var newEntityPose = BoneTransform.Multiply(curEntityTransform, motionDeltaPose);
			
			boneData[0] = newEntityPose;
		}
	}
	
	[BurstCompile]
	private struct FillRigToSkinBonesRemapTableCacheJob: IJob
	{
		[ReadOnly]
		public ComponentLookup<RigDefinitionComponent> RigDefinitionArr;
		[ReadOnly]
		public NativeList<AnimatedSkinnedMeshComponent> SkinnedMeshes;
		public NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>> RigToSkinnedMeshRemapTables;
	
#if AURORE_DEBUG
		public bool doLogging;
#endif
	
		public void Execute()
		{
			for (var l = 0; l < SkinnedMeshes.Length; ++l)
			{
				var sm = SkinnedMeshes[l];
				if (!RigDefinitionArr.TryGetComponent(sm.AnimatedRigEntity, out var rigDef))
					continue;
	
				//	Try cache first
				var h = ApplyAnimationToSkinnedMeshJob.CalculateBoneRemapTableHash(sm.BoneInfos, rigDef.RigBlob);
				if (RigToSkinnedMeshRemapTables.TryGetValue(h, out var rv))
					continue;
	
				//	Compute new remap table
				var bb = new BlobBuilder(Allocator.Temp);
				ref var brt = ref bb.ConstructRoot<BoneRemapTableBlob>();
	
#if AURORE_DEBUG
				ref var rnd = ref rigDef.RigBlob.Value.Name;
				ref var snd = ref sm.BoneInfos.Value.skeletonName;
				if (doLogging)
					Debug.Log($"[FillRigToSkinBonesRemapTableCacheJob] Creating rig '{rnd.ToFixedString()}' to skinned mesh '{snd.ToFixedString()}' remap table");
#endif
				
				var bba = bb.Allocate(ref brt.RigBoneToSkinnedMeshBoneRemapIndices, rigDef.RigBlob.Value.Bones.Length);
				for (var i = 0; i < bba.Length; ++i)
				{
					bba[i] = -1;
					ref var rb = ref rigDef.RigBlob.Value.Bones[i];
					var rbHash =  rb.Hash;
					
					for (var j = 0; j < sm.BoneInfos.Value.Bones.Length; ++j)
					{
						ref var bn = ref sm.BoneInfos.Value.Bones[j];
						var bnHash = bn.Hash;
	
						if (bnHash == rbHash)
						{ 
							bba[i] = j;
#if AURORE_DEBUG
							if (doLogging)
								Debug.Log($"[FillRigToSkinBonesRemapTableCacheJob] Remap {rb.Name.ToFixedString()}->{bn.name.ToFixedString()} : {i} -> {j}");
#endif
						}
					}
				}
				rv = bb.CreateBlobAssetReference<BoneRemapTableBlob>(Allocator.Persistent);
				RigToSkinnedMeshRemapTables.Add(h, rv);
			}
		}
	}	

	#endregion
}

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
		using var boneObjectEntitiesWithParentQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<AnimatorEntityRefComponent, Parent>()
			.WithAllRW<LocalTransform>();
		m_BoneObjectEntitiesWithParentQuery = state.GetEntityQuery(boneObjectEntitiesWithParentQueryBuilder);

		using var boneObjectEntitiesNoParentQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<AnimatorEntityRefComponent>()
			.WithNone<Parent>()
			.WithAllRW<LocalTransform>();
		m_BoneObjectEntitiesNoParentQuery = state.GetEntityQuery(boneObjectEntitiesNoParentQueryBuilder);

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

		var fillRigToSkinnedMeshRemapTablesJobHandle = FillRigToSkinBonesRemapTableCache(ref state);

		//	Compute root motion
		var rootMotionJobHandle = ComputeRootMotion(ref state, runtimeData, fillRigToSkinnedMeshRemapTablesJobHandle);

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
			RigDefinitionLookup = rigDefinitionComponentLookup,
			RigToSkinnedMeshRemapTables = m_RigToSkinnedMeshRemapTables,
			SkinnedMeshes = skinnedMeshes,
#if AURORE_DEBUG
			DoLogging = dc.logAnimationCalculationProcesses
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
			BoneTransformFlags = runtimeData.BoneTransformFlagsBuffer
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

	#region Jobs Implement

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
	
			ref var rigBoneBlobArray = ref rigDef.RigBlob.Value.Bones;
	
			var boneTransformsForRig = BoneTransforms.GetSpan(boneDataOffset.x, rigBoneBlobArray.Length);
			var boneFlags = AnimationTransformFlags.CreateFromBufferRW(BoneTransformFlags, boneDataOffset.y, rigBoneBlobArray.Length);
	
			// Iterate over all animated bones and calculate absolute transform in-place
			for (var animationBoneIndex = 0; animationBoneIndex < rigBoneBlobArray.Length; ++animationBoneIndex)
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
			var hash = new Hash128(skinnedMesh.Value.Hash.Value.x, skinnedMesh.Value.Hash.Value.y, rigDef.Value.Hash.Value.z, rigDef.Value.Hash.Value.w);
			return hash;
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
			if (boneData.IsEmpty) return;
			
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
		[ReadOnly] public ComponentLookup<RigDefinitionComponent> RigDefinitionLookup;
		[ReadOnly] public NativeList<AnimatedSkinnedMeshComponent> SkinnedMeshes;
		public NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>> RigToSkinnedMeshRemapTables;
	
#if AURORE_DEBUG
		public bool DoLogging;
#endif
	
		public void Execute()
		{
			for (var l = 0; l < SkinnedMeshes.Length; ++l)
			{
				var skinnedMesh = SkinnedMeshes[l];
				if (!RigDefinitionLookup.TryGetComponent(skinnedMesh.AnimatedRigEntity, out var rigDefinition))
					continue;
	
				//	Try cache first
				var hash = ApplyAnimationToSkinnedMeshJob.CalculateBoneRemapTableHash(skinnedMesh.BoneInfos, rigDefinition.RigBlob);
				if (RigToSkinnedMeshRemapTables.TryGetValue(hash, out var boneRemapTableBlobReference))
					continue;
	
				//	Compute new remap table
				var blobBuilder = new BlobBuilder(Allocator.Temp);
				ref var boneRemapTableBlob = ref blobBuilder.ConstructRoot<BoneRemapTableBlob>();
	
#if AURORE_DEBUG
				ref var rnd = ref rigDefinition.RigBlob.Value.Name;
				ref var snd = ref skinnedMesh.BoneInfos.Value.skeletonName;
				if (DoLogging) Debug.Log($"[FillRigToSkinBonesRemapTableCacheJob] Creating rig '{rnd.ToFixedString()}' to skinned mesh '{snd.ToFixedString()}' remap table");
#endif

				var blobBuilderArray = blobBuilder.Allocate(ref boneRemapTableBlob.RigBoneToSkinnedMeshBoneRemapIndices, rigDefinition.RigBlob.Value.Bones.Length);
				for (var i = 0; i < blobBuilderArray.Length; ++i)
				{
					blobBuilderArray[i] = -1;
					ref var rigBoneInfo = ref rigDefinition.RigBlob.Value.Bones[i];
					var rigBoneInfoHash =  rigBoneInfo.Hash;

					for (var j = 0; j < skinnedMesh.BoneInfos.Value.Bones.Length; ++j)
					{
						ref var skinnedMeshBoneInfo = ref skinnedMesh.BoneInfos.Value.Bones[j];
						var skinnedMeshBoneInfoHash = skinnedMeshBoneInfo.Hash;
	
						if (skinnedMeshBoneInfoHash == rigBoneInfoHash)
						{ 
							blobBuilderArray[i] = j;
#if AURORE_DEBUG
							if (DoLogging) Debug.Log($"[FillRigToSkinBonesRemapTableCacheJob] Remap {rigBoneInfo.Name.ToFixedString()}->{skinnedMeshBoneInfo.name.ToFixedString()} : {i} -> {j}");
#endif
						}
					}
				}
				boneRemapTableBlobReference = blobBuilder.CreateBlobAssetReference<BoneRemapTableBlob>(Allocator.Persistent);
				RigToSkinnedMeshRemapTables.Add(hash, boneRemapTableBlobReference);
			}
		}
	}	

	#endregion
}

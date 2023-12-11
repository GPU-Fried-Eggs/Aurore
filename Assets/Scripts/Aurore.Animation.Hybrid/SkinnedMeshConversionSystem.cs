using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using FixedStringName = Unity.Collections.FixedString512Bytes;
using Hash128 = Unity.Entities.Hash128;
#if AURORE_DEBUG
using UnityEngine;
#endif

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[RequireMatchingQueriesForUpdate]
[UpdateAfter(typeof(RigDefinitionConversionSystem))]
public partial class SkinnedMeshConversionSystem : SystemBase
{
	private EntityQuery m_SkinnedMeshRenderersQuery;

	private struct SkinnedMeshBakerDataSorter: IComparer<SkinnedMeshBakerData>
	{
		public int Compare(SkinnedMeshBakerData a, SkinnedMeshBakerData b)
		{
			if (a.Hash < b.Hash) return -1;
			if (a.Hash > b.Hash) return 1;
			return 0;
		}
	}

	protected override void OnCreate()
	{
		base.OnCreate();

		using var ecb = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<SkinnedMeshBakerData>()
			.WithOptions(EntityQueryOptions.IncludePrefab);

		m_SkinnedMeshRenderersQuery = GetEntityQuery(ecb);
	}

	protected override void OnDestroy()
	{
		//	Cleanup conversion data
		using var skinnedMeshesData = m_SkinnedMeshRenderersQuery.ToComponentDataArray<SkinnedMeshBakerData>(Allocator.Temp);
		foreach (var s in skinnedMeshesData) s.SkinnedMeshBones.Dispose();
	}

	protected override void OnUpdate()
	{
		using var skinnedMeshesData = m_SkinnedMeshRenderersQuery.ToComponentDataArray<SkinnedMeshBakerData>(Allocator.TempJob);
		if (m_SkinnedMeshRenderersQuery.IsEmpty) return;

#if AURORE_DEBUG
		SystemAPI.TryGetSingleton<DebugConfigurationComponent>(out var dc);
		if (dc.logSkinnedMeshBaking) Debug.Log($"=== [SkinnedMeshConversionSystem] BEGIN CONVERSION ===");
#endif

		//	Prepare data for blob assets
		skinnedMeshesData.Sort(new SkinnedMeshBakerDataSorter());

		var startIndex = 0;
		var startHash = skinnedMeshesData[0].Hash;

		using var jobHandles = new NativeList<JobHandle>(skinnedMeshesData.Length, Allocator.Temp);
		using var blobAssets = new NativeArray<BlobAssetReference<SkinnedMeshInfoBlob>>(skinnedMeshesData.Length, Allocator.TempJob);
		var blobUniqueIndices = new NativeList<int>(Allocator.Temp);

		for (var i = 1; i <= skinnedMeshesData.Length; ++i)
		{
			var rd = i < skinnedMeshesData.Length ? skinnedMeshesData[i] : default;
			if (rd.Hash != startHash)
			{
				var numDuplicates = i - startIndex;
				var blobAssetsSlice = new NativeSlice<BlobAssetReference<SkinnedMeshInfoBlob>>(blobAssets, startIndex, numDuplicates);
				var refSkinnedMesh = skinnedMeshesData[startIndex];
				var j = new CreateBlobAssetsJob
				{
					Data = refSkinnedMesh,
					OutBlobAssets = blobAssetsSlice,
				};

				var jobHandle = j.Schedule();
				jobHandles.Add(jobHandle);
				blobUniqueIndices.Add(startIndex);

				startHash = rd.Hash;
				startIndex = i;
#if AURORE_DEBUG
				if (dc.logSkinnedMeshBaking) Debug.Log($"Creating blob asset for skinned mesh '{refSkinnedMesh.SkeletonName}'. Entities count: {numDuplicates}");
#endif
			}
		}

		var combinedJh = JobHandle.CombineDependencies(jobHandles.AsArray());
		using var ecb = new EntityCommandBuffer(Allocator.TempJob);

		var animatedEntityRefLookup = GetComponentLookup<AnimatorEntityRefComponent>(true);

		var createComponentDatasJob = new CreateComponentDatasJob
		{
			ECB = ecb.AsParallelWriter(),
			BakerData = skinnedMeshesData,
			BlobAssets = blobAssets,
			AnimEntityRefLookup = animatedEntityRefLookup,
#if AURORE_DEBUG
			EnableLog = dc.logSkinnedMeshBaking
#endif
		};

		createComponentDatasJob.ScheduleBatch(skinnedMeshesData.Length, 32, combinedJh).Complete();

		//	Register blob assets in store to prevent memory leaks
		RegisterBlobAssetsInAssetStore(blobAssets, blobUniqueIndices.AsArray());

		ecb.Playback(EntityManager);

#if AURORE_DEBUG
		if (dc.logSkinnedMeshBaking)
		{
			Debug.Log($"Total converted skinned meshes: {skinnedMeshesData.Length}");
			Debug.Log($"=== [SkinnedMeshConversionSystem] END CONVERSION ===");
		}
#endif
	}

	private void RegisterBlobAssetsInAssetStore(NativeArray<BlobAssetReference<SkinnedMeshInfoBlob>> blobAssets, NativeArray<int> blobUniqueIndices)
	{
		var bakingSystem = World.GetExistingSystemManaged<BakingSystem>();
		var blobAssetStore = bakingSystem.BlobAssetStore;
		for (var i = 0; i < blobUniqueIndices.Length; ++i)
		{
			var idx = blobUniqueIndices[i];
			var skinnedMeshInfoBlobReference = blobAssets[idx];
			blobAssetStore.TryAdd(ref skinnedMeshInfoBlobReference);
		}
	}

	[BurstCompile]
	public struct CreateBlobAssetsJob: IJob
	{
		public SkinnedMeshBakerData Data;
		[NativeDisableContainerSafetyRestriction]
		public NativeSlice<BlobAssetReference<SkinnedMeshInfoBlob>> OutBlobAssets;

		public void Execute()
		{
			var bb = new BlobBuilder(Allocator.Temp);
			ref var blobAsset = ref bb.ConstructRoot<SkinnedMeshInfoBlob>();
#if AURORE_DEBUG
			bb.AllocateString(ref blobAsset.skeletonName, ref Data.SkeletonName);
#endif

			var boneInfoArr = bb.Allocate(ref blobAsset.Bones, Data.SkinnedMeshBones.Bones.Length);
			for (var i = 0; i < Data.SkinnedMeshBones.Bones.Length; ++i)
			{
				var src = Data.SkinnedMeshBones.Bones[i];
				ref var smbi = ref boneInfoArr[i];
				smbi.Hash = src.Hash;
				smbi.BindPose = src.BindPose;
#if AURORE_DEBUG
				bb.AllocateString(ref smbi.name, ref src.Name);
#endif
			}
			var pn = new FixedStringName(Data.SkinnedMeshBones.ParentBoneName);
			var ph = pn.CalculateHash128();
			blobAsset.RootBoneNameHash = ph;
			blobAsset.Hash = new Hash128((uint)Data.Hash, ph.Value.w, ph.Value.z, ph.Value.y);

			var rv = bb.CreateBlobAssetReference<SkinnedMeshInfoBlob>(Allocator.Persistent);
			for (var i = 0; i < OutBlobAssets.Length; ++i)
			{
				OutBlobAssets[i] = rv;
			}
		}
	}

	[BurstCompile]
	public struct CreateComponentDatasJob: IJobParallelForBatch
	{
		[ReadOnly] public NativeArray<SkinnedMeshBakerData> BakerData;
		[ReadOnly] public NativeArray<BlobAssetReference<SkinnedMeshInfoBlob>> BlobAssets;
		[ReadOnly] public ComponentLookup<AnimatorEntityRefComponent> AnimEntityRefLookup;

		public EntityCommandBuffer.ParallelWriter ECB;

#if AURORE_DEBUG
		public bool EnableLog;
#endif

		public void Execute(int startIndex, int count)
		{
			for (var i = startIndex; i < startIndex + count; ++i)
			{
				var smb = BakerData[i];

				var e = smb.TargetEntity;
				var bnc = new AnimatedSkinnedMeshComponent();
				bnc.BoneInfos = BlobAssets[i];
				bnc.AnimatedRigEntity = smb.AnimatedRigEntity;
				bnc.RootBoneIndexInRig = -1;

				if (AnimEntityRefLookup.HasComponent(smb.RootBoneEntity))
				{
					var are = AnimEntityRefLookup[smb.RootBoneEntity];
					bnc.RootBoneIndexInRig = are.BoneIndexInAnimationRig;
				}
				
				ECB.AddComponent(startIndex, e, bnc);

#if AURORE_DEBUG
				if (EnableLog) Debug.Log($"Adding 'AnimatedSkinnedMeshComponent' to entity '{e.Index}:{e.Version}'");
#endif
			}
		}
	}
}

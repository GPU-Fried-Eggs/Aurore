using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Hybrid.Baking;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[RequireMatchingQueriesForUpdate]
[UpdateAfter(typeof(BakingOnlyEntityAuthoringBakingSystem))]
public partial class RigDefinitionConversionSystem : SystemBase
{
	private EntityQuery m_RigDefinitionQuery;
	private ComponentLookup<BakingOnlyEntity> m_BakingOnlyEntityLookup;

	public static readonly AvatarMaskBodyPart[] humanPartToAvatarMaskPartRemapTable = 
	{
		//	Hips = 0,
		AvatarMaskBodyPart.Root,
		//	LeftUpperLeg = 1,
		AvatarMaskBodyPart.LeftLeg,
		//	RightUpperLeg = 2,
		AvatarMaskBodyPart.RightLeg,
		//	LeftLowerLeg = 3,
		AvatarMaskBodyPart.LeftLeg,
		//	RightLowerLeg = 4,
		AvatarMaskBodyPart.RightLeg,
		//	LeftFoot = 5,
		AvatarMaskBodyPart.LeftLeg,
		//	RightFoot = 6,
		AvatarMaskBodyPart.RightLeg,
		//	Spine = 7,
		AvatarMaskBodyPart.Body,
		//	Chest = 8,
		AvatarMaskBodyPart.Body,
		//	Neck = 9,
		AvatarMaskBodyPart.Head,
		//	Head = 10,
		AvatarMaskBodyPart.Head,
		//	LeftShoulder = 11,
		AvatarMaskBodyPart.LeftArm,
		//	RightShoulder = 12,
		AvatarMaskBodyPart.RightArm,
		//	LeftUpperArm = 13,
		AvatarMaskBodyPart.LeftArm,
		//	RightUpperArm = 14,
		AvatarMaskBodyPart.RightArm,
		//	LeftLowerArm = 15,
		AvatarMaskBodyPart.LeftArm,
		//	RightLowerArm = 16,
		AvatarMaskBodyPart.RightArm,
		//	LeftHand = 17,
		AvatarMaskBodyPart.LeftArm,
		//	RightHand = 18,
		AvatarMaskBodyPart.RightArm,
		//	LeftToes = 19,
		AvatarMaskBodyPart.LeftLeg,
		//	RightToes = 20,
		AvatarMaskBodyPart.RightLeg,
		//	LeftEye = 21,
		AvatarMaskBodyPart.Head,
		//	RightEye = 22,
		AvatarMaskBodyPart.Head,
		//	Jaw = 23,
		AvatarMaskBodyPart.Head,
		//	LeftThumbProximal = 24,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftThumbIntermediate = 25,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftThumbDistal = 26,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftIndexProximal = 27,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftIndexIntermediate = 28,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftIndexDistal = 29,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftMiddleProximal = 30,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftMiddleIntermediate = 31,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftMiddleDistal = 32,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftRingProximal = 33,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftRingIntermediate = 34,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftRingDistal = 35,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftLittleProximal = 36,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftLittleIntermediate = 37,
		AvatarMaskBodyPart.LeftFingers,
		//	LeftLittleDistal = 38,
		AvatarMaskBodyPart.LeftFingers,
		//	RightThumbProximal = 39,
		AvatarMaskBodyPart.RightFingers,
		//	RightThumbIntermediate = 40,
		AvatarMaskBodyPart.RightFingers,
		//	RightThumbDistal = 41,
		AvatarMaskBodyPart.RightFingers,
		//	RightIndexProximal = 42,
		AvatarMaskBodyPart.RightFingers,
		//	RightIndexIntermediate = 43,
		AvatarMaskBodyPart.RightFingers,
		//	RightIndexDistal = 44,
		AvatarMaskBodyPart.RightFingers,
		//	RightMiddleProximal = 45,
		AvatarMaskBodyPart.RightFingers,
		//	RightMiddleIntermediate = 46,
		AvatarMaskBodyPart.RightFingers,
		//	RightMiddleDistal = 47,
		AvatarMaskBodyPart.RightFingers,
		//	RightRingProximal = 48,
		AvatarMaskBodyPart.RightFingers,
		//	RightRingIntermediate = 49,
		AvatarMaskBodyPart.RightFingers,
		//	RightRingDistal = 50,
		AvatarMaskBodyPart.RightFingers,
		//	RightLittleProximal = 51,
		AvatarMaskBodyPart.RightFingers,
		//	RightLittleIntermediate = 52,
		AvatarMaskBodyPart.RightFingers,
		//	RightLittleDistal = 53,
		AvatarMaskBodyPart.RightFingers,
		//	UpperChest = 54,
		AvatarMaskBodyPart.Body,
	};

	private struct RigDefinitionDataSorter: IComparer<RigDefinitionBakerComponent>
	{
		public int Compare(RigDefinitionBakerComponent a, RigDefinitionBakerComponent b)
		{
			if (a.Hash < b.Hash) return -1;
			if (a.Hash > b.Hash) return 1;
			return 0;
		}
	}

	protected override void OnCreate()
	{
		base.OnCreate();

		using var builder = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<RigDefinitionBakerComponent>()
			.WithOptions(EntityQueryOptions.IncludePrefab);

		m_RigDefinitionQuery = GetEntityQuery(builder);

		m_BakingOnlyEntityLookup = GetComponentLookup<BakingOnlyEntity>(true);
	}

	protected override void OnDestroy()
	{
		using var allRigs = m_RigDefinitionQuery.ToComponentDataArray<RigDefinitionBakerComponent>(Allocator.Temp);
		foreach (var rig in allRigs) rig.RigDefData.Dispose();
	}

	protected override void OnUpdate()
	{
		using var allRigs = m_RigDefinitionQuery.ToComponentDataArray<RigDefinitionBakerComponent>(Allocator.TempJob);
		if (allRigs.Length == 0) return;

#if AURORE_DEBUG
		SystemAPI.TryGetSingleton<DebugConfigurationComponent>(out var dc);
		if (dc.logRigDefinitionBaking) Debug.Log($"=== [RigDefinition] BEGIN CONVERSION ===");
#endif

		allRigs.Sort(new RigDefinitionDataSorter());

		//	Create blob assets
		using var blobAssetsArr = new NativeArray<BlobAssetReference<RigDefinitionBlob>>(allRigs.Length, Allocator.TempJob);

		var startIndex = 0;
		var startHash = allRigs[0].Hash;

		using var jobHandles = new NativeList<JobHandle>(allRigs.Length, Allocator.Temp);

		for (var i = 1; i <= allRigs.Length; ++i)
		{
			var rd = i < allRigs.Length ? allRigs[i] : default;
			if (rd.Hash != startHash)
			{
				var numDuplicates = i - startIndex;
				var blobAssetsSlice = new NativeSlice<BlobAssetReference<RigDefinitionBlob>>(blobAssetsArr, startIndex, numDuplicates);
				var refRig = allRigs[startIndex];
				var createBlobAssetsJob = new CreateBlobAssetsJob
				{
					InData = refRig.RigDefData,
					OutBlobAssets = blobAssetsSlice,
				};

				var jobHandle = createBlobAssetsJob.Schedule();
				jobHandles.Add(jobHandle);

				startHash = rd.Hash;
				startIndex = i;

#if AURORE_DEBUG
				if (dc.logSkinnedMeshBaking) Debug.Log($"Creating blob asset for skinned mesh '{refRig.Name}'. Entities count: {numDuplicates}");
#endif
			}
		}

		var combineDependencies = JobHandle.CombineDependencies(jobHandles.AsArray());
		using var ecb = new EntityCommandBuffer(Allocator.TempJob);
		m_BakingOnlyEntityLookup.Update(this);

		var createComponentDatasJob = new CreateComponentDatasJob
		{
			ECB = ecb.AsParallelWriter(),
			BakerData = allRigs,
			BlobAssets = blobAssetsArr,
			BakingOnlyLookup = m_BakingOnlyEntityLookup
		};

		createComponentDatasJob.ScheduleBatch(allRigs.Length, 32, combineDependencies).Complete();

		ecb.Playback(EntityManager);
		OnDestroy();

#if AURORE_DEBUG
		if (dc.logRigDefinitionBaking)
		{
			Debug.Log($"Total converted rigs: {allRigs.Length}");
			Debug.Log($"=== [RigDefinition] END CONVERSION ===");
		}
#endif
	}
	
	[BurstCompile]
	private struct CreateBlobAssetsJob: IJob
	{
		[NativeDisableContainerSafetyRestriction]
		public NativeSlice<BlobAssetReference<RigDefinitionBlob>> OutBlobAssets;
		public RTP.RigDefinition InData;

		public void Execute()
		{
			var data = InData;
			var bb = new BlobBuilder(Allocator.Temp);
			ref var c = ref bb.ConstructRoot<RigDefinitionBlob>();

#if AURORE_DEBUG
			bb.AllocateString(ref c.Name, ref data.Name);
#endif
			var hasher = new xxHash3.StreamingState();

			var bonesArr = bb.Allocate(ref c.Bones, data.RigBones.Length);
			for (var l = 0; l < bonesArr.Length; ++l)
			{
				var db = data.RigBones[l];
				ref var rbi = ref bonesArr[l];
				rbi.Hash = db.Hash;
				rbi.HumanBodyPart = (AvatarMaskBodyPart)(-1);
				rbi.ParentBoneIndex = db.ParentBoneIndex;
				rbi.RefPose = db.RefPose;

#if AURORE_DEBUG
				if (db.Name.Length > 0)
					bb.AllocateString(ref rbi.Name, ref db.Name);
#endif
				hasher.Update(rbi.Hash);
			}

			if (data.IsHuman)
			{
				ref var humanData = ref bb.Allocate(ref c.HumanData);
				var humanToRigArr = bb.Allocate(ref humanData.HumanBoneToSkeletonBoneIndices, (int)HumanBodyBones.LastBone);
				var humanRotArr = bb.Allocate(ref humanData.HumanRotData, data.RigBones.Length);
				
				for (var j = 0; j < humanToRigArr.Length; ++j)
					humanToRigArr[j] = -1;

				for (var l = 0; l < humanRotArr.Length; ++l)
				{
					var db = data.RigBones[l].HumanRotation;
					ref var hrd = ref humanRotArr[l];
					hrd.PreRot = db.PreRot;
					hrd.PostRot = math.inverse(db.PostRot);
					hrd.Sign = db.Sign;
					hrd.MinMuscleAngles = db.MinAngle;
					hrd.MaxMuscleAngles = db.MaxAngle;

					if (db.HumanRigIndex >= 0)
					{
						humanToRigArr[db.HumanRigIndex] = l;
						//	Make muscle neutral ref pose
						ref var rbi = ref bonesArr[l];
						rbi.RefPose.Rotation = math.mul(hrd.PreRot, hrd.PostRot);
					}
				}

				SetHumanBodyBodyPartForBones(bonesArr, data);
			}

			c.Hash = new Hash128(hasher.DigestHash128());
			c.RootBoneIndex = data.RootBoneIndex;

			var rv = bb.CreateBlobAssetReference<RigDefinitionBlob>(Allocator.Persistent);

			for (var i = 0; i < OutBlobAssets.Length; ++i)
			{
				OutBlobAssets[i] = rv;
			}
		}

		private void SetHumanBodyBodyPartForBones(BlobBuilderArray<RigBoneInfo> rigBones, in RTP.RigDefinition rd)
		{
			var rigHumanAvatarMaskBodyParts = new NativeArray<AvatarMaskBodyPart>(rd.RigBones.Length, Allocator.Temp);

			//	Loop over bones and set human bones avatar mask directly
			for (var i = 0; i < rd.RigBones.Length; ++i)
			{
				var b = rd.RigBones[i];
				var humanRigIndex = b.HumanRotation.HumanRigIndex;
				if (humanRigIndex >= 0 && humanRigIndex < humanPartToAvatarMaskPartRemapTable.Length)
				{
					rigHumanAvatarMaskBodyParts[i] = humanPartToAvatarMaskPartRemapTable[humanRigIndex];
				}
				else
				{
					rigHumanAvatarMaskBodyParts[i] = (AvatarMaskBodyPart)(-1);
				}
			}

			//	Root bone is special case
			rigHumanAvatarMaskBodyParts[0] = AvatarMaskBodyPart.Root;

			//	For other bones search for parent with body part is set and set it to the same value
			for (var i = 0; i < rigBones.Length; ++i)
			{
				if (rigHumanAvatarMaskBodyParts[i] >= 0) continue;

				var l = i;
				var rl = rd.RigBones[l];
				while (rigHumanAvatarMaskBodyParts[l] < 0 && rl.ParentBoneIndex >= 0)
				{
					l = rl.ParentBoneIndex;
					rl = rd.RigBones[l];
				}

				if (l != i)
					rigHumanAvatarMaskBodyParts[i] = rigHumanAvatarMaskBodyParts[l];
			}

			for (var i = 0; i < rigBones.Length; ++i)
				rigBones[i].HumanBodyPart = rigHumanAvatarMaskBodyParts[i];
		}
	}

	[BurstCompile]
	private struct CreateComponentDatasJob: IJobParallelForBatch
	{
		[ReadOnly] public NativeArray<RigDefinitionBakerComponent> BakerData;
		[ReadOnly] public NativeArray<BlobAssetReference<RigDefinitionBlob>> BlobAssets;
		[ReadOnly] public ComponentLookup<BakingOnlyEntity> BakingOnlyLookup;

		public EntityCommandBuffer.ParallelWriter ECB;

		public void Execute(int startIndex, int count)
		{
			for (var i = startIndex; i < startIndex + count; ++i)
			{
				var rigBlob = BlobAssets[i];
				var rd = BakerData[i];

				var rdc = new RigDefinitionComponent
				{
					RigBlob = rigBlob,
					ApplyRootMotion = rd.ApplyRootMotion
				};

				ECB.AddComponent(startIndex, rd.TargetEntity, rdc);

				for (var l = 0; l < rd.RigDefData.RigBones.Length; ++l)
				{
					var rb = rd.RigDefData.RigBones[l];

					var boneEntity = rb.BoneObjectEntity;
					if (boneEntity != Entity.Null && !BakingOnlyLookup.HasComponent(boneEntity))
					{
						var animatorEntityRefComponent = new AnimatorEntityRefComponent
						{
							AnimatorEntity = rd.TargetEntity,
							BoneIndexInAnimationRig = l
						};
						ECB.AddComponent(startIndex, boneEntity, animatorEntityRefComponent);
					}
				}

				ECB.AddBuffer<RootMotionAnimationStateComponent>(startIndex, rd.TargetEntity);
			}
		}
	}
}
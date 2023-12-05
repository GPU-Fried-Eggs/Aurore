using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using FixedStringName = Unity.Collections.FixedString512Bytes;

[TemporaryBakingType]
public struct SkinnedMeshBakerData: IComponentData
{
	public RTP.SkinnedMeshBoneData SkinnedMeshBones;
	public Entity TargetEntity;
	public Entity RootBoneEntity;
	public Entity AnimatedRigEntity;
	public int Hash;
#if AURORE_DEBUG
	public FixedStringName SkeletonName;
#endif
}

public class SkinnedMeshBaker: Baker<SkinnedMeshRenderer>
{
	public override void Bake(SkinnedMeshRenderer a)
	{
		var skinnedMeshBoneData = CreateSkinnedMeshBoneData(a);

		//	Create additional "bake-only" entity that will be removed from live world
		var entity = CreateAdditionalEntity(TransformUsageFlags.None, true);
		var data = new SkinnedMeshBakerData
		{
			SkinnedMeshBones = skinnedMeshBoneData,
			TargetEntity = GetEntity(TransformUsageFlags.Dynamic),
			AnimatedRigEntity = GetEntity(a.gameObject.GetComponentInParent<RigDefinitionAuthoring>(true), TransformUsageFlags.Dynamic),
			RootBoneEntity = GetEntity(a.rootBone, TransformUsageFlags.Dynamic),
			Hash = a.sharedMesh.GetHashCode(),
#if AURORE_DEBUG
			SkeletonName = a.name
#endif
		};

		AddComponent(entity, data);
	}

	private RTP.SkinnedMeshBoneData CreateSkinnedMeshBoneData(SkinnedMeshRenderer r)
	{ 
		var bakedBoneData = new RTP.SkinnedMeshBoneData();
		bakedBoneData.Bones = new UnsafeList<RTP.SkinnedMeshBoneDefinition>(r.bones.Length, Allocator.Persistent);
		bakedBoneData.Bones.Length = r.bones.Length;
		bakedBoneData.SkeletonName = r.name;
		bakedBoneData.ParentBoneName = r.rootBone != null ? r.rootBone.name : "";
		for (var j = 0; j < r.bones.Length; ++j)
		{
			var bone = r.bones[j];
			var bakedBoneInfo = new RTP.SkinnedMeshBoneDefinition();
#if AURORE_DEBUG
			bakedBoneInfo.Name = bone.name;
#endif
			var bn = new FixedStringName(bone.name);
			bakedBoneInfo.Hash = bn.CalculateHash128();
			bakedBoneInfo.BindPose = r.sharedMesh.bindposes[j];
			bakedBoneData.Bones[j] = bakedBoneInfo;
		}
		return bakedBoneData;
	}
}
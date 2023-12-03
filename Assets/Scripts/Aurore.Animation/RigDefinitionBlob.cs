using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

public struct RigBoneInfo
{
#if AURORE_DEBUG
	public BlobString Name;
#endif
	public Hash128 Hash;
	public int ParentBoneIndex;
	public BoneTransform RefPose;
	public AvatarMaskBodyPart HumanBodyPart;
}

public struct HumanRotationData
{
	public float3 MinMuscleAngles;
	public float3 MaxMuscleAngles;
	public quaternion PreRot;
	public quaternion PostRot;
	public float3 Sign;
}

public struct HumanData
{
	public BlobArray<HumanRotationData> HumanRotData;
	public BlobArray<int> HumanBoneToSkeletonBoneIndices;
}

public struct RigDefinitionBlob
{
#if AURORE_DEBUG
	public BlobString Name;
#endif
	public Hash128 Hash;
	public BlobArray<RigBoneInfo> Bones;
	public BlobPtr<HumanData> HumanData;
	public int RootBoneIndex;
}

public struct RigDefinitionComponent: IComponentData, IEnableableComponent
{
	public BlobAssetReference<RigDefinitionBlob> RigBlob;
	public bool ApplyRootMotion;
}

public struct BoneRemapTableBlob
{
	public BlobArray<int> RigBoneToSkinnedMeshBoneRemapIndices;
}
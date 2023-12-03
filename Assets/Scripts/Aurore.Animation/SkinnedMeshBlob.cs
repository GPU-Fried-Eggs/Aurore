using Unity.Entities;
using Unity.Mathematics;
using Hash128 = Unity.Entities.Hash128;

public struct SkinnedMeshBoneInfo
{
#if AURORE_DEBUG
	public BlobString name;
#endif
	public Hash128 Hash;
	public float4x4 BindPose;
}

public struct SkinnedMeshInfoBlob
{
#if AURORE_DEBUG
	public BlobString skeletonName;
#endif
	public Hash128 Hash;
	public Hash128 RootBoneNameHash;
	public BlobArray<SkinnedMeshBoneInfo> Bones;
}
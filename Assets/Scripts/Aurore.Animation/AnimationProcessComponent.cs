using Unity.Entities;
using FixedStringName = Unity.Collections.FixedString512Bytes;

[InternalBufferCapacity(4)]
[ChunkSerializable]
public struct AnimationToProcessComponent: IBufferElementData
{
	public float Weight;
	public float Time;
	public ExternalBlobPtr<AnimationClipBlob> Animation;
	public ExternalBlobPtr<AvatarMaskBlob> AvatarMask;
	public AnimationBlendingMode BlendMode;
	public float LayerWeight;
	public int LayerIndex;
}

public struct AnimatorEntityRefComponent: IComponentData
{
	public int BoneIndexInAnimationRig;
	public Entity AnimatorEntity;
}

public struct AnimatedSkinnedMeshComponent: IComponentData
{
	public Entity AnimatedRigEntity;
	public int RootBoneIndexInRig;
	public BlobAssetReference<SkinnedMeshInfoBlob> BoneInfos;
}

public struct RootMotionAnimationStateComponent: IBufferElementData, IEnableableComponent
{
	public Hash128 AnimationHash;
	public BoneTransform AnimationState;
}
public static class SpecialBones
{
	public static readonly FixedStringName unnamedRootBoneName = "AURORE_UnnamedRootBone";
	public static readonly FixedStringName rootMotionDeltaBoneName = "AURORE_RootDeltaMotionBone";
	public static readonly FixedStringName invalidBoneName = "AURORE_INVALID_BONE";
}
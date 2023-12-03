using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;

public enum BindingType : short
{
    Translation,
    Quaternion,
    EulerAngles,
    HumanMuscle,
    Scale,
    Unknown
}

public struct KeyFrame
{
    public float V;
    public float InTan, OutTan;
    public float Time;
}

public struct AnimationCurve
{
    public BindingType BindingType;
    public short ChannelIndex; // 0, 1, 2, 3 -> x, y, z, w
    public BlobArray<KeyFrame> KeyFrames;
}

public struct BoneClipBlob
{
#if AURORE_DEBUG
	public BlobString Name;
#endif
    public Hash128 Hash;
    public bool IsHumanMuscleClip;
    public BlobArray<AnimationCurve> AnimationCurves;
}

public struct AvatarMaskBlob
{
#if AURORE_DEBUG
	public BlobString Name;
	public BlobArray<BlobString> IncludedBoneNames;
#endif
    public Hash128 Hash;
    public BlobArray<Hash128> IncludedBoneHashes;
    public uint HumanBodyPartsAvatarMask;
}

public struct AnimationClipBlob
{
#if AURORE_DEBUG
	public BlobString Name;
#endif
    public Hash128 Hash;
    public BlobArray<BoneClipBlob> Bones;
    public BlobArray<int2> BonesPerfectHashSeedTable;
    public BlobArray<BoneClipBlob> Curves;
    uint m_Flags;
    public float CycleOffset;
    public float Length;
    public float AdditiveReferencePoseTime;

    public bool Looped
    {
        get => GetFlag(1);
        set => SetFlag(1, value);
    }

    public bool LoopPoseBlend
    {
        get => GetFlag(2);
        set => SetFlag(2, value);
    }

    public bool HasRootMotionCurves
    {
        get => GetFlag(3);
        set => SetFlag(3, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetFlag(int index, bool value)
    {
        var v = 1u << index;
        var mask = ~v;
        var valueBits = math.select(0, v, value);
        m_Flags = m_Flags & mask | valueBits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool GetFlag(int index)
    {
        var v = 1u << index;
        return (m_Flags & v) != 0;
    }
}
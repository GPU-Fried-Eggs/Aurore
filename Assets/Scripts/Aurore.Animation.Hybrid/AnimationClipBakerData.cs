using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using FixedStringName = Unity.Collections.FixedString512Bytes;

namespace RTP
{
    public struct AnimationClip : IDisposable
    {
        public FixedStringName Name;
        public UnsafeList<BoneClip> Bones;
        public UnsafeList<BoneClip> Curves;
        public bool Looped;
        public bool LoopPoseBlend;
        public float CycleOffset;
        public float Length;
        public float AdditiveReferencePoseTime;
        public bool HasRootMotionCurves;
        public Hash128 Hash;

        public void Dispose()
        {
            foreach (var a in Bones) a.Dispose();
            foreach (var a in Curves) a.Dispose();

            Bones.Dispose();
        }
    }

    public struct BoneClip : IEquatable<Hash128>, IDisposable
    {
        public FixedStringName Name;
        public Hash128 NameHash;
        public bool IsHumanMuscleClip;
        public UnsafeList<AnimationCurve> AnimationCurves;

        public bool Equals(Hash128 o) => o == NameHash;

        public void SetName(string n)
        {
            Name = n;
            NameHash = Name.CalculateHash128();
        }

        public void DisposeCurves()
        {
            foreach (var a in AnimationCurves) a.Dispose();
            AnimationCurves.Clear();
        }

        public void Dispose()
        {
            DisposeCurves();
            AnimationCurves.Dispose();
        }
    }

    public struct AnimationCurve : IDisposable
    {
        public BindingType BindingType;
        public short ChannelIndex; // 0, 1, 2, 3 -> x, y, z, w
        public UnsafeList<KeyFrame> KeyFrames;

        public void Dispose() => KeyFrames.Dispose();
    }
}
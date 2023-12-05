using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using FixedStringName = Unity.Collections.FixedString512Bytes;

namespace RTP
{
    public struct SkinnedMeshBoneDefinition
    {
        public FixedStringName Name;
        public Hash128 Hash;
        public float4x4 BindPose;
    }

    public struct SkinnedMeshBoneData : IDisposable
    {
        public FixedStringName SkeletonName;
        public FixedStringName ParentBoneName;
        public UnsafeList<SkinnedMeshBoneDefinition> Bones;

        public void Dispose() => Bones.Dispose();
    }
}
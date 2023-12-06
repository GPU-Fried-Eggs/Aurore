using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Hash128 = Unity.Entities.Hash128;
using FixedStringName = Unity.Collections.FixedString512Bytes;

namespace RTP
{
    public struct RigBoneInfo : IEquatable<Hash128>
    {
        public struct HumanRotationData
        {
            public float3 MinAngle, MaxAngle;
            public quaternion PreRot, PostRot;
            public float3 Sign;
            public int HumanRigIndex;

            public static readonly HumanRotationData Identity = new HumanRotationData
            {
                PreRot = quaternion.identity,
                PostRot = quaternion.identity,
                HumanRigIndex = -1,
                Sign = 1
            };
        };

        public FixedStringName Name;
        public Hash128 Hash;
        public int ParentBoneIndex;
        public BoneTransform RefPose;
        public Entity BoneObjectEntity;
        public HumanRotationData HumanRotation;

        public bool Equals(Hash128 o) => o == Hash;
    }

    public struct RigDefinition : IDisposable
    {
        public FixedStringName Name;
        public UnsafeList<RigBoneInfo> RigBones;
        public bool IsHuman;
        public int RootBoneIndex;

        public void Dispose() => RigBones.Dispose();

        public override unsafe int GetHashCode()
        {
            var hh = new xxHash3.StreamingState();
            hh.Update(Name.GetUnsafePtr(), Name.Length);
            foreach (var b in RigBones)
            {
                hh.Update(b.Hash.Value);
            }

            var rv = math.hash(hh.DigestHash128());
            return (int)rv;
        }
    }
}
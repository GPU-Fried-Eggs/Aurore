using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Physics
{
    [Serializable]
    public struct GlobalGravityZone : IComponentData
    {
        public float3 Gravity;
    }
}
using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Interactive
{
    [Serializable]
    public struct WindZone : IComponentData
    {
        public float3 WindForce;
    }
}
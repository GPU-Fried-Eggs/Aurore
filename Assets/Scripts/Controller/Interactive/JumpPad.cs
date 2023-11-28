using System;
using Unity.Entities;

namespace Interactive
{
    [Serializable]
    public struct JumpPad : IComponentData
    {
        public float JumpPower;
        public float UngroundingDotThreshold;
    }
}
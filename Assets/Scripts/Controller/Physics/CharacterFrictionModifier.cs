using System;
using Unity.Entities;

namespace Physics
{
    [Serializable]
    public struct CharacterFrictionModifier : IComponentData
    {
        public float Friction;
    }
}

using System;
using Unity.Entities;

namespace Interactive
{
    [Serializable]
    public struct Teleporter : IComponentData
    {
        public Entity DestinationEntity;
    }
}
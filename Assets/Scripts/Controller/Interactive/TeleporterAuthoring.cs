using Unity.Entities;
using UnityEngine;

namespace Interactive
{
    [DisallowMultipleComponent]
    public class TeleporterAuthoring : MonoBehaviour
    {
        public GameObject Destination;

        private class TeleporterBaker : Baker<TeleporterAuthoring>
        {
            public override void Bake(TeleporterAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new Teleporter
                {
                    DestinationEntity = GetEntity(authoring.Destination, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}
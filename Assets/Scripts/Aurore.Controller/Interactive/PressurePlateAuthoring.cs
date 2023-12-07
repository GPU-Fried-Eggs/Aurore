using Unity.Entities;
using UnityEngine;

namespace Interactive
{
    public class PressurePlateAuthoring : MonoBehaviour
    {
        public PressurePlate PressurePlate = default;

        private class PressurePlateBaker : Baker<PressurePlateAuthoring>
        {
            public override void Bake(PressurePlateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, authoring.PressurePlate);
            }
        }
    }
}
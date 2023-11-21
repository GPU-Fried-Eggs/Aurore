using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Physics
{
    public class GlobalGravityZoneAuthoring : MonoBehaviour
    {
        public float3 Gravity;

        private class GlobalGravityZoneBaker : Baker<GlobalGravityZoneAuthoring>
        {
            public override void Bake(GlobalGravityZoneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new GlobalGravityZone
                {
                    Gravity = authoring.Gravity
                });
            }
        }
    }
}
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Physics
{
    public class GravityZoneAuthoring : MonoBehaviour
    {
        public float3 Gravity;

        private class GravityZoneBaker : Baker<GravityZoneAuthoring>
        {
            public override void Bake(GravityZoneAuthoring authoring)
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
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Interactive
{
    public class WindZoneAuthoring : MonoBehaviour
    {
        public float3 WindForce;

        private class WindZoneBaker : Baker<WindZoneAuthoring>
        {
            public override void Bake(WindZoneAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new WindZone
                {
                    WindForce = authoring.WindForce
                });
            }
        }
    }
}
using Unity.Entities;
using UnityEngine;

namespace Physics
{
    public class CustomGravityAuthoring : MonoBehaviour
    {
        public float GravityMultiplier = 1f;

        private class CustomGravityBaker : Baker<CustomGravityAuthoring>
        {
            public override void Bake(CustomGravityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new CustomGravity
                {
                    GravityMultiplier = authoring.GravityMultiplier
                });
            }
        }
    }
}

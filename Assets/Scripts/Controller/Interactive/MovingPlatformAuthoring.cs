using Unity.Entities;
using UnityEngine;

namespace Interactive
{
    [DisallowMultipleComponent]
    public class MovingPlatformAuthoring : MonoBehaviour
    {
        public MovingPlatform MovingPlatform = default;

        private class MovingPlatformBaker : Baker<MovingPlatformAuthoring>
        {
            public override void Bake(MovingPlatformAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                authoring.MovingPlatform.OriginalPosition = authoring.transform.position;
                authoring.MovingPlatform.OriginalRotation = authoring.transform.rotation;

                AddComponent(entity, authoring.MovingPlatform);
            }
        }
    }
}
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Interactive
{
    [DisallowMultipleComponent]
    public class TrackedTransformAuthoring : MonoBehaviour
    {
        private class Baker : Baker<TrackedTransformAuthoring>
        {
            public override void Bake(TrackedTransformAuthoring authoring)
            {
                var currentTransform = new RigidTransform(authoring.transform.rotation, authoring.transform.position);
                var trackedTransform = new TrackedTransform
                {
                    CurrentFixedRateTransform = currentTransform,
                    PreviousFixedRateTransform = currentTransform,
                };

                AddComponent(GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace), trackedTransform);
            }
        }
    }
}

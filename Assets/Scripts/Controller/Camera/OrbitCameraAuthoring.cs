using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Camera
{
    [DisallowMultipleComponent]
    public class OrbitCameraAuthoring : MonoBehaviour
    {
        public List<GameObject> IgnoredEntities = new();
        public OrbitCamera OrbitCamera = OrbitCamera.GetDefault();

        private class OrbitCameraBaker : Baker<OrbitCameraAuthoring>
        {
            public override void Bake(OrbitCameraAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
        
                authoring.OrbitCamera.CurrentDistanceFromMovement = authoring.OrbitCamera.TargetDistance;
                authoring.OrbitCamera.CurrentDistanceFromObstruction = authoring.OrbitCamera.TargetDistance;
                authoring.OrbitCamera.PlanarForward = -math.forward();

                AddComponent(entity, authoring.OrbitCamera);
                AddComponent(entity, new OrbitCameraControl());
                var ignoredEntitiesBuffer = AddBuffer<OrbitCameraIgnoredEntityBufferElement>(entity);

                foreach (var ignoredEntity in authoring.IgnoredEntities)
                {
                    ignoredEntitiesBuffer.Add(new OrbitCameraIgnoredEntityBufferElement
                    {
                        Entity = GetEntity(ignoredEntity, TransformUsageFlags.None),
                    });
                }
            }
        }
    }
}

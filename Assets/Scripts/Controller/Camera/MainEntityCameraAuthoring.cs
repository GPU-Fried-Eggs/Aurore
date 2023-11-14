using Unity.Entities;
using UnityEngine;

namespace Camera
{
    [DisallowMultipleComponent]
    public class MainEntityCameraAuthoring : MonoBehaviour
    {
        private class MainEntityCameraBaker : Baker<MainEntityCameraAuthoring>
        {
            public override void Bake(MainEntityCameraAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<MainEntityCamera>(entity);
            }
        }
    }
}
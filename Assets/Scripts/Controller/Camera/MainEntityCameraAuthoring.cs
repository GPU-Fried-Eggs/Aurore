using Unity.Entities;
using UnityEngine;

namespace Camera
{
    public class MainGameObjectCamera : MonoBehaviour
    {
        public static UnityEngine.Camera Instance;

        private void Awake() => Instance = GetComponent<UnityEngine.Camera>();
    }

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
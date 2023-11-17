using Unity.Entities;
using Unity.Transforms;

namespace Camera
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MainEntityCameraSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (MainGameObjectCamera.Instance != null && SystemAPI.HasSingleton<MainEntityCamera>())
            {
                var mainEntityCameraEntity = SystemAPI.GetSingletonEntity<MainEntityCamera>();
                var targetLocalToWorld = SystemAPI.GetComponent<LocalToWorld>(mainEntityCameraEntity);
                MainGameObjectCamera.Instance.transform.SetPositionAndRotation(targetLocalToWorld.Position, targetLocalToWorld.Rotation);
            }
        }
    }
}

using Unity.Entities;
using UnityEngine;

namespace Player
{
    [DisallowMultipleComponent]
    public class PlayerAuthoring : MonoBehaviour
    {
        public GameObject ControlledCharacter;
        public GameObject ControlledCamera;

        private class PlayerBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new PlayerData
                {
                    ControlledCharacter = GetEntity(authoring.ControlledCharacter, TransformUsageFlags.Dynamic),
                    ControlledCamera = GetEntity(authoring.ControlledCamera, TransformUsageFlags.Dynamic),
                });
                AddComponent(entity, new PlayerInputs());
            }
        }
    }
}
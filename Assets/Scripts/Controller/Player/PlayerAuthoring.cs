using Unity.Entities;
using UnityEngine;

namespace Controller.Player
{
    [DisallowMultipleComponent]
    public class PlayerAuthoring : MonoBehaviour
    {
        public GameObject ControlledCharacter;

        private class PlayerBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new PlayerData
                {
                    ControlledCharacter = GetEntity(authoring.ControlledCharacter, TransformUsageFlags.Dynamic),
                });
                AddComponent<PlayerCommands>(entity);
            }
        }
    }
}
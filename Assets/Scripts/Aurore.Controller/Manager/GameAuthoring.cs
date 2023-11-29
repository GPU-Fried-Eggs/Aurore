using Unity.Entities;
using UnityEngine;

namespace Manager
{
    public class GameAuthoring : MonoBehaviour
    {
        public GameObject CharacterPrefabEntity;
        public GameObject CameraPrefabEntity;
        public GameObject PlayerPrefabEntity;

        private class GameBaker : Baker<GameAuthoring>
        {
            public override void Bake(GameAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new GameData
                {
                    CharacterPrefabEntity = GetEntity(authoring.CharacterPrefabEntity, TransformUsageFlags.Dynamic),
                    CameraPrefabEntity = GetEntity(authoring.CameraPrefabEntity, TransformUsageFlags.Dynamic),
                    PlayerPrefabEntity = GetEntity(authoring.PlayerPrefabEntity, TransformUsageFlags.None),
                });
            }
        }
    }
}
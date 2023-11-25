using Unity.Entities;
using UnityEngine;
using Utilities;

namespace Manager
{
    public class GameAuthoring : MonoBehaviour
    {
        [Header("Network Parameters")] 
        public int TickRate = 60;
        public int SendRate = 60;
        public int MaxSimulationStepsPerFrame = 4;
        public float JoinTimeout = 10f;

        [Header("Scenes")] 
        public BakedSubSceneReference GameMenuScene;
        public BakedSubSceneReference GameConfigScene;
        public BakedSubSceneReference GameScene;

        [Header("Reference")]
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
                    CharacterPrefabEntity = GetEntity(authoring.CharacterPrefabEntity, TransformUsageFlags.None),
                    CameraPrefabEntity = GetEntity(authoring.CameraPrefabEntity, TransformUsageFlags.None),
                    PlayerPrefabEntity = GetEntity(authoring.PlayerPrefabEntity, TransformUsageFlags.None),
                });
            }
        }
    }
}

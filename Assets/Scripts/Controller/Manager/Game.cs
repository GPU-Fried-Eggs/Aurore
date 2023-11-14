using System;
using Unity.Entities;

namespace Manager
{
    [Serializable]
    public struct GameData : IComponentData
    {
        /// <summary>
        /// The character prefab includes character authoring
        /// </summary>
        public Entity CharacterPrefabEntity;
        /// <summary>
        /// The camera prefab includes camera authoring
        /// </summary>
        public Entity CameraPrefabEntity;
        /// <summary>
        /// The player prefab includes player authoring
        /// </summary>
        public Entity PlayerPrefabEntity;
    }
}
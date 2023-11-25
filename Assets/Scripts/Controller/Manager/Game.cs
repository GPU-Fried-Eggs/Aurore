using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.NetCode;

namespace Manager
{
    [Serializable]
    public struct GameData : IComponentData
    {
        /// <summary>
        /// The fixed simulation frequency on the server and prediction loop
        /// </summary>
        public int TickRate;
        /// <summary>
        /// The rate at which the server sends snapshots to the clients
        /// </summary>
        public int SendRate;
        /// <summary>
        /// This setting puts a limit on how many such updates it can do in a single frame
        /// </summary>
        public int MaxSimulationStepsPerFrame;
        /// <summary>
        /// The maximum join timeout
        /// </summary>
        public float JoinTimeout;

        /// <summary>
        /// The entrance scene
        /// </summary>
        public EntitySceneReference GameMenuScene;
        /// <summary>
        /// The global game data holder
        /// </summary>
        public EntitySceneReference GameConfigScene;
        /// <summary>
        /// The game scene
        /// </summary>
        public EntitySceneReference GameScene;

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

        /// <summary>
        /// Get the client and server tick rate
        /// </summary>
        /// <returns> The client and server tick rate config </returns>
        public ClientServerTickRate GetClientServerTickRate()
        {
             var tickRate = new ClientServerTickRate();
            tickRate.ResolveDefaults();
            tickRate.SimulationTickRate = TickRate;
            tickRate.NetworkTickRate = SendRate;
            tickRate.MaxSimulationStepsPerFrame = MaxSimulationStepsPerFrame;
            tickRate.PredictedFixedStepSimulationTickRatio = 1;
            return tickRate;
        }
    }

    [Serializable]
    public struct LocalGameData : IComponentData
    {
        /// <summary>
        /// The local player name
        /// </summary>
        public FixedString128Bytes LocalPlayerName;
    }

    public enum MenuState
    {
        InMenu,
        Connecting,
        InGame,
    }
}

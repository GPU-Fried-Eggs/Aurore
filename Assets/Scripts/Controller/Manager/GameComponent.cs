using System;
using Unity.Entities;
using Unity.NetCode;

namespace Controller.Manager
{
    [Serializable]
    public struct GameComponent : IComponentData
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

        public ClientServerTickRate GetClientServerTickRate()
        {
             var tickRate = new ClientServerTickRate();
            tickRate.ResolveDefaults();
            tickRate.SimulationTickRate = TickRate;
            tickRate.NetworkTickRate = SendRate;
            tickRate.MaxSimulationStepsPerFrame = MaxSimulationStepsPerFrame;
            return tickRate;
        }
    }
}
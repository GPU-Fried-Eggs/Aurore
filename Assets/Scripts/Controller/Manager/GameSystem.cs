using System;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine.Scripting;

namespace Controller.Manager
{
    [Preserve]
    public class NetCodeBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            CreateLocalWorld(defaultWorldName);
            return true;
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class GameSystem : SystemBase
    {
        [Serializable]
        public struct Singleton : IComponentData
        {
            
        }

        public World ClientWorld;
        public World ServerWorld;
    
        public const string k_LocalHost = "127.0.0.1";

        protected override void OnCreate()
        {
            base.OnCreate();

            // Auto-create singleton
            EntityManager.CreateEntity(typeof(Singleton));

            RequireForUpdate<GameComponent>();
            RequireForUpdate<Singleton>();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
        
            // Start a tmp server just once so we can get a firewall prompt when running the game for the first time
            {
                var tmpNetDriver = NetworkDriver.Create();
                var tmpEndPoint = NetworkEndpoint.Parse(k_LocalHost, 7777);
                if (tmpNetDriver.Bind(tmpEndPoint) == 0) tmpNetDriver.Listen();
                tmpNetDriver.Dispose();
            }
        }

        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                .CreateCommandBuffer(World.Unmanaged);

            ref var singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
            var gameComponent = SystemAPI.GetSingleton<GameComponent>();
        }
    }
}
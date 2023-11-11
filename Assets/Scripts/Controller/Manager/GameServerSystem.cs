using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Random = Unity.Mathematics.Random;

namespace Controller.Manager
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct GameServerSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public bool RequestingDisconnect;

            public Random Random;
            public bool AcceptJoins;

            public int DisconnectionFramesCounter;
        }

        public struct JoinRequestAccepted : IRpcCommand { }

        private EntityQuery m_SingletonQuery;
        private EntityQuery m_JoinRequestQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameComponent>();

            m_SingletonQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<Singleton>()
                .Build(state.EntityManager);
            m_JoinRequestQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<GameClientSystem.JoinRequest, ReceiveRpcCommandRequest>()
                .Build(ref state);
        
            // Auto-create singleton
            var randomSeed = (uint)DateTime.Now.Millisecond;
            var singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new Singleton
            {
                Random = Random.CreateFromIndex(randomSeed),
            });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var singleton = ref m_SingletonQuery.GetSingletonRW<Singleton>().ValueRW;
            var gameComponent = SystemAPI.GetSingleton<GameComponent>();
            
        }
    }
}
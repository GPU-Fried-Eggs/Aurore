using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Controller.Manager
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [BurstCompile]
    public partial struct GameClientSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public Random Random;
        
            public float TimeWithoutAConnection;

            public int DisconnectionFramesCounter;
        }

        public struct JoinRequest : IRpcCommand
        {
            public FixedString128Bytes PlayerName;
        }

        public struct DisconnectRequest : IComponentData { }

        private EntityQuery m_SingletonQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameComponent>();

            m_SingletonQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<Singleton>()
                .Build(state.EntityManager);

            // Auto-create singleton
            var singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new Singleton
            {
                Random = Random.CreateFromIndex(0),
            });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var singleton = ref m_SingletonQuery.GetSingletonRW<Singleton>().ValueRW;
            var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
            var postTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(true);
            var gameComponent = SystemAPI.GetSingleton<GameComponent>();
            
        }
    }
}
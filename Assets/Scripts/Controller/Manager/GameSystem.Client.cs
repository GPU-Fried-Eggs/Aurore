using Character.Kinematic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Manager
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [BurstCompile]
    public partial struct ClientGameSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public Random Random;
        
            public float TimeWithoutAConnection;

            public int DisconnectionFramesCounter;
        }

        public struct JoinOnceScenesLoadedRequest : IComponentData
        {
            public Entity PendingSceneLoadRequest;
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
            state.RequireForUpdate<GameData>();

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
            var gameData = SystemAPI.GetSingleton<GameData>();

            if (SystemAPI.HasSingleton<DisableCharacterDynamicContacts>())
            {
                state.EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<DisableCharacterDynamicContacts>());
            }

            HandleSendJoinRequestOncePendingScenesLoaded(ref state);
            HandlePendingJoinRequest(ref state, ref singleton);
            HandleDisconnect(ref state, ref singleton, gameData);
        }

        private void HandleSendJoinRequestOncePendingScenesLoaded(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);
        
            foreach (var (request, entity) in SystemAPI.Query<JoinOnceScenesLoadedRequest>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<SceneLoadRequest>(request.PendingSceneLoadRequest) &&
                    SystemAPI.GetComponent<SceneLoadRequest>(request.PendingSceneLoadRequest).IsLoaded)
                {
                    var localData = SystemAPI.GetSingleton<LocalGameData>();

                    // Send join request
                    if (SystemAPI.HasSingleton<NetworkId>())
                    {
                        var joinRequestEntity = ecb.CreateEntity();
                        ecb.AddComponent(joinRequestEntity, new JoinRequest { PlayerName = localData.LocalPlayerName });
                        ecb.AddComponent(joinRequestEntity, new SendRpcCommandRequest());
                
                        ecb.DestroyEntity(request.PendingSceneLoadRequest);
                        ecb.DestroyEntity(entity);
                    }
                }
            }
        }

        private void HandlePendingJoinRequest(ref SystemState state, ref Singleton singleton)
        {
            var ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);

            if (SystemAPI.HasSingleton<NetworkId>() && !SystemAPI.HasSingleton<NetworkStreamInGame>())
            {
                singleton.TimeWithoutAConnection = 0f;
            
                // Check for request accept
                foreach (var (requestAccepted, rpcReceive, entity) in SystemAPI
                             .Query<ServerGameSystem.JoinRequestAccepted, ReceiveRpcCommandRequest>()
                             .WithEntityAccess())
                {
                    // Stream in game
                    ecb.AddComponent(SystemAPI.GetSingletonEntity<NetworkId>(), new NetworkStreamInGame());

                    ecb.DestroyEntity(entity);
                }
            }
        }

        private void HandleDisconnect(ref SystemState state, ref Singleton singleton, GameData data)
        {
            var ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Check for connection timeout
            if (!SystemAPI.HasSingleton<NetworkId>())
            {
                singleton.TimeWithoutAConnection += SystemAPI.Time.DeltaTime;
                if (singleton.TimeWithoutAConnection > data.JoinTimeout)
                {
                    var disconnectEntity = ecb.CreateEntity();
                    ecb.AddComponent(disconnectEntity, new DisconnectRequest());
                }
            }

            // Handle disconnecting & properly disposing world
            var disconnectRequestQuery = SystemAPI.QueryBuilder().WithAll<DisconnectRequest>().Build();
            if (disconnectRequestQuery.CalculateEntityCount() > 0)
            {
                // Add disconnect request to connection
                foreach (var (connection, entity) in SystemAPI.Query<NetworkId>()
                             .WithNone<NetworkStreamRequestDisconnect>()
                             .WithEntityAccess())
                {
                    ecb.AddComponent(entity, new NetworkStreamRequestDisconnect());
                }
            
                // Allow systems to have updated since disconnection, for cleanup
                if (singleton.DisconnectionFramesCounter > 3)
                {
                    var disposeRequestEntity = ecb.CreateEntity();
                    ecb.AddComponent(disposeRequestEntity, new GameSystem.DisposeClientWorldRequest());
                    ecb.AddComponent(disposeRequestEntity, new MoveToLocalWorld());
                    ecb.DestroyEntity(disconnectRequestQuery, EntityQueryCaptureMode.AtRecord);
                }
                
                singleton.DisconnectionFramesCounter++;
            }
        }
    }
}

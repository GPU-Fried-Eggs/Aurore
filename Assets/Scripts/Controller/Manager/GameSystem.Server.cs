using System;
using Camera;
using Character.Kinematic;
using Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace Manager
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct ServerGameSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            public bool RequestingDisconnect;

            public Random Random;
            public bool AcceptJoins;

            public int DisconnectionFramesCounter;
            public NativeHashMap<int, Entity> ConnectionEntityMap;
        }

        public struct AcceptJoinsOnceScenesLoadedRequest : IComponentData
        {
            public Entity PendingSceneLoadRequest;
        }

        public struct PendingClient : IComponentData
        {
            public float TimeConnected;
            public bool IsJoining;
        }

        public struct JoinedClient : IComponentData
        {
            public Entity PlayerEntity;
        }

        public struct JoinRequestAccepted : IRpcCommand { }

        public struct ClientOwnedEntities : ICleanupBufferElementData
        {
            public Entity Entity;
        }

        public struct DisconnectRequest : IComponentData { }

        public struct CharacterSpawnRequest : IComponentData
        {
            public Entity ForConnection;
            public float Delay;
        }

        private EntityQuery m_JoinRequestQuery;
        private EntityQuery m_ConnectionsQuery;
        private NativeHashMap<int, Entity> m_ConnectionEntityMap;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameData>();

            m_JoinRequestQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ClientGameSystem.JoinRequest, ReceiveRpcCommandRequest>()
                .Build(ref state);
            m_ConnectionsQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<NetworkId>()
                .Build(state.EntityManager);

            m_ConnectionEntityMap = new NativeHashMap<int, Entity>(300, Allocator.Persistent);
        
            // Auto-create singleton
            var randomSeed = (uint)DateTime.Now.Millisecond;
            var singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new Singleton
            {
                Random = Random.CreateFromIndex(randomSeed),
                ConnectionEntityMap = m_ConnectionEntityMap
            });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (m_ConnectionEntityMap.IsCreated)
            {
                m_ConnectionEntityMap.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
            var gameData = SystemAPI.GetSingleton<GameData>();

            if (SystemAPI.HasSingleton<DisableCharacterDynamicContacts>())
            {
                state.EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<DisableCharacterDynamicContacts>());
            }

            BuildConnectionEntityMap(ref state, ref singleton);
            HandleAcceptJoinsOncePendingScenesAreLoaded(ref state, ref singleton);
            HandleJoinRequests(ref state, ref singleton, gameData);
            HandlePendingJoinClientTimeout(ref state, gameData);
            HandleDisconnect(ref state, ref singleton);
            HandleSpawnCharacter(ref state, ref singleton, gameData);
        }

        private void BuildConnectionEntityMap(ref SystemState state, ref Singleton singleton)
        {
            var connectionEntities = m_ConnectionsQuery.ToEntityArray(state.WorldUpdateAllocator);
            var connections = m_ConnectionsQuery.ToComponentDataArray<NetworkId>(state.WorldUpdateAllocator);
        
            singleton.ConnectionEntityMap.Clear();
            for (var i = 0; i < connections.Length; i++)
            {
                singleton.ConnectionEntityMap.TryAdd(connections[i].Value, connectionEntities[i]);
            }

            connectionEntities.Dispose(state.Dependency);
            connections.Dispose(state.Dependency);
        }

        private void HandleAcceptJoinsOncePendingScenesAreLoaded(ref SystemState state, ref Singleton singleton)
        {
            var ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (request, entity) in SystemAPI.Query<AcceptJoinsOnceScenesLoadedRequest>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<SceneLoadRequest>(request.PendingSceneLoadRequest) &&
                    SystemAPI.GetComponent<SceneLoadRequest>(request.PendingSceneLoadRequest).IsLoaded)
                {
                    singleton.AcceptJoins = true;
                    ecb.DestroyEntity(request.PendingSceneLoadRequest);
                    ecb.DestroyEntity(entity);
                }
            }
        }

        private void HandleJoinRequests(ref SystemState state, ref Singleton singleton, GameData data)
        {
            if (singleton.AcceptJoins && m_JoinRequestQuery.CalculateEntityCount() > 0)
            {
                var ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged);

                // Process join requests
                foreach (var (request, rpcReceive, entity) in SystemAPI
                             .Query<ClientGameSystem.JoinRequest, ReceiveRpcCommandRequest>()
                             .WithEntityAccess())
                {
                    if (SystemAPI.HasComponent<NetworkId>(rpcReceive.SourceConnection) &&
                        !SystemAPI.HasComponent<JoinedClient>(rpcReceive.SourceConnection))
                    {
                        var ownerConnectionId = SystemAPI.GetComponent<NetworkId>(rpcReceive.SourceConnection).Value;

                        // Mark connection as joined
                        ecb.RemoveComponent<PendingClient>(rpcReceive.SourceConnection);
                        ecb.AddBuffer<ClientOwnedEntities>(rpcReceive.SourceConnection);

                        // Spawn player
                        var playerEntity = ecb.Instantiate(data.PlayerPrefabEntity);
                        ecb.SetComponent(playerEntity, new GhostOwner { NetworkId = ownerConnectionId });
                        ecb.AppendToBuffer(rpcReceive.SourceConnection, new ClientOwnedEntities { Entity = playerEntity });

                        // Set player data
                        var player = SystemAPI.GetComponent<PlayerData>(data.PlayerPrefabEntity);
                        player.Name = request.PlayerName;
                        ecb.SetComponent(playerEntity, player);

                        // Request to spawn character
                        var spawnCharacterRequestEntity = ecb.CreateEntity();
                        ecb.AddComponent(spawnCharacterRequestEntity, new CharacterSpawnRequest
                        {
                            ForConnection = rpcReceive.SourceConnection,
                            Delay = -1f
                        });

                        // Remember player for connection
                        ecb.AddComponent(rpcReceive.SourceConnection, new JoinedClient
                        {
                            PlayerEntity = playerEntity
                        });

                        // Accept join request
                        var joinRequestAcceptedEntity = state.EntityManager.CreateEntity();
                        ecb.AddComponent(joinRequestAcceptedEntity, new JoinRequestAccepted());
                        ecb.AddComponent(joinRequestAcceptedEntity, new SendRpcCommandRequest
                        {
                            TargetConnection = rpcReceive.SourceConnection
                        });

                        // Stream in game
                        ecb.AddComponent(rpcReceive.SourceConnection, new NetworkStreamInGame());
                    }

                    ecb.DestroyEntity(entity);
                }
            }
        }

        private void HandlePendingJoinClientTimeout(ref SystemState state, GameData data)
        {
            // Add ConnectionState component
            {
                var ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged);

                foreach (var (netId, entity) in SystemAPI.Query<NetworkId>()
                             .WithNone<ConnectionState>()
                             .WithEntityAccess())
                {
                    ecb.AddComponent(entity, new ConnectionState());
                }
            }

            // Mark unjoined clients as pending
            {
                var ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged);

                foreach (var (netId, entity) in SystemAPI.Query<NetworkId>()
                             .WithNone<PendingClient>()
                             .WithNone<JoinedClient>()
                             .WithEntityAccess())
                {
                    ecb.AddComponent(entity, new PendingClient());
                }
            }

            // Handle join timeout for pending clients
            {
                var ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged);

                foreach (var (netId, pendingClient, entity) in SystemAPI
                             .Query<NetworkId, RefRW<PendingClient>>()
                             .WithEntityAccess())
                {
                    pendingClient.ValueRW.TimeConnected += SystemAPI.Time.DeltaTime;
                    if (pendingClient.ValueRW.TimeConnected > data.JoinTimeout)
                    {
                        ecb.DestroyEntity(entity);
                    }
                }
            }
        }

        private void HandleDisconnect(ref SystemState state, ref Singleton singleton)
        {
            var ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Client disconnect
            foreach (var (connectionState, ownedEntities, entity) in SystemAPI
                         .Query<ConnectionState, DynamicBuffer<ClientOwnedEntities>>()
                         .WithEntityAccess())
            {
                if (connectionState.CurrentState == ConnectionState.State.Disconnected)
                {
                    // Destroy all entities owned by client
                    for (var i = 0; i < ownedEntities.Length; i++)
                        ecb.DestroyEntity(ownedEntities[i].Entity);

                    ecb.RemoveComponent<ClientOwnedEntities>(entity);
                    ecb.RemoveComponent<ConnectionState>(entity);
                }
            }

            // Disconnect requests
            var disconnectRequestQuery = SystemAPI.QueryBuilder().WithAll<DisconnectRequest>().Build();
            if (disconnectRequestQuery.CalculateEntityCount() > 0)
            {
                // Allow systems to have updated since disconnection, for cleanup
                if (singleton.DisconnectionFramesCounter > 3)
                {
                    var disposeRequestEntity = ecb.CreateEntity();
                    ecb.AddComponent(disposeRequestEntity, new GameSystem.DisposeServerWorldRequest());
                    ecb.AddComponent(disposeRequestEntity, new MoveToLocalWorld());
                    ecb.DestroyEntity(disconnectRequestQuery, EntityQueryCaptureMode.AtRecord);
                }

                singleton.DisconnectionFramesCounter++;
            }
        }

        private void HandleSpawnCharacter(ref SystemState state, ref Singleton singleton, GameData data)
        {
            if (SystemAPI.QueryBuilder().WithAll<CharacterSpawnRequest>().Build().CalculateEntityCount() > 0)
            {
                var ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                    .CreateCommandBuffer(state.WorldUnmanaged);
                var spawnPointsQuery = SystemAPI.QueryBuilder().WithAll<SpawnPoint, LocalToWorld>().Build();
                var spawnPointLtWs = spawnPointsQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);

                foreach (var (spawnRequest, entity) in SystemAPI.Query<RefRW<CharacterSpawnRequest>>()
                             .WithEntityAccess())
                {
                    if (spawnRequest.ValueRW.Delay > 0f)
                    {
                        spawnRequest.ValueRW.Delay -= SystemAPI.Time.DeltaTime;
                    }
                    else
                    {
                        if (SystemAPI.HasComponent<NetworkId>(spawnRequest.ValueRW.ForConnection) &&
                            SystemAPI.HasComponent<JoinedClient>(spawnRequest.ValueRW.ForConnection))
                        {
                            var connectionId = SystemAPI.GetComponent<NetworkId>(spawnRequest.ValueRW.ForConnection).Value;
                            var playerEntity = SystemAPI.GetComponent<JoinedClient>(spawnRequest.ValueRW.ForConnection).PlayerEntity;
                            var randomSpawnPosition = spawnPointLtWs[singleton.Random.NextInt(0, spawnPointLtWs.Length - 1)].Position;

                            // Spawn character
                            var characterEntity = ecb.Instantiate(data.CharacterPrefabEntity);
                            ecb.SetComponent(characterEntity, new GhostOwner { NetworkId = connectionId });
                            ecb.SetComponent(characterEntity, LocalTransform.FromPosition(randomSpawnPosition));
                            ecb.AppendToBuffer(spawnRequest.ValueRW.ForConnection, new ClientOwnedEntities { Entity = characterEntity });

                            // Spawn camera
                            var cameraEntity = ecb.Instantiate(data.CameraPrefabEntity);
                            ecb.SetComponent(cameraEntity, new MainEntityCamera());

                            // Assign character to player
                            var player = SystemAPI.GetComponent<PlayerData>(playerEntity);
                            player.ControlledCharacter = characterEntity;
                            player.ControlledCamera = cameraEntity;
                            ecb.SetComponent(playerEntity, player);
                        }

                        ecb.DestroyEntity(entity);
                    }
                }

                spawnPointLtWs.Dispose();
            }
        }
    }
}

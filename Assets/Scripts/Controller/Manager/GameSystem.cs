using System;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using Utilities;

namespace Manager
{
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class GameSystem : SystemBase
    {
        [Serializable]
        public struct Singleton : IComponentData
        {
            public MenuState MenuState;
            public Entity MenuVisualsSceneInstance;
        }

        [Serializable]
        public struct JoinRequest : IComponentData
        {
            public FixedString128Bytes LocalPlayerName;
            public NetworkEndpoint EndPoint;
        }

        [Serializable]
        public struct HostRequest : IComponentData {
            public NetworkEndpoint EndPoint;
        }

        [Serializable]
        public struct DisconnectRequest : IComponentData { }

        [Serializable]
        public struct DisposeClientWorldRequest : IComponentData { }

        [Serializable]
        public struct DisposeServerWorldRequest : IComponentData { }

        public World ClientWorld;
        public World ServerWorld;

        public const string k_LocalHost = "127.0.0.1";

        protected override void OnCreate()
        {
            base.OnCreate();

            // Auto-create singleton
            EntityManager.CreateEntity(typeof(Singleton));

            RequireForUpdate<GameData>();
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
            var gameData = SystemAPI.GetSingleton<GameData>();

            ProcessHostRequests(ref ecb, gameData);
            ProcessJoinRequests(ref ecb, gameData);
            ProcessDisconnectRequests(ref ecb);
            HandleMenuState(ref singleton);
        }

        private void ProcessHostRequests(ref EntityCommandBuffer ecb, GameData data)
        {
            var serverECB = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, entity) in SystemAPI.Query<RefRO<HostRequest>>().WithEntityAccess())
            {
                if (!WorldUtilities.IsValidAndCreated(ServerWorld))
                {
                    // Create server world
                    ServerWorld = NetCodeBootstrap.CreateServerWorld("ServerWorld");
                
                    // Tickrate
                    {
                        var tickRateSingletonQuery = new EntityQueryBuilder(Allocator.Temp)
                            .WithAllRW<ClientServerTickRate>()
                            .Build(ServerWorld.EntityManager);
                        if (tickRateSingletonQuery.HasSingleton<ClientServerTickRate>())
                        {
                            serverECB.SetComponent(tickRateSingletonQuery.GetSingletonEntity(), data.GetClientServerTickRate());
                        }
                        else
                        {
                            var tickRateEntity = serverECB.CreateEntity();
                            serverECB.AddComponent(tickRateEntity, data.GetClientServerTickRate());
                        }
                    }

                    // Listen to endpoint
                    var serverNetworkDriverQuery = new EntityQueryBuilder(Allocator.Temp)
                        .WithAllRW<NetworkStreamDriver>()
                        .Build(ServerWorld.EntityManager);
                    serverNetworkDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(request.ValueRO.EndPoint);

                    // Load game resources subscene
                    SceneSystem.LoadSceneAsync(ServerWorld.Unmanaged, data.GameConfigScene);
                
                    // Create a request to accept joins once the game scenes have been loaded
                    {
                        var serverGameSingletonQuery = new EntityQueryBuilder(Allocator.Temp)
                            .WithAllRW<ServerGameSystem.Singleton>()
                            .Build(ServerWorld.EntityManager);
                        ref var serverGameSingleton = ref serverGameSingletonQuery.GetSingletonRW<ServerGameSystem.Singleton>().ValueRW;
                        serverGameSingleton.AcceptJoins = false;

                        var requestAcceptJoinsEntity = serverECB.CreateEntity();
                        serverECB.AddComponent(requestAcceptJoinsEntity, new ServerGameSystem.AcceptJoinsOnceScenesLoadedRequest
                        {
                            PendingSceneLoadRequest = SceneLoadSystem.CreateSceneLoadRequest(serverECB, data.GameScene),
                        });
                    }
                }
            
                ecb.DestroyEntity(entity);
                break;
            }

            if (WorldUtilities.IsValidAndCreated(ServerWorld))
                serverECB.Playback(ServerWorld.EntityManager);

            serverECB.Dispose();
        }

        private void ProcessJoinRequests(ref EntityCommandBuffer ecb, GameData gameData)
        {
            var clientECB = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, entity) in SystemAPI.Query<RefRO<JoinRequest>>().WithEntityAccess())
            {
                if (!WorldUtilities.IsValidAndCreated(ClientWorld))
                {
                    // Create client world
                    ClientWorld = NetCodeBootstrap.CreateClientWorld("ClientWorld");

                    // Connect to endpoint
                    var clientNetworkDriverQuery = new EntityQueryBuilder(Allocator.Temp)
                        .WithAllRW<NetworkStreamDriver>()
                        .Build(ClientWorld.EntityManager);
                    clientNetworkDriverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(ClientWorld.EntityManager, request.ValueRO.EndPoint);

                    // Create local game data singleton in client world
                    var localGameDataEntity = ClientWorld.EntityManager.CreateEntity();
                    ClientWorld.EntityManager.AddComponentData(localGameDataEntity, new LocalGameData
                    {
                        LocalPlayerName = request.ValueRO.LocalPlayerName,
                    });

                    // Load game resources subscene
                    SceneSystem.LoadSceneAsync(ClientWorld.Unmanaged, gameData.GameConfigScene);

                    // Create a request to join once the game scenes have been loaded
                    {
                        var requestAcceptJoinsEntity = clientECB.CreateEntity();
                        clientECB.AddComponent(requestAcceptJoinsEntity, new ClientGameSystem.JoinOnceScenesLoadedRequest
                        {
                            PendingSceneLoadRequest = SceneLoadSystem.CreateSceneLoadRequest(clientECB, gameData.GameScene),
                        });
                    }
                }

                ecb.DestroyEntity(entity);
                break;
            }

            if (WorldUtilities.IsValidAndCreated(ClientWorld))
                clientECB.Playback(ClientWorld.EntityManager);

            clientECB.Dispose();
        }

        private void ProcessDisconnectRequests(ref EntityCommandBuffer ecb)
        {
            var disconnectRequestQuery = SystemAPI.QueryBuilder().WithAll<DisconnectRequest>().Build();
            if (disconnectRequestQuery.CalculateEntityCount() > 0)
            {
                if (WorldUtilities.IsValidAndCreated(ClientWorld))
                {
                    var disconnectClientRequestEntity = ecb.CreateEntity();
                    ecb.AddComponent(disconnectClientRequestEntity, new ClientGameSystem.DisconnectRequest());
                    ecb.AddComponent(disconnectClientRequestEntity, new MoveToClientWorld());
                }

                if (WorldUtilities.IsValidAndCreated(ServerWorld))
                {
                    var disconnectServerRequestEntity = ecb.CreateEntity();
                    ecb.AddComponent(disconnectServerRequestEntity, new ServerGameSystem.DisconnectRequest());
                    ecb.AddComponent(disconnectServerRequestEntity, new MoveToServerWorld());
                }
            }
            ecb.DestroyEntity(disconnectRequestQuery, EntityQueryCaptureMode.AtRecord);
        }

        private void HandleMenuState(ref Singleton singleton)
        {
            #region Detect state changes

            if (WorldUtilities.IsValidAndCreated(ClientWorld))
            {
                var connectionInGameQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<NetworkId, NetworkStreamInGame>()
                    .Build(ClientWorld.EntityManager);
                singleton.MenuState = connectionInGameQuery.CalculateEntityCount() == 0 ? MenuState.Connecting : MenuState.InGame;
            }
            else if (WorldUtilities.IsValidAndCreated(ServerWorld))
            {
                var serverGameSingletonQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<ServerGameSystem.Singleton>()
                    .Build(ServerWorld.EntityManager);
                var serverGameSingleton = serverGameSingletonQuery.GetSingleton<ServerGameSystem.Singleton>();
                singleton.MenuState = serverGameSingleton.AcceptJoins ? MenuState.InGame : MenuState.Connecting;
            }
            else
            {
                singleton.MenuState = MenuState.InMenu;
            }

            #endregion

            #region Handle state update

            if (singleton.MenuState == MenuState.InMenu)
            {
                // load menu scene if it doesn't exist
                if (!SystemAPI.HasComponent<SceneReference>(singleton.MenuVisualsSceneInstance))
                {
                    singleton.MenuVisualsSceneInstance = SceneSystem.LoadSceneAsync(World.Unmanaged, SystemAPI.GetSingleton<GameData>().GameMenuScene);
                }
            }
            else
            {
                // unload menu scene if it exists
                if (SystemAPI.HasComponent<SceneReference>(singleton.MenuVisualsSceneInstance))
                {
                    SceneSystem.UnloadScene(World.Unmanaged, singleton.MenuVisualsSceneInstance, SceneSystem.UnloadParameters.DestroyMetaEntities);
                }
            }

            #endregion

            #region Handle dispose client server worlds and return to menu

            var disposeClientRequestQuery = SystemAPI.QueryBuilder().WithAll<DisposeClientWorldRequest>().Build();
            if (disposeClientRequestQuery.CalculateEntityCount() > 0)
            {
                if (WorldUtilities.IsValidAndCreated(ClientWorld)) ClientWorld.Dispose();

                EntityManager.DestroyEntity(disposeClientRequestQuery);
            }

            var disposeServerRequestQuery = SystemAPI.QueryBuilder().WithAll<DisposeServerWorldRequest>().Build();
            if (disposeServerRequestQuery.CalculateEntityCount() > 0)
            {
                if (WorldUtilities.IsValidAndCreated(ServerWorld)) ServerWorld.Dispose();

                EntityManager.DestroyEntity(disposeServerRequestQuery);
            }

            #endregion
        }
    }
}

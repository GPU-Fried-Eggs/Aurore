using Camera;
using Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Manager
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct GameSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<GameData>())
            {
                ref var gameData = ref SystemAPI.GetSingletonRW<GameData>().ValueRW;
                var spawnPointsQuery = SystemAPI.QueryBuilder().WithAll<SpawnPoint, LocalToWorld>().Build();
                var spawnPointLtWs = spawnPointsQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);

                // Cursor
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                // Spawn player
                var playerEntity = state.EntityManager.Instantiate(gameData.PlayerPrefabEntity);

                // Spawn character at spawn point
                var characterEntity = state.EntityManager.Instantiate(gameData.CharacterPrefabEntity);
                var randomSpawnPosition = spawnPointLtWs.Length > 0
                    ? spawnPointLtWs[Random.CreateFromIndex(0).NextInt(0, spawnPointLtWs.Length - 1)]
                    : new LocalToWorld { Value = float4x4.identity };
                SystemAPI.SetComponent(characterEntity, LocalTransform.FromPositionRotation(randomSpawnPosition.Position, randomSpawnPosition.Rotation));

                // Spawn camera
                var cameraEntity = state.EntityManager.Instantiate(gameData.CameraPrefabEntity);
                state.EntityManager.AddComponentData(cameraEntity, new MainEntityCamera());

                // Assign camera & character to player
                var player = SystemAPI.GetComponent<PlayerData>(playerEntity);
                player.ControlledCharacter = characterEntity;
                player.ControlledCamera = cameraEntity;
                SystemAPI.SetComponent(playerEntity, player);
            
                state.EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<GameData>());
                spawnPointLtWs.Dispose();
            }
        }
    }
}
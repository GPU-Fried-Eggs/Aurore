using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Scenes;

namespace Manager
{
    public partial struct SceneLoadSystem : ISystem
    {
        private EntityQuery m_SceneLoadRequestQuery;
    
        public void OnCreate(ref SystemState state)
        {
            m_SceneLoadRequestQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SceneLoadRequest, SceneIdentifier>()
                .Build(ref state);
        
            state.RequireForUpdate(m_SceneLoadRequestQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var sceneRequestsToLoad = new NativeList<Entity>(Allocator.Temp);
            var sceneBufferLookup = SystemAPI.GetBufferLookup<SceneIdentifier>(false);

            foreach (var (loadRequest, entity) in SystemAPI.Query<RefRW<SceneLoadRequest>>().WithEntityAccess())
            {
                if (sceneBufferLookup.HasBuffer(entity))
                {
                    var sceneBuffer = sceneBufferLookup[entity];

                    var hasAnyScenesNotStartedLoading = false;
                    foreach (var scene in sceneBuffer)
                    {
                        if (scene.SceneEntity == Entity.Null)
                        {
                            hasAnyScenesNotStartedLoading = true;
                        }
                    }

                    if (hasAnyScenesNotStartedLoading)
                    {
                        sceneRequestsToLoad.Add(entity);
                    }
                    else
                    {
                        var allScenesLoaded = true;
                        for (var i = 0; i < sceneBuffer.Length; i++)
                        {
                            var scene = sceneBuffer[i];

                            // Start loading scene if no entity
                            if (scene.SceneEntity != Entity.Null)
                            {
                                // Check if scene loaded
                                if (!SceneSystem.IsSceneLoaded(state.WorldUnmanaged, scene.SceneEntity))
                                {
                                    allScenesLoaded = false;
                                }

                                sceneBuffer[i] = scene;
                            }
                        }

                        loadRequest.ValueRW.IsLoaded = allScenesLoaded;
                    }
                }
            }

            foreach (var entity in sceneRequestsToLoad)
            {
                if (SystemAPI.GetBufferLookup<SceneIdentifier>(false).HasBuffer(entity))
                {
                    var scenesArray = SystemAPI.GetBufferLookup<SceneIdentifier>(false)[entity]
                        .ToNativeArray(Allocator.Temp);
                    for (var j = 0; j < scenesArray.Length; j++)
                    {
                        var sceneId = scenesArray[j];
                        if (sceneId.SceneEntity == Entity.Null)
                        {
                            sceneId.SceneEntity = SceneSystem.LoadSceneAsync(state.WorldUnmanaged, sceneId.SceneReference);

                            // Required due to structural changes
                            var buffer = SystemAPI.GetBufferLookup<SceneIdentifier>(false)[entity];
                            buffer[j] = sceneId;
                        }
                    }
                    scenesArray.Dispose();
                }
            }

            sceneRequestsToLoad.Dispose();
        }

        public static Entity CreateSceneLoadRequest(EntityCommandBuffer ecb, EntitySceneReference sceneReference)
        {
            var requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, new SceneLoadRequest());

            var scenesBuffer = ecb.AddBuffer<SceneIdentifier>(requestEntity);
            scenesBuffer.Add(new SceneIdentifier(sceneReference));

            return requestEntity;
        }

        public static Entity CreateSceneLoadRequest(EntityCommandBuffer ecb, NativeList<EntitySceneReference> sceneReferences, bool autoDisposeList)
        {
            var requestEntity = ecb.CreateEntity();
            ecb.AddComponent(requestEntity, new SceneLoadRequest());

            var scenesBuffer = ecb.AddBuffer<SceneIdentifier>(requestEntity);
            foreach (var reference in sceneReferences)
                scenesBuffer.Add(new SceneIdentifier(reference));

            if (autoDisposeList) sceneReferences.Dispose();

            return requestEntity;
        }

        public static Entity CreateSceneLoadRequest(EntityManager entityManager, EntitySceneReference sceneReference)
        {
            var requestEntity = entityManager.CreateEntity(typeof(SceneLoadRequest));

            var scenesBuffer = entityManager.AddBuffer<SceneIdentifier>(requestEntity);
            scenesBuffer.Add(new SceneIdentifier(sceneReference));

            return requestEntity;
        }

        public static Entity CreateSceneLoadRequest(EntityManager entityManager, NativeList<EntitySceneReference> sceneReferences, bool autoDisposeList)
        {
            var requestEntity = entityManager.CreateEntity(typeof(SceneLoadRequest));

            var scenesBuffer = entityManager.AddBuffer<SceneIdentifier>(requestEntity);
            foreach (var reference in sceneReferences)
                scenesBuffer.Add(new SceneIdentifier(reference));

            if (autoDisposeList) sceneReferences.Dispose();

            return requestEntity;
        }
    }
}
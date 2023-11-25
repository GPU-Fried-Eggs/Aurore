using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Entities.Serialization;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
#endif

namespace Utilities
{
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(BakedGameObjectSceneReference))]
    public class BakedGameObjectSceneReferencePropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var inspector = new VisualElement();
            inspector.Add(new PropertyField(property.FindPropertyRelative("SceneAsset"), property.name));
            return inspector;
        }
    }

    [CustomPropertyDrawer(typeof(BakedSubSceneReference))]
    public class BakedSubSceneReferencePropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var inspector = new VisualElement();
            inspector.Add(new PropertyField(property.FindPropertyRelative("SceneAsset"), property.name));
            return inspector;
        }
    }
#endif

    [Serializable]
    public struct BakedGameObjectSceneReference
    {
#if UNITY_EDITOR
        public SceneAsset SceneAsset;
#endif

        public int GetIndexInBuildScenes()
        {
#if UNITY_EDITOR
            if (SceneAsset != null)
            {
                var scenePath = AssetDatabase.GetAssetPath(SceneAsset);
                for (var i = 0; i < EditorBuildSettings.scenes.Length; i++)
                {
                    var buildScene = EditorBuildSettings.scenes[i];
                    if (buildScene.path == scenePath)
                    {
                        return i;
                    }
                }
            
                Debug.LogError($"Error: Scene \"{SceneAsset.name}\" assigned in {typeof(BakedGameObjectSceneReference)} has invalid build index. Make sure this scene is added to build settings");
            }
#endif

            return -1;
        }
    }

    [Serializable]
    public struct BakedSubSceneReference
    {
#if UNITY_EDITOR
        public SceneAsset SceneAsset;
#endif

        public EntitySceneReference GetEntitySceneReference()
        {
#if UNITY_EDITOR
            return new EntitySceneReference(SceneAsset);
#else
            return default;
#endif
        }
    }

    public static class WorldUtilities
    {
        public static bool IsValidAndCreated(World world) => world is { IsCreated: true };

        public static void CopyEntitiesToWorld(EntityManager srcEntityManager, EntityManager dstEntityManager, EntityQuery entityQuery)
        {
            var entitiesToCopy = entityQuery.ToEntityArray(Allocator.Temp);
            dstEntityManager.CopyEntitiesFrom(srcEntityManager, entitiesToCopy);
            entitiesToCopy.Dispose();
        }

        public static void SetShadowModeInHierarchy(EntityManager entityManager,
            EntityCommandBuffer ecb,
            Entity onEntity,
            ref BufferLookup<Child> childBufferFromEntity,
            UnityEngine.Rendering.ShadowCastingMode mode)
        {
            if (entityManager.HasComponent<RenderFilterSettings>(onEntity))
            {
                var renderFilterSettings = entityManager.GetSharedComponent<RenderFilterSettings>(onEntity);
                renderFilterSettings.ShadowCastingMode = mode;
                ecb.SetSharedComponent(onEntity, renderFilterSettings);
            }

            if (childBufferFromEntity.HasBuffer(onEntity))
            {
                var childBuffer = childBufferFromEntity[onEntity];
                for (var i = 0; i < childBuffer.Length; i++)
                {
                    SetShadowModeInHierarchy(entityManager, ecb, childBuffer[i].Value, ref childBufferFromEntity, mode);
                }
            }
        }

        public static void DisableRenderingInHierarchy(EntityCommandBuffer ecb, Entity onEntity, ref BufferLookup<Child> childBufferFromEntity)
        {
            ecb.RemoveComponent<MaterialMeshInfo>(onEntity);

            if (childBufferFromEntity.HasBuffer(onEntity))
            {
                var childBuffer = childBufferFromEntity[onEntity];
                for (var i = 0; i < childBuffer.Length; i++)
                {
                    DisableRenderingInHierarchy(ecb, childBuffer[i].Value, ref childBufferFromEntity);
                }
            }
        }
    }
}
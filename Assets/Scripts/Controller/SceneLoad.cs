using System;
using Unity.Entities;
using Unity.Entities.Serialization;

namespace Manager
{
    [Serializable]
    public struct SceneLoadRequest : IComponentData
    {
        /// <summary>
        /// A flag whether scene is loaded
        /// </summary>
        public bool IsLoaded;
    }

    [Serializable]
    public struct SceneIdentifier : IBufferElementData
    {
        public EntitySceneReference SceneReference;
        public Entity SceneEntity;

        public SceneIdentifier(EntitySceneReference sceneReference)
        {
            SceneReference = sceneReference;
            SceneEntity = default;
        }
    }
}
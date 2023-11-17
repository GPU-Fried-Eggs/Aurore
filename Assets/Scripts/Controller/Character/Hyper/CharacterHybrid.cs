using System;
using Unity.Entities;
using UnityEngine;

namespace Character.Hyper
{
    [Serializable]
    public class CharacterHybridData : IComponentData
    {
        /// <summary>
        /// The mesh hyper prefab
        /// </summary>
        public GameObject MeshPrefab;
    }

    [Serializable]
    public class CharacterHybridLink : ICleanupComponentData
    {
        public GameObject Object;
        public Animator Animator;
    }
}
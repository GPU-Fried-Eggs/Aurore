using System;
using Unity.Entities;
using UnityEngine;

namespace Character.Hybrid
{
    [Serializable]
    public class CharacterHybridData : IComponentData
    {
        public GameObject MeshPrefab;
    }

    [Serializable]
    public class CharacterHybridLink : ICleanupComponentData
    {
        public GameObject Object;
        public Animator Animator;
    }
}
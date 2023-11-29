using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Interactive
{
    [Serializable]
    public struct MovingPlatform : IComponentData
    {
        public float3 TranslationAxis;
        public float TranslationAmplitude;
        public float TranslationSpeed;
        public float3 RotationAxis;
        public float RotationSpeed;
        public float3 OscillationAxis;
        public float OscillationAmplitude;
        public float OscillationSpeed;

        [HideInInspector] public float3 OriginalPosition;
        [HideInInspector] public quaternion OriginalRotation;
    }
}
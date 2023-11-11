using System;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Controller.Physics
{
    [Serializable]
    public struct TrackedTransform : IComponentData
    {
        /// <summary>
        /// Current transform
        /// </summary>
        [HideInInspector]
        public RigidTransform CurrentFixedRateTransform;
        /// <summary>
        /// Previous transform
        /// </summary>
        [HideInInspector]
        public RigidTransform PreviousFixedRateTransform;

        /// <summary>
        /// Calculate a point that results from moving a given point from the previous transform to the current transform
        /// </summary>
        /// <param name="point"> The point to move </param>
        /// <returns> The moved point </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 CalculatePointDisplacement(float3 point)
        {
            var characterLocalPositionToPreviousParentTransform = math.transform(math.inverse(PreviousFixedRateTransform), point);
            var characterTargetTranslation = math.transform(CurrentFixedRateTransform, characterLocalPositionToPreviousParentTransform);
            return characterTargetTranslation - point;
        }

        /// <summary>
        /// Calculates the linear velocity of a point that gets moved from the previous transform to the current transform over a time delta
        /// </summary>
        /// <param name="point"> The point to move </param>
        /// <param name="deltaTime"> The time delta </param>
        /// <returns> The calculated linear velocity </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 CalculatePointVelocity(float3 point, float deltaTime) => CalculatePointDisplacement(point) / deltaTime;
    }
}
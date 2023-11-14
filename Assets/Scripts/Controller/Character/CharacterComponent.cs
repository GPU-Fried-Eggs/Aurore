using System;
using Character.Kinematic;
using Unity.Entities;
using Unity.Mathematics;

namespace Character
{
    [Serializable]
    public struct CharacterData : IComponentData
    {
        public Entity DefaultCameraTargetEntity;
        public float RotationSharpness;
        public float GroundMaxSpeed;
        public float GroundedMovementSharpness;
        public float AirAcceleration;
        public float AirMaxSpeed;
        public float AirDrag;
        public float JumpSpeed;
        public float3 Gravity;
        public bool PreventAirAccelerationAgainstUngroundedHits;
        public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling;
    }

    [Serializable]
    public struct CharacterControl : IComponentData
    {
        public float3 MoveVector;
    
        public bool JumpHeld;

        public bool JumpPressed;
    }
}
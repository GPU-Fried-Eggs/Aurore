using System;
using Character.Kinematic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;

namespace Character
{
    [Serializable]
    public struct CharacterData : IComponentData
    {
        [Header("References")]
        public Entity DefaultCameraTargetEntity;
        public Entity SwimmingCameraTargetEntity;
        public Entity ClimbingCameraTargetEntity;
        public Entity CrouchingCameraTargetEntity;
        public Entity MeshRootEntity;
        public Entity MeshPrefab;

        [Header("Ground movement")]
        public float GroundRunMaxSpeed;
        public float GroundSprintMaxSpeed;
        public float GroundedMovementSharpness;
        public float GroundedRotationSharpness;

        [Header("Crouching")]
        public float CrouchedMaxSpeed;
        public float CrouchedMovementSharpness;
        public float CrouchedRotationSharpness;

        [Header("Air movement")]
        public float AirAcceleration;
        public float AirMaxSpeed;
        public float AirDrag;
        public float AirRotationSharpness;

        [Header("Flying")]
        public float FlyingMaxSpeed;
        public float FlyingMovementSharpness;

        [Header("Jumping")]
        public float GroundJumpSpeed;
        public float JumpHeldAcceleration;
        public float MaxHeldJumpTime;
        public float JumpAfterUngroundedGraceTime;
        public float JumpBeforeGroundedGraceTime;

        [Header("Swimming")]
        public float SwimmingAcceleration;
        public float SwimmingMaxSpeed;
        public float SwimmingDrag;
        public float SwimmingRotationSharpness;
        public float SwimmingStandUpDistanceFromSurface;
        public float WaterDetectionDistance;
        public float SwimmingJumpSpeed;
        public float SwimmingSurfaceDiveThreshold;

        [Header("Climbing")]
        public float ClimbingDistanceFromSurface;
        public float ClimbingSpeed;
        public float ClimbingMovementSharpness;
        public float ClimbingRotationSharpness;

        [Header("Step & Slope")]
        public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling;

        [Header("Misc")]
        public CustomPhysicsBodyTags StickySurfaceTag;
        public CustomPhysicsBodyTags ClimbableTag;
        public PhysicsCategoryTags WaterPhysicsCategory;
        public float UpOrientationAdaptationSharpness;
        public CapsuleGeometryDefinition StandingGeometry;
        public CapsuleGeometryDefinition CrouchingGeometry;
        public CapsuleGeometryDefinition ClimbingGeometry;
        public CapsuleGeometryDefinition SwimmingGeometry;

        [HideInInspector] public float3 LocalSwimmingDetectionPoint;
        [HideInInspector] public float HeldJumpTimeCounter;
        [HideInInspector] public bool JumpPressedBeforeBecameGrounded;
        [HideInInspector] public bool AllowJumpAfterBecameUngrounded;
        [HideInInspector] public bool AllowHeldJumpInAir;
        [HideInInspector] public float LastTimeJumpPressed;
        [HideInInspector] public float LastTimeWasGrounded;
        [HideInInspector] public bool HasDetectedMoveAgainstWall;
        [HideInInspector] public float3 LastKnownWallNormal;
        [HideInInspector] public float DistanceFromWaterSurface;
        [HideInInspector] public float3 DirectionToWaterSurface;
        [HideInInspector] public bool IsSprinting;
        [HideInInspector] public bool IsOnStickySurface;
    }

    public struct CharacterInitialized : IComponentData
    {
        public Entity Reference;
    }

    [Serializable]
    public struct CharacterControl : IComponentData
    {
        public float3 MoveVector;

        public bool JumpHeld;
        public bool CrouchHeld;
        public bool SprintHeld;

        public bool JumpPressed;
        public bool CrouchPressed;
        public bool ClimbPressed;
        public bool GodModePressed;
    }

    [Serializable]
    public struct CapsuleGeometryDefinition
    {
        public float Radius;
        public float Height;
        public float3 Center;

        public CapsuleGeometry ToCapsuleGeometry()
        {
            Height = math.max(Height, (Radius + math.EPSILON) * 2f);
            var halfHeight = Height * 0.5f;

            return new CapsuleGeometry
            {
                Radius = Radius,
                Vertex0 = Center + (-math.up() * (halfHeight - Radius)),
                Vertex1 = Center + (math.up() * (halfHeight - Radius)),
            };
        }
    }
}
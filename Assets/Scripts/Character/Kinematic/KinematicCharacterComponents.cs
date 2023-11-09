using System;
using System.Runtime.CompilerServices;
using Character.Utilities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;
using Material = Unity.Physics.Material;

namespace Character.Kinematic
{
    [Serializable]
    public struct AuthoringKinematicCharacterData
    {
        [Header("General Properties")]
        [Tooltip("Physics tags to be added to the character's physics body")]
        public CustomPhysicsBodyTags CustomPhysicsBodyTags;

        [Tooltip("Enables interpolating the character's position between fixed update steps, for smoother movement")]
        public bool InterpolatePosition;

        [Tooltip("Enables interpolating the character's rotation between fixed update steps, for smoother movement. Note: the Standard Characters must keep this option disabled, because by default they all handle their rotation on regular update rather than fixed update")]
        public bool InterpolateRotation;


        [Header("Grounding")]
        [Tooltip("Enables detecting ground and evaluating grounding for each hit")]
        public bool EvaluateGrounding;

        [Tooltip("Enables snapping to the ground surface below the character")]
        public bool SnapToGround;

        [Tooltip("Distance to snap to ground, if SnapToGround is enabled")]
        public float GroundSnappingDistance;

        [Tooltip("Computes a more precise distance to ground hits when the original query returned a distance of 0f due to imprecisions. Helps reduce jitter in certain situations, but can have an additional performance cost. It is recommended that you only enable this if you notice jitter problems when moving against angled walls")]
        public bool EnhancedGroundPrecision;

        [Tooltip("The max slope angle that the character can be considered grounded on")]
        public float MaxGroundedSlopeAngle;


        [Header("Collisions")]
        [Tooltip("Enables detecting and solving movement collisions with a collider cast, based on character's velocity")]
        public bool DetectMovementCollisions;

        [Tooltip("Enables detecting and solving overlaps")]
        public bool DecollideFromOverlaps;

        [Tooltip("Enables doing an extra physics check to project velocity on initial overlaps before the character moves. This can help with tunneling issues when you rotate your character in a way that could change the detected collisions (which doesn\'t happen if your character has an upright capsule shape and only rotates around up axis, for example), but it has a performance cost.")]
        public bool ProjectVelocityOnInitialOverlaps;

        [Tooltip("The maximum amount of times per frame that the character should try to cast its collider for detecting hits")]
        public byte MaxContinuousCollisionsIterations;

        [Tooltip("The maximum amount of times per frame that the character should try to decollide itself from overlaps")]
        public byte MaxOverlapDecollisionIterations;

        [Tooltip("Whether we should reset the remaining move distance to zero when the character exceeds the maximum collision iterations")]
        public bool DiscardMovementWhenExceedMaxIterations;

        [Tooltip("Whether we should reset the velocity to zero when the character exceeds the maximum collision iterations")]
        public bool KillVelocityWhenExceedMaxIterations;

        [Tooltip("Enables doing a collider cast to detect obstructions when being moved by a parent body, instead of simply moving the character transform along")]
        public bool DetectObstructionsForParentBodyMovement;


        [Header("Dynamics")]
        [Tooltip("Enables physics interactions (push and be pushed) with other dynamic bodies. Note that in order to be pushed properly, the character's collision response has to be either \"None\" or \"Raise Trigger Events\"")]
        public bool SimulateDynamicBody;

        [Tooltip("The mass used to simulate dynamic body interactions")]
        public float Mass;

        /// <summary>
        /// Gets a sensible default set of parameters for this struct
        /// </summary>
        public static AuthoringKinematicCharacterData GetDefault()
        {
            return new AuthoringKinematicCharacterData
            {
                // Body Properties
                CustomPhysicsBodyTags = CustomPhysicsBodyTags.Nothing,
                InterpolatePosition = true,
                InterpolateRotation = false,

                // Grounding
                EvaluateGrounding = true,
                SnapToGround = true,
                GroundSnappingDistance = 0.5f,
                EnhancedGroundPrecision = false,
                MaxGroundedSlopeAngle = 60f,

                // Collisions
                DetectMovementCollisions = true,
                DecollideFromOverlaps = true,
                ProjectVelocityOnInitialOverlaps = false,
                MaxContinuousCollisionsIterations = 8,
                MaxOverlapDecollisionIterations = 2,
                DiscardMovementWhenExceedMaxIterations = true,
                KillVelocityWhenExceedMaxIterations = true,
                DetectObstructionsForParentBodyMovement = false,

                // Dynamics
                SimulateDynamicBody = true,
                Mass = 1f,
            };
       }
    }

    [Serializable]
    public struct KinematicCharacterData : IComponentData
    {
        /// <summary>
        /// Enables detecting ground and evaluating grounding for each hit
        /// </summary>
        public bool EvaluateGrounding;
        /// <summary>
        /// Enables snapping to the ground surface below the character
        /// </summary>
        public bool SnapToGround;
        /// <summary>
        /// Distance to snap to ground, if SnapToGround is enabled
        /// </summary>
        public float GroundSnappingDistance;
        /// <summary>
        /// Computes a more precise distance to ground hits when the original query returned a distance of 0f due to imprecisions. Helps reduce jitter in certain situations, but can have an additional performance cost. It is recommended that you only enable this if you notice jitter problems when moving against angled walls
        /// </summary>
        public bool EnhancedGroundPrecision;
        /// <summary>
        /// The max slope angle that the character can be considered grounded on
        /// </summary>
        public float MaxGroundedSlopeDotProduct;

        /// <summary>
        /// Enables detecting and solving movement collisions with a collider cast, based on character's velocity
        /// </summary>
        public bool DetectMovementCollisions;
        /// <summary>
        /// Enables detecting and solving overlaps
        /// </summary>
        public bool DecollideFromOverlaps;
        /// <summary>
        /// Enables doing an extra physics check to project velocity on initial overlaps before the character moves. This can help with tunneling issues when you rotate your character in a way that could change the detected collisions (which doesn't happen if your character has an upright capsule shape and only rotates around up axis, for example), but it has a performance cost.
        /// </summary>
        public bool ProjectVelocityOnInitialOverlaps;
        /// <summary>
        /// The maximum amount of times per frame that the character should try to cast its collider for detecting hits
        /// </summary>
        public byte MaxContinuousCollisionsIterations;
        /// <summary>
        /// The maximum amount of times per frame that the character should try to decollide itself from overlaps
        /// </summary>
        public byte MaxOverlapDecollisionIterations;
        /// <summary>
        /// Whether we should reset the remaining move distance to zero when the character exceeds the maximum collision iterations
        /// </summary>
        public bool DiscardMovementWhenExceedMaxIterations;
        /// <summary>
        /// Whether we should reset the velocity to zero when the character exceeds the maximum collision iterations
        /// </summary>
        public bool KillVelocityWhenExceedMaxIterations;
        /// <summary>
        /// Enables doing a collider cast to detect obstructions when being moved by a parent body, instead of simply moving the character transform along
        /// </summary>
        public bool DetectObstructionsForParentBodyMovement;

        /// <summary>
        /// Enables physics interactions (push and be pushed) with other dynamic bodies. Note that in order to be pushed properly, the character's collision response has to be either \"None\" or \"Raise Trigger Events\"
        /// </summary>
        public bool SimulateDynamicBody;
        /// <summary>
        /// The mass used to simulate dynamic body interactions
        /// </summary>
        public float Mass;

        public KinematicCharacterData(AuthoringKinematicCharacterData authoring)
        {
            EvaluateGrounding = authoring.EvaluateGrounding;
            SnapToGround = authoring.SnapToGround;
            GroundSnappingDistance = authoring.GroundSnappingDistance;
            EnhancedGroundPrecision = authoring.EnhancedGroundPrecision;
            MaxGroundedSlopeDotProduct = MathUtilities.AngleRadiansToDotRatio(math.radians(authoring.MaxGroundedSlopeAngle));

            DetectMovementCollisions = authoring.DetectMovementCollisions;
            DecollideFromOverlaps = authoring.DecollideFromOverlaps;
            ProjectVelocityOnInitialOverlaps = authoring.ProjectVelocityOnInitialOverlaps;
            MaxContinuousCollisionsIterations = authoring.MaxContinuousCollisionsIterations;
            MaxOverlapDecollisionIterations = authoring.MaxOverlapDecollisionIterations;
            DiscardMovementWhenExceedMaxIterations = authoring.DiscardMovementWhenExceedMaxIterations;
            KillVelocityWhenExceedMaxIterations = authoring.KillVelocityWhenExceedMaxIterations;
            DetectObstructionsForParentBodyMovement = authoring.DetectObstructionsForParentBodyMovement;

            SimulateDynamicBody = authoring.SimulateDynamicBody;
            Mass = authoring.Mass;
        }

        /// <summary>
        /// Whether or not dynamic rigidbody collisions should be enabled, with the current character properties
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldIgnoreDynamicBodies() => !SimulateDynamicBody;
    }

    [Serializable]
    public struct KinematicCharacterBody : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// Whether the character is currently grounded or not
        /// </summary>
        public bool IsGrounded;
        /// <summary>
        /// The character's velocity relatively to its assigned parent's velocity (if any)
        /// </summary>
        public float3 RelativeVelocity;
        /// <summary>
        /// The character's parent entity
        /// </summary>
        public Entity ParentEntity;
        /// <summary>
        /// The character's anchor point to its parent, expressed in the parent's local space
        /// </summary>
        public float3 ParentLocalAnchorPoint;
        
        // The following data is fully reset at the beginning of the character update, or recalculated during the update.
        // This means it typically doesn't need any network sync unless you access that data before the character update.
        
        /// <summary>
        /// The character's grounding up direction
        /// </summary>
        public float3 GroundingUp;
        /// <summary>
        /// The character's detected ground hit
        /// </summary>
        public BasicHit GroundHit;
        /// <summary>
        /// The calculated velocity of the character's parent
        /// </summary>
        public float3 ParentVelocity;
        /// <summary>
        /// The previous parent entity
        /// </summary>
        public Entity PreviousParentEntity;
        /// <summary>
        /// The rotation resulting from the parent's movement over the latest update
        /// </summary>
        public quaternion RotationFromParent;
        /// <summary>
        /// The last known delta time of the character update
        /// </summary>
        public float LastPhysicsUpdateDeltaTime;
        /// <summary>
        /// Whether or not the character was considered grounded at the beginning of the update, before ground is detected
        /// </summary>
        public bool WasGroundedBeforeCharacterUpdate;

        /// <summary>
        /// Returns a sensible default for this component
        /// </summary>
        public static KinematicCharacterBody GetDefault()
        {
            return new KinematicCharacterBody
            {
                IsGrounded = default,
                RelativeVelocity = default,
                ParentEntity = default,
                ParentLocalAnchorPoint = default,
                
                GroundingUp = math.up(),
                GroundHit = default,
                ParentVelocity = default,
                PreviousParentEntity = default,
                RotationFromParent = quaternion.identity,
                LastPhysicsUpdateDeltaTime = 0f,
                WasGroundedBeforeCharacterUpdate = default,
            };
        }

        /// <summary>
        /// Whether or not the character has become grounded on this frame
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasBecomeGrounded() => !WasGroundedBeforeCharacterUpdate && IsGrounded;

        /// <summary>
        /// Whether or not the character has become ungrounded on this frame
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasBecomeUngrounded() => WasGroundedBeforeCharacterUpdate && !IsGrounded;
    }

    [Serializable]
    public struct KinematicCharacterStoredData : IComponentData
    {
        /// <summary>
        /// Enables physics interactions (push and be pushed) with other dynamic bodies. Note that in order to be pushed
        /// properly, the character's collision response has to be either \"None\" or \"Raise Trigger Events\"
        /// </summary>
        public bool SimulateDynamicBody;
        /// <summary>
        /// The mass used to simulate dynamic body interactions
        /// </summary>
        public float Mass;
        /// <summary>
        /// The character's velocity relatively to its assigned parent's velocity (if any)
        /// </summary>
        public float3 RelativeVelocity;
        /// <summary>
        /// The calculated velocity of the character's parent
        /// </summary>
        public float3 ParentVelocity;
    }

    [Serializable]
    [InternalBufferCapacity(0)]
    public struct KinematicCharacterDeferredImpulse : IBufferElementData
    {
        /// <summary>
        /// Entity on which to apply the impulse
        /// </summary>
        public Entity OnEntity;
        /// <summary>
        /// The impulse's change in linear velocity
        /// </summary>
        public float3 LinearVelocityChange;
        /// <summary>
        /// The impulse's change in angular velocity
        /// </summary>
        public float3 AngularVelocityChange;
        /// <summary>
        /// The impulse's change in position
        /// </summary>
        public float3 Displacement;
    }

    [Serializable]
    [InternalBufferCapacity(0)]
    public struct KinematicCharacterHit : IBufferElementData
    {
        /// <summary>
        /// Hit entity
        /// </summary>
        public Entity Entity;
        /// <summary>
        /// Hit rigidbody index
        /// </summary>
        public int RigidBodyIndex;
        /// <summary>
        /// Hit collider key
        /// </summary>
        public ColliderKey ColliderKey;
        /// <summary>
        /// Hit point
        /// </summary>
        public float3 Position;
        /// <summary>
        /// Hit normal
        /// </summary>
        public float3 Normal;
        /// <summary>
        /// The hit physics material
        /// </summary>
        public Material Material;
        /// <summary>
        /// Whether or not the character was grounded when the hit was detected
        /// </summary>
        public bool WasCharacterGroundedOnHitEnter;
        /// <summary>
        /// Whether or not the character would consider itself grounded on this hit
        /// </summary>
        public bool IsGroundedOnHit;
        /// <summary>
        /// The character's velocity before velocity projection on this hit
        /// </summary>
        public float3 CharacterVelocityBeforeHit;
        /// <summary>
        /// The character's velocity after velocity projection on this hit
        /// </summary>
        public float3 CharacterVelocityAfterHit;
    }

    [Serializable]
    [InternalBufferCapacity(0)]
    public struct KinematicVelocityProjectionHit : IBufferElementData
    {
        /// <summary>
        /// Hit entity
        /// </summary>
        public Entity Entity;
        /// <summary>
        /// Hit rigidbody index
        /// </summary>
        public int RigidBodyIndex;
        /// <summary>
        /// Hit collider key
        /// </summary>
        public ColliderKey ColliderKey;
        /// <summary>
        /// Hit point
        /// </summary>
        public float3 Position;
        /// <summary>
        /// Hit normal
        /// </summary>
        public float3 Normal;
        /// <summary>
        /// Hit material
        /// </summary>
        public Material Material;
        /// <summary>
        /// Whether or not the character would consider itself grounded on this hit
        /// </summary>
        public bool IsGroundedOnHit;

        public KinematicVelocityProjectionHit(KinematicCharacterHit hit)
        {
            Entity = hit.Entity;
            RigidBodyIndex = hit.RigidBodyIndex;
            ColliderKey = hit.ColliderKey;
            Position = hit.Position;
            Normal = hit.Normal;
            Material = hit.Material;
            IsGroundedOnHit = hit.IsGroundedOnHit;
        }

        public KinematicVelocityProjectionHit(BasicHit hit, bool isGroundedOnHit)
        {
            Entity = hit.Entity;
            RigidBodyIndex = hit.RigidBodyIndex;
            ColliderKey = hit.ColliderKey;
            Position = hit.Position;
            Normal = hit.Normal;
            Material = hit.Material;
            IsGroundedOnHit = isGroundedOnHit;
        }

        public KinematicVelocityProjectionHit(float3 normal, float3 position, bool isGroundedOnHit)
        {
            Entity = Entity.Null;
            RigidBodyIndex = -1;
            ColliderKey = default;
            Position = position;
            Normal = normal;
            Material = default;
            IsGroundedOnHit = isGroundedOnHit;
        }
    }

    [Serializable]
    [InternalBufferCapacity(0)]
    public struct StatefulKinematicCharacterHit : IBufferElementData
    {
        /// <summary>
        /// State of the hit (enter/exit/stay)
        /// </summary>
        public CharacterHitState State;
        /// <summary>
        /// The character hit
        /// </summary>
        public KinematicCharacterHit Hit;

        public StatefulKinematicCharacterHit(KinematicCharacterHit characterHit)
        {
            State = default;
            Hit = characterHit;
        }
    }
}
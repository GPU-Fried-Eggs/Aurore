using System;
using System.Runtime.CompilerServices;
using Physics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine;
using Utilities;
using RaycastHit = Unity.Physics.RaycastHit;

namespace Character.Kinematic
{
    public interface IKinematicCharacterProcessor<C> where C : unmanaged
    {
        /// <summary>
        /// Requests that the grounding up direction should be updated.
        /// </summary>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        void UpdateGroundingUp(ref C context, ref KinematicCharacterUpdateContext baseContext);

        /// <summary>
        /// Determines if a hit can be collided with or not.
        /// </summary>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hit"> The evaluated hit </param> 
        /// <returns> Return true if the hit can be collided with, return false if not. </returns>
        bool CanCollideWithHit(ref C context, ref KinematicCharacterUpdateContext baseContext, in BasicHit hit);

        /// <summary>
        /// Determines if the character can be grounded the hit or not.
        /// </summary>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hit"> The evaluated hit </param> 
        /// <param name="groundingEvaluationType"> An identifier meant to indicate what type of grounding evaluation is being done at the moment of calling this. </param>
        /// <returns></returns>
        bool IsGroundedOnHit(ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            in BasicHit hit,
            int groundingEvaluationType);

        /// <summary>
        /// Determines what happens when the character detects a hit during its movement phase.
        /// </summary>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hit"> The evaluated hit </param> 
        /// <param name="remainingMovementDirection"> The direction of the movement vector that remains to be processed </param>
        /// <param name="remainingMovementLength"> The magnitude of the movement vector that remains to be processed </param>
        /// <param name="originalVelocityDirection"> The original direction of the movement vector before any movement projection happened </param>
        /// <param name="hitDistance"> The distance of the detected hit </param>
        void OnMovementHit(ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterHit hit,
            ref float3 remainingMovementDirection,
            ref float remainingMovementLength,
            float3 originalVelocityDirection,
            float hitDistance);

        /// <summary>
        /// Requests that the character velocity be projected on the hits detected so far in the character update.
        /// </summary>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="velocity"> The character velocity that needs to be projected </param>
        /// <param name="characterIsGrounded"> Whether the character is grounded or not </param>
        /// <param name="characterGroundHit"> The current effective ground hit of the character </param>
        /// <param name="velocityProjectionHits"> The hits that have been detected so far during the character update </param>
        /// <param name="originalVelocityDirection"> The original velocity direction of the character at the beginning of the character update, before any projection has happened </param>
        void ProjectVelocityOnHits(ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref float3 velocity,
            ref bool characterIsGrounded,
            ref BasicHit characterGroundHit,
            in DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits,
            float3 originalVelocityDirection);

        /// <summary>
        /// Provides an opportunity to modify the physics masses used to solve impulses between characters and detected hit bodies.
        /// </summary>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterMass"> The mass of the character </param>
        /// <param name="otherMass"> The mass of the other body that we've detected a hit with </param>
        /// <param name="hit"> The evaluated hit with the dynamic body </param>
        void OverrideDynamicHitMasses(ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref PhysicsMass characterMass,
            ref PhysicsMass otherMass,
            BasicHit hit);
    }

    [Serializable]
    public unsafe struct KinematicCharacterStateSave : IDisposable
    {
        /// <summary>
        /// The local transform of the character
        /// </summary>
        public LocalTransform SavedTransform;
        /// <summary>
        /// The character properties component
        /// </summary>
        public KinematicCharacterData savedCharacterData;
        /// <summary>
        /// The character body component
        /// </summary>
        public KinematicCharacterBody SavedCharacterBody;
        
        /// <summary>
        /// Size of the saved physics collider, in bytes
        /// </summary>
        public int SavedPhysicsColliderMemorySize;
        /// <summary>
        /// Saved physics collider data
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<byte> SavedPhysicsColliderMemory;

        /// <summary>
        /// Count for the saved character hits buffer
        /// </summary>
        public int SavedCharacterHitsBufferCount;
        /// <summary>
        /// The character hits buffer
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<KinematicCharacterHit> SavedCharacterHitsBuffer;
        /// <summary>
        /// Count for the saved stateful character hits buffer
        /// </summary>
        public int SavedStatefulHitsBufferCount;
        /// <summary>
        /// The stateful character hits buffer
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<StatefulKinematicCharacterHit> SavedStatefulHitsBuffer;
        /// <summary>
        /// Count for the saved deferred impulses buffer
        /// </summary>
        public int SavedDeferredImpulsesBufferCount;
        /// <summary>
        /// The deferred impulses buffer
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<KinematicCharacterDeferredImpulse> SavedDeferredImpulsesBuffer;
        /// <summary>
        /// Count for the saved velocity projection hits buffer
        /// </summary>
        public int SavedVelocityProjectionHitsCount;
        /// <summary>
        /// The velocity projection hits buffer
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<KinematicVelocityProjectionHit> SavedVelocityProjectionHits;
        
        /// <summary>
        /// Saves the character state. Only reallocates data arrays if the current arrays are not allocated or don't have the required capacity
        /// </summary>
        /// <param name="characterAspect"> The character aspect that provides access to the components to save </param>
        /// <param name="allocator"> The type of allocation that will be used to store arrays of data </param>
        public void Save(KinematicCharacterAspect characterAspect, Allocator allocator = Allocator.Temp)
        {
            SavedTransform = characterAspect.LocalTransform.ValueRO;
            savedCharacterData = characterAspect.CharacterData.ValueRO;
            SavedCharacterBody = characterAspect.CharacterBody.ValueRO;

            var characterAspectPhysicsCollider = characterAspect.PhysicsCollider.ValueRO;
            SavedPhysicsColliderMemorySize = characterAspectPhysicsCollider.ColliderPtr->MemorySize;
            CheckReallocateArray(ref SavedPhysicsColliderMemory, SavedPhysicsColliderMemorySize, allocator);
            UnsafeUtility.MemCpy(SavedPhysicsColliderMemory.GetUnsafePtr(), characterAspectPhysicsCollider.ColliderPtr, SavedPhysicsColliderMemorySize);

            SavedCharacterHitsBufferCount = characterAspect.CharacterHitsBuffer.Length;
            CheckReallocateArray(ref SavedCharacterHitsBuffer, SavedCharacterHitsBufferCount, allocator);
            UnsafeUtility.MemCpy(SavedCharacterHitsBuffer.GetUnsafePtr(), characterAspect.CharacterHitsBuffer.GetUnsafePtr(), SavedCharacterHitsBufferCount);

            SavedStatefulHitsBufferCount = characterAspect.StatefulHitsBuffer.Length;
            CheckReallocateArray(ref SavedStatefulHitsBuffer, SavedStatefulHitsBufferCount, allocator);
            UnsafeUtility.MemCpy(SavedStatefulHitsBuffer.GetUnsafePtr(), characterAspect.StatefulHitsBuffer.GetUnsafePtr(), SavedStatefulHitsBufferCount);

            SavedDeferredImpulsesBufferCount = characterAspect.DeferredImpulsesBuffer.Length;
            CheckReallocateArray(ref SavedDeferredImpulsesBuffer, SavedDeferredImpulsesBufferCount, allocator);
            UnsafeUtility.MemCpy(SavedDeferredImpulsesBuffer.GetUnsafePtr(), characterAspect.DeferredImpulsesBuffer.GetUnsafePtr(), SavedDeferredImpulsesBufferCount);

            SavedVelocityProjectionHitsCount = characterAspect.VelocityProjectionHits.Length;
            CheckReallocateArray(ref SavedVelocityProjectionHits, SavedVelocityProjectionHitsCount, allocator);
            UnsafeUtility.MemCpy(SavedVelocityProjectionHits.GetUnsafePtr(), characterAspect.VelocityProjectionHits.GetUnsafePtr(), SavedVelocityProjectionHitsCount);
        }

        /// <summary>
        /// Restores the character state.
        /// </summary>
        /// <param name="characterAspect"> The character aspect that provides access to the components to restore the state to </param>
        public void Restore(KinematicCharacterAspect characterAspect)
        {
            characterAspect.LocalTransform.ValueRW = SavedTransform;
            characterAspect.CharacterData.ValueRW = savedCharacterData;
            characterAspect.CharacterBody.ValueRW = SavedCharacterBody;

            var characterAspectPhysicsCollider = characterAspect.PhysicsCollider.ValueRW;
            if (characterAspectPhysicsCollider.ColliderPtr->MemorySize == SavedPhysicsColliderMemorySize)
                UnsafeUtility.MemCpy(characterAspectPhysicsCollider.ColliderPtr, SavedPhysicsColliderMemory.GetUnsafePtr(), SavedPhysicsColliderMemorySize);
            else
                Debug.LogError(
                    "Error: trying to restore collider state, but memory size of the PhysicsCollider component data on the character entity is different from the saved state. " +
                    "This may have happened because the collider type has been changed since saving the state. " +
                    "In this case, you have the responsibility of manually restoring the original collider type/MemorySize before you restore state.");

            characterAspect.CharacterHitsBuffer.ResizeUninitialized(SavedCharacterHitsBufferCount);
            UnsafeUtility.MemCpy(characterAspect.CharacterHitsBuffer.GetUnsafePtr(), SavedCharacterHitsBuffer.GetUnsafePtr(), SavedCharacterHitsBufferCount);

            characterAspect.StatefulHitsBuffer.ResizeUninitialized(SavedStatefulHitsBufferCount);
            UnsafeUtility.MemCpy(characterAspect.StatefulHitsBuffer.GetUnsafePtr(), SavedStatefulHitsBuffer.GetUnsafePtr(), SavedStatefulHitsBufferCount);

            characterAspect.DeferredImpulsesBuffer.ResizeUninitialized(SavedDeferredImpulsesBufferCount);
            UnsafeUtility.MemCpy(characterAspect.DeferredImpulsesBuffer.GetUnsafePtr(), SavedDeferredImpulsesBuffer.GetUnsafePtr(), SavedDeferredImpulsesBufferCount);

            characterAspect.VelocityProjectionHits.ResizeUninitialized(SavedVelocityProjectionHitsCount);
            UnsafeUtility.MemCpy(characterAspect.VelocityProjectionHits.GetUnsafePtr(), SavedVelocityProjectionHits.GetUnsafePtr(), SavedVelocityProjectionHitsCount);
        }
        
        /// <summary>
        /// Disposes all data arrays stored in the character state save
        /// </summary>
        public void Dispose()
        {
            if (SavedPhysicsColliderMemory.IsCreated) SavedPhysicsColliderMemory.Dispose();
            if (SavedCharacterHitsBuffer.IsCreated) SavedCharacterHitsBuffer.Dispose();
            if (SavedStatefulHitsBuffer.IsCreated) SavedStatefulHitsBuffer.Dispose();
            if (SavedDeferredImpulsesBuffer.IsCreated) SavedDeferredImpulsesBuffer.Dispose();
            if (SavedVelocityProjectionHits.IsCreated) SavedVelocityProjectionHits.Dispose();
        }

        /// <summary>
        /// Reallocates a native array only if it is not created or if it does not have the required specified capacity
        /// </summary>
        /// <param name="arr"> The array to reallocate </param>
        /// <param name="requiredCapacity"> The minimum required capacity that the array should have </param>
        /// <param name="allocator"> The type of allocator to use </param>
        /// <typeparam name="T"> The type of elements stored in the array </typeparam>
        public static void CheckReallocateArray<T>(ref NativeArray<T> arr, int requiredCapacity, Allocator allocator)
            where T : unmanaged
        {
            if (!arr.IsCreated || arr.Length < requiredCapacity)
            {
                if (arr.IsCreated) arr.Dispose();

                arr = new NativeArray<T>(requiredCapacity, allocator);
            }
        }
    }

    public struct KinematicCharacterUpdateContext
    {
        /// <summary>
        /// Global time data
        /// </summary>
        public TimeData Time;

        /// <summary>
        /// The physics world this character is part of
        /// </summary>
        [ReadOnly] public PhysicsWorld PhysicsWorld;

        /// <summary>
        /// Lookup for the KinematicCharacterStoredData component
        /// </summary>
        [ReadOnly] public ComponentLookup<KinematicCharacterStoredData> CharacterStoredDataLookup;
        /// <summary>
        /// Lookup for the TrackedTransform component
        /// </summary>
        [ReadOnly] public ComponentLookup<TrackedTransform> TrackedTransformLookup;

        /// <summary>
        /// Temporary raycast hits list
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeList<RaycastHit> TmpRaycastHits;
        /// <summary>
        /// Temporary collider cast hits list
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeList<ColliderCastHit> TmpColliderCastHits;
        /// <summary>
        /// Temporary distance hits list
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeList<DistanceHit> TmpDistanceHits;
        /// <summary>
        /// Temporary rigidbody indexes list used for keeping track of unique rigidbodies collided with
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        public NativeList<int> TmpRigidbodyIndexesProcessed;

        /// <summary>
        /// Provides an opportunity to get and store global data at the moment of a system's creation 
        /// </summary>
        /// <param name="state"> The state of the system calling this method </param>
        public void OnSystemCreate(ref SystemState state)
        {
            CharacterStoredDataLookup = state.GetComponentLookup<KinematicCharacterStoredData>(true);
            TrackedTransformLookup = state.GetComponentLookup<TrackedTransform>(true);
        }

        /// <summary>
        /// Provides an opportunity to update stored data during a system's update
        /// </summary>
        /// <param name="state"> The state of the system calling this method </param>
        /// <param name="time"> The time data passed on by the system calling this method </param>
        /// <param name="physicsWorldSingleton"> The physics world singleton passed on by the system calling this method </param>
        public void OnSystemUpdate(ref SystemState state, TimeData time, PhysicsWorldSingleton physicsWorldSingleton)
        {
            Time = time;
            PhysicsWorld = physicsWorldSingleton.PhysicsWorld;

            CharacterStoredDataLookup.Update(ref state);
            TrackedTransformLookup.Update(ref state);

            TmpRaycastHits = default;
            TmpColliderCastHits = default;
            TmpDistanceHits = default;
            TmpRigidbodyIndexesProcessed = default;
        }

        /// <summary>
        /// Ensures that the temporary collections held in this struct are created. This should normally be called within a job, before the character update
        /// </summary>
        public void EnsureCreationOfTmpCollections()
        {
            if (!TmpRaycastHits.IsCreated)
                TmpRaycastHits = new NativeList<RaycastHit>(24, Allocator.Temp);
            if (!TmpColliderCastHits.IsCreated)
                TmpColliderCastHits = new NativeList<ColliderCastHit>(24, Allocator.Temp);
            if (!TmpDistanceHits.IsCreated)
                TmpDistanceHits = new NativeList<DistanceHit>(24, Allocator.Temp);
            if (!TmpRigidbodyIndexesProcessed.IsCreated)
                TmpRigidbodyIndexesProcessed = new NativeList<int>(24, Allocator.Temp);
        }
    }

    [Serializable]
    public struct BasicStepAndSlopeHandlingParameters
    {
        [Header("Step Handling")]
        [Tooltip("Whether or not step handling logic is enabled")]
        public bool StepHandling;

        [Tooltip("Max height that the character can step on")]
        public float MaxStepHeight;

        [Tooltip("Horizontal offset distance of extra downwards raycasts used to detect grounding around a step")]
        public float ExtraStepChecksDistance;

        [Tooltip("Character width used to determine grounding for steps. For a capsule this should be 2x capsule " +
                 "radius, and for a box it should be maximum box width. This is for cases where character with a " +
                 "spherical base tries to step onto an angled surface that is near the character's max step height. " +
                 "In thoses cases, the character might be grounded on steps on one frame, but wouldn't be grounded " +
                 "on the next frame as the spherical nature of its shape would push it a bit further up beyond its " +
                 "max step height.")]
        public float CharacterWidthForStepGroundingCheck;


        [Header("Slope Changes")]
        [Tooltip("Whether or not to cancel grounding when the character is moving off a ledge. This prevents the " +
                 "character from \"snapping\" onto the ledge as it moves off of it")]
        public bool PreventGroundingWhenMovingTowardsNoGrounding;

        [Tooltip("Whether or not the character has a max slope change that it can stay grounded on")]
        public bool HasMaxDownwardSlopeChangeAngle;

        [Tooltip("Max slope change that the character can stay grounded on (degrees)")]
        [Range(0f, 180f)]
        public float MaxDownwardSlopeChangeAngle;
        

        [Header("Misc")]
        [Tooltip("Whether or not to constrain the character velocity to ground plane when it hits a non-grounded slope")]
        public bool ConstrainVelocityToGroundPlane;

        /// <summary>
        /// Gets a default initialized version of step and slope handling parameters
        /// </summary>
        /// <returns> Default parameters struct </returns>
        public static BasicStepAndSlopeHandlingParameters GetDefault()
        {
            return new BasicStepAndSlopeHandlingParameters
            {
                StepHandling = false,
                MaxStepHeight = 0.5f,
                ExtraStepChecksDistance = 0.1f,
                CharacterWidthForStepGroundingCheck = 1f,

                PreventGroundingWhenMovingTowardsNoGrounding = true,
                HasMaxDownwardSlopeChangeAngle = false,
                MaxDownwardSlopeChangeAngle = 90f,

                ConstrainVelocityToGroundPlane = true,
            };
        }
    }

    public readonly partial struct KinematicCharacterAspect : IAspect
    {
        /// <summary>
        /// Offset value representing a desired distance to stay away from any collisions for the character
        /// </summary>
        public const float k_CollisionOffset = 0.01f;
        /// <summary>
        /// Minimum squared velocity length required to make grounding ignore checks
        /// </summary>
        public const float k_MinVelocityLengthSqForGroundingIgnoreCheck = 0.01f * 0.01f;
        /// <summary>
        /// Error margin for considering that the dot product of two vectors is 0f (same direction)
        /// </summary>
        public const float k_DotProductSimilarityEpsilon = 0.001f;
        /// <summary>
        /// Default max length multiplier of reverse projection
        /// </summary>
        public const float k_DefaultReverseProjectionMaxLengthRatio = 10f;
        /// <summary>
        /// Max distance of valid ground hits compared to the closest detected ground hits
        /// </summary>
        public const float k_GroundedHitDistanceTolerance = k_CollisionOffset * 6f;
        /// <summary>
        /// Squared max distance of valid ground hits compared to the closest detected ground hits
        /// </summary>
        public const float k_GroundedHitDistanceToleranceSq = k_GroundedHitDistanceTolerance * k_GroundedHitDistanceTolerance;
        /// <summary>
        /// Horizontal offset of step detection raycasts
        /// </summary>
        public const float k_StepGroundingDetectionHorizontalOffset = 0.01f;
        /// <summary>
        /// Minimum dot product value between velocity and grounding up in order to allow stepping up hits
        /// </summary>
        public const float k_MinVelocityDotRatioWithGroundingUpForSteppingUpHits = -0.85f;
        /// <summary>
        /// Minimum dot product value between grounding up and slope normal for a character to consider vertical decollision
        /// </summary>
        public const float k_MinDotRatioForVerticalDecollision = 0.1f;

        /// <summary>
        /// The entity of the character
        /// </summary>
        public readonly Entity Entity;
        /// <summary>
        /// The local transform component of the character entity
        /// </summary>
        public readonly RefRW<LocalTransform> LocalTransform;
        /// <summary>
        /// The <see cref="KinematicCharacterData"/> component of the character entity
        /// </summary>
        public readonly RefRW<KinematicCharacterData> CharacterData;
        /// <summary>
        /// The <see cref="KinematicCharacterBody"/> component of the character entity
        /// </summary>
        public readonly RefRW<KinematicCharacterBody> CharacterBody;
        /// <summary>
        /// The <see cref="PhysicsCollider"/> component of the character entity
        /// </summary>
        public readonly RefRW<PhysicsCollider> PhysicsCollider;
        /// <summary>
        /// The <see cref="KinematicCharacterHit"/> dynamic buffer of the character entity
        /// </summary>
        public readonly DynamicBuffer<KinematicCharacterHit> CharacterHitsBuffer;
        /// <summary>
        /// The <see cref="StatefulKinematicCharacterHit"/> dynamic buffer of the character entity
        /// </summary>
        public readonly DynamicBuffer<StatefulKinematicCharacterHit> StatefulHitsBuffer;
        /// <summary>
        /// The <see cref="KinematicCharacterDeferredImpulse"/> dynamic buffer of the character entity
        /// </summary>
        public readonly DynamicBuffer<KinematicCharacterDeferredImpulse> DeferredImpulsesBuffer;
        /// <summary>
        /// The <see cref="KinematicVelocityProjectionHit"/> dynamic buffer of the character entity
        /// </summary>
        public readonly DynamicBuffer<KinematicVelocityProjectionHit> VelocityProjectionHits;
        
        /// <summary>
        /// Returns the forward direction of the character transform
        /// </summary>
        public float3 Forward => math.mul(LocalTransform.ValueRO.Rotation, math.forward());

        /// <summary>
        /// Returns the back direction of the character transform
        /// </summary>
        public float3 Back => math.mul(LocalTransform.ValueRO.Rotation, -math.forward());

        /// <summary>
        /// Returns the up direction of the character transform
        /// </summary>
        public float3 Up => math.mul(LocalTransform.ValueRO.Rotation, math.up());

        /// <summary>
        /// Returns the down direction of the character transform
        /// </summary>
        public float3 Down => math.mul(LocalTransform.ValueRO.Rotation, -math.up());

        /// <summary>
        /// Returns the right direction of the character transform
        /// </summary>
        public float3 Right => math.mul(LocalTransform.ValueRO.Rotation, math.right());

        /// <summary>
        /// Returns the left direction of the character transform
        /// </summary>
        public float3 Left => math.mul(LocalTransform.ValueRO.Rotation, -math.right());

        /// <summary>
        /// Detects grounding at the current character pose
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="groundProbingLength"> Ground probing collider cast distance </param>
        /// <param name="isGrounded"> Outputs whether or not valid ground was detected </param>
        /// <param name="groundHit"> Outputs the detected ground hit </param>
        /// <param name="distanceToGround"> Outputs the distance of the detected ground hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public unsafe void GroundDetection<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            float groundProbingLength,
            out bool isGrounded,
            out BasicHit groundHit,
            out float distanceToGround)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            isGrounded = false;
            groundHit = default;
            distanceToGround = 0f;
            
            var characterRotation = LocalTransform.ValueRO.Rotation;
            var characterPosition = LocalTransform.ValueRO.Position;
            var characterBody = CharacterBody.ValueRO;
            var characterData = CharacterData.ValueRO;
            var characterPhysicsCollider = PhysicsCollider.ValueRO;

            var input = new ColliderCastInput(characterPhysicsCollider.Value, characterPosition,
                characterPosition + (-characterBody.GroundingUp * groundProbingLength),
                characterRotation);
            baseContext.TmpColliderCastHits.Clear();
            var collector = new AllHitsCollector<ColliderCastHit>(1f, ref baseContext.TmpColliderCastHits);
            baseContext.PhysicsWorld.CastCollider(input, ref collector);

            if (FilterColliderCastHitsForGroundProbing(in processor, ref context, ref baseContext,
                    ref baseContext.TmpColliderCastHits, -characterBody.GroundingUp,
                    characterData.ShouldIgnoreDynamicBodies(), out var closestHit))
            {
                // Ground hit is closest hit by default
                groundHit = new BasicHit(closestHit);
                distanceToGround = closestHit.Fraction * groundProbingLength;

                // Check grounding status
                if (characterData.EvaluateGrounding)
                {
                    var isGroundedOnClosestHit = processor.IsGroundedOnHit(ref context, ref baseContext,
                        in groundHit, (int)GroundingEvaluationType.GroundProbing);
                    if (isGroundedOnClosestHit)
                    {
                        isGrounded = true;
                    }
                    else
                    {
                        // If the closest hit wasn't grounded but other hits were detected, try to find the closest grounded hit within tolerance range
                        if (baseContext.TmpColliderCastHits.Length > 1)
                        {
                            // Sort hits in ascending fraction order
                            // TODO: We are doing a sort because, presumably, it would be faster to sort & have potentially less hits to evaluate for grounding
                            baseContext.TmpColliderCastHits.Sort(default(HitFractionComparer));

                            foreach (var tmpHit in baseContext.TmpColliderCastHits)
                            {
                                // Skip if this is our ground hit
                                if (tmpHit.RigidBodyIndex == groundHit.RigidBodyIndex && tmpHit.ColliderKey.Equals(groundHit.ColliderKey))
                                    continue;

                                //Only accept if within tolerance distance
                                var tmpHitDistance = tmpHit.Fraction * groundProbingLength;
                                if (math.distancesq(tmpHitDistance, distanceToGround) <= k_GroundedHitDistanceToleranceSq)
                                {
                                    var tmpClosestGroundedHit = new BasicHit(tmpHit);
                                    var isGroundedOnHit = processor.IsGroundedOnHit(ref context, ref baseContext,
                                        in tmpClosestGroundedHit, (int)GroundingEvaluationType.GroundProbing);
                                    if (isGroundedOnHit)
                                    {
                                        isGrounded = true;
                                        distanceToGround = tmpHitDistance;
                                        groundHit = tmpClosestGroundedHit; 
                                        break;
                                    }
                                }
                                else
                                {
                                    // if we're starting to see hits with a distance greater than tolerance dist, give
                                    // up trying to evaluate hits since the list is sorted in ascending fraction order
                                    break;
                                }
                            }
                        }
                    }
                }
                
                // Enhanced ground distance computing
                if (characterData.EnhancedGroundPrecision && distanceToGround <= 0f)
                {
                    var otherBody = baseContext.PhysicsWorld.Bodies[closestHit.RigidBodyIndex];
                    if (otherBody.Collider.AsPtr()->GetLeaf(closestHit.ColliderKey, out var leafCollider))
                    {
                        var characterWorldTransform = new RigidTransform(characterRotation, characterPosition);
                        characterWorldTransform = math.mul(characterWorldTransform, characterPhysicsCollider.ColliderPtr->MassProperties.MassDistribution.Transform);
                        var otherBodyWorldTransform = math.mul(otherBody.WorldFromBody, leafCollider.TransformFromChild);
                        var characterRelativeToOther = math.mul(math.inverse(otherBodyWorldTransform), characterWorldTransform);

                        var correctionInput = new ColliderDistanceInput(characterPhysicsCollider.Value, 1, characterRelativeToOther);
                        if (otherBody.Collider.AsPtr()->CalculateDistance(correctionInput, out var correctionHit))
                        {
                            if(correctionHit.Distance > 0f)
                            { 
                                var reconstructedHitNormal = math.mul(otherBodyWorldTransform.rot, correctionHit.SurfaceNormal);
                                if (math.dot(-reconstructedHitNormal, -characterBody.GroundingUp) > 0f)
                                {
                                    var angleBetweenGroundingDownAndClosestPointOnOther = math.PI * 0.5f - MathUtilities.AngleRadians(-reconstructedHitNormal, -characterBody.GroundingUp);
                                    var sineAngle = math.sin(angleBetweenGroundingDownAndClosestPointOnOther);
                                    if(sineAngle > 0f)
                                    {
                                        var correctedDistance = correctionHit.Distance / math.sin(angleBetweenGroundingDownAndClosestPointOnOther);
                                        distanceToGround = math.clamp(correctedDistance, 0f, k_CollisionOffset);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles calculating forces resulting from character hits, and these forces may be applied both to the character or to the hit body.
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void ProcessCharacterHitDynamics<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterRotation = LocalTransform.ValueRO.Rotation;
            var characterPosition = LocalTransform.ValueRO.Position;
            var characterData = CharacterData.ValueRO;

            baseContext.TmpRigidbodyIndexesProcessed.Clear();

            foreach (var characterHit in CharacterHitsBuffer)
            {
                if (characterHit.RigidBodyIndex < 0) continue;

                var hitBodyIndex = characterHit.RigidBodyIndex;
                var hitBody = baseContext.PhysicsWorld.Bodies[hitBodyIndex];
                var hitBodyEntity = hitBody.Entity;

                if (hitBodyEntity != characterBody.ParentEntity)
                {
                    var bodyHasPhysicsVelocityAndMass = PhysicsUtilities.DoesBodyHavePhysicsVelocityAndMass(in baseContext.PhysicsWorld, hitBodyIndex);
                    if (bodyHasPhysicsVelocityAndMass)
                    {
                        if (!baseContext.TmpRigidbodyIndexesProcessed.Contains(characterHit.RigidBodyIndex))
                        {
                            baseContext.TmpRigidbodyIndexesProcessed.Add(characterHit.RigidBodyIndex);

                            var selfPhysicsVelocity = new PhysicsVelocity
                            {
                                Linear = characterBody.RelativeVelocity + characterBody.ParentVelocity,
                                Angular = default
                            };
                            var selfPhysicsMass = PhysicsUtilities.GetKinematicCharacterPhysicsMass(characterData);
                            var selfTransform = new RigidTransform(characterRotation, characterPosition);

                            // Compute other body's data depending on if it's a character or not
                            var otherIsCharacter = false;
                            var otherIsDynamic = false;
                            var otherPhysicsVelocity = new PhysicsVelocity();
                            var otherPhysicsMass = new PhysicsMass();
                            var otherTransform = hitBody.WorldFromBody;
                            if (baseContext.CharacterStoredDataLookup.HasComponent(hitBodyEntity))
                            {
                                var data = baseContext.CharacterStoredDataLookup[hitBodyEntity];
                                otherIsCharacter = true;
                                otherIsDynamic = data.SimulateDynamicBody;
                                otherPhysicsVelocity = new PhysicsVelocity
                                {
                                    Linear = data.RelativeVelocity + data.ParentVelocity,
                                    Angular = float3.zero
                                };
                                otherPhysicsMass = PhysicsUtilities.GetKinematicCharacterPhysicsMass(baseContext.CharacterStoredDataLookup[hitBodyEntity]);
                            }
                            else if (PhysicsUtilities.DoesBodyHavePhysicsVelocityAndMass(in baseContext.PhysicsWorld, hitBodyIndex))
                            {
                                PhysicsUtilities.GetBodyComponents(in baseContext.PhysicsWorld, hitBodyIndex, out var transform, out otherPhysicsVelocity, out otherPhysicsMass);

                                otherIsDynamic = otherPhysicsMass.InverseMass > 0f;
                            }

                            // Correct the normal of the hit based on grounding considerations
                            var effectiveHitNormalFromOtherToSelf = characterHit.Normal;
                            if (characterHit is { WasCharacterGroundedOnHitEnter: true, IsGroundedOnHit: false })
                            {
                                effectiveHitNormalFromOtherToSelf = math.normalizesafe(MathUtilities.ProjectOnPlane(characterHit.Normal, characterBody.GroundingUp));
                            }
                            else if (characterHit.IsGroundedOnHit)
                            {
                                effectiveHitNormalFromOtherToSelf = characterBody.GroundingUp;
                            }

                            // Prevent a grounding-reoriented normal for dynamic bodies
                            if (otherIsDynamic && !characterHit.IsGroundedOnHit)
                            {
                                effectiveHitNormalFromOtherToSelf = characterHit.Normal;
                            }

                            // Mass overrides
                            if (characterData.SimulateDynamicBody && otherIsDynamic && !otherIsCharacter)
                            {
                                if (selfPhysicsMass.InverseMass > 0f && otherPhysicsMass.InverseMass > 0f)
                                {
                                    processor.OverrideDynamicHitMasses(ref context, ref baseContext, ref selfPhysicsMass, ref otherPhysicsMass, new BasicHit(characterHit));
                                }
                            }

                            // Special cases with kinematic VS kinematic
                            if (!characterData.SimulateDynamicBody && !otherIsDynamic)
                            {
                                // Pretend we have a mass of 1 against a kinematic body
                                selfPhysicsMass.InverseMass = 1f;

                                // When other is kinematic character, cancel their velocity towards us if any, for the sake of impulse calculations. This prevents bumping
                                if (otherIsCharacter && math.dot(otherPhysicsVelocity.Linear, effectiveHitNormalFromOtherToSelf) > 0f)
                                {
                                    otherPhysicsVelocity.Linear = MathUtilities.ProjectOnPlane(otherPhysicsVelocity.Linear, effectiveHitNormalFromOtherToSelf);
                                }
                            }

                            // Restore the portion of the character velocity that got lost during hit projection (so we can re-solve it with dynamics)
                            var velocityLostInOriginalProjection = math.projectsafe(characterHit.CharacterVelocityBeforeHit - characterHit.CharacterVelocityAfterHit, effectiveHitNormalFromOtherToSelf);
                            selfPhysicsVelocity.Linear += velocityLostInOriginalProjection;

                            // Solve impulses
                            PhysicsUtilities.SolveCollisionImpulses(selfPhysicsVelocity,
                                otherPhysicsVelocity,
                                selfPhysicsMass,
                                otherPhysicsMass,
                                selfTransform,
                                otherTransform,
                                characterHit.Position,
                                effectiveHitNormalFromOtherToSelf,
                                out var impulseOnSelf,
                                out var impulseOnOther);

                            // Apply impulse to self
                            var previousCharacterLinearVel = selfPhysicsVelocity.Linear;
                            selfPhysicsVelocity.ApplyLinearImpulse(in selfPhysicsMass, impulseOnSelf);
                            var characterLinearVelocityChange = velocityLostInOriginalProjection + (selfPhysicsVelocity.Linear - previousCharacterLinearVel);
                            characterBody.RelativeVelocity += characterLinearVelocityChange;

                            // TODO: this ignores custom vel projection.... any alternatives?
                            // trim off any velocity that goes towards ground (prevents reoriented velocity issue)
                            if (characterHit.IsGroundedOnHit && math.dot(characterBody.RelativeVelocity, characterHit.Normal) < -k_DotProductSimilarityEpsilon)
                            {
                                characterBody.RelativeVelocity = MathUtilities.ProjectOnPlane(characterBody.RelativeVelocity, characterBody.GroundingUp);
                                characterBody.RelativeVelocity = MathUtilities.ReorientVectorOnPlaneAlongDirection(characterBody.RelativeVelocity, characterHit.Normal, characterBody.GroundingUp);
                            }

                            // if a character is moving towards is, they will also solve the collision themselves in their own update.
                            // In order to prevent solving the coll twice, we won't apply any impulse on them in that case
                            var otherIsCharacterMovingTowardsUs = otherIsCharacter && math.dot(otherPhysicsVelocity.Linear, effectiveHitNormalFromOtherToSelf) > k_DotProductSimilarityEpsilon;

                            // Apply velocity change on hit body (only if dynamic and not character. Characters will solve the impulse on themselves)
                            if (!otherIsCharacterMovingTowardsUs && otherIsDynamic && math.lengthsq(impulseOnOther) > 0f)
                            {
                                var previousLinearVel = otherPhysicsVelocity.Linear;
                                var previousAngularVel = otherPhysicsVelocity.Angular;

                                otherPhysicsVelocity.ApplyImpulse(otherPhysicsMass, otherTransform.pos, otherTransform.rot, impulseOnOther, characterHit.Position);

                                DeferredImpulsesBuffer.Add(new KinematicCharacterDeferredImpulse
                                {
                                    OnEntity = hitBodyEntity,
                                    LinearVelocityChange = otherPhysicsVelocity.Linear - previousLinearVel,
                                    AngularVelocityChange = otherPhysicsVelocity.Angular - previousAngularVel,
                                });
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles casting the character shape in the velocity direction/magnitude order to detect hits, projecting the character velocity on those hits, and moving the character.
        /// The process is repeated until no new hits are detected, or until a certain max amount of iterations is reached.
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="originalVelocityDirection"> Direction of the character velocity before any projection of velocity happened on this update </param>
        /// <param name="confirmedNoOverlapsOnLastMoveIteration"> Whether or not we can confirm that the character wasn't overlapping with any colliders after the last movement iteration </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void MoveWithCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition,
            float3 originalVelocityDirection,
            out bool confirmedNoOverlapsOnLastMoveIteration)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            confirmedNoOverlapsOnLastMoveIteration = false;

            var characterRotation = LocalTransform.ValueRO.Rotation;
            var characterData = CharacterData.ValueRO;
            var characterPhysicsCollider = PhysicsCollider.ValueRO;

            // Project on ground hit
            if (characterBody.IsGrounded)
                ProjectVelocityOnGrounding(ref characterBody.RelativeVelocity, characterBody.GroundHit.Normal, characterBody.GroundingUp);

            var remainingMovementLength = math.length(characterBody.RelativeVelocity) * baseContext.Time.DeltaTime;
            var remainingMovementDirection = math.normalizesafe(characterBody.RelativeVelocity);

            // Add all close distance hits to velocity projection hits buffer
            // Helps fix some tunneling issues with rotating character colliders
            if (characterData is { DetectMovementCollisions: true, ProjectVelocityOnInitialOverlaps: true })
            {
                if (CalculateDistanceAllCollisions(in processor, ref context, ref baseContext, characterPosition,
                        characterRotation, 0f, characterData.ShouldIgnoreDynamicBodies(),
                        out var overlapHits))
                {
                    foreach (var overlapHit in overlapHits)
                    {
                        var movementHit = new BasicHit(overlapHit);

                        if (math.dot(movementHit.Normal, characterBody.RelativeVelocity) < k_DotProductSimilarityEpsilon)
                        {
                            var isGroundedOnTmpHit = false;
                            if (characterData.EvaluateGrounding)
                            {
                                isGroundedOnTmpHit = processor.IsGroundedOnHit(ref context, ref baseContext,
                                    in movementHit, (int)GroundingEvaluationType.InitialOverlaps);
                            }

                            // Add hit to projection hits
                            var currentCharacterHit = KinematicCharacterUtilities.CreateCharacterHit(in movementHit,
                                characterBody.IsGrounded, characterBody.RelativeVelocity, isGroundedOnTmpHit);
                            VelocityProjectionHits.Add(new KinematicVelocityProjectionHit(currentCharacterHit));

                            processor.OnMovementHit(ref context, ref baseContext, ref currentCharacterHit,
                                ref remainingMovementDirection, ref remainingMovementLength,
                                originalVelocityDirection, 0f);

                            currentCharacterHit.CharacterVelocityAfterHit = characterBody.RelativeVelocity;
                            CharacterHitsBuffer.Add(currentCharacterHit);
                        }
                    }
                }
            }

            // Movement cast iterations
            if (characterData.DetectMovementCollisions)
            {
                var movementCastIterationsMade = 0;
                while (movementCastIterationsMade < characterData.MaxContinuousCollisionsIterations && remainingMovementLength > 0f)
                {
                    confirmedNoOverlapsOnLastMoveIteration = false;

                    var castStartPosition = characterPosition;
                    var castDirection = remainingMovementDirection;
                    var castLength = remainingMovementLength + k_CollisionOffset; // TODO: shoud we keep this offset?

                    // Cast collider for movement
                    var castInput = new ColliderCastInput(characterPhysicsCollider.Value, castStartPosition,
                        castStartPosition + (castDirection * castLength),
                        characterRotation);
                    baseContext.TmpColliderCastHits.Clear();
                    var collector = new AllHitsCollector<ColliderCastHit>(1f, ref baseContext.TmpColliderCastHits);
                    baseContext.PhysicsWorld.CastCollider(castInput, ref collector);
                    var foundMovementHit = FilterColliderCastHitsForMove(in processor,
                        ref context,
                        ref baseContext,
                        ref baseContext.TmpColliderCastHits,
                        !characterData.SimulateDynamicBody,
                        castDirection,
                        Entity.Null,
                        characterData.ShouldIgnoreDynamicBodies(),
                        out var closestHit,
                        out var foundAnyOverlaps);

                    if (!foundAnyOverlaps) confirmedNoOverlapsOnLastMoveIteration = true;

                    if (foundMovementHit)
                    {
                        var movementHit = new BasicHit(closestHit);
                        var movementHitDistance = castLength * closestHit.Fraction;
                        movementHitDistance = math.max(0f, movementHitDistance - k_CollisionOffset);

                        var isGroundedOnMovementHit = false;
                        if (characterData.EvaluateGrounding)
                        {
                            // Grounding calculation
                            isGroundedOnMovementHit = processor.IsGroundedOnHit(ref context, ref baseContext,
                                in movementHit, (int)GroundingEvaluationType.MovementHit);
                        }

                        // Add hit to projection hits
                        var currentCharacterHit = KinematicCharacterUtilities.CreateCharacterHit(in movementHit,
                            characterBody.IsGrounded, characterBody.RelativeVelocity, isGroundedOnMovementHit);

                        processor.OnMovementHit(ref context, ref baseContext, ref currentCharacterHit,
                            ref remainingMovementDirection, ref remainingMovementLength,
                            originalVelocityDirection, movementHitDistance);

                        currentCharacterHit.CharacterVelocityAfterHit = characterBody.RelativeVelocity;
                        CharacterHitsBuffer.Add(currentCharacterHit);
                    }
                    // If no hits detected, just consume the rest of the movement, which will end the iterations
                    else
                    {
                        characterPosition += (remainingMovementDirection * remainingMovementLength);
                        remainingMovementLength = 0f;
                    }

                    movementCastIterationsMade++;
                }

                // If there is still movement left after all iterations (in other words; if we were not able to solve the movement completely)....
                if (remainingMovementLength > 0f)
                {
                    if (characterData.KillVelocityWhenExceedMaxIterations)
                    {
                        characterBody.RelativeVelocity = float3.zero;
                    }

                    if (!characterData.DiscardMovementWhenExceedMaxIterations)
                    {
                        characterPosition += (remainingMovementDirection * remainingMovementLength);
                    }
                }
            }
            else
            {
                characterPosition += characterBody.RelativeVelocity * baseContext.Time.DeltaTime;
            }
        }

        /// <summary>
        /// Handles the special case of projecting character velocity on a grounded hit, where the velocity magnitude is
        /// multiplied by a factor of 1 when it is parallel to the ground, and a factor of 0 when it is parallel to the
        /// character's "grounding up direction".
        /// </summary>
        /// <param name="velocity"> The velocity to project </param>
        /// <param name="groundNormal"> The detected ground normal </param>
        /// <param name="groundingUp"> The grounding up direction of the character </param>
        public void ProjectVelocityOnGrounding(ref float3 velocity, float3 groundNormal, float3 groundingUp)
        {
            // Make the velocity be 100% of its magnitude when it is perfectly parallel to ground, 0% when it is towards character up,
            // and interpolated when it's in-between those
            if (math.lengthsq(velocity) > 0f)
            {
                var velocityLength = math.length(velocity);
                var originalDirection = math.normalizesafe(velocity);
                var reorientedDirection = math.normalizesafe(MathUtilities.ReorientVectorOnPlaneAlongDirection(velocity, groundNormal, groundingUp));
                var dotOriginalWithUp = math.dot(originalDirection, groundingUp);
                var dotReorientedWithUp = math.dot(reorientedDirection, groundingUp);

                var ratioFromVerticalToSlopeDirection = 0f;
                // If velocity is going towards ground, interpolate between reoriented direction and down direction (-1f ratio with up)
                if (dotOriginalWithUp < dotReorientedWithUp)
                {
                    ratioFromVerticalToSlopeDirection = math.distance(dotOriginalWithUp, -1f) / math.distance(dotReorientedWithUp, -1f);
                }
                // If velocity is going towards air, interpolate between reoriented direction and up direction (1f ratio with up)
                else
                {
                    ratioFromVerticalToSlopeDirection = math.distance(dotOriginalWithUp, 1f) / math.distance(dotReorientedWithUp, 1f);
                }
                velocity = reorientedDirection * math.lerp(0f, velocityLength, ratioFromVerticalToSlopeDirection);
            }
        }

        /// <summary>
        /// Handles detecting current overlap hits, and decolliding the character from them
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="originalVelocityDirection"> Direction of the character velocity before any projection of velocity happened on this update </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void SolveOverlaps<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition,
            float3 originalVelocityDirection)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            baseContext.TmpRigidbodyIndexesProcessed.Clear();

            var characterRotation = LocalTransform.ValueRO.Rotation;
            var characterData = CharacterData.ValueRO;
            var characterPhysicsCollider = PhysicsCollider.ValueRO;

            var decollisionIterationsMade = 0;
            while (decollisionIterationsMade < characterData.MaxOverlapDecollisionIterations)
            {
                decollisionIterationsMade++;

                var distanceInput = new ColliderDistanceInput(characterPhysicsCollider.Value, 0f,
                    math.RigidTransform(characterRotation, characterPosition));
                baseContext.TmpDistanceHits.Clear();
                var collector = new AllHitsCollector<DistanceHit>(distanceInput.MaxDistance, ref baseContext.TmpDistanceHits);
                baseContext.PhysicsWorld.CalculateDistance(distanceInput, ref collector);
                FilterDistanceHitsForSolveOverlaps(in processor, ref context, ref baseContext,
                    ref baseContext.TmpDistanceHits, out var mostPenetratingHit,
                    out var mostPenetratingDynamicHit, out var mostPenetratingNonDynamicHit);

                var foundHitForDecollision = false;

                // Dynamic mode
                if (characterData.SimulateDynamicBody)
                {
                    var chosenDecollisionHit = mostPenetratingNonDynamicHit.Distance < 0f
                        ? mostPenetratingNonDynamicHit // assume we decollide from closest nondynamic hit by default
                        : default;

                    var chosenHitIsDynamic = false;
                    var isGroundedOnChosenHit = false;
                    var calculatedChosenHitIsGrounded = false;

                    // Remember all dynamic bodies as hits and push back those that cause an obstructed collision
                    foreach (var dynamicHit in baseContext.TmpDistanceHits)
                    {
                        var basicDynamicHit = new BasicHit(dynamicHit);

                        var isGroundedOnHit = false;
                        if (characterData.EvaluateGrounding)
                        {
                            isGroundedOnHit = processor.IsGroundedOnHit(ref context, ref baseContext,
                                in basicDynamicHit, (int)GroundingEvaluationType.OverlapDecollision);
                        }

                        // is this happens to be the most penetrating hit, remember as chosen hit
                        if (dynamicHit.RigidBodyIndex == mostPenetratingHit.RigidBodyIndex &&
                            dynamicHit.ColliderKey.Value == mostPenetratingHit.ColliderKey.Value)
                        {
                            chosenDecollisionHit = dynamicHit;

                            chosenHitIsDynamic = true;
                            isGroundedOnChosenHit = isGroundedOnHit;
                            calculatedChosenHitIsGrounded = true;
                        }
                    }

                    if (chosenDecollisionHit.Entity != Entity.Null)
                    {
                        var basicChosenHit = new BasicHit(chosenDecollisionHit);

                        if (!calculatedChosenHitIsGrounded)
                        {
                            if (characterData.EvaluateGrounding)
                            {
                                isGroundedOnChosenHit = processor.IsGroundedOnHit(ref context, ref baseContext,
                                    in basicChosenHit, (int)GroundingEvaluationType.OverlapDecollision);
                            }
                        }

                        DecollideFromHit(in processor,
                            ref context,
                            ref baseContext,
                            ref characterBody,
                            ref characterPosition,
                            in basicChosenHit,
                            -chosenDecollisionHit.Distance,
                            originalVelocityDirection,
                            characterData.SimulateDynamicBody,
                            isGroundedOnChosenHit,
                            chosenHitIsDynamic,
                            true,
                            true);

                        foundHitForDecollision = true;
                    }
                }
                // Kinematic mode
                else
                {
                    var foundValidNonDynamicHitToDecollideFrom = mostPenetratingNonDynamicHit.Entity != Entity.Null && mostPenetratingNonDynamicHit.Distance < 0f;
                    var isLastIteration = !foundValidNonDynamicHitToDecollideFrom || decollisionIterationsMade >= characterData.MaxOverlapDecollisionIterations;

                    // Push back all dynamic bodies & remember as hits, but only on last iteration
                    if (isLastIteration)
                    {
                        foreach (var dynamicHit in baseContext.TmpDistanceHits)
                        {
                            var basicDynamicHit = new BasicHit(dynamicHit);

                            // Add as character hit
                            var ovelapHit = KinematicCharacterUtilities.CreateCharacterHit(in basicDynamicHit,
                                characterBody.IsGrounded, characterBody.RelativeVelocity, false);
                            CharacterHitsBuffer.Add(ovelapHit);

                            // Add a position displacement impulse
                            if (!baseContext.TmpRigidbodyIndexesProcessed.Contains(dynamicHit.RigidBodyIndex))
                            {
                                baseContext.TmpRigidbodyIndexesProcessed.Add(dynamicHit.RigidBodyIndex);

                                DeferredImpulsesBuffer.Add(new KinematicCharacterDeferredImpulse
                                {
                                    OnEntity = dynamicHit.Entity,
                                    Displacement = dynamicHit.SurfaceNormal * dynamicHit.Distance,
                                });
                            }
                        }
                    }

                    // Remember that we must decollide only from the closest nonDynamic hit, if any
                    if (foundValidNonDynamicHitToDecollideFrom)
                    {
                        var basicChosenHit = new BasicHit(mostPenetratingNonDynamicHit);

                        var isGroundedOnHit = false;
                        if (characterData.EvaluateGrounding)
                        {
                            isGroundedOnHit = processor.IsGroundedOnHit(ref context, ref baseContext,
                                in basicChosenHit, (int)GroundingEvaluationType.OverlapDecollision);
                        }

                        DecollideFromHit(in processor,
                            ref context,
                            ref baseContext,
                            ref characterBody,
                            ref characterPosition,
                            in basicChosenHit,
                            -mostPenetratingNonDynamicHit.Distance,
                            originalVelocityDirection,
                            characterData.SimulateDynamicBody,
                            isGroundedOnHit,
                            false,
                            true,
                            true);

                        foundHitForDecollision = true;
                    }
                }

                // Early exit when found no hit to decollide from
                if (!foundHitForDecollision) break;
            }
        }
        
        /// <summary>
        /// Recalculates the decollision vector to resolve an overlap by projecting the overlap along the new decollision direction
        /// </summary>
        /// <param name="decollisionVector">A reference to the decollision vector that is to be recalculated </param>
        /// <param name="originalHitNormal">The normal of the hit surface from which the decollision is being calculated </param>
        /// <param name="newDecollisionDirection">The new direction in which to resolve the collision </param>
        /// <param name="decollisionDistance">The distance over which decollision is to occur </param>
        private void RecalculateDecollisionVector(ref float3 decollisionVector,
            float3 originalHitNormal,
            float3 newDecollisionDirection,
            float decollisionDistance)
        {
            var overlapDistance = math.max(decollisionDistance, 0f);
            if (overlapDistance > 0f)
                decollisionVector += MathUtilities.ReverseProjectOnVector(originalHitNormal * overlapDistance,
                    newDecollisionDirection, overlapDistance * k_DefaultReverseProjectionMaxLengthRatio);
        }

        /// <summary>
        /// Decollides character from a specific hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="hit"> The hit to decollide from </param>
        /// <param name="decollisionDistance"></param>
        /// <param name="originalVelocityDirection"> Direction of the character velocity before any projection of velocity happened on this update </param>
        /// <param name="characterSimulateDynamic"></param>
        /// <param name="isGroundedOnHit"></param>
        /// <param name="hitIsDynamic"></param>
        /// <param name="addToCharacterHits"></param>
        /// <param name="projectVelocityOnHit"></param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void DecollideFromHit<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition,
            in BasicHit hit,
            float decollisionDistance,
            float3 originalVelocityDirection,
            bool characterSimulateDynamic,
            bool isGroundedOnHit,
            bool hitIsDynamic,
            bool addToCharacterHits,
            bool projectVelocityOnHit)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterRotation = LocalTransform.ValueRO.Rotation;

            // Grounding considerations for decollision (modified decollision direction)
            var decollisionDirection = hit.Normal;
            var decollisionVector = decollisionDirection * decollisionDistance;
            if (isGroundedOnHit)
            {
                if (math.dot(characterBody.GroundingUp, hit.Normal) > k_MinDotRatioForVerticalDecollision)
                {
                    // Always decollide vertically from grounded hits
                    decollisionDirection = characterBody.GroundingUp;
                    RecalculateDecollisionVector(ref decollisionVector, hit.Normal, decollisionDirection, decollisionDistance);
                }
                else if (characterBody.IsGrounded && !hitIsDynamic)
                {
                    // If we are grounded and hit is nongrounded, decollide horizontally on the plane of our ground normal
                    decollisionDirection = math.normalizesafe(MathUtilities.ProjectOnPlane(decollisionDirection, characterBody.GroundHit.Normal));
                    RecalculateDecollisionVector(ref decollisionVector, hit.Normal, decollisionDirection, decollisionDistance);
                }
            }

            // In simulateDynamic mode, before we decollide from a dynamic body, check if the decollision would be obstructed by anything other than the decollided body
            if (characterSimulateDynamic && hitIsDynamic &&
                CastColliderClosestCollisions(in processor, ref context, ref baseContext,
                    characterPosition, characterRotation, decollisionDirection, decollisionDistance,
                    true, true, out var closestHit, out var closestHitDistance) &&
                closestHit.Entity != hit.Entity)
            {
                // Move based on how far the obstruction was
                characterPosition += decollisionDirection * closestHitDistance;

                // Displacement impulse
                if (!baseContext.TmpRigidbodyIndexesProcessed.Contains(hit.RigidBodyIndex))
                {
                    baseContext.TmpRigidbodyIndexesProcessed.Add(hit.RigidBodyIndex);
                    DeferredImpulsesBuffer.Add(new KinematicCharacterDeferredImpulse
                    {
                        OnEntity = hit.Entity,
                        Displacement = -hit.Normal * (decollisionDistance - closestHitDistance),
                    });
                }
            }
            // fully decollide otherwise
            else
            {
                characterPosition += decollisionVector;
            }

            // Velocity projection
            var characterRelativeVelocityBeforeProjection = characterBody.RelativeVelocity;
            if (projectVelocityOnHit)
            {
                VelocityProjectionHits.Add(new KinematicVelocityProjectionHit(hit, isGroundedOnHit));

                // Project velocity on obstructing overlap
                if (math.dot(characterBody.RelativeVelocity, hit.Normal) < 0f)
                {
                    processor.ProjectVelocityOnHits(ref context, ref baseContext, ref characterBody.RelativeVelocity,
                        ref characterBody.IsGrounded, ref characterBody.GroundHit, in VelocityProjectionHits,
                        originalVelocityDirection);
                }
            }

            // Add to character hits
            if (addToCharacterHits)
            {
                var ovelapCharacterHit = KinematicCharacterUtilities.CreateCharacterHit(in hit, characterBody.IsGrounded,
                    characterRelativeVelocityBeforeProjection, isGroundedOnHit);
                ovelapCharacterHit.CharacterVelocityAfterHit = characterBody.RelativeVelocity;
                CharacterHitsBuffer.Add(ovelapCharacterHit);
            }
        }
        
        /// <summary>
        /// Determines if grounded status should be prevented, based on the velocity of the character as well as the velocity of the hit body, if any.
        /// </summary>
        /// <param name="physicsWorld"> The physics world in which the hit body exists </param>
        /// <param name="hit"> The hit to evaluate </param>
        /// <param name="wasGroundedBeforeCharacterUpdate"> Whether or not the character was grounded at the start of its update, before ground detection </param>
        /// <param name="relativeVelocity"> The relative velocity of the character</param>
        /// <returns> Whether or not grounding should be set to false </returns>
        public bool ShouldPreventGroundingBasedOnVelocity(in PhysicsWorld physicsWorld,
            in BasicHit hit,
            bool wasGroundedBeforeCharacterUpdate,
            float3 relativeVelocity)
        {
            // Prevent grounding if nongrounded and going away from ground normal
            // (this prevents snapping to ground when you are in air, going upwards, and hopping onto the side of a platform)
            if (!wasGroundedBeforeCharacterUpdate &&
                math.dot(relativeVelocity, hit.Normal) > k_DotProductSimilarityEpsilon &&
                math.lengthsq(relativeVelocity) > k_MinVelocityLengthSqForGroundingIgnoreCheck)
            {
                if (PhysicsUtilities.DoesBodyHavePhysicsVelocityAndMass(in physicsWorld, hit.RigidBodyIndex))
                {
                    PhysicsUtilities.GetBodyComponents(in physicsWorld, hit.RigidBodyIndex, out var hitTransform, out var hitPhysicsVelocity, out var hitPhysicsMass);

                    var groundVelocityAtPoint = hitPhysicsVelocity.GetLinearVelocity(hitPhysicsMass, hitTransform.Position, hitTransform.Rotation, hit.Position);

                    var characterVelocityOnNormal = math.dot(relativeVelocity, hit.Normal);
                    var groundVelocityOnNormal = math.dot(groundVelocityAtPoint, hit.Normal);

                    // Ignore grounding if our velocity is escaping the ground velocity
                    if (characterVelocityOnNormal > groundVelocityOnNormal) return true;
                }
                // If the ground has no velocity and our velocity is going away from it
                else
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determines if step-handling considerations would make a character be grounded on a hit 
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hit"> The hit to decollide from </param>
        /// <param name="maxStepHeight"> The maximum height that the character can step over </param>
        /// <param name="extraStepChecksDistance"> The horizontal distance at which extra downward step-detection raycasts will be made, in order to allow stepping over steps that are slightly angled </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not step-handling would make the character grounded on this hit </returns>
        public bool IsGroundedOnSteps<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            in BasicHit hit,
            float maxStepHeight,
            float extraStepChecksDistance)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterBody = CharacterBody.ValueRO;
            var characterData = CharacterData.ValueRO;

            if (maxStepHeight > 0f)
            {
                var isGroundedOnBackStep = false;
                var isGroundedOnForwardStep = false;
                var backCheckDirection = math.normalizesafe(MathUtilities.ProjectOnPlane(hit.Normal, characterBody.GroundingUp));

                // Close back step hit
                var backStepHitFound = RaycastClosestCollisions(in processor, ref context, ref baseContext,
                    hit.Position + (backCheckDirection * k_StepGroundingDetectionHorizontalOffset),
                    -characterBody.GroundingUp, maxStepHeight, characterData.ShouldIgnoreDynamicBodies(),
                    out var backStepHit, out var backHitDistance);
                if (backStepHitFound && backHitDistance > 0f)
                    isGroundedOnBackStep = IsGroundedOnSlopeNormal(characterData.MaxGroundedSlopeDotProduct,
                        backStepHit.SurfaceNormal, characterBody.GroundingUp);

                if (!isGroundedOnBackStep && extraStepChecksDistance > k_StepGroundingDetectionHorizontalOffset)
                {
                    // Extra back step hit
                    backStepHitFound = RaycastClosestCollisions(in processor, ref context, ref baseContext,
                        hit.Position + (backCheckDirection * extraStepChecksDistance),
                        -characterBody.GroundingUp, maxStepHeight, characterData.ShouldIgnoreDynamicBodies(),
                        out backStepHit, out backHitDistance);
                    if (backStepHitFound && backHitDistance > 0f)
                        isGroundedOnBackStep = IsGroundedOnSlopeNormal(characterData.MaxGroundedSlopeDotProduct,
                            backStepHit.SurfaceNormal, characterBody.GroundingUp);
                }

                if (isGroundedOnBackStep)
                {
                    var forwardCheckHeight = maxStepHeight - backHitDistance;

                    // Detect forward obstruction
                    var forwardStepHitFound = RaycastClosestCollisions(in processor, ref context, ref baseContext,
                        hit.Position + (characterBody.GroundingUp * forwardCheckHeight), -backCheckDirection,
                        k_StepGroundingDetectionHorizontalOffset, characterData.ShouldIgnoreDynamicBodies(),
                        out var forwardStepHit, out var forwardHitDistance);
                    if (forwardStepHitFound && forwardHitDistance > 0f)
                        isGroundedOnForwardStep = IsGroundedOnSlopeNormal(characterData.MaxGroundedSlopeDotProduct,
                            forwardStepHit.SurfaceNormal, characterBody.GroundingUp);

                    if (!forwardStepHitFound)
                    {
                        // Close forward step hit
                        forwardStepHitFound = RaycastClosestCollisions(in processor, ref context, ref baseContext,
                            hit.Position + (characterBody.GroundingUp * forwardCheckHeight) + (-backCheckDirection * k_StepGroundingDetectionHorizontalOffset),
                            -characterBody.GroundingUp, maxStepHeight, characterData.ShouldIgnoreDynamicBodies(),
                            out forwardStepHit, out forwardHitDistance);
                        if (forwardStepHitFound && forwardHitDistance > 0f)
                            isGroundedOnForwardStep = IsGroundedOnSlopeNormal(characterData.MaxGroundedSlopeDotProduct,
                                forwardStepHit.SurfaceNormal, characterBody.GroundingUp);

                        if (!isGroundedOnForwardStep &&
                            extraStepChecksDistance > k_StepGroundingDetectionHorizontalOffset)
                        {
                            // Extra forward step hit obstruction
                            forwardStepHitFound = RaycastClosestCollisions(in processor, ref context, ref baseContext,
                                hit.Position + (characterBody.GroundingUp * forwardCheckHeight), -backCheckDirection,
                                extraStepChecksDistance, characterData.ShouldIgnoreDynamicBodies(),
                                out forwardStepHit, out forwardHitDistance);
                            if (forwardStepHitFound && forwardHitDistance > 0f)
                                isGroundedOnForwardStep = IsGroundedOnSlopeNormal(characterData.MaxGroundedSlopeDotProduct,
                                    forwardStepHit.SurfaceNormal, characterBody.GroundingUp);

                            if (!forwardStepHitFound)
                            {
                                // Extra forward step hit
                                forwardStepHitFound = RaycastClosestCollisions(in processor, ref context, ref baseContext,
                                    hit.Position + (characterBody.GroundingUp * forwardCheckHeight) + (-backCheckDirection * extraStepChecksDistance),
                                    -characterBody.GroundingUp, maxStepHeight, characterData.ShouldIgnoreDynamicBodies(),
                                    out forwardStepHit, out forwardHitDistance);
                                if (forwardStepHitFound && forwardHitDistance > 0f)
                                    isGroundedOnForwardStep = IsGroundedOnSlopeNormal(characterData.MaxGroundedSlopeDotProduct,
                                        forwardStepHit.SurfaceNormal, characterBody.GroundingUp);
                            }
                        }
                    }
                }

                return isGroundedOnBackStep && isGroundedOnForwardStep;
            }

            return false;
        }

        /// <summary>
        /// Handles the stepping-up-a-step logic during character movement iterations
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="hit"> The hit to decollide from </param>
        /// <param name="remainingMovementDirection"></param>
        /// <param name="remainingMovementLength"></param>
        /// <param name="hitDistance"></param>
        /// <param name="stepHandling"></param>
        /// <param name="maxStepHeight"></param>
        /// <param name="characterWidthForStepGroundingCheck"> Character width used to determine grounding for steps </param>
        /// <param name="hasSteppedUp"></param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void CheckForSteppingUpHit<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition,
            ref KinematicCharacterHit hit,
            ref float3 remainingMovementDirection,
            ref float remainingMovementLength,
            float hitDistance,
            bool stepHandling,
            float maxStepHeight,
            float characterWidthForStepGroundingCheck,
            out bool hasSteppedUp)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            hasSteppedUp = false;

            var characterRotation = LocalTransform.ValueRO.Rotation;
            var characterData = CharacterData.ValueRO;

            // Step up hits (only needed if not grounded on that hit)
            if (characterData.EvaluateGrounding && stepHandling && !hit.IsGroundedOnHit && maxStepHeight > 0f &&
                !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
            {
                var startPositionOfUpCheck = characterPosition;
                var upCheckDirection = characterBody.GroundingUp;
                var upCheckDistance = maxStepHeight;

                // Up cast
                var foundUpStepHit = CastColliderClosestCollisions(in processor, ref context, ref baseContext,
                    startPositionOfUpCheck, characterRotation, upCheckDirection, upCheckDistance,
                    true, characterData.ShouldIgnoreDynamicBodies(),
                    out var upStepHit, out var upStepHitDistance);

                upStepHitDistance = foundUpStepHit
                    ? math.max(0f, upStepHitDistance - k_CollisionOffset)
                    : upCheckDistance;

                if (upStepHitDistance > 0f)
                {
                    var startPositionOfForwardCheck = startPositionOfUpCheck + (upCheckDirection * upStepHitDistance);
                    var distanceOverStep = math.length(math.projectsafe(remainingMovementDirection * (remainingMovementLength - hitDistance), hit.Normal));
                    var endPositionOfForwardCheck = startPositionOfForwardCheck + (remainingMovementDirection * (remainingMovementLength + k_CollisionOffset));
                    var minimumDistanceOverStep = k_CollisionOffset * 3f;
                    if (distanceOverStep < minimumDistanceOverStep)
                        endPositionOfForwardCheck += -hit.Normal * (minimumDistanceOverStep - distanceOverStep);

                    var forwardCheckDirection = math.normalizesafe(endPositionOfForwardCheck - startPositionOfForwardCheck);
                    var forwardCheckDistance = math.length(endPositionOfForwardCheck - startPositionOfForwardCheck);

                    // Forward cast
                    var foundForwardStepHit = CastColliderClosestCollisions(in processor, ref context, ref baseContext,
                        startPositionOfForwardCheck, characterRotation, forwardCheckDirection, forwardCheckDistance,
                        true, characterData.ShouldIgnoreDynamicBodies(),
                        out var forwardStepHit, out var forwardStepHitDistance);

                    forwardStepHitDistance = foundForwardStepHit
                        ? math.max(0f, forwardStepHitDistance - k_CollisionOffset)
                        : forwardCheckDistance;

                    if (forwardStepHitDistance > 0f)
                    {
                        var startPositionOfDownCheck = startPositionOfForwardCheck + (forwardCheckDirection * forwardStepHitDistance);
                        var downCheckDirection = -characterBody.GroundingUp;
                        var downCheckDistance = upStepHitDistance;

                        // Down cast
                        var foundDownStepHit = CastColliderClosestCollisions(in processor, ref context, ref baseContext,
                            startPositionOfDownCheck, characterRotation, downCheckDirection, downCheckDistance,
                            true, characterData.ShouldIgnoreDynamicBodies(),
                            out var downStepHit, out var downStepHitDistance);

                        if (foundDownStepHit && downStepHitDistance > 0f)
                        {
                            var stepHit = new BasicHit(downStepHit);
                            var isGroundedOnStepHit = false;
                            if (characterData.EvaluateGrounding)
                            {
                                isGroundedOnStepHit = processor.IsGroundedOnHit(ref context, ref baseContext, in stepHit, (int)GroundingEvaluationType.StepUpHit);
                            }

                            if (isGroundedOnStepHit)
                            {
                                var hitHeight = upStepHitDistance - downStepHitDistance;
                                var steppedHeight = hitHeight;
                                steppedHeight = math.max(0f, steppedHeight + k_CollisionOffset);

                                // Add slope & character width consideration to stepped height
                                if (characterWidthForStepGroundingCheck > 0f)
                                {
                                    // Find the effective slope normal
                                    var forwardSlopeCheckDirection = -math.normalizesafe(math.cross(math.cross(characterBody.GroundingUp, stepHit.Normal), stepHit.Normal));

                                    if (RaycastClosestCollisions(in processor, ref context, ref baseContext,
                                            stepHit.Position + (characterBody.GroundingUp * k_CollisionOffset) + (forwardSlopeCheckDirection * k_CollisionOffset),
                                            -characterBody.GroundingUp, maxStepHeight, characterData.ShouldIgnoreDynamicBodies(),
                                            out var forwardSlopeCheckHit, out var forwardSlopeCheckHitDistance))
                                    {
                                        var effectiveSlopeNormal = forwardSlopeCheckHit.SurfaceNormal;
                                        var slopeRadians = MathUtilities.AngleRadians(characterBody.GroundingUp, effectiveSlopeNormal);
                                        var extraHeightFromAngleAndCharacterWidth = math.tan(slopeRadians) * characterWidthForStepGroundingCheck * 0.5f;
                                        steppedHeight += extraHeightFromAngleAndCharacterWidth;
                                    }
                                }

                                if (steppedHeight < maxStepHeight)
                                {
                                    // Step up
                                    characterPosition += characterBody.GroundingUp * hitHeight;
                                    characterPosition += forwardCheckDirection * forwardStepHitDistance;

                                    characterBody.IsGrounded = true;
                                    characterBody.GroundHit = stepHit;

                                    // Project vel
                                    var characterVelocityBeforeHit = characterBody.RelativeVelocity;
                                    characterBody.RelativeVelocity = MathUtilities.ProjectOnPlane(characterBody.RelativeVelocity, characterBody.GroundingUp);
                                    remainingMovementDirection = math.normalizesafe(characterBody.RelativeVelocity);
                                    remainingMovementLength -= forwardStepHitDistance;

                                    // Replace hit with step hit
                                    hit = KinematicCharacterUtilities.CreateCharacterHit(stepHit, characterBody.IsGrounded, characterVelocityBeforeHit, isGroundedOnStepHit);
                                    hit.CharacterVelocityAfterHit = characterBody.RelativeVelocity;

                                    hasSteppedUp = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Detects how the ground slope will change over the next character update, based on current character velocity
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="verticalOffset"> Vertical upwards distance where detection raycasts will begin </param>
        /// <param name="downDetectionDepth"> Distance of downwards slope detection raycasts </param>
        /// <param name="deltaTimeIntoFuture"> Time delta into future to detect slopes at with the current character velocity </param>
        /// <param name="secondaryNoGroundingCheckDistance"> Extra horizontal raycast distance for a secondary slope detection raycast </param>
        /// <param name="stepHandling"> Whether step-handling is enabled or not </param>
        /// <param name="maxStepHeight"> Maximum height of steps that can be stepped on </param>
        /// <param name="isMovingTowardsNoGrounding"> Whether or not the character is moving towards a place where it wouldn't be grounded </param>
        /// <param name="foundSlopeHit"> Whether or not we found a slope hit in the future </param>
        /// <param name="futureSlopeChangeAnglesRadians"> The detected slope angle change (in radians) in the future </param>
        /// <param name="futureSlopeHit"> The detected slope hit in the future </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void DetectFutureSlopeChange<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            float verticalOffset,
            float downDetectionDepth,
            float deltaTimeIntoFuture,
            float secondaryNoGroundingCheckDistance,
            bool stepHandling,
            float maxStepHeight,
            out bool isMovingTowardsNoGrounding,
            out bool foundSlopeHit,
            out float futureSlopeChangeAnglesRadians,
            out RaycastHit futureSlopeHit)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            isMovingTowardsNoGrounding = false;
            foundSlopeHit = false;
            futureSlopeChangeAnglesRadians = 0f;
            futureSlopeHit = default;

            var characterBody = CharacterBody.ValueRO;
            var characterData = CharacterData.ValueRO;

            if (IsGroundedOnSlopeNormal(characterData.MaxGroundedSlopeDotProduct, characterBody.GroundHit.Normal, characterBody.GroundingUp))
            {
                downDetectionDepth = stepHandling
                    ? math.max(maxStepHeight, downDetectionDepth) + verticalOffset
                    : downDetectionDepth + verticalOffset;

                var velocityDirection = math.normalizesafe(characterBody.RelativeVelocity);
                var rayStartPoint = characterBody.GroundHit.Position + (characterBody.GroundingUp * verticalOffset);
                var rayDirection = velocityDirection;
                var rayLength = math.length(characterBody.RelativeVelocity * deltaTimeIntoFuture);

                if (rayLength > math.EPSILON)
                {
                    // Raycast forward 
                    var forwardHitFound = RaycastClosestCollisions(in processor, ref context, ref baseContext,
                        rayStartPoint, rayDirection, rayLength, characterData.ShouldIgnoreDynamicBodies(),
                        out var forwardHit, out var forwardHitDistance);

                    if (forwardHitFound)
                    {
                        foundSlopeHit = true;
                        futureSlopeChangeAnglesRadians = CalculateAngleOfHitWithGroundUp(
                            characterBody.GroundHit.Normal, forwardHit.SurfaceNormal, velocityDirection,
                            characterBody.GroundingUp);
                        futureSlopeHit = forwardHit;
                    }
                    else
                    {
                        rayStartPoint = rayStartPoint + (rayDirection * rayLength);
                        rayDirection = -characterBody.GroundingUp;
                        rayLength = downDetectionDepth;

                        // Raycast down 
                        var downHitFound = RaycastClosestCollisions(in processor, ref context, ref baseContext,
                            rayStartPoint, rayDirection, rayLength, characterData.ShouldIgnoreDynamicBodies(),
                            out var downHit, out var downHitDistance);

                        if (downHitFound)
                        {
                            foundSlopeHit = true;
                            futureSlopeChangeAnglesRadians = CalculateAngleOfHitWithGroundUp(
                                characterBody.GroundHit.Normal, downHit.SurfaceNormal, velocityDirection,
                                characterBody.GroundingUp);
                            futureSlopeHit = downHit;

                            if (!IsGroundedOnSlopeNormal(characterData.MaxGroundedSlopeDotProduct,
                                    downHit.SurfaceNormal, characterBody.GroundingUp))
                            {
                                isMovingTowardsNoGrounding = true;
                            }
                        }
                        else
                        {
                            isMovingTowardsNoGrounding = true;
                        }

                        if (isMovingTowardsNoGrounding)
                        {
                            rayStartPoint += velocityDirection * secondaryNoGroundingCheckDistance;

                            // Raycast down (secondary)
                            var secondDownHitFound = RaycastClosestCollisions(in processor, ref context, ref baseContext,
                                rayStartPoint, rayDirection, rayLength, characterData.ShouldIgnoreDynamicBodies(),
                                out var secondDownHit, out var secondDownHitDistance);

                            if (secondDownHitFound)
                            {
                                if (!foundSlopeHit)
                                {
                                    foundSlopeHit = true;
                                    futureSlopeChangeAnglesRadians = CalculateAngleOfHitWithGroundUp(
                                        characterBody.GroundHit.Normal, secondDownHit.SurfaceNormal, velocityDirection,
                                        characterBody.GroundingUp);
                                    futureSlopeHit = secondDownHit;
                                }

                                if (IsGroundedOnSlopeNormal(characterData.MaxGroundedSlopeDotProduct,
                                        secondDownHit.SurfaceNormal, characterBody.GroundingUp))
                                    isMovingTowardsNoGrounding = false;
                            }
                            else
                            {
                                rayStartPoint += rayDirection * rayLength;
                                rayDirection = -velocityDirection;
                                rayLength = math.length(characterBody.RelativeVelocity * deltaTimeIntoFuture) + secondaryNoGroundingCheckDistance;

                                // Raycast backward
                                var backHitFound = RaycastClosestCollisions(in processor, ref context, ref baseContext,
                                    rayStartPoint, rayDirection, rayLength, characterData.ShouldIgnoreDynamicBodies(),
                                    out var backHit, out var backHitDistance);

                                if (backHitFound)
                                {
                                    foundSlopeHit = true;
                                    futureSlopeChangeAnglesRadians = CalculateAngleOfHitWithGroundUp(
                                        characterBody.GroundHit.Normal, backHit.SurfaceNormal, velocityDirection,
                                        characterBody.GroundingUp);
                                    futureSlopeHit = backHit;

                                    if (IsGroundedOnSlopeNormal(characterData.MaxGroundedSlopeDotProduct,
                                            backHit.SurfaceNormal, characterBody.GroundingUp))
                                        isMovingTowardsNoGrounding = false;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determines if the slope angle is within grounded tolerance
        /// </summary>
        /// <param name="maxGroundedSlopeDotProduct"> Dot product between grounding up and maximum slope normal direction </param>
        /// <param name="slopeSurfaceNormal"> Evaluated slope normal </param>
        /// <param name="groundingUp"> Character's grounding up </param>
        /// <returns> Whether or not the character can be grounded on this slope </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsGroundedOnSlopeNormal(float maxGroundedSlopeDotProduct,
            float3 slopeSurfaceNormal,
            float3 groundingUp)
        {
            return math.dot(groundingUp, slopeSurfaceNormal) > maxGroundedSlopeDotProduct;
        }

        /// <summary>
        /// Determines if the character movement collision detection would detect non-grounded obstructions with the designated movement vector
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="movement"> The movement vector of the character </param>
        /// <param name="hit"> The hit to decollide from </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not a non-grounded obstruction would be hit with the designated movement </returns>
        public bool MovementWouldHitNonGroundedObstruction<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            float3 movement,
            out ColliderCastHit hit)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            hit = default;

            var characterRotation = LocalTransform.ValueRO.Rotation;
            var characterPosition = LocalTransform.ValueRO.Position;
            var characterData = CharacterData.ValueRO;

            if (CastColliderClosestCollisions(in processor, ref context, ref baseContext, characterPosition,
                    characterRotation, math.normalizesafe(movement), math.length(movement),
                    true, characterData.ShouldIgnoreDynamicBodies(), out hit,
                    out var hitDistance))
            {
                if (characterData.EvaluateGrounding)
                {
                    if (!processor.IsGroundedOnHit(ref context, ref baseContext, new BasicHit(hit), (int)GroundingEvaluationType.Default))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Called on every character physics update in order to set a parent body for the character
        /// </summary>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="parentEntity"> The parent entity of the character </param>
        /// <param name="anchorPointLocalParentSpace"> The contact point between character and parent, in the parent's local space, around which the character will be rotated </param>
        public void SetOrUpdateParentBody(ref KinematicCharacterUpdateContext baseContext, 
            ref KinematicCharacterBody characterBody,
            Entity parentEntity,
            float3 anchorPointLocalParentSpace)
        {
            if (parentEntity != Entity.Null && baseContext.TrackedTransformLookup.HasComponent(parentEntity))
            {
                characterBody.ParentEntity = parentEntity;
                characterBody.ParentLocalAnchorPoint = anchorPointLocalParentSpace;
            }
            else
            {
                characterBody.ParentEntity = Entity.Null;
                characterBody.ParentLocalAnchorPoint = default;
            }
        }
        
        /// <summary>
        /// Determines the effective signed slope angle of a hit based on character movement direction (negative sign means downward)
        /// </summary>
        /// <param name="currentGroundUp"> Current ground hit normal </param>
        /// <param name="hitNormal"> Evaluated hit normal </param>
        /// <param name="velocityDirection"> Direction of the character's velocity </param>
        /// <param name="groundingUp"> Grounding up of the character </param>
        /// <returns> The signed slope angle of the hit in the character's movement direction </returns>
        public float CalculateAngleOfHitWithGroundUp(float3 currentGroundUp,
            float3 hitNormal,
            float3 velocityDirection,
            float3 groundingUp)
        {
            var velocityRight = math.normalizesafe(math.cross(velocityDirection, -groundingUp));
            var currentGroundNormalOnPlane = MathUtilities.ProjectOnPlane(currentGroundUp, velocityRight);
            var downHitNormalOnPlane = MathUtilities.ProjectOnPlane(hitNormal, velocityRight);
            var slopeChangeAnglesRadians = MathUtilities.AngleRadians(currentGroundNormalOnPlane, downHitNormalOnPlane);

            // invert angle sign if it's a downward slope change
            if(math.dot(currentGroundNormalOnPlane, velocityDirection) < math.dot(downHitNormalOnPlane, velocityDirection))
                slopeChangeAnglesRadians *= -1;

            return slopeChangeAnglesRadians;
        }
    }
}
using Character.Kinematic;
using Character.States;
using Physics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Utilities;

namespace Character
{
    public struct CharacterUpdateContext
    {
        /// <summary>
        /// Current chunk index
        /// </summary>
        public int ChunkIndex;

        /// <summary>
        /// A thread-safe command buffer that can buffer commands that affect entities and components for later playback.
        /// </summary>
        public EntityCommandBuffer.ParallelWriter EndFrameECB;

        /// <summary>
        /// Lookup for the CharacterFrictionModifier component
        /// </summary>
        [ReadOnly] public ComponentLookup<CharacterFrictionModifier> CharacterFrictionModifierLookup;
        /// <summary>
        /// Lookup for the LinkedEntityGroup component
        /// </summary>
        [ReadOnly] public BufferLookup<LinkedEntityGroup> LinkedEntityGroupLookup;

        /// <summary>
        /// The setter for <see cref="ChunkIndex"/> 
        /// </summary>
        /// <param name="chunkIndex"> The chunk index </param>
        public void SetChunkIndex(int chunkIndex) => ChunkIndex = chunkIndex;

        /// <summary>
        /// Provides an opportunity to get and store global data at the moment of a system's creation
        /// </summary>
        /// <param name="state"> The state of the system calling this method </param>
        public void OnSystemCreate(ref SystemState state)
        {
            CharacterFrictionModifierLookup = state.GetComponentLookup<CharacterFrictionModifier>(true);
            LinkedEntityGroupLookup = state.GetBufferLookup<LinkedEntityGroup>(true);
        }

        /// <summary>
        /// Provides an opportunity to update stored data during a system's update
        /// </summary>
        /// <param name="state"> The state of the system calling this method </param>
        /// <param name="endFrameECB">The EndFrameECB of current context</param>
        public void OnSystemUpdate(ref SystemState state, EntityCommandBuffer endFrameECB)
        {
            EndFrameECB = endFrameECB.AsParallelWriter();
            CharacterFrictionModifierLookup.Update(ref state);
            LinkedEntityGroupLookup.Update(ref state);
        }
    }

    public readonly partial struct CharacterAspect : IAspect, IKinematicCharacterProcessor<CharacterUpdateContext>
    {
        /// <summary>
        /// The abstract class of the character entity
        /// </summary>
        public readonly KinematicCharacterAspect KinematicAspect;
        /// <summary>
        /// The <see cref="Character.CharacterData"/> component of the character entity
        /// </summary>
        public readonly RefRW<CharacterData> Character;
        /// <summary>
        /// The <see cref="CharacterControl"/> component of the character entity
        /// </summary>
        public readonly RefRW<CharacterControl> CharacterControl;
        /// <summary>
        /// The <see cref="CharacterStateMachine"/> component of the character entity
        /// </summary>
        public readonly RefRW<CharacterStateMachine> StateMachine;
        /// <summary>
        /// The <see cref="CustomGravity"/> component of the character entity
        /// </summary>
        public readonly RefRW<CustomGravity> CustomGravity;

        public void PhysicsUpdate(ref CharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            ref var characterBody = ref KinematicAspect.CharacterBody.ValueRW;
            ref var character = ref Character.ValueRW;
            ref var characterControl = ref CharacterControl.ValueRW;
            ref var stateMachine = ref StateMachine.ValueRW;

            #region Common pre-update logic across states

            // Handle initial state transition
            if (stateMachine.CurrentState == CharacterState.Uninitialized)
            {
                stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in this);
            }

            if (characterControl.JumpHeld)
            {
                character.HeldJumpTimeCounter += baseContext.Time.DeltaTime;
            }
            else
            {
                character.HeldJumpTimeCounter = 0f;
                character.AllowHeldJumpInAir = false;
            }

            if (characterControl.JumpPressed)
            {
                character.LastTimeJumpPressed = (float)baseContext.Time.ElapsedTime;
            }

            character.HasDetectedMoveAgainstWall = false;
            if (characterBody.IsGrounded)
            {
                character.LastTimeWasGrounded = (float)baseContext.Time.ElapsedTime;
                character.AllowJumpAfterBecameUngrounded = true;
                character.AllowHeldJumpInAir = true;
            }

            #endregion

            stateMachine.OnStatePhysicsUpdate(stateMachine.CurrentState, ref context, ref baseContext, in this);

            #region Common post-update logic across states

            character.JumpPressedBeforeBecameGrounded = false;

            #endregion
        }

        public void VariableUpdate(ref CharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            ref var characterBody = ref KinematicAspect.CharacterBody.ValueRW;
            ref var stateMachine = ref StateMachine.ValueRW;
            ref var characterRotation = ref KinematicAspect.LocalTransform.ValueRW.Rotation;
        
            KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(ref characterRotation,
                characterBody.RotationFromParent, baseContext.Time.DeltaTime,
                characterBody.LastPhysicsUpdateDeltaTime);

            stateMachine.OnStateVariableUpdate(stateMachine.CurrentState, ref context, ref baseContext, in this);
        }

        public bool DetectGlobalTransitions(ref CharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            ref var stateMachine = ref StateMachine.ValueRW;
            ref var characterControl = ref CharacterControl.ValueRW;
        
            if (stateMachine.CurrentState != CharacterState.Swimming && stateMachine.CurrentState != CharacterState.GodMode)
            {
                if (SwimmingState.DetectWaterZones(ref context, ref baseContext, in this, out var tmpDirection, out var tmpDistance))
                {
                    if (tmpDistance < 0f)
                    {
                        stateMachine.TransitionToState(CharacterState.Swimming, ref context, ref baseContext, in this);
                        return true;
                    }
                }
            }

            if (characterControl.GodModePressed)
            {
                if (stateMachine.CurrentState == CharacterState.GodMode)
                {
                    stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in this);
                    return true;
                }
                else
                {
                    stateMachine.TransitionToState(CharacterState.GodMode, ref context, ref baseContext, in this);
                    return true;
                }
            }

            return false;
        }

        public void HandlePhysicsUpdateFirstPhase(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            bool allowParentHandling,
            bool allowGroundingDetection)
        {
            ref var characterBody = ref KinematicAspect.CharacterBody.ValueRW;
            ref var characterPosition = ref KinematicAspect.LocalTransform.ValueRW.Position;

            KinematicAspect.InitializeUpdate(in this, ref context, ref baseContext, ref characterBody, baseContext.Time.DeltaTime);

            if (allowParentHandling)
                KinematicAspect.ParentMovementUpdate(in this, ref context, ref baseContext, ref characterBody, ref characterPosition, characterBody.WasGroundedBeforeCharacterUpdate);

            if (allowGroundingDetection)
                KinematicAspect.GroundingUpdate(in this, ref context, ref baseContext, ref characterBody, ref characterPosition);
        }

        public void HandlePhysicsUpdateSecondPhase(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            bool allowPreventGroundingFromFutureSlopeChange,
            bool allowGroundingPushing,
            bool allowMovementAndDecollisions,
            bool allowMovingPlatformDetection,
            bool allowParentHandling)
        {
            ref var character = ref Character.ValueRW;
            ref var characterBody = ref KinematicAspect.CharacterBody.ValueRW;
            ref var characterPosition = ref KinematicAspect.LocalTransform.ValueRW.Position;
            var customGravity = CustomGravity.ValueRO;

            if (allowPreventGroundingFromFutureSlopeChange)
                KinematicAspect.PreventGroundingFromFutureSlopeChangeUpdate(in this, ref context, ref baseContext, ref characterBody, in character.StepAndSlopeHandling);

            if (allowGroundingPushing)
                KinematicAspect.GroundPushingUpdate(in this, ref context, ref baseContext, customGravity.Gravity);

            if (allowMovementAndDecollisions)
                KinematicAspect.MovementAndDecollisionsUpdate(in this, ref context, ref baseContext, ref characterBody, ref characterPosition);

            if (allowMovingPlatformDetection)
                KinematicAspect.MovingPlatformDetectionUpdate(ref baseContext, ref characterBody);

            if (allowParentHandling)
                KinematicAspect.ParentMomentumUpdate(ref baseContext, ref characterBody);

            KinematicAspect.ProcessStatefulCharacterHitsUpdate();
        }

        public unsafe void SetCapsuleGeometry(CapsuleGeometry capsuleGeometry)
        {
            ref var physicsCollider = ref KinematicAspect.PhysicsCollider.ValueRW;
        
            var capsuleCollider = (CapsuleCollider*)physicsCollider.ColliderPtr;
            capsuleCollider->Geometry = capsuleGeometry;
        }

        public float3 GetGeometryCenter(CapsuleGeometryDefinition geometry)
        {
            var characterPosition = KinematicAspect.LocalTransform.ValueRW.Position;
            var characterRotation = KinematicAspect.LocalTransform.ValueRW.Rotation;

            var characterTransform = new RigidTransform(characterRotation, characterPosition);
            var geometryCenter = math.transform(characterTransform, geometry.Center);

            return geometryCenter;
        }

        public unsafe bool CanStandUp(ref CharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            ref var physicsCollider = ref KinematicAspect.PhysicsCollider.ValueRW;
            ref var character = ref Character.ValueRW;
            ref var characterPosition = ref KinematicAspect.LocalTransform.ValueRW.Position;
            ref var characterRotation = ref KinematicAspect.LocalTransform.ValueRW.Rotation;
            var characterScale = KinematicAspect.LocalTransform.ValueRO.Scale;
            ref var characterData = ref KinematicAspect.CharacterData.ValueRW;
        
            // Overlap test with standing geometry to see if we have space to stand
            var capsuleCollider = ((CapsuleCollider*)physicsCollider.ColliderPtr);

            var initialGeometry = capsuleCollider->Geometry;
            capsuleCollider->Geometry = character.StandingGeometry.ToCapsuleGeometry();

            var isObstructed = KinematicAspect.CalculateDistanceClosestCollisions(in this, ref context, ref baseContext,
                characterPosition, characterRotation, characterScale,
                0f, characterData.ShouldIgnoreDynamicBodies(),
                out var hit);

            capsuleCollider->Geometry = initialGeometry;

            return !isObstructed;
        }

        #region Character Processor Callbacks

        public void UpdateGroundingUp(ref CharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            ref var characterBody = ref KinematicAspect.CharacterBody.ValueRW;
        
            KinematicAspect.DefaultUpdateGroundingUp(ref characterBody);
        }

        public bool CanCollideWithHit(ref CharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext,
            in BasicHit hit)
        {
            return PhysicsUtilities.IsCollidable(hit.Material);
        }

        public bool IsGroundedOnHit(ref CharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext,
            in BasicHit hit, int groundingEvaluationType)
        {
            var characterComponent = Character.ValueRO;
        
            return KinematicAspect.DefaultIsGroundedOnHit(in this,
                ref context,
                ref baseContext,
                in hit,
                in characterComponent.StepAndSlopeHandling,
                groundingEvaluationType);
        }

        public void OnMovementHit(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterHit hit,
            ref float3 remainingMovementDirection,
            ref float remainingMovementLength,
            float3 originalVelocityDirection,
            float hitDistance)
        {
            ref var characterBody = ref KinematicAspect.CharacterBody.ValueRW;
            ref var characterPosition = ref KinematicAspect.LocalTransform.ValueRW.Position;
            var characterComponent = Character.ValueRO;
        
            KinematicAspect.DefaultOnMovementHit(in this,
                ref context,
                ref baseContext,
                ref characterBody,
                ref characterPosition,
                ref hit,
                ref remainingMovementDirection,
                ref remainingMovementLength,
                originalVelocityDirection,
                hitDistance,
                characterComponent.StepAndSlopeHandling.StepHandling,
                characterComponent.StepAndSlopeHandling.MaxStepHeight,
                characterComponent.StepAndSlopeHandling.CharacterWidthForStepGroundingCheck);
        }

        public void ProjectVelocityOnHits(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            ref float3 velocity,
            ref bool characterIsGrounded,
            ref BasicHit characterGroundHit,
            in DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits,
            float3 originalVelocityDirection)
        {
            var characterComponent = Character.ValueRO;

            KinematicAspect.DefaultProjectVelocityOnHits(ref velocity,
                ref characterIsGrounded,
                ref characterGroundHit,
                in velocityProjectionHits,
                originalVelocityDirection,
                characterComponent.StepAndSlopeHandling.ConstrainVelocityToGroundPlane);
        }

        public void OverrideDynamicHitMasses(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            ref PhysicsMass characterMass,
            ref PhysicsMass otherMass,
            BasicHit hit)
        { }

        #endregion
    }
}
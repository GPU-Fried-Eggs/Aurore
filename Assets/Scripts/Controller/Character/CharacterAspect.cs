using Character.Kinematic;
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
        /// The setter for <see cref="ChunkIndex"/> 
        /// </summary>
        /// <param name="chunkIndex"> The chunk index </param>
        public void SetChunkIndex(int chunkIndex) => ChunkIndex = chunkIndex;

        /// <summary>
        /// Provides an opportunity to get and store global data at the moment of a system's creation
        /// </summary>
        /// <param name="state"> The state of the system calling this method </param>
        public void OnSystemCreate(ref SystemState state) { }

        /// <summary>
        /// Provides an opportunity to update stored data during a system's update
        /// </summary>
        /// <param name="state"> The state of the system calling this method </param>
        /// <param name="endFrameECB">The EndFrameECB of current context</param>
        public void OnSystemUpdate(ref SystemState state, EntityCommandBuffer endFrameECB)
        {
            EndFrameECB = endFrameECB.AsParallelWriter();
        }
    }

    public readonly partial struct CharacterAspect : IAspect, IKinematicCharacterProcessor<CharacterUpdateContext>
    {
        /// <summary>
        /// The abstract class of the character entity
        /// </summary>
        public readonly KinematicCharacterAspect KinematicAspect;
        /// <summary>
        /// The <see cref="CharacterData"/> component of the character entity
        /// </summary>
        public readonly RefRW<CharacterData> Character;
        /// <summary>
        /// The <see cref="CharacterControl"/> component of the character entity
        /// </summary>
        public readonly RefRW<CharacterControl> CharacterControl;

        public void PhysicsUpdate(ref CharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            ref var characterBody = ref KinematicAspect.CharacterBody.ValueRW;
            ref var character = ref Character.ValueRW;
            ref var characterControl = ref CharacterControl.ValueRW;
            ref var characterPosition = ref KinematicAspect.LocalTransform.ValueRW.Position;

            // First phase of default character update
            KinematicAspect.InitializeUpdate(in this, ref context, ref baseContext, ref characterBody, baseContext.Time.DeltaTime);
            KinematicAspect.ParentMovementUpdate(in this, ref context, ref baseContext, ref characterBody, ref characterPosition, characterBody.WasGroundedBeforeCharacterUpdate);
            KinematicAspect.GroundingUpdate(in this, ref context, ref baseContext, ref characterBody, ref characterPosition);
        
            // Update desired character velocity after grounding was detected, but before doing additional processing that depends on velocity
            var deltaTime = baseContext.Time.DeltaTime;

            // Rotate move input and velocity to take into account parent rotation
            if (characterBody.ParentEntity != Entity.Null)
            {
                characterControl.MoveVector = math.rotate(characterBody.RotationFromParent, characterControl.MoveVector);
                characterBody.RelativeVelocity = math.rotate(characterBody.RotationFromParent, characterBody.RelativeVelocity);
            }

            if (characterBody.IsGrounded)
            {
                // Move on ground
                var targetVelocity = characterControl.MoveVector * character.GroundMaxSpeed;
                CharacterControlUtilities.StandardGroundMoveInterpolated(ref characterBody.RelativeVelocity, targetVelocity, character.GroundedMovementSharpness, deltaTime, characterBody.GroundingUp, characterBody.GroundHit.Normal);

                // Jump
                if (characterControl.JumpPressed)
                {
                    CharacterControlUtilities.StandardJump(ref characterBody, characterBody.GroundingUp * character.JumpSpeed, true, characterBody.GroundingUp);
                }
            }
            else
            {
                // Move in air
                var airAcceleration = characterControl.MoveVector * character.AirAcceleration;
                if (math.lengthsq(airAcceleration) > 0f)
                {
                    var tmpVelocity = characterBody.RelativeVelocity;
                    CharacterControlUtilities.StandardAirMove(ref characterBody.RelativeVelocity, airAcceleration, character.AirMaxSpeed, characterBody.GroundingUp, deltaTime, false);

                    // Cancel air acceleration from input if we would hit a non-grounded surface (prevents air-climbing slopes at high air accelerations)
                    if (character.PreventAirAccelerationAgainstUngroundedHits && KinematicAspect.MovementWouldHitNonGroundedObstruction(in this, ref context, ref baseContext, characterBody.RelativeVelocity * deltaTime, out ColliderCastHit hit))
                    {
                        characterBody.RelativeVelocity = tmpVelocity;
                    }
                }
            
                // Gravity
                CharacterControlUtilities.AccelerateVelocity(ref characterBody.RelativeVelocity, character.Gravity, deltaTime);

                // Drag
                CharacterControlUtilities.ApplyDragToVelocity(ref characterBody.RelativeVelocity, deltaTime, character.AirDrag);
            }

            // Second phase of default character update
            KinematicAspect.PreventGroundingFromFutureSlopeChangeUpdate(in this, ref context, ref baseContext, ref characterBody, in character.StepAndSlopeHandling);
            KinematicAspect.GroundPushingUpdate(in this, ref context, ref baseContext, character.Gravity);
            KinematicAspect.MovementAndDecollisionsUpdate(in this, ref context, ref baseContext, ref characterBody, ref characterPosition);
            KinematicAspect.MovingPlatformDetectionUpdate(ref baseContext, ref characterBody); 
            KinematicAspect.ParentMomentumUpdate(ref baseContext, ref characterBody);
            KinematicAspect.ProcessStatefulCharacterHitsUpdate();
        }

        public void VariableUpdate(ref CharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext)
        {
            ref var characterBody = ref KinematicAspect.CharacterBody.ValueRW;
            ref var character = ref Character.ValueRW;
            ref var characterControl = ref CharacterControl.ValueRW;
            ref var characterRotation = ref KinematicAspect.LocalTransform.ValueRW.Rotation;
        
            KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(ref characterRotation,
                characterBody.RotationFromParent, baseContext.Time.DeltaTime,
                characterBody.LastPhysicsUpdateDeltaTime);

            if (math.lengthsq(characterControl.MoveVector) > 0f)
            {
                CharacterControlUtilities.SlerpRotationTowardsDirectionAroundUp(ref characterRotation,
                    baseContext.Time.DeltaTime,
                    math.normalizesafe(characterControl.MoveVector),
                    MathUtilities.GetUpFromRotation(characterRotation),
                    character.RotationSharpness);
            }
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
        
            return KinematicAspect.DefaultIsGroundedOnHit(in this, ref context, ref baseContext,
                in hit, in characterComponent.StepAndSlopeHandling, groundingEvaluationType);
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
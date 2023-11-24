using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Utilities;

namespace Character.Kinematic
{
    public readonly partial struct KinematicCharacterAspect
    {
        /// <summary>
        /// The initialization step of the character update (should be called on every character update). This resets key component values and buffers
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="characterBody"> The character body component </param>
        /// <param name="deltaTime"> The time delta of the character update </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void InitializeUpdate<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            float deltaTime)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            CharacterHitsBuffer.Clear();
            DeferredImpulsesBuffer.Clear();
            VelocityProjectionHits.Clear();

            characterBody.WasGroundedBeforeCharacterUpdate = characterBody.IsGrounded;
            characterBody.PreviousParentEntity = characterBody.ParentEntity;

            characterBody.RotationFromParent = quaternion.identity;
            characterBody.IsGrounded = false;
            characterBody.GroundHit = default;
            characterBody.LastPhysicsUpdateDeltaTime = deltaTime;

            processor.UpdateGroundingUp(ref context, ref baseContext);
        }

        /// <summary>
        /// Handles moving the character based on its currently-assigned ParentEntity, if any.
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="constrainRotationToGroundingUp"> Whether or not to limit rotation around the grounding up direction </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void ParentMovementUpdate<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition,
            bool constrainRotationToGroundingUp)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterTransform = LocalTransform.ValueRO;
            var characterRotation = characterTransform.Rotation;
            var characterScale = characterTransform.Scale;
            var characterData = CharacterData.ValueRO;
            var characterPhysicsCollider = PhysicsCollider.ValueRO;

            // Reset parent if parent entity doesn't exist anymore
            if (characterBody.ParentEntity != Entity.Null && !baseContext.TrackedTransformLookup.HasComponent(characterBody.ParentEntity))
                characterBody.ParentEntity = Entity.Null;

            // Reset parent velocity only if we don't have a previous parent
            // This means that if there is no current parent but there is a previous parent, parent velocity will be preserved from last character update
            if (characterBody.PreviousParentEntity == Entity.Null)
                characterBody.ParentVelocity = default;

            // Movement from parent body
            if (characterBody.ParentEntity != Entity.Null)
            {
                var parentTrackedTransform = baseContext.TrackedTransformLookup[characterBody.ParentEntity];

                // Position
                var previousLocalPosition = math.transform(math.inverse(parentTrackedTransform.PreviousFixedRateTransform), characterPosition);
                var targetWorldPosition = math.transform(parentTrackedTransform.CurrentFixedRateTransform, previousLocalPosition);

                // Rotation
                var previousLocalRotation = math.mul(math.inverse(parentTrackedTransform.PreviousFixedRateTransform.rot), characterRotation);
                var targetWorldRotation = math.mul(parentTrackedTransform.CurrentFixedRateTransform.rot, previousLocalRotation);

                // Rotation up correction
                if (constrainRotationToGroundingUp)
                {
                    var targetWorldAnchorPoint = math.transform(parentTrackedTransform.CurrentFixedRateTransform, characterBody.ParentLocalAnchorPoint);
                    var correctedRotation = MathUtilities.CreateRotationWithUpPriority(characterBody.GroundingUp, MathUtilities.GetForwardFromRotation(targetWorldRotation));
                    MathUtilities.SetRotationAroundPoint(ref targetWorldRotation, ref targetWorldPosition, targetWorldAnchorPoint, correctedRotation);
                }

                // Store data about parent movement
                var displacementFromParentMovement = targetWorldPosition - characterPosition;
                characterBody.ParentVelocity = (targetWorldPosition - characterPosition) / baseContext.Time.DeltaTime;
                characterBody.RotationFromParent = math.mul(math.inverse(characterRotation), targetWorldRotation);

                // Move Position
                if (characterData is { DetectMovementCollisions: true, DetectObstructionsForParentBodyMovement: true } &&
                    math.lengthsq(displacementFromParentMovement) > math.EPSILON)
                {
                    var castDirection = math.normalizesafe(displacementFromParentMovement);
                    var castLength = math.length(displacementFromParentMovement);

                    var endPosition = characterPosition + (castDirection * castLength);

                    var castInput = new ColliderCastInput(characterPhysicsCollider.Value, characterPosition, endPosition, characterRotation, characterScale);
                    baseContext.TmpColliderCastHits.Clear();
                    var collector = new AllHitsCollector<ColliderCastHit>(1f, ref baseContext.TmpColliderCastHits);
                    baseContext.PhysicsWorld.CastCollider(castInput, ref collector);
                    if (FilterColliderCastHitsForMove(in processor,
                            ref context,
                            ref baseContext,
                            ref baseContext.TmpColliderCastHits,
                            !characterData.SimulateDynamicBody,
                            castDirection,
                            characterBody.ParentEntity,
                            characterData.ShouldIgnoreDynamicBodies(),
                            out var closestHit,
                            out var foundAnyOverlaps))
                        characterPosition += castDirection * closestHit.Fraction * castLength;
                    else
                        characterPosition += displacementFromParentMovement;
                }
                else
                {
                    characterPosition += displacementFromParentMovement;
                }
            }
        }

        /// <summary>
        /// Handles detecting character grounding and storing results in <see cref="KinematicCharacterBody"/>
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void GroundingUpdate<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterData = CharacterData.ValueRO;

            // Detect ground
            var newIsGrounded = false;
            BasicHit newGroundHit = default;
            if (characterData.EvaluateGrounding)
            {
                // Calculate ground probe length based on circumstances
                var groundDetectionLength = characterData.SnapToGround && characterBody.WasGroundedBeforeCharacterUpdate
                    ? characterData.GroundSnappingDistance
                    : k_CollisionOffset * 3f;

                GroundDetection(in processor, ref context, ref baseContext, groundDetectionLength,
                    out newIsGrounded, out newGroundHit, out var distanceToGround);

                // Ground snapping
                if (characterData.SnapToGround && newIsGrounded)
                {
                    characterPosition -= characterBody.GroundingUp * distanceToGround;
                    characterPosition += characterBody.GroundingUp * k_CollisionOffset;
                }

                // Add ground hit as a character hit and project velocity
                if (newIsGrounded)
                {
                    var groundCharacterHit = KinematicCharacterUtilities.CreateCharacterHit(in newGroundHit,
                        characterBody.WasGroundedBeforeCharacterUpdate,
                        characterBody.RelativeVelocity,
                        newIsGrounded);
                    VelocityProjectionHits.Add(new KinematicVelocityProjectionHit(groundCharacterHit));

                    var tmpIsGrounded = characterBody.WasGroundedBeforeCharacterUpdate;
                    processor.ProjectVelocityOnHits(ref context,
                        ref baseContext,
                        ref characterBody.RelativeVelocity,
                        ref tmpIsGrounded,
                        ref newGroundHit,
                        in VelocityProjectionHits,
                        math.normalizesafe(characterBody.RelativeVelocity));

                    groundCharacterHit.CharacterVelocityAfterHit = characterBody.RelativeVelocity;
                    CharacterHitsBuffer.Add(groundCharacterHit);
                }
            }

            characterBody.IsGrounded = newIsGrounded;
            characterBody.GroundHit = newGroundHit;
        }

        /// <summary>
        /// Handles moving the character and solving collisions, based on character velocity, rotation, character grounding, and various other properties
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void MovementAndDecollisionsUpdate<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition) where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            var characterData = CharacterData.ValueRO;

            var originalVelocityDirectionBeforeMove = math.normalizesafe(characterBody.RelativeVelocity);

            // Move character based on relativeVelocity
            MoveWithCollisions(in processor, ref context, ref baseContext, ref characterBody, ref characterPosition,
                originalVelocityDirectionBeforeMove, out var moveConfirmedThereWereNoOverlaps);

            // This has to be after movement has been processed, in order to let our movement to take us
            // out of the collision with a platform before we try to decollide from it
            if (characterData.DecollideFromOverlaps && !moveConfirmedThereWereNoOverlaps)
                SolveOverlaps(in processor, ref context, ref baseContext, ref characterBody, ref characterPosition,
                    originalVelocityDirectionBeforeMove);

            // Process moving body hit velocities
            if (CharacterHitsBuffer.Length > 0)
                ProcessCharacterHitDynamics(in processor, ref context, ref baseContext, ref characterBody);
        }

        /// <summary>
        /// Handles predicting future slope changes in order to prevent grounding in certain scenarios
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="characterBody"> The character body component </param>
        /// <param name="stepAndSlopeHandling"> Parameters for step and slope handling </param>
        /// <param name="slopeDetectionVerticalOffset"> The vertical distance from ground hit at which slope detection raycasts will start </param>
        /// <param name="slopeDetectionDownDetectionDepth"> The distance of downward slope detection raycasts, added to the initial vertical offset </param>
        /// <param name="slopeDetectionSecondaryNoGroundingCheckDistance"> The forward distance of an extra raycast meant
        /// to detect slopes that are slightly further away than where our velocity would bring us over the next update </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void PreventGroundingFromFutureSlopeChangeUpdate<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref KinematicCharacterBody characterBody,
            in BasicStepAndSlopeHandlingParameters stepAndSlopeHandling,
            float slopeDetectionVerticalOffset = 0.05f,
            float slopeDetectionDownDetectionDepth = 0.05f,
            float slopeDetectionSecondaryNoGroundingCheckDistance = 0.25f)
            where T : unmanaged, IKinematicCharacterProcessor<C> where C : unmanaged
        {
            if (characterBody.IsGrounded && (stepAndSlopeHandling.PreventGroundingWhenMovingTowardsNoGrounding ||
                                             stepAndSlopeHandling.HasMaxDownwardSlopeChangeAngle))
            {
                DetectFutureSlopeChange(in processor,
                    ref context,
                    ref baseContext,
                    slopeDetectionVerticalOffset,
                    slopeDetectionDownDetectionDepth,
                    baseContext.Time.DeltaTime,
                    slopeDetectionSecondaryNoGroundingCheckDistance,
                    stepAndSlopeHandling.StepHandling,
                    stepAndSlopeHandling.MaxStepHeight,
                    out var isMovingTowardsNoGrounding,
                    out var foundSlopeHit,
                    out var futureSlopeChangeAnglesRadians,
                    out var futureSlopeHit);

                if ((stepAndSlopeHandling.PreventGroundingWhenMovingTowardsNoGrounding && isMovingTowardsNoGrounding) ||
                    (stepAndSlopeHandling.HasMaxDownwardSlopeChangeAngle && foundSlopeHit &&
                     math.degrees(futureSlopeChangeAnglesRadians) < -stepAndSlopeHandling.MaxDownwardSlopeChangeAngle))
                {
                    characterBody.IsGrounded = false;
                }
            }
        }

        /// <summary>
        /// Handles applying ground push forces to the currently-detected ground hit, if applicable
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="gravity"> The effective gravity used to create a force to apply to the ground, in combination with the character mass </param>
        /// <param name="forceMultiplier"> An arbitrary multiplier to apply to the calculated force to apply to the ground </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void GroundPushingUpdate<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            float3 gravity,
            float forceMultiplier = 1f)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterTransform = LocalTransform.ValueRO;
            var characterRotation = characterTransform.Rotation;
            var characterPosition = characterTransform.Position;
            var characterData = CharacterData.ValueRO;
            var characterBody = CharacterBody.ValueRO;

            if (characterBody.IsGrounded && characterData.SimulateDynamicBody)
            {
                var groundEntity = characterBody.GroundHit.Entity;

                if (groundEntity != Entity.Null &&
                    PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, characterBody.GroundHit.RigidBodyIndex))
                {
                    PhysicsUtilities.GetBodyComponents(in baseContext.PhysicsWorld,
                        characterBody.GroundHit.RigidBodyIndex,
                        out var groundLocalTransform,
                        out var groundPhysicsVelocity,
                        out var groundPhysicsMass);

                    var selfPhysicsMass = PhysicsUtilities.GetKinematicCharacterPhysicsMass(characterData);
                    var selfTransform = new RigidTransform(characterRotation, characterPosition);
                    var groundTransform = new RigidTransform(groundLocalTransform.Rotation, groundLocalTransform.Position);

                    selfPhysicsMass.InverseMass = 1f / characterData.Mass;
                    processor.OverrideDynamicHitMasses(ref context, ref baseContext, ref selfPhysicsMass,
                        ref groundPhysicsMass, characterBody.GroundHit);

                    var groundPointVelocity = groundPhysicsVelocity.GetLinearVelocity(groundPhysicsMass,
                        groundLocalTransform.Position, groundLocalTransform.Rotation, characterBody.GroundHit.Position);

                    // Solve impulses
                    PhysicsUtilities.SolveCollisionImpulses(
                        new PhysicsVelocity
                        {
                            Linear = groundPointVelocity + (gravity * baseContext.Time.DeltaTime),
                            Angular = default
                        },
                        groundPhysicsVelocity,
                        selfPhysicsMass,
                        groundPhysicsMass,
                        selfTransform,
                        groundTransform,
                        characterBody.GroundHit.Position,
                        -math.normalizesafe(gravity),
                        out var impulseOnSelf,
                        out var impulseOnOther);

                    var previousLinearVel = groundPhysicsVelocity.Linear;
                    var previousAngularVel = groundPhysicsVelocity.Angular;

                    groundPhysicsVelocity.ApplyImpulse(groundPhysicsMass, groundTransform.pos, groundTransform.rot,
                        impulseOnOther * forceMultiplier, characterBody.GroundHit.Position);

                    DeferredImpulsesBuffer.Add(new KinematicCharacterDeferredImpulse
                    {
                        OnEntity = groundEntity,
                        LinearVelocityChange = groundPhysicsVelocity.Linear - previousLinearVel,
                        AngularVelocityChange = groundPhysicsVelocity.Angular - previousAngularVel,
                    });
                }
            }
        }

        /// <summary>
        /// Handles detecting valid moving platforms based on current ground hit, and automatically sets them as the character's parent entity
        /// </summary>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="characterBody"> The character body component </param>
        public void MovingPlatformDetectionUpdate(ref KinematicCharacterUpdateContext baseContext, ref KinematicCharacterBody characterBody)
        {
            if (characterBody.IsGrounded && baseContext.TrackedTransformLookup.HasComponent(characterBody.GroundHit.Entity))
            {
                var groundWorldTransform = baseContext.PhysicsWorld.Bodies[characterBody.GroundHit.RigidBodyIndex].WorldFromBody;
                SetOrUpdateParentBody(ref baseContext, ref characterBody, characterBody.GroundHit.Entity,
                    math.transform(math.inverse(groundWorldTransform), characterBody.GroundHit.Position));
            }
            else
            {
                SetOrUpdateParentBody(ref baseContext, ref characterBody, Entity.Null, default);
            }
        }

        /// <summary>
        /// Handles preserving velocity momentum when getting unparented from a parent body (such as a moving platform).
        /// </summary>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        public void ParentMomentumUpdate(ref KinematicCharacterUpdateContext baseContext, ref KinematicCharacterBody characterBody)
        {
            var characterPosition = LocalTransform.ValueRO.Position;

            // Reset parent if parent entity doesn't exist anymore
            if (characterBody.ParentEntity != Entity.Null && !baseContext.TrackedTransformLookup.HasComponent(characterBody.ParentEntity))
                characterBody.ParentEntity = Entity.Null;

            // Handle adding parent body momentum
            if (characterBody.ParentEntity != characterBody.PreviousParentEntity)
            {
                // Handle preserving momentum from previous parent when there has been a parent change
                if (characterBody.PreviousParentEntity != Entity.Null)
                {
                    characterBody.RelativeVelocity += characterBody.ParentVelocity;
                    characterBody.ParentVelocity = default;
                }

                // Handle compensating momentum for new parent body
                if (characterBody.ParentEntity != Entity.Null)
                {
                    var parentTrackedTransform = baseContext.TrackedTransformLookup[characterBody.ParentEntity];
                    characterBody.ParentVelocity = parentTrackedTransform.CalculatePointVelocity(characterPosition, baseContext.Time.DeltaTime);
                    characterBody.RelativeVelocity -= characterBody.ParentVelocity;

                    if (characterBody.IsGrounded)
                    {
                        ProjectVelocityOnGrounding(ref characterBody.RelativeVelocity, characterBody.GroundHit.Normal, characterBody.GroundingUp);
                    }
                }
            }
        }

        /// <summary>
        /// Handles filling the stateful hits buffer on the character entity, with character hits that have an Enter/Exit/Stay state associated to them
        /// </summary>
        public void ProcessStatefulCharacterHitsUpdate()
        {
            bool OldStatefulHitsEntity(in DynamicBuffer<StatefulKinematicCharacterHit> statefulCharacterHitsBuffer,
                Entity entity, int lastIndexOfOldStatefulHits, out CharacterHitState oldState)
            {
                oldState = default;

                if (lastIndexOfOldStatefulHits < 0) return false;

                for (var i = 0; i <= lastIndexOfOldStatefulHits; i++)
                {
                    var oldStatefulHit = statefulCharacterHitsBuffer[i];
                    if (oldStatefulHit.Hit.Entity == entity)
                    {
                        oldState = oldStatefulHit.State;
                        return true;
                    }
                }

                return false;
            }

            bool NewStatefulHitsEntity(in DynamicBuffer<StatefulKinematicCharacterHit> statefulCharacterHitsBuffer,
                Entity entity, int firstIndexOfNewStatefulHits)
            {
                if (firstIndexOfNewStatefulHits >= statefulCharacterHitsBuffer.Length) return false;

                for (var i = firstIndexOfNewStatefulHits; i < statefulCharacterHitsBuffer.Length; i++)
                {
                    var newStatefulHit = statefulCharacterHitsBuffer[i];
                    if (newStatefulHit.Hit.Entity == entity)
                    {
                        return true;
                    }
                }

                return false;
            }

            var lastIndexOfOldStatefulHits = StatefulHitsBuffer.Length - 1;

            // Add new stateful hits
            foreach (var characterHit in CharacterHitsBuffer)
            {
                if (NewStatefulHitsEntity(in StatefulHitsBuffer, characterHit.Entity, lastIndexOfOldStatefulHits + 1))
                    continue;

                var newStatefulHit = new StatefulKinematicCharacterHit(characterHit);
                var entityWasInStatefulHitsBefore = OldStatefulHitsEntity(in StatefulHitsBuffer, characterHit.Entity, lastIndexOfOldStatefulHits, out var oldHitState);

                if (entityWasInStatefulHitsBefore)
                {
                    switch (oldHitState)
                    {
                        case CharacterHitState.Enter:
                            newStatefulHit.State = CharacterHitState.Stay;
                            break;
                        case CharacterHitState.Stay:
                            newStatefulHit.State = CharacterHitState.Stay;
                            break;
                        case CharacterHitState.Exit:
                            newStatefulHit.State = CharacterHitState.Enter;
                            break;
                    }
                }
                else
                {
                    newStatefulHit.State = CharacterHitState.Enter;
                }

                StatefulHitsBuffer.Add(newStatefulHit);
            }

            // Detect Exit states
            for (var i = 0; i <= lastIndexOfOldStatefulHits; i++)
            {
                var oldStatefulHit = StatefulHitsBuffer[i];

                // If an old hit entity isn't in new hits, add as Exit state
                if (oldStatefulHit.State == CharacterHitState.Exit ||
                    NewStatefulHitsEntity(in StatefulHitsBuffer, oldStatefulHit.Hit.Entity, lastIndexOfOldStatefulHits + 1))
                    continue;

                oldStatefulHit.State = CharacterHitState.Exit;
                StatefulHitsBuffer.Add(oldStatefulHit);
            }

            // Remove all old stateful hits
            if (lastIndexOfOldStatefulHits >= 0)
            {
                StatefulHitsBuffer.RemoveRange(0, lastIndexOfOldStatefulHits + 1);
            }
        }
    }
}
using Character.Utilities;
using Unity.Entities;
using Unity.Mathematics;

namespace Character.Kinematic
{
    public readonly partial struct KinematicCharacterAspect
    {
        /// <summary>
        /// Default implementation of the "IsGroundedOnHit" processor callback. Calls default grounding evaluation for a hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="hit"> The hit to decollide from </param>
        /// <param name="stepAndSlopeHandling"> Whether or not step-handling is enabled </param>
        /// <param name="groundingEvaluationType"> Identifier for the type of grounding evaluation that's being requested </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not the character is grounded on the hit </returns>
        public bool DefaultIsGroundedOnHit<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            in BasicHit hit,
            in BasicStepAndSlopeHandlingParameters stepAndSlopeHandling,
            int groundingEvaluationType)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterData = CharacterData.ValueRO;
            var characterBody = CharacterBody.ValueRO;
            
            var physicsWorld = baseContext.PhysicsWorld;
            if (ShouldPreventGroundingBasedOnVelocity(in physicsWorld, in hit, characterBody.WasGroundedBeforeCharacterUpdate, characterBody.RelativeVelocity))
                return false;

            var isGroundedOnSlope = IsGroundedOnSlopeNormal(characterData.MaxGroundedSlopeDotProduct, hit.Normal, characterBody.GroundingUp);

            // Handle detecting grounding on step edges if not grounded on slope
            var isGroundedOnSteps = false;
            if (!isGroundedOnSlope && stepAndSlopeHandling is { StepHandling: true, MaxStepHeight: > 0f })
            {
                var hitIsOnCharacterBottom = math.dot(characterBody.GroundingUp, hit.Normal) > Constants.DotProductSimilarityEpsilon;
                if (hitIsOnCharacterBottom &&
                    groundingEvaluationType is (int)GroundingEvaluationType.GroundProbing or (int)GroundingEvaluationType.StepUpHit)
                {
                    // Prevent step grounding detection on dynamic bodies, to prevent cases of character stepping onto sphere rolling towards it
                    if (!PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                    {
                        isGroundedOnSteps = IsGroundedOnSteps(
                            in processor,
                            ref context,
                            ref baseContext,
                            in hit,
                            stepAndSlopeHandling.MaxStepHeight,
                            stepAndSlopeHandling.ExtraStepChecksDistance);
                    }
                }
            }

            return isGroundedOnSlope || isGroundedOnSteps;
        }
        
        /// <summary>
        /// Default implementation of the "UpdateGroundingUp" processor callback. Sets the character ground up to the character transform's up direction
        /// </summary>
        /// <param name="characterBody"> The character body component </param>
        public void DefaultUpdateGroundingUp(ref KinematicCharacterBody characterBody)
        {
            var characterRotation = LocalTransform.ValueRO.Rotation;
        
            // GroundingUp must be a normalized vector representing the "up" direction that we use to evaluate slope angles with.
            // By default this is the up direction of the character transform
            characterBody.GroundingUp = MathUtilities.GetUpFromRotation(characterRotation);
        }

        /// <summary>
        /// Default implementation of the "ProjectVelocityOnHits" processor callback. Projects velocity based on grounding considerations
        /// </summary>
        /// <param name="velocity"> Character velocity </param>
        /// <param name="characterIsGrounded"> Whether character is grounded or not </param>
        /// <param name="characterGroundHit"> The ground hit of the character </param>
        /// <param name="velocityProjectionHits"> List of hits that the velocity must be projected on, from oldest to most recent </param>
        /// <param name="originalVelocityDirection"> Original character velocity direction before any projection happened </param>
        /// <param name="constrainToGoundPlane"> Whether or not to constrain </param>
        public void DefaultProjectVelocityOnHits(
            ref float3 velocity,
            ref bool characterIsGrounded,
            ref BasicHit characterGroundHit,
            in DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits,
            float3 originalVelocityDirection,
            bool constrainToGoundPlane)
        {
            bool IsSamePlane(float3 planeA, float3 planeB) => math.dot(planeA, planeB) > (1f - Constants.DotProductSimilarityEpsilon);

            void ProjectVelocityOnSingleHit(ref float3 velocity, ref bool characterIsGrounded, ref BasicHit characterGroundHit, in KinematicVelocityProjectionHit hit, float3 groundingUp)
            {
                if (characterIsGrounded)
                {
                    if (hit.IsGroundedOnHit)
                    {
                        // Simply reorient velocity
                        velocity = MathUtilities.ReorientVectorOnPlaneAlongDirection(velocity, hit.Normal, groundingUp);
                    }
                    else
                    {
                        if (constrainToGoundPlane)
                        {
                            // Project velocity on crease formed between ground normal and obstruction
                            var groundedCreaseDirection = math.normalizesafe(math.cross(characterGroundHit.Normal, hit.Normal));
                            velocity = math.projectsafe(velocity, groundedCreaseDirection);
                        }
                        else
                        {
                            // Regular projection
                            velocity = MathUtilities.ProjectOnPlane(velocity, hit.Normal);
                        }
                    }
                }
                else
                {
                    if (hit.IsGroundedOnHit)
                    {
                        // Handle grounded landing
                        velocity = MathUtilities.ProjectOnPlane(velocity, groundingUp);
                        velocity = MathUtilities.ReorientVectorOnPlaneAlongDirection(velocity, hit.Normal, groundingUp);
                    }
                    else
                    {
                        // Regular projection
                        velocity = MathUtilities.ProjectOnPlane(velocity, hit.Normal);
                    }
                }

                // Replace grounding when the hit is grounded (or when not trying to constrain movement to ground plane
                if (hit.IsGroundedOnHit || !constrainToGoundPlane)
                {
                    // This could be a virtual hit, so make sure to only count it if it has a valid rigidbody
                    if (hit.RigidBodyIndex >= 0)
                    {
                        // make sure to only count as ground if the normal is pointing up
                        if (math.dot(groundingUp, hit.Normal) > Constants.DotProductSimilarityEpsilon)
                        {
                            characterIsGrounded = hit.IsGroundedOnHit;
                            characterGroundHit = new BasicHit(hit);
                        }
                    }
                }
            }

            if (math.lengthsq(velocity) <= 0f || math.lengthsq(originalVelocityDirection) <= 0f) return;

            var characterBody = CharacterBody.ValueRO;

            var hitsCount = velocityProjectionHits.Length;
            var firstHitIndex = velocityProjectionHits.Length - 1;
            var firstHit = velocityProjectionHits[firstHitIndex];
            var velocityDirection = math.normalizesafe(velocity);

            if (math.dot(velocityDirection, firstHit.Normal) < 0f)
            {
                // Project on first plane
                ProjectVelocityOnSingleHit(ref velocity, ref characterIsGrounded, ref characterGroundHit, in firstHit, characterBody.GroundingUp);
                velocityDirection = math.normalizesafe(velocity);

                // Original velocity direction will act as a plane constraint just like other hits, to prevent our
                // velocity from going back the way it came from. Hit index -1 represents original velocity
                KinematicVelocityProjectionHit originalVelocityHit = default;
                originalVelocityHit.Normal = characterIsGrounded
                    ? math.normalizesafe(MathUtilities.ProjectOnPlane(originalVelocityDirection, characterBody.GroundingUp))
                    : originalVelocityDirection;

                // Detect creases and corners by observing how the projected velocity would interact with previously-detected planes
                for (var secondHitIndex = -1; secondHitIndex < hitsCount; secondHitIndex++)
                {
                    if (secondHitIndex == firstHitIndex) continue;

                    var secondHit = secondHitIndex >= 0 ? velocityProjectionHits[secondHitIndex] : originalVelocityHit;

                    if (IsSamePlane(firstHit.Normal, secondHit.Normal))
                        continue;

                    if (math.dot(velocityDirection, secondHit.Normal) > -Constants.DotProductSimilarityEpsilon)
                        continue;

                    // Project on second plane
                    ProjectVelocityOnSingleHit(ref velocity, ref characterIsGrounded, ref characterGroundHit, in secondHit, characterBody.GroundingUp);
                    velocityDirection = math.normalizesafe(velocity);

                    // If the velocity projected on second plane goes back in first plane, it's a crease
                    if (math.dot(velocityDirection, firstHit.Normal) > -Constants.DotProductSimilarityEpsilon)
                        continue;

                    // Special case corner detection when grounded: if crease is made out of 2 non-grounded planes; it's a corner
                    if (characterIsGrounded && !firstHit.IsGroundedOnHit && !secondHit.IsGroundedOnHit)
                    {
                        velocity = default;
                        break;
                    }

                    // Velocity projection on crease
                    var creaseDirection = math.normalizesafe(math.cross(firstHit.Normal, secondHit.Normal));
                    if (secondHit.IsGroundedOnHit)
                    {
                        velocity = MathUtilities.ReorientVectorOnPlaneAlongDirection(velocity, secondHit.Normal, characterBody.GroundingUp);
                    }
                    velocity = math.projectsafe(velocity, creaseDirection);
                    velocityDirection = math.normalizesafe(velocity);

                    // Corner detection: see if projected velocity would enter back a third plane we already detected
                    for (var thirdHitIndex = -1; thirdHitIndex < hitsCount; thirdHitIndex++)
                    {
                        if (thirdHitIndex == firstHitIndex && thirdHitIndex == secondHitIndex)
                            continue;

                        var thirdHit = thirdHitIndex >= 0 ? velocityProjectionHits[thirdHitIndex] : originalVelocityHit;

                        if (IsSamePlane(firstHit.Normal, thirdHit.Normal) || IsSamePlane(secondHit.Normal, thirdHit.Normal))
                            continue;

                        if (math.dot(velocityDirection, thirdHit.Normal) < -Constants.DotProductSimilarityEpsilon)
                        {
                            // Velocity projection on corner
                            velocity = default;
                            break;
                        }
                    }

                    if (math.lengthsq(velocity) <= math.EPSILON) break;
                }
            }
        }
        
        /// <summary>
        /// Default implementation of the "OnMovementHit" processor callback
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param> 
        /// <param name="characterBody"> The character body component </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="hit"> The hit to decollide from </param>
        /// <param name="remainingMovementDirection"> Direction of the character movement that's left to be processed </param>
        /// <param name="remainingMovementLength"> Magnitude of the character movement that's left to be processed </param>
        /// <param name="originalVelocityDirection"> Original character velocity direction before any projection happened </param>
        /// <param name="movementHitDistance"> Distance of the hit </param>
        /// <param name="stepHandling"> Whether step-handling is enabled or not </param>
        /// <param name="maxStepHeight"> Maximum height of steps that can be stepped on </param>
        /// <param name="characterWidthForStepGroundingCheck"> Character width used to determine grounding for steps </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void DefaultOnMovementHit<T, C>(
            in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext, 
            ref KinematicCharacterBody characterBody,
            ref float3 characterPosition,
            ref KinematicCharacterHit hit,
            ref float3 remainingMovementDirection,
            ref float remainingMovementLength,
            float3 originalVelocityDirection,
            float movementHitDistance,
            bool stepHandling,
            float maxStepHeight,
            float characterWidthForStepGroundingCheck = 0f)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var hasSteppedUp = false;

            if (stepHandling && !hit.IsGroundedOnHit &&
                math.dot(math.normalizesafe(characterBody.RelativeVelocity), characterBody.GroundingUp) > Constants.MinVelocityDotRatioWithGroundingUpForSteppingUpHits)
            {
                CheckForSteppingUpHit(
                    in processor,
                    ref context,
                    ref baseContext, 
                    ref characterBody,
                    ref characterPosition,
                    ref hit,
                    ref remainingMovementDirection,
                    ref remainingMovementLength,
                    movementHitDistance,
                    stepHandling,
                    maxStepHeight,
                    characterWidthForStepGroundingCheck,
                    out hasSteppedUp);
            }
            
            // Add velocityProjection hits only after potential correction from step handling
            VelocityProjectionHits.Add(new KinematicVelocityProjectionHit(hit));

            if (!hasSteppedUp)
            {
                // Advance position to closest hit
                characterPosition += remainingMovementDirection * movementHitDistance;
                remainingMovementLength -= movementHitDistance;

                // Project velocity
                var velocityBeforeProjection = characterBody.RelativeVelocity;

                processor.ProjectVelocityOnHits(
                    ref context,
                    ref baseContext,
                    ref characterBody.RelativeVelocity,
                    ref characterBody.IsGrounded,
                    ref characterBody.GroundHit,
                    in VelocityProjectionHits,
                    originalVelocityDirection);
                
                // Recalculate remaining movement after projection
                var projectedVelocityLengthFactor = math.length(characterBody.RelativeVelocity) / math.length(velocityBeforeProjection);
                remainingMovementLength *= projectedVelocityLengthFactor;
                remainingMovementDirection = math.normalizesafe(characterBody.RelativeVelocity);
            }
        }
    }
}
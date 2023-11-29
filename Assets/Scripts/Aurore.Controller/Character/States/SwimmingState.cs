using Character.Kinematic;
using Player;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Utilities;

namespace Character.States
{
    public struct SwimmingState : ICharacterState
    {
        public bool HasJumpedWhileSwimming;
        public bool HasDetectedGrounding;
        public bool ShouldExitSwimming;

        private const float k_DistanceFromSurfaceToAllowJumping = -0.05f;
        private const float k_ForcedDistanceFromSurface = 0.01f;

        public void OnStateEnter(CharacterState previousState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            ref var characterBody = ref aspect.KinematicAspect.CharacterBody.ValueRW;
            ref var characterData = ref aspect.KinematicAspect.CharacterData.ValueRW;
            ref var character = ref aspect.Character.ValueRW;

            aspect.SetCapsuleGeometry(character.SwimmingGeometry.ToCapsuleGeometry());

            characterData.SnapToGround = false;
            characterBody.IsGrounded = false;

            HasJumpedWhileSwimming = false;
            ShouldExitSwimming = false;
        }

        public void OnStateExit(CharacterState nextState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            ref var characterData = ref aspect.KinematicAspect.CharacterData.ValueRW;

            characterData.SnapToGround = true;
        }

        public void OnStatePhysicsUpdate(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            aspect.HandlePhysicsUpdateFirstPhase(ref context, ref baseContext, true, true);

            PreMovementUpdate(ref context, ref baseContext, in aspect);

            aspect.HandlePhysicsUpdateSecondPhase(ref context, ref baseContext, false, false, true, false, true);

            PostMovementUpdate(ref context, ref baseContext, in aspect);

            DetectTransitions(ref context, ref baseContext, in aspect);
        }

        public void OnStateVariableUpdate(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            var deltaTime = baseContext.Time.DeltaTime;
            ref var characterBody = ref aspect.KinematicAspect.CharacterBody.ValueRW;
            ref var character = ref aspect.Character.ValueRW;
            ref var characterControl = ref aspect.CharacterControl.ValueRW;
            ref var characterPosition = ref aspect.KinematicAspect.LocalTransform.ValueRW.Position;
            ref var characterRotation = ref aspect.KinematicAspect.LocalTransform.ValueRW.Rotation;
            var customGravity = aspect.CustomGravity.ValueRO;

            if (ShouldExitSwimming) return;

            if (character.DistanceFromWaterSurface > character.SwimmingStandUpDistanceFromSurface)
            {
                // when close to surface, orient self up
                var upPlane = -math.normalizesafe(customGravity.Gravity);
                float3 targetForward = default;
                if (math.lengthsq(characterControl.MoveVector) > 0f)
                {
                    targetForward = math.normalizesafe(MathUtilities.ProjectOnPlane(characterControl.MoveVector, upPlane));
                }
                else
                {
                    targetForward = math.normalizesafe(MathUtilities.ProjectOnPlane(MathUtilities.GetForwardFromRotation(characterRotation), upPlane));
                    if (math.dot(characterBody.GroundingUp, upPlane) < 0f)
                    {
                        targetForward = -targetForward;
                    }
                }
                var targetRotation = MathUtilities.CreateRotationWithUpPriority(upPlane, targetForward);
                targetRotation = math.slerp(characterRotation, targetRotation,
                    MathUtilities.GetSharpnessInterpolant(character.SwimmingRotationSharpness, deltaTime));
                MathUtilities.SetRotationAroundPoint(ref characterRotation, ref characterPosition,
                    aspect.GetGeometryCenter(character.SwimmingGeometry), targetRotation);
            }
            else
            {
                if (math.lengthsq(characterControl.MoveVector) > 0f)
                {
                    // Make character up face the movement direction, and character forward face gravity direction as much as it can
                    var targetRotation = MathUtilities.CreateRotationWithUpPriority(
                        math.normalizesafe(characterControl.MoveVector), math.normalizesafe(customGravity.Gravity));
                    targetRotation = math.slerp(characterRotation, targetRotation,
                        MathUtilities.GetSharpnessInterpolant(character.SwimmingRotationSharpness, deltaTime));
                    MathUtilities.SetRotationAroundPoint(ref characterRotation, ref characterPosition,
                        aspect.GetGeometryCenter(character.SwimmingGeometry), targetRotation);
                }
            }
        }

        public void GetCameraParameters(in CharacterData character, out Entity cameraTarget, out bool calculateUpFromGravity)
        {
            cameraTarget = character.SwimmingCameraTargetEntity;
            calculateUpFromGravity = true;
        }

        public void PreMovementUpdate(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            var deltaTime = baseContext.Time.DeltaTime;
            ref var characterBody = ref aspect.KinematicAspect.CharacterBody.ValueRW;
            ref var character = ref aspect.Character.ValueRW;
            ref var characterControl = ref aspect.CharacterControl.ValueRW;
            ref var characterRotation = ref aspect.KinematicAspect.LocalTransform.ValueRW.Rotation;

            HasDetectedGrounding = characterBody.IsGrounded;
            characterBody.IsGrounded = false;

            if (DetectWaterZones(ref context, ref baseContext, in aspect, out character.DirectionToWaterSurface,
                    out character.DistanceFromWaterSurface))
            {
                // Movement
                var addedMoveVector = float3.zero;
                if (character.DistanceFromWaterSurface > character.SwimmingStandUpDistanceFromSurface)
                {
                    // When close to water surface, prevent moving down unless the input points strongly down
                    var dotMoveDirectionWithSurface = math.dot(math.normalizesafe(characterControl.MoveVector),
                        character.DirectionToWaterSurface);
                    if (dotMoveDirectionWithSurface > character.SwimmingSurfaceDiveThreshold)
                    {
                        characterControl.MoveVector = MathUtilities.ProjectOnPlane(characterControl.MoveVector,
                            character.DirectionToWaterSurface);
                    }

                    // Add an automatic move towards surface
                    addedMoveVector = character.DirectionToWaterSurface * 0.1f;
                }

                var acceleration = (characterControl.MoveVector + addedMoveVector) * character.SwimmingAcceleration;
                CharacterControlUtilities.StandardAirMove(ref characterBody.RelativeVelocity, acceleration,
                    character.SwimmingMaxSpeed, -MathUtilities.GetForwardFromRotation(characterRotation), deltaTime,
                    true);

                // Water drag
                CharacterControlUtilities.ApplyDragToVelocity(ref characterBody.RelativeVelocity, deltaTime,
                    character.SwimmingDrag);

                // Handle jumping out of water when close to water surface
                HasJumpedWhileSwimming = false;
                if (characterControl.JumpPressed && character.DistanceFromWaterSurface > k_DistanceFromSurfaceToAllowJumping)
                {
                    CharacterControlUtilities.StandardJump(ref characterBody,
                        characterBody.GroundingUp * character.SwimmingJumpSpeed, true, characterBody.GroundingUp);
                    HasJumpedWhileSwimming = true;
                }
            }
            else
            {
                ShouldExitSwimming = true;
            }
        }

        public void PostMovementUpdate(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            ref var characterBody = ref aspect.KinematicAspect.CharacterBody.ValueRW;
            ref var character = ref aspect.Character.ValueRW;
            ref var characterPosition = ref aspect.KinematicAspect.LocalTransform.ValueRW.Position;

            var determinedHasExitedWater = false;
            if (DetectWaterZones(ref context, ref baseContext, in aspect, out character.DirectionToWaterSurface,
                    out character.DistanceFromWaterSurface))
            {
                // Handle snapping to water surface when trying to swim out of the water
                if (character.DistanceFromWaterSurface > -k_ForcedDistanceFromSurface)
                {
                    var currentDistanceToTargetDistance = -k_ForcedDistanceFromSurface - character.DistanceFromWaterSurface;
                    var translationSnappedToWaterSurface = characterPosition + (character.DirectionToWaterSurface * currentDistanceToTargetDistance);

                    // Only snap to water surface if we're not jumping out of the water, or if we'd be obstructed when trying to snap back (allows us to walk out of water)
                    if (HasJumpedWhileSwimming || characterBody.GroundHit.Entity != Entity.Null)
                    {
                        determinedHasExitedWater = true;
                    }
                    else
                    {
                        // Snap position bact to water surface
                        characterPosition = translationSnappedToWaterSurface;

                        // Project velocity on water surface normal
                        characterBody.RelativeVelocity = MathUtilities.ProjectOnPlane(characterBody.RelativeVelocity,
                            character.DirectionToWaterSurface);
                    }
                }
            }

            ShouldExitSwimming = determinedHasExitedWater;
        }
        public bool DetectTransitions(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            ref var stateMachine = ref aspect.StateMachine.ValueRW;

            if (ShouldExitSwimming || HasDetectedGrounding)
            {
                if (HasDetectedGrounding)
                {
                    stateMachine.TransitionToState(CharacterState.GroundMove, ref context, ref baseContext, in aspect);
                    return true;
                }
                else
                {
                    stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in aspect);
                    return true;
                }
            }

            return aspect.DetectGlobalTransitions(ref context, ref baseContext);
        }

        public void GetMoveVectorFromPlayerInput(in PlayerInputs inputs, quaternion cameraRotation, out float3 moveVector)
        {
            var cameraForward = math.mul(cameraRotation, math.forward());
            var cameraRight = math.mul(cameraRotation, math.right());
            var cameraUp = math.mul(cameraRotation, math.up());
        
            moveVector = (cameraRight * inputs.Move.x) + (cameraForward * inputs.Move.y);
            if (inputs.JumpHeld) moveVector += cameraUp;

            if (inputs.CrouchHeld) moveVector -= cameraUp;
            moveVector = MathUtilities.ClampToMaxLength(moveVector, 1f);
        }

        public static unsafe bool DetectWaterZones(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect,
            out float3 directionToWaterSurface,
            out float waterSurfaceDistance)
        {
            directionToWaterSurface = default;
            waterSurfaceDistance = 0f;
        
            ref var physicsCollider = ref aspect.KinematicAspect.PhysicsCollider.ValueRW;
            ref var character = ref aspect.Character.ValueRW;
            ref var characterPosition = ref aspect.KinematicAspect.LocalTransform.ValueRW.Position;
            ref var characterRotation = ref aspect.KinematicAspect.LocalTransform.ValueRW.Rotation;

            var characterRigidTransform = new RigidTransform(characterRotation, characterPosition);
            var swimmingDetectionPointWorldPosition = math.transform(characterRigidTransform, character.LocalSwimmingDetectionPoint);
            var waterDetectionFilter = new CollisionFilter
            {
                BelongsTo = physicsCollider.ColliderPtr->GetCollisionFilter().BelongsTo,
                CollidesWith = character.WaterPhysicsCategory.Value,
            };

            var pointInput = new PointDistanceInput
            {
                Filter = waterDetectionFilter,
                MaxDistance = character.WaterDetectionDistance,
                Position = swimmingDetectionPointWorldPosition,
            };

            if (baseContext.PhysicsWorld.CalculateDistance(pointInput, out var closestHit))
            {
                directionToWaterSurface = closestHit.SurfaceNormal; // always goes in the direction of decolliding from the target collider
                waterSurfaceDistance = closestHit.Distance; // positive means above surface
                return true;
            }

            return false;
        }
    }
}
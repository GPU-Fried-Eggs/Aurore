using Character.Kinematic;
using Player;
using Unity.Entities;
using Unity.Mathematics;
using Utilities;

namespace Character.States
{
    public struct AirMoveState : ICharacterState
    {
        public void OnStateEnter(CharacterState previousState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            ref var character = ref aspect.Character.ValueRW;

            aspect.SetCapsuleGeometry(character.StandingGeometry.ToCapsuleGeometry());
        }

        public void OnStateExit(CharacterState nextState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        { }

        public void OnStatePhysicsUpdate(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            var deltaTime = baseContext.Time.DeltaTime;
            var elapsedTime = (float)baseContext.Time.ElapsedTime;
            ref var characterBody = ref aspect.KinematicAspect.CharacterBody.ValueRW;
            ref var character = ref aspect.Character.ValueRW;
            ref var characterControl = ref aspect.CharacterControl.ValueRW;
            var customGravity = aspect.CustomGravity.ValueRO;

            aspect.HandlePhysicsUpdateFirstPhase(ref context, ref baseContext, true, true);

            #region Move

            var airAcceleration = characterControl.MoveVector * character.AirAcceleration;
            if (math.lengthsq(airAcceleration) > 0f)
            {
                var tmpVelocity = characterBody.RelativeVelocity;
                CharacterControlUtilities.StandardAirMove(ref characterBody.RelativeVelocity, airAcceleration, character.AirMaxSpeed, characterBody.GroundingUp, deltaTime, false);

                // Cancel air acceleration from input if we would hit a non-grounded surface (prevents air-climbing slopes at high air accelerations)
                if (aspect.KinematicAspect.MovementWouldHitNonGroundedObstruction(in aspect, ref context, ref baseContext,
                        characterBody.RelativeVelocity * deltaTime, out var hit))
                {
                    characterBody.RelativeVelocity = tmpVelocity;
                
                    character.HasDetectedMoveAgainstWall = true;
                    character.LastKnownWallNormal = hit.SurfaceNormal;
                }
            }

            #endregion

            #region Jumping

            if (characterControl.JumpPressed)
            {
                // Allow jumping shortly after getting degrounded
                if (character.AllowJumpAfterBecameUngrounded && elapsedTime < character.LastTimeWasGrounded + character.JumpAfterUngroundedGraceTime)
                {
                    CharacterControlUtilities.StandardJump(ref characterBody,
                        characterBody.GroundingUp * character.GroundJumpSpeed,
                        true,
                        characterBody.GroundingUp);
                    character.HeldJumpTimeCounter = 0f;
                }
                // Air jumps
                else if (character.CurrentUngroundedJumps < character.MaxUngroundedJumps)
                {
                    CharacterControlUtilities.StandardJump(ref characterBody,
                        characterBody.GroundingUp * character.AirJumpSpeed,
                        true,
                        characterBody.GroundingUp);
                    character.CurrentUngroundedJumps++;
                }
                // Remember that we wanted to jump before we became grounded
                else
                {
                    character.JumpPressedBeforeBecameGrounded = true;
                }

                character.AllowJumpAfterBecameUngrounded = false;
            }

            // Additional jump power when holding jump
            if (character.AllowHeldJumpInAir && characterControl.JumpHeld && character.HeldJumpTimeCounter < character.MaxHeldJumpTime)
            {
                characterBody.RelativeVelocity += characterBody.GroundingUp * character.JumpHeldAcceleration * deltaTime;
            }

            #endregion

            // Gravity
            CharacterControlUtilities.AccelerateVelocity(ref characterBody.RelativeVelocity, customGravity.Gravity, deltaTime);

            // Drag
            CharacterControlUtilities.ApplyDragToVelocity(ref characterBody.RelativeVelocity, deltaTime, character.AirDrag);

            aspect.HandlePhysicsUpdateSecondPhase(ref context, ref baseContext, true, true, true, true, true);

            DetectTransitions(ref context, ref baseContext, in aspect);
        }

        public void OnStateVariableUpdate(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            var deltaTime = baseContext.Time.DeltaTime;
            ref var character = ref aspect.Character.ValueRW;
            ref var characterControl = ref aspect.CharacterControl.ValueRW;
            ref var characterRotation = ref aspect.KinematicAspect.LocalTransform.ValueRW.Rotation;
            var customGravity = aspect.CustomGravity.ValueRO;

            if (math.lengthsq(characterControl.MoveVector) > 0f)
            {
                CharacterControlUtilities.SlerpRotationTowardsDirectionAroundUp(ref characterRotation,
                    deltaTime,
                    math.normalizesafe(characterControl.MoveVector),
                    MathUtilities.GetUpFromRotation(characterRotation),
                    character.AirRotationSharpness);
            }
            CharacterControlUtilities.SlerpCharacterUpTowardsDirection(ref characterRotation,
                deltaTime,
                math.normalizesafe(-customGravity.Gravity),
                character.UpOrientationAdaptationSharpness);
        }

        public void GetCameraParameters(in CharacterData character, out Entity cameraTarget, out bool calculateUpFromGravity)
        {
            cameraTarget = character.DefaultCameraTargetEntity;
            calculateUpFromGravity = true;
        }

        public void GetMoveVectorFromPlayerInput(in PlayerInputs inputs, quaternion cameraRotation, out float3 moveVector)
        {
            moveVector = math.mul(cameraRotation, math.right()) * inputs.Move.x +
                         math.mul(cameraRotation, math.forward()) * inputs.Move.y;
        }

        public bool DetectTransitions(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            ref var characterBody = ref aspect.KinematicAspect.CharacterBody.ValueRW;
            ref var character = ref aspect.Character.ValueRW;
            ref var characterControl = ref aspect.CharacterControl.ValueRW;
            ref var stateMachine = ref aspect.StateMachine.ValueRW;

            if (characterBody.IsGrounded)
            {
                stateMachine.TransitionToState(CharacterState.GroundMove, ref context, ref baseContext, in aspect);
                return true;
            }

            return aspect.DetectGlobalTransitions(ref context, ref baseContext);
        }
    }
}
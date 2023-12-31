﻿using Character.Kinematic;
using Player;
using Unity.Entities;
using Unity.Mathematics;
using Utilities;

namespace Character.States
{
    public struct CrouchedState : ICharacterState
    {
        public void OnStateEnter(CharacterState previousState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            ref var character = ref aspect.Character.ValueRW;

            aspect.SetCapsuleGeometry(character.CrouchingGeometry.ToCapsuleGeometry());
        }

        public void OnStateExit(CharacterState nextState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            ref var character = ref aspect.Character.ValueRW;

            character.IsOnStickySurface = false;
        }

        public void OnStatePhysicsUpdate(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            var deltaTime = baseContext.Time.DeltaTime;
            ref var characterBody = ref aspect.KinematicAspect.CharacterBody.ValueRW;
            ref var character = ref aspect.Character.ValueRW;
            ref var characterControl = ref aspect.CharacterControl.ValueRW;

            aspect.HandlePhysicsUpdateFirstPhase(ref context, ref baseContext, true, true);

            // Rotate move input and velocity to take into account parent rotation
            if(characterBody.ParentEntity != Entity.Null)
            {
                characterControl.MoveVector = math.rotate(characterBody.RotationFromParent, characterControl.MoveVector);
                characterBody.RelativeVelocity = math.rotate(characterBody.RotationFromParent, characterBody.RelativeVelocity);
            }

            var chosenMaxSpeed = character.CrouchedMaxSpeed;

            var chosenSharpness = character.CrouchedMovementSharpness;
            if (context.CharacterFrictionModifierLookup.TryGetComponent(characterBody.GroundHit.Entity, out var frictionModifier))
            {
                chosenSharpness *= frictionModifier.Friction;
            }

            var moveVectorOnPlane = math.normalizesafe(MathUtilities.ProjectOnPlane(characterControl.MoveVector, characterBody.GroundingUp)) * math.length(characterControl.MoveVector);
            var targetVelocity = moveVectorOnPlane * chosenMaxSpeed;
            CharacterControlUtilities.StandardGroundMoveInterpolated(ref characterBody.RelativeVelocity, targetVelocity, chosenSharpness, deltaTime, characterBody.GroundingUp, characterBody.GroundHit.Normal);

            aspect.HandlePhysicsUpdateSecondPhase(ref context, ref baseContext, true, true, true, true, true);

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
            ref var characterRotation = ref aspect.KinematicAspect.LocalTransform.ValueRW.Rotation;
            var customGravity = aspect.CustomGravity.ValueRO;
        
            if (math.lengthsq(characterControl.MoveVector) > 0f)
            {
                CharacterControlUtilities.SlerpRotationTowardsDirectionAroundUp(ref characterRotation,
                    deltaTime,
                    math.normalizesafe(characterControl.MoveVector),
                    MathUtilities.GetUpFromRotation(characterRotation),
                    character.CrouchedRotationSharpness);
            }
        
            character.IsOnStickySurface = PhysicsUtilities.HasPhysicsTag(in baseContext.PhysicsWorld,
                characterBody.GroundHit.RigidBodyIndex, character.StickySurfaceTag);

            if (character.IsOnStickySurface)
            {
                CharacterControlUtilities.SlerpCharacterUpTowardsDirection(ref characterRotation,
                    deltaTime,
                    characterBody.GroundHit.Normal,
                    character.UpOrientationAdaptationSharpness);
            }
            else 
            {
                CharacterControlUtilities.SlerpCharacterUpTowardsDirection(ref characterRotation,
                    deltaTime,
                    math.normalizesafe(-customGravity.Gravity),
                    character.UpOrientationAdaptationSharpness);
            }
        }

        public void GetCameraParameters(in CharacterData character, out Entity cameraTarget, out bool calculateUpFromGravity)
        {
            cameraTarget = character.CrouchingCameraTargetEntity;
            calculateUpFromGravity = !character.IsOnStickySurface;
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
            ref var characterControl = ref aspect.CharacterControl.ValueRW;
            ref var stateMachine = ref aspect.StateMachine.ValueRW;
        
            if (characterControl.CrouchPressed)
            {
                if (aspect.CanStandUp(ref context, ref baseContext))
                {
                    if (characterBody.IsGrounded)
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
            }

            if (!characterBody.IsGrounded)
            {
                stateMachine.TransitionToState(CharacterState.AirMove, ref context, ref baseContext, in aspect);
                return true;
            }

            return aspect.DetectGlobalTransitions(ref context, ref baseContext);
        }
    }
}
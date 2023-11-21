using System;
using Character.Kinematic;
using Character.States;
using Player;
using Unity.Entities;
using Unity.Mathematics;

namespace Character
{
    [Serializable]
    public struct CharacterStateMachine : IComponentData
    {
        public CharacterState CurrentState;
        public CharacterState PreviousState;

        public GroundMoveState GroundMoveState;
        public CrouchedState CrouchedState;
        public AirMoveState AirMoveState;
        public SwimmingState SwimmingState;
        public GodModeState GodModeState;

        public void TransitionToState(CharacterState newState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            PreviousState = CurrentState;
            CurrentState = newState;

            OnStateExit(PreviousState, CurrentState, ref context, ref baseContext, in aspect);
            OnStateEnter(CurrentState, PreviousState, ref context, ref baseContext, in aspect);
        }

        public void OnStateEnter(CharacterState state,
            CharacterState previousState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            switch (state)
            {
                case CharacterState.GroundMove:
                    GroundMoveState.OnStateEnter(previousState, ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.Crouched:
                    CrouchedState.OnStateEnter(previousState, ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.AirMove:
                    AirMoveState.OnStateEnter(previousState, ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.Swimming:
                    SwimmingState.OnStateEnter(previousState, ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.GodMode:
                    GodModeState.OnStateEnter(previousState, ref context, ref baseContext, in aspect);
                    break;
            }
        }

        public void OnStateExit(CharacterState state,
            CharacterState newState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            switch (state)
            {
                case CharacterState.GroundMove:
                    GroundMoveState.OnStateExit(newState, ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.Crouched:
                    CrouchedState.OnStateExit(newState, ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.AirMove:
                    AirMoveState.OnStateExit(newState, ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.Swimming:
                    SwimmingState.OnStateExit(newState, ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.GodMode:
                    GodModeState.OnStateExit(newState, ref context, ref baseContext, in aspect);
                    break;
            }
        }
        
        public void OnStatePhysicsUpdate(CharacterState state,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            switch (state)
            {
                case CharacterState.GroundMove:
                    GroundMoveState.OnStatePhysicsUpdate(ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.Crouched:
                    CrouchedState.OnStatePhysicsUpdate(ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.AirMove:
                    AirMoveState.OnStatePhysicsUpdate(ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.Swimming:
                    SwimmingState.OnStatePhysicsUpdate(ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.GodMode:
                    GodModeState.OnStatePhysicsUpdate(ref context, ref baseContext, in aspect);
                    break;
            }
        }

        public void OnStateVariableUpdate(CharacterState state,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            switch (state)
            {
                case CharacterState.GroundMove:
                    GroundMoveState.OnStateVariableUpdate(ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.Crouched:
                    CrouchedState.OnStateVariableUpdate(ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.AirMove:
                    AirMoveState.OnStateVariableUpdate(ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.Swimming:
                    SwimmingState.OnStateVariableUpdate(ref context, ref baseContext, in aspect);
                    break;
                case CharacterState.GodMode:
                    GodModeState.OnStateVariableUpdate(ref context, ref baseContext, in aspect);
                    break;
            }
        }

        public void GetCameraParameters(CharacterState state,
            in CharacterData character,
            out Entity cameraTarget,
            out bool calculateUpFromGravity)
        {
            cameraTarget = default;
            calculateUpFromGravity = default;

            switch (state)
            {
                case CharacterState.GroundMove:
                    GroundMoveState.GetCameraParameters(in character, out cameraTarget, out calculateUpFromGravity);
                    break;
                case CharacterState.Crouched:
                    CrouchedState.GetCameraParameters(in character, out cameraTarget, out calculateUpFromGravity);
                    break;
                case CharacterState.AirMove:
                    AirMoveState.GetCameraParameters(in character, out cameraTarget, out calculateUpFromGravity);
                    break;
                case CharacterState.Swimming:
                    SwimmingState.GetCameraParameters(in character, out cameraTarget, out calculateUpFromGravity);
                    break;
                case CharacterState.GodMode:
                    GodModeState.GetCameraParameters(in character, out cameraTarget, out calculateUpFromGravity);
                    break;
            }
        }

        public void GetMoveVectorFromPlayerInput(CharacterState state,
            in PlayerInputs inputs,
            quaternion cameraRotation,
            out float3 moveVector)
        {
            moveVector = default;

            switch (state)
            {
                case CharacterState.GroundMove:
                    GroundMoveState.GetMoveVectorFromPlayerInput(in inputs, cameraRotation, out moveVector);
                    break;
                case CharacterState.Crouched:
                    CrouchedState.GetMoveVectorFromPlayerInput(in inputs, cameraRotation, out moveVector);
                    break;
                case CharacterState.AirMove:
                    AirMoveState.GetMoveVectorFromPlayerInput(in inputs, cameraRotation, out moveVector);
                    break;
                case CharacterState.Swimming:
                    SwimmingState.GetMoveVectorFromPlayerInput(in inputs, cameraRotation, out moveVector);
                    break;
                case CharacterState.GodMode:
                    GodModeState.GetMoveVectorFromPlayerInput(in inputs, cameraRotation, out moveVector);
                    break;
            }
        }
    }
}
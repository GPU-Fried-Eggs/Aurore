using Character.Kinematic;
using Player;
using Unity.Entities;
using Unity.Mathematics;
using Utilities;

namespace Character.States
{
    public struct GodModeState : ICharacterState
    {
        public void OnStateEnter(CharacterState previousState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            ref var characterBody = ref aspect.KinematicAspect.CharacterBody.ValueRW;
            ref var characterData = ref aspect.KinematicAspect.CharacterData.ValueRW;
            ref var characterCollider = ref aspect.KinematicAspect.PhysicsCollider.ValueRW;
            ref var character = ref aspect.Character.ValueRW;
        
            aspect.SetCapsuleGeometry(character.StandingGeometry.ToCapsuleGeometry());
        
            KinematicCharacterUtilities.SetCollisionDetectionActive(false, ref characterData, ref characterCollider);
            characterBody.IsGrounded = false;
        }

        public void OnStateExit(CharacterState nextState,
            ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            ref var characterData = ref aspect.KinematicAspect.CharacterData.ValueRW;
            ref var characterCollider = ref aspect.KinematicAspect.PhysicsCollider.ValueRW;
        
            KinematicCharacterUtilities.SetCollisionDetectionActive(true, ref characterData, ref characterCollider);
        }

        public void OnStatePhysicsUpdate(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            var deltaTime = baseContext.Time.DeltaTime;
            ref var characterBody = ref aspect.KinematicAspect.CharacterBody.ValueRW;
            ref var character = ref aspect.Character.ValueRW;
            ref var characterControl = ref aspect.CharacterControl.ValueRW;
            ref var characterPosition = ref aspect.KinematicAspect.LocalTransform.ValueRW.Position;
        
            aspect.KinematicAspect.InitializeUpdate(in aspect, ref context, ref baseContext, ref characterBody, deltaTime);
        
            // Movement
            var targetVelocity = characterControl.MoveVector * character.FlyingMaxSpeed;
            CharacterControlUtilities.InterpolateVelocityTowardsTarget(ref characterBody.RelativeVelocity, targetVelocity, deltaTime, character.FlyingMovementSharpness);
            characterPosition += characterBody.RelativeVelocity * deltaTime;

            aspect.DetectGlobalTransitions(ref context, ref baseContext);
        }

        public void OnStateVariableUpdate(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            ref var characterRotation = ref aspect.KinematicAspect.LocalTransform.ValueRW.Rotation;
        
            characterRotation = quaternion.identity;
        }

        public void GetCameraParameters(in CharacterData character, out Entity cameraTarget, out bool calculateUpFromGravity)
        {
            cameraTarget = character.DefaultCameraTargetEntity;
            calculateUpFromGravity = false;
        }

        public void GetMoveVectorFromPlayerInput(in PlayerInputs inputs, quaternion cameraRotation, out float3 moveVector)
        {
            moveVector = math.mul(cameraRotation, math.right()) * inputs.Move.x +
                         math.mul(cameraRotation, math.forward()) * inputs.Move.y;
            var verticalInput = (inputs.JumpHeld ? 1f : 0f);
            moveVector = MathUtilities.ClampToMaxLength(moveVector + (math.mul(cameraRotation, math.up()) * verticalInput), 1f);
        }
    }
}
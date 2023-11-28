using System;
using Character.Kinematic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Character
{
    [Serializable]
    public struct CharacterAnimation : IComponentData
    {
        [HideInInspector] public int ClipIndexParameterHash;

        [HideInInspector] public int IdleClip;
        [HideInInspector] public int RunClip;
        [HideInInspector] public int SprintClip;
        [HideInInspector] public int InAirClip;
        [HideInInspector] public int CrouchIdleClip;
        [HideInInspector] public int CrouchMoveClip;
        [HideInInspector] public int ClimbingMoveClip;
        [HideInInspector] public int SwimmingIdleClip;
        [HideInInspector] public int SwimmingMoveClip;

        [HideInInspector] public CharacterState LastAnimationCharacterState;
    }

    public static class CharacterAnimationHandler
    {
        public static void UpdateAnimation(this Animator animator,
            ref CharacterAnimation characterAnimation,
            in KinematicCharacterBody characterBody,
            in CharacterData characterComponent,
            in CharacterStateMachine characterStateMachine,
            in CharacterControl characterControl,
            in LocalTransform localTransform)
        {
            var velocityMagnitude = math.length(characterBody.RelativeVelocity);

            switch (characterStateMachine.CurrentState)
            {
                case CharacterState.GroundMove:
                {
                    if (math.length(characterControl.MoveVector) < 0.01f)
                    {
                        animator.speed = 1f;
                        animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.IdleClip);
                    }
                    else
                    {
                        if (characterComponent.IsSprinting)
                        {
                            var velocityRatio = velocityMagnitude / characterComponent.GroundSprintMaxSpeed;
                            animator.speed = velocityRatio;
                            animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.SprintClip);
                        }
                        else
                        {
                            var velocityRatio = velocityMagnitude / characterComponent.GroundRunMaxSpeed;
                            animator.speed = velocityRatio;
                            animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.RunClip);
                        }
                    }

                    break;
                }
                case CharacterState.Crouched:
                {
                    if (math.length(characterControl.MoveVector) < 0.01f)
                    {
                        animator.speed = 1f;
                        animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.CrouchIdleClip);
                    }
                    else
                    {
                        var velocityRatio = velocityMagnitude / characterComponent.CrouchedMaxSpeed;
                        animator.speed = velocityRatio;
                        animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.CrouchMoveClip);
                    }

                    break;
                }
                case CharacterState.AirMove:
                {
                    animator.speed = 1f;
                    animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.InAirClip);

                    break;
                }
                case CharacterState.Swimming:
                {
                    var velocityRatio = velocityMagnitude / characterComponent.SwimmingMaxSpeed;
                    if (velocityRatio < 0.1f)
                    {
                        animator.speed = 1f;
                        animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.SwimmingIdleClip);
                    }
                    else
                    {
                        animator.speed = velocityRatio;
                        animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.SwimmingMoveClip);
                    }

                    break;
                }
                case CharacterState.Climbing:
                {
                    var velocityRatio = velocityMagnitude / characterComponent.ClimbingSpeed;
                    animator.speed = velocityRatio;
                    animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.ClimbingMoveClip);
                    break;
                }
                case CharacterState.GodMode:
                {
                    animator.speed = 1f;
                    animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.IdleClip);

                    break;
                }
            }

            characterAnimation.LastAnimationCharacterState = characterStateMachine.CurrentState;
        }
    }
}
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
        [HideInInspector] public int SwimmingIdleClip;
        [HideInInspector] public int SwimmingMoveClip;

        [HideInInspector] public CharacterState LastAnimationCharacterState;
    }

    public static class CharacterAnimationHandler
    {
        /// <summary>
        /// Updates the animation state of a character based on its current movement and state
        /// </summary>
        /// <param name="animator"> The animator component responsible for character animation </param>
        /// <param name="characterAnimation"> The reference to character's animation settings and state </param>
        /// <param name="characterBody"> The kinematic body of the character providing velocity and movement data </param>
        /// <param name="characterData"> The character's properties and attributes </param>
        /// <param name="characterStateMachine"> The current state machine of the character defining its behavior state </param>
        /// <param name="characterControl"> The character control input and movement vector data </param>
        /// <param name="localTransform"> The local transform of the character for position and orientation </param>
        public static void UpdateAnimation(this Animator animator,
            ref CharacterAnimation characterAnimation,
            in KinematicCharacterBody characterBody,
            in CharacterData characterData,
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
                        if (characterData.IsSprinting)
                        {
                            var velocityRatio = velocityMagnitude / characterData.GroundSprintMaxSpeed;
                            animator.speed = velocityRatio;
                            animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.SprintClip);
                        }
                        else
                        {
                            var velocityRatio = velocityMagnitude / characterData.GroundRunMaxSpeed;
                            animator.speed = velocityRatio;
                            animator.SetInteger(characterAnimation.ClipIndexParameterHash, characterAnimation.RunClip);
                        }
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
                    var velocityRatio = velocityMagnitude / characterData.SwimmingMaxSpeed;
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
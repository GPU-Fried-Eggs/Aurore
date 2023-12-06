using Character.Kinematic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Character
{
    [UpdateBefore(typeof(AnimationSystemGroup))]
    public partial struct CharacterAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach 
                (var (characterAnimation, characterBody, characterTransform, characterData,
                     characterStateMachine, characterControl, entity) in SystemAPI
                     .Query<
                         RefRW<CharacterAnimation>,
                         KinematicCharacterBody,
                         LocalTransform,
                         CharacterData,
                         CharacterStateMachine,
                         CharacterControl>()
                     .WithEntityAccess())
            {
                foreach (var (animatorParamsAspect, animatorTransform) in SystemAPI
                             .Query<AnimatorParametersAspect, RefRW<LocalTransform>>())
                {
                    var meshRootLTW = SystemAPI.GetComponent<LocalToWorld>(characterData.MeshRootEntity);

                    animatorTransform.ValueRW.Position = meshRootLTW.Position;
                    animatorTransform.ValueRW.Rotation = meshRootLTW.Rotation;

                    var velocityMagnitude = math.length(characterBody.RelativeVelocity);

                    switch (characterStateMachine.CurrentState)
                    {
                        case CharacterState.GroundMove:
                        {
                            if (math.length(characterControl.MoveVector) < 0.01f)
                            {
                                animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.SpeedMultiplierParameter, 1f);
                                animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.ClipIndexParameter, characterAnimation.ValueRO.IdleClip);
                            }
                            else
                            {
                                if (characterData.IsSprinting)
                                {
                                    var velocityRatio = velocityMagnitude / characterData.GroundSprintMaxSpeed;
                                    animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.SpeedMultiplierParameter, velocityRatio);
                                    animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.ClipIndexParameter, characterAnimation.ValueRO.SprintClip);
                                }
                                else
                                {
                                    var velocityRatio = velocityMagnitude / characterData.GroundRunMaxSpeed;
                                    animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.SpeedMultiplierParameter, velocityRatio);
                                    animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.ClipIndexParameter, characterAnimation.ValueRO.RunClip);
                                }
                            }

                            break;
                        }
                        case CharacterState.Crouched:
                        {
                            if (math.length(characterControl.MoveVector) < 0.01f)
                            {
                                animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.SpeedMultiplierParameter, 1f);
                                animatorParamsAspect.SetParameterValue(characterAnimation.ValueRW.ClipIndexParameter, characterAnimation.ValueRW.CrouchIdleClip);
                            }
                            else
                            {
                                var velocityRatio = velocityMagnitude / characterData.CrouchedMaxSpeed;
                                animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.SpeedMultiplierParameter, velocityRatio);
                                animatorParamsAspect.SetParameterValue(characterAnimation.ValueRW.ClipIndexParameter, characterAnimation.ValueRW.CrouchMoveClip);
                            }

                            break;
                        }
                        case CharacterState.AirMove:
                        {
                            animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.SpeedMultiplierParameter, 1f);
                            animatorParamsAspect.SetParameterValue(characterAnimation.ValueRW.ClipIndexParameter, characterAnimation.ValueRW.InAirClip);

                            break;
                        }
                        case CharacterState.Swimming:
                        {
                            var velocityRatio = velocityMagnitude / characterData.SwimmingMaxSpeed;
                            if (velocityRatio < 0.1f)
                            {
                                animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.SpeedMultiplierParameter, 1f);
                                animatorParamsAspect.SetParameterValue(characterAnimation.ValueRW.ClipIndexParameter, characterAnimation.ValueRW.SwimmingIdleClip);
                            }
                            else
                            {
                                animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.SpeedMultiplierParameter, velocityRatio);
                                animatorParamsAspect.SetParameterValue(characterAnimation.ValueRW.ClipIndexParameter, characterAnimation.ValueRW.SwimmingMoveClip);
                            }

                            break;
                        }
                        case CharacterState.Climbing:
                        {
                            var velocityRatio = velocityMagnitude / characterData.ClimbingSpeed;
                            animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.SpeedMultiplierParameter, velocityRatio);
                            animatorParamsAspect.SetParameterValue(characterAnimation.ValueRW.ClipIndexParameter, characterAnimation.ValueRW.ClimbingMoveClip);

                            break;
                        }
                        case CharacterState.GodMode:
                        {
                            animatorParamsAspect.SetParameterValue(characterAnimation.ValueRO.SpeedMultiplierParameter, 1f);
                            animatorParamsAspect.SetParameterValue(characterAnimation.ValueRW.ClipIndexParameter, characterAnimation.ValueRW.IdleClip);

                            break;
                        }
                    }

                    characterAnimation.ValueRW.LastAnimationCharacterState = characterStateMachine.CurrentState;
                }
            }
        }
    }
}
using Character.Kinematic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;
using Unity.Transforms;
using Utilities;

namespace Interactive
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [UpdateBefore(typeof(KinematicCharacterPhysicsUpdateGroup))]
    [BurstCompile]
    public partial struct JumpPadSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new JumpPadJob
            {
                CharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(false),
            };
            job.Schedule();
        }

        [BurstCompile]
        public partial struct JumpPadJob : IJobEntity
        {
            public ComponentLookup<KinematicCharacterBody> CharacterBodyLookup;

            private void Execute(Entity entity,
                in LocalTransform localTransform,
                in JumpPad jumpPad,
                in DynamicBuffer<StatefulTriggerEvent> triggerEventsBuffer)
            {
                for (var i = 0; i < triggerEventsBuffer.Length; i++)
                {
                    var triggerEvent = triggerEventsBuffer[i];
                    var otherEntity = triggerEvent.GetOtherEntity(entity);

                    // If a character has entered the trigger, add jumppad power to it
                    if (triggerEvent.State == StatefulEventState.Enter &&
                        CharacterBodyLookup.TryGetComponent(otherEntity, out var characterBody))
                    {
                        var jumpVelocity = MathUtilities.GetForwardFromRotation(localTransform.Rotation) * jumpPad.JumpPower;
                        characterBody.RelativeVelocity = jumpVelocity;

                        // Unground the character
                        if (characterBody.IsGrounded && math.dot(math.normalizesafe(jumpVelocity), characterBody.GroundHit.Normal) > jumpPad.UngroundingDotThreshold)
                        {
                            characterBody.IsGrounded = false;
                        }

                        CharacterBodyLookup[otherEntity] = characterBody;
                    }
                }
            }
        }
    }
}
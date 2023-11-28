using Character.Kinematic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Interactive
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [BurstCompile]
    public partial struct TeleporterSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new TeleporterJob
            {
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
                CharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(true),
                CharacterInterpolationLookup = SystemAPI.GetComponentLookup<CharacterInterpolation>(false),
            };
            job.Schedule();
        }

        [BurstCompile]
        public partial struct TeleporterJob : IJobEntity
        {
            public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<KinematicCharacterBody> CharacterBodyLookup;
            public ComponentLookup<CharacterInterpolation> CharacterInterpolationLookup;

            private void Execute(Entity entity, in Teleporter teleporter, in DynamicBuffer<StatefulTriggerEvent> triggerEventsBuffer)
            {
                // Only teleport if there is a destination
                if (teleporter.DestinationEntity == Entity.Null) return;

                for (var i = 0; i < triggerEventsBuffer.Length; i++)
                {
                    var triggerEvent = triggerEventsBuffer[i];
                    var otherEntity = triggerEvent.GetOtherEntity(entity);

                    // If a character has entered the trigger, move its translation to the destination
                    if (triggerEvent.State == StatefulEventState.Enter && CharacterBodyLookup.HasComponent(otherEntity))
                    {
                        var localTransform = LocalTransformLookup[otherEntity];
                        localTransform.Position = LocalTransformLookup[teleporter.DestinationEntity].Position;
                        localTransform.Rotation = LocalTransformLookup[teleporter.DestinationEntity].Rotation;
                        LocalTransformLookup[otherEntity] = localTransform;

                        // Bypass interpolation
                        if (CharacterInterpolationLookup.HasComponent(otherEntity))
                        {
                            var interpolation = CharacterInterpolationLookup[otherEntity];
                            interpolation.SkipNextInterpolation();
                            CharacterInterpolationLookup[otherEntity] = interpolation;
                        }
                    }
                }
            }
        }
    }
}
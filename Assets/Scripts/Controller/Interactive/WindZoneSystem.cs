using Character;
using Character.Kinematic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Stateful;
using Unity.Physics.Systems;

namespace Interactive
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [UpdateAfter(typeof(KinematicCharacterPhysicsUpdateGroup))]
    [BurstCompile]
    public partial struct WindZoneSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new WindZoneJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                CharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(false),
                CharacterStateMachineLookup = SystemAPI.GetComponentLookup<CharacterStateMachine>(true),
                PhysicsVelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(false),
                PhysicsMassLookup = SystemAPI.GetComponentLookup<PhysicsMass>(true),
            };
            job.Schedule();
        }

        [BurstCompile]
        public partial struct WindZoneJob : IJobEntity
        {
            public float DeltaTime;
            public ComponentLookup<KinematicCharacterBody> CharacterBodyLookup;
            [ReadOnly] public ComponentLookup<CharacterStateMachine> CharacterStateMachineLookup;
            public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            [ReadOnly] public ComponentLookup<PhysicsMass> PhysicsMassLookup;

            private void Execute(Entity entity, in WindZone windZone, in DynamicBuffer<StatefulTriggerEvent> triggerEventsBuffer)
            {
                for (var i = 0; i < triggerEventsBuffer.Length; i++)
                {
                    var triggerEvent = triggerEventsBuffer[i];
                    var otherEntity = triggerEvent.GetOtherEntity(entity);

                    if (triggerEvent.State == StatefulEventState.Stay)
                    {
                        // Characters
                        if (CharacterBodyLookup.TryGetComponent(otherEntity, out var characterBody) && 
                            CharacterStateMachineLookup.TryGetComponent(otherEntity, out var characterStateMachine))
                        {
                            if (CharacterAspect.CanBeAffectedByWindZone(characterStateMachine.CurrentState))
                            {
                                characterBody.RelativeVelocity += windZone.WindForce * DeltaTime;
                                CharacterBodyLookup[otherEntity] = characterBody;
                            }
                        }
                        // Dynamic physics bodies
                        if (PhysicsVelocityLookup.TryGetComponent(otherEntity, out var physicsVelocity) && 
                            PhysicsMassLookup.TryGetComponent(otherEntity, out var physicsMass))
                        {
                            if (physicsMass.InverseMass > 0f)
                            {
                                physicsVelocity.Linear += windZone.WindForce * DeltaTime;
                                PhysicsVelocityLookup[otherEntity] = physicsVelocity;
                            }
                        }
                    }
                }
            }
        }
    }
}
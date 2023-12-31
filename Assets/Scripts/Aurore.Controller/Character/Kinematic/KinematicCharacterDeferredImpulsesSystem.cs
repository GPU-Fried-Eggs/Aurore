using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Utilities;

namespace Character.Kinematic
{
    [UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct KinematicCharacterDeferredImpulsesSystem : ISystem
    {
        private EntityQuery m_CharacterQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_CharacterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder().Build(ref state);
            state.RequireForUpdate(m_CharacterQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new KinematicCharacterDeferredImpulsesJob
            {
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
                PhysicsVelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(false),
                CharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(false),
                CharacterDataLookup = SystemAPI.GetComponentLookup<KinematicCharacterData>(true),
            };
            job.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct KinematicCharacterDeferredImpulsesJob : IJobEntity
        {
            public ComponentLookup<LocalTransform> TransformLookup;
            public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            public ComponentLookup<KinematicCharacterBody> CharacterBodyLookup;
            [ReadOnly] public ComponentLookup<KinematicCharacterData> CharacterDataLookup;

            private void Execute(in DynamicBuffer<KinematicCharacterDeferredImpulse> characterDeferredImpulsesBuffer)
            {
                for (var index = 0; index < characterDeferredImpulsesBuffer.Length; index++)
                {
                    var deferredImpulse = characterDeferredImpulsesBuffer[index];
                    // Impulse
                    var isImpulseOnCharacter = CharacterDataLookup.HasComponent(deferredImpulse.OnEntity);
                    if (isImpulseOnCharacter)
                    {
                        var hitCharacterProperties = CharacterDataLookup[deferredImpulse.OnEntity];
                        if (hitCharacterProperties.SimulateDynamicBody)
                        {
                            var hitCharacterBody = CharacterBodyLookup[deferredImpulse.OnEntity];
                            hitCharacterBody.RelativeVelocity += deferredImpulse.LinearVelocityChange;
                            CharacterBodyLookup[deferredImpulse.OnEntity] = hitCharacterBody;
                        }
                    }
                    else
                    {
                        if (PhysicsVelocityLookup.TryGetComponent(deferredImpulse.OnEntity, out var bodyPhysicsVelocity))
                        {
                            bodyPhysicsVelocity.Linear += deferredImpulse.LinearVelocityChange;
                            bodyPhysicsVelocity.Angular += deferredImpulse.AngularVelocityChange;

                            PhysicsVelocityLookup[deferredImpulse.OnEntity] = bodyPhysicsVelocity;
                        }
                    }

                    // Displacement
                    if (math.lengthsq(deferredImpulse.Displacement) > 0f)
                    {
                        if (TransformLookup.TryGetComponent(deferredImpulse.OnEntity, out var bodyTransform))
                        {
                            bodyTransform.Position += deferredImpulse.Displacement;
                            TransformLookup[deferredImpulse.OnEntity] = bodyTransform;
                        }
                    }
                }
            }
        }
    }
}

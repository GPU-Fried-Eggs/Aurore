using Character.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

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
                foreach (var deferredImpulse in characterDeferredImpulsesBuffer)
                {
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
                        var bodyPhysicsVelocity = PhysicsVelocityLookup[deferredImpulse.OnEntity];

                        bodyPhysicsVelocity.Linear += deferredImpulse.LinearVelocityChange;
                        bodyPhysicsVelocity.Angular += deferredImpulse.AngularVelocityChange;

                        PhysicsVelocityLookup[deferredImpulse.OnEntity] = bodyPhysicsVelocity;
                    }

                    // Displacement
                    if (math.lengthsq(deferredImpulse.Displacement) > 0f)
                    {
                        var bodyTransform = TransformLookup[deferredImpulse.OnEntity];
                        bodyTransform.Position += deferredImpulse.Displacement;
                        TransformLookup[deferredImpulse.OnEntity] = bodyTransform;
                    }
                }
            }
        }
    }
}
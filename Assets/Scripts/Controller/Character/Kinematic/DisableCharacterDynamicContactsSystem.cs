using Controller.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Controller.Character.Kinematic
{
    public struct DisableCharacterDynamicContacts : IComponentData { }

    [UpdateInGroup(typeof(PhysicsSimulationGroup))]
    [UpdateAfter(typeof(PhysicsCreateContactsGroup))]
    [UpdateBefore(typeof(PhysicsCreateJacobiansGroup))]
    [RequireMatchingQueriesForUpdate]
    public partial struct DisableCharacterDynamicContactsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Create singleton
            var singleton = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singleton, new DisableCharacterDynamicContacts());

            var characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder().Build(ref state);

            state.RequireForUpdate(characterQuery);
            state.RequireForUpdate<DisableCharacterDynamicContacts>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<SimulationSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingletonRW<PhysicsWorldSingleton>().ValueRW.PhysicsWorld;
            var simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();

            if (physicsWorld.Bodies.Length <= 0) return;

            var job = new DisableCharacterDynamicContactsJob
            {
                PhysicsWorld = physicsWorld,
                StoredCharacterDataLookup = SystemAPI.GetComponentLookup<KinematicCharacterStoredData>(true),
            };
            state.Dependency = job.Schedule(simulationSingleton, ref physicsWorld, state.Dependency);
        }

        [BurstCompile]
        public struct DisableCharacterDynamicContactsJob : IContactsJob
        {
            [ReadOnly] public PhysicsWorld PhysicsWorld;
            [ReadOnly] public ComponentLookup<KinematicCharacterStoredData> StoredCharacterDataLookup;

            public void Execute(ref ModifiableContactHeader manifold, ref ModifiableContactPoint contact)
            {
                // Both should be non-static
                if (manifold.BodyIndexA >= PhysicsWorld.NumDynamicBodies ||
                    manifold.BodyIndexB >= PhysicsWorld.NumDynamicBodies) return;

                var aIsKinematic = PhysicsWorld.MotionVelocities[manifold.BodyIndexA].IsKinematic;
                var bIsKinematic = PhysicsWorld.MotionVelocities[manifold.BodyIndexB].IsKinematic;

                // One should be kinematic and the other should be dynamic
                if (aIsKinematic == bIsKinematic) return;

                var kinematicEntity = aIsKinematic ? manifold.EntityA : manifold.EntityB;
                var dynamicBodyIndex = aIsKinematic ? manifold.BodyIndexB : manifold.BodyIndexA;
                var dynamicBodyColliderKey = aIsKinematic ? manifold.ColliderKeyB : manifold.ColliderKeyA;

                // Disable only if dynamic entity is collidable
                var dynamicBodyCollisionResponse = PhysicsWorld.Bodies[dynamicBodyIndex].Collider.Value.GetCollisionResponse(dynamicBodyColliderKey);
                if (dynamicBodyCollisionResponse is CollisionResponsePolicy.Collide or CollisionResponsePolicy.CollideRaiseCollisionEvents)
                {
                    // Disable only if kinematic entity is character and is simulated dynamic
                    if (StoredCharacterDataLookup.TryGetComponent(kinematicEntity, out var characterData) && characterData.SimulateDynamicBody)
                        manifold.JacobianFlags |= JacobianFlags.Disabled;
                }
            }
        }
    }
}

using Character;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using Utilities;

namespace Physics
{
    [UpdateInGroup(typeof(SimulationSystemGroup))] // update in variable update because the camera can use gravity to adjust its up direction
    [UpdateBefore(typeof(CharacterVariableUpdateSystem))]
    public partial class GravityZonesSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Update transforms so we have the proper interpolated position of our entities to calculate spherical gravities from
            // (without this, we'd see jitter on the planet)
            World.GetOrCreateSystem<TransformSystemGroup>().Update(World.Unmanaged);

            var resetGravitiesJob = new ResetGravitiesJob();
            resetGravitiesJob.Schedule();

            if (SystemAPI.TryGetSingleton(out GlobalGravityZone globalGravityZone))
            {
                var globalGravityJob = new GlobalGravityJob
                {
                    GlobalGravityZone = globalGravityZone,
                };
                globalGravityJob.Schedule();
            }

            var applyGravityJob = new ApplyGravityJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            };
            applyGravityJob.Schedule();
        }

        [BurstCompile]
        public partial struct ResetGravitiesJob : IJobEntity
        {
            private void Execute(Entity entity, ref CustomGravity customGravity)
            {
                customGravity.LastZoneEntity = customGravity.CurrentZoneEntity;
                customGravity.TouchedByNonGlobalGravity = false;
            }
        }

        [BurstCompile]
        public partial struct GlobalGravityJob : IJobEntity
        {
            public GlobalGravityZone GlobalGravityZone;

            private void Execute(Entity entity, ref CustomGravity customGravity)
            {
                if (!customGravity.TouchedByNonGlobalGravity)
                {
                    customGravity.Gravity = GlobalGravityZone.Gravity * customGravity.GravityMultiplier;
                    customGravity.CurrentZoneEntity = Entity.Null;
                }
            }
        }

        [BurstCompile]
        public partial struct ApplyGravityJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(Entity entity,
                ref PhysicsVelocity physicsVelocity,
                in PhysicsMass physicsMass,
                in CustomGravity customGravity)
            {
                if (physicsMass.InverseMass > 0f)
                {
                    CharacterControlUtilities.AccelerateVelocity(ref physicsVelocity.Linear, customGravity.Gravity,
                        DeltaTime);
                }
            }
        }
    }
}
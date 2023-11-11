using Controller.Character.Kinematic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Controller.Physics
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [UpdateBefore(typeof(KinematicCharacterPhysicsUpdateGroup))]
    [BurstCompile]
    public partial struct TrackedTransformFixedSimulationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TrackedTransform>(); 
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new TrackedTransformFixedSimulationJob();
            job.ScheduleParallel();
        }

        [WithAll(typeof(Simulate))]
        public partial struct TrackedTransformFixedSimulationJob : IJobEntity
        {
            private void Execute(ref TrackedTransform trackedTransform, in LocalTransform transform)
            {
                trackedTransform.PreviousFixedRateTransform = trackedTransform.CurrentFixedRateTransform;
                trackedTransform.CurrentFixedRateTransform = new RigidTransform(transform.Rotation, transform.Position);
            }
        }
    }
}
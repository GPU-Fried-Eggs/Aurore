using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Character.Kinematic
{
    [UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct KinematicCharacterDataStoreSystem : ISystem
    {
        private EntityQuery m_StoredCharacterQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_StoredCharacterQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<KinematicCharacterStoredData, KinematicCharacterData>()
                .Build(ref state);

            state.RequireForUpdate(m_StoredCharacterQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new KinematicCharacterBodyDataStoreJob();
            job.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct KinematicCharacterBodyDataStoreJob : IJobEntity
        {
            private void Execute(ref KinematicCharacterStoredData storedData,
                in KinematicCharacterData characterData,
                in KinematicCharacterBody characterBody)
            {
                storedData.SimulateDynamicBody = characterData.SimulateDynamicBody;
                storedData.Mass = characterData.Mass;
                storedData.RelativeVelocity = characterBody.RelativeVelocity;
                storedData.ParentVelocity = characterBody.ParentVelocity;
            }
        }
    }
}

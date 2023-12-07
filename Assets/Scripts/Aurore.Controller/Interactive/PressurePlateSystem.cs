using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Interactive
{
    public partial struct PressurePlateSystem : ISystem
    {
        private Functions<PressurePlateHandleData> m_InjectFunctions;

        public void OnCreate(ref SystemState state)
        {
            m_InjectFunctions = new FunctionsBuilder<PressurePlateHandleData>(Allocator.Temp)
                .ReflectAll(ref state)
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
            m_InjectFunctions.OnDestroy(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_InjectFunctions.Update(ref state);

            var job = new PressurePlateJob
            {
                InjectFunctions = m_InjectFunctions,
                ECB = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged),
            };
            job.Schedule();
        }

        [BurstCompile]
        private partial struct PressurePlateJob : IJobEntity
        {
            public Functions<PressurePlateHandleData> InjectFunctions;

            public EntityCommandBuffer ECB;

            private void Execute(Entity entity, in PressurePlate plate)
            {
                var data = new PressurePlateHandleData
                {
                    Activated = plate.Activated,
                    EntityCommandBuffer = ECB
                };

                for (var i = 0; i < InjectFunctions.Length; i++)
                {
                    InjectFunctions.Execute(i, ref data);
                }
            }
        }
    }
}
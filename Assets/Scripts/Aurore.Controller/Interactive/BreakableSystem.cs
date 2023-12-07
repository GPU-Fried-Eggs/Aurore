using Unity.Burst;
using Unity.Entities;
using Unity.Physics.Stateful;
using UnityEngine;

namespace Interactive
{
    [BurstCompile]
    public partial struct BreakableSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new BreakableJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                ECB = SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
            };
            job.Schedule();
        }
        
        public partial struct BreakableJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer ECB;

            private void Execute(Entity entity,
                ref Breakable breakable,
                in DynamicBuffer<StatefulCollisionEvent> collisionEventsBuffer)
            {
                if (breakable.Triggered)
                {
                    breakable.TimeSinceTrigger += DeltaTime;
                    if (breakable.TimeSinceTrigger > breakable.DestructTime)
                    {
                        ECB.DestroyEntity(entity);
                    }
                }
                else
                {
                    for (var i = 0; i < collisionEventsBuffer.Length; i++)
                    {
                        var collisionEvent = collisionEventsBuffer[i];
                        if (collisionEvent.TryGetDetails(out var details))
                        {
                            if (details.EstimatedImpulse > breakable.Threshold)
                                breakable.Triggered = true;
                        }
                    }   
                }
            }
        }
    }
}
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics.Systems;

namespace Unity.Physics.Stateful
{
    /// <summary>
    /// This system converts stream of CollisionEvents to StatefulCollisionEvents that can be stored in a Dynamic Buffer.
    /// In order for this conversion, it is required to:
    ///    1) Use the 'Collide Raise Collision Events' option of the 'Collision Response' property on a <see cref="PhysicsShapeAuthoring"/> component, and
    ///    2) Add a <see cref="StatefulCollisionEventBufferAuthoring"/> component to that entity (and select if details should be calculated or not)
    /// or, if this is desired on a Character Controller:
    ///    1) Tick the 'Raise Collision Events' flag on the <see cref="CharacterControllerAuthoring"/> component.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    [BurstCompile]
    public partial struct StatefulCollisionEventBufferSystem : ISystem
    {
        private StatefulSimulationEventBuffers<StatefulCollisionEvent> m_StateFulEventBuffers;

        // Component that does nothing. Made in order to use a generic job. See OnUpdate() method for details.
        internal struct DummyExcludeComponent : IComponentData {};

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_StateFulEventBuffers = new StatefulSimulationEventBuffers<StatefulCollisionEvent>();
            m_StateFulEventBuffers.AllocateBuffers();

            state.RequireForUpdate<StatefulCollisionEvent>();
            state.RequireForUpdate<SimulationSingleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_StateFulEventBuffers.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ClearCollisionEventDynamicBufferJob()
                .ScheduleParallel(state.Dependency);

            m_StateFulEventBuffers.SwapBuffers();

            var currentEvents = m_StateFulEventBuffers.Current;
            var previousEvents = m_StateFulEventBuffers.Previous;

            state.Dependency = new CollectCollisionEventsWithDetails
            {
                CollisionEvents = currentEvents,
                PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                EventDetails = SystemAPI.GetComponentLookup<StatefulCollisionEventDetails>(true)
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);

            state.Dependency = new ConvertEventStreamToDynamicBufferJob<StatefulCollisionEvent, DummyExcludeComponent>
            {
                CurrentEvents = currentEvents,
                PreviousEvents = previousEvents,
                EventBuffers = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(),

                UseExcludeComponent = false,
                EventExcludeLookup = SystemAPI.GetComponentLookup<DummyExcludeComponent>(true)
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct ClearCollisionEventDynamicBufferJob : IJobEntity
        {
            private void Execute(ref DynamicBuffer<StatefulCollisionEvent> eventBuffer) => eventBuffer.Clear();
        }
    }
}

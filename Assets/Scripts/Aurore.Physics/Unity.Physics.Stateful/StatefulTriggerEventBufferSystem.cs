using Unity.Entities;
using Unity.Jobs;
using Unity.Physics.Systems;
using Unity.Burst;

namespace Unity.Physics.Stateful
{
    /// <summary>
    /// This system converts stream of TriggerEvents to StatefulTriggerEvents that can be stored in a Dynamic Buffer.
    /// In order for this conversion, it is required to:
    ///    1) Use the 'Raise Trigger Events' option of the 'Collision Response' property on a <see cref="PhysicsShapeAuthoring"/> component, and
    ///    2) Add a <see cref="StatefulTriggerEventBufferAuthoring"/> component to that entity
    /// or, if this is desired on a Character Controller:
    ///    1) Tick the 'Raise Trigger Events' flag on the <see cref="CharacterControllerAuthoring"/> component.
    ///       Note: the Character Controller will not become a trigger, it will raise events when overlapping with one
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    [BurstCompile]
    public partial struct StatefulTriggerEventBufferSystem : ISystem
    {
        private StatefulSimulationEventBuffers<StatefulTriggerEvent> m_StateFulEventBuffers;
        private EntityQuery m_TriggerEventQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_StateFulEventBuffers = new StatefulSimulationEventBuffers<StatefulTriggerEvent>();
            m_StateFulEventBuffers.AllocateBuffers();

            m_TriggerEventQuery = SystemAPI.QueryBuilder()
                .WithAllRW<StatefulTriggerEvent>()
                .WithNone<StatefulTriggerEventExclude>().Build();
            state.RequireForUpdate(m_TriggerEventQuery);

            state.RequireForUpdate<SimulationSingleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_StateFulEventBuffers.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ClearTriggerEventDynamicBufferJob()
                .ScheduleParallel(m_TriggerEventQuery, state.Dependency);

            m_StateFulEventBuffers.SwapBuffers();

            var currentEvents = m_StateFulEventBuffers.Current;
            var previousEvents = m_StateFulEventBuffers.Previous;

            state.Dependency = new CollectTriggerEvents
            {
                TriggerEvents = currentEvents
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);

            state.Dependency = new ConvertEventStreamToDynamicBufferJob<StatefulTriggerEvent, StatefulTriggerEventExclude>
            {
                CurrentEvents = currentEvents,
                PreviousEvents = previousEvents,
                EventBuffers = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(),

                UseExcludeComponent = true,
                EventExcludeLookup = SystemAPI.GetComponentLookup<StatefulTriggerEventExclude>(true)
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        public partial struct ClearTriggerEventDynamicBufferJob : IJobEntity
        {
            private void Execute(ref DynamicBuffer<StatefulTriggerEvent> eventBuffer) => eventBuffer.Clear();
        }
    }
}

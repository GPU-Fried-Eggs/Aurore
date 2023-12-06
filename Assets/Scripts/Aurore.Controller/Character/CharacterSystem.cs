using Character.Kinematic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using Utilities;

namespace Character
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct CharacterInitializationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);
            var linkedEntitiesLookup = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);

            foreach (var (character, stateMachine, entity) in SystemAPI
                         .Query<RefRW<CharacterData>, RefRW<CharacterStateMachine>>()
                         .WithNone<CharacterInitialized>()
                         .WithEntityAccess())
            {
                // Make sure the transform system has done a pass on it first
                if (linkedEntitiesLookup.HasBuffer(entity))
                {
                    ecb.Instantiate(character.ValueRO.MeshPrefab);

                    ecb.AddComponent<CharacterInitialized>(entity);
                }
            }
        }
    }

    [UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup))]
    [BurstCompile]
    public partial struct CharacterPhysicsUpdateSystem : ISystem
    {
        private EntityQuery m_CharacterQuery;
        private CharacterUpdateContext m_Context;
        private KinematicCharacterUpdateContext m_BaseContext;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_CharacterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
                .WithAll<CharacterData, CharacterControl, CharacterStateMachine>()
                .Build(ref state);

            m_Context = new CharacterUpdateContext();
            m_Context.OnSystemCreate(ref state);
            m_BaseContext = new KinematicCharacterUpdateContext();
            m_BaseContext.OnSystemCreate(ref state);

            state.RequireForUpdate(m_CharacterQuery);
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_Context.OnSystemUpdate(ref state, SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged));
            m_BaseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());
        
            var job = new CharacterPhysicsUpdateJob
            {
                Context = m_Context,
                BaseContext = m_BaseContext,
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct CharacterPhysicsUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public CharacterUpdateContext Context;
            public KinematicCharacterUpdateContext BaseContext;

            private void Execute([ChunkIndexInQuery] int chunkIndex, CharacterAspect characterAspect)
            {
                Context.SetChunkIndex(chunkIndex);
                characterAspect.PhysicsUpdate(ref Context, ref BaseContext);
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                BaseContext.EnsureCreationOfTmpCollections();
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
            { }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct CharacterVariableUpdateSystem : ISystem
    {
        private EntityQuery m_CharacterQuery;
        private CharacterUpdateContext m_Context;
        private KinematicCharacterUpdateContext m_BaseContext;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_CharacterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
                .WithAll<CharacterData, CharacterControl>()
                .Build(ref state);

            m_Context = new CharacterUpdateContext();
            m_Context.OnSystemCreate(ref state);
            m_BaseContext = new KinematicCharacterUpdateContext();
            m_BaseContext.OnSystemCreate(ref state);

            state.RequireForUpdate(m_CharacterQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_Context.OnSystemUpdate(ref state, SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged));
            m_BaseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());

            var job = new CharacterVariableUpdateJob
            {
                Context = m_Context,
                BaseContext = m_BaseContext,
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct CharacterVariableUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
        {
            public CharacterUpdateContext Context;
            public KinematicCharacterUpdateContext BaseContext;

            private void Execute([ChunkIndexInQuery] int chunkIndex, CharacterAspect characterAspect)
            {
                Context.SetChunkIndex(chunkIndex);
                characterAspect.VariableUpdate(ref Context, ref BaseContext);
            }

            public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                BaseContext.EnsureCreationOfTmpCollections();
                return true;
            }

            public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
            { }
        }
    }
}

using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Character.Kinematic
{
    [UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct CharacterInterpolationRememberTransformSystem : ISystem
    {
        public struct Singleton : IComponentData
        {
            /// <summary>
            /// Represents the duration of an interpolation between two fixed updates
            /// </summary>
            public float InterpolationDeltaTime;
            /// <summary>
            /// Represents the elapsed time when we last remembered the transforms characters should be interpolating from
            /// </summary>
            public double LastTimeRememberedInterpolationTransforms;
        }

        private ComponentTypeHandle<LocalTransform> m_TransformType;
        private ComponentTypeHandle<CharacterInterpolation> m_CharacterInterpolationType;
        private EntityQuery m_InterpolatedQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_InterpolatedQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<LocalTransform, CharacterInterpolation>()
                .Build(ref state);

            m_TransformType = state.GetComponentTypeHandle<LocalTransform>(true);
            m_CharacterInterpolationType = state.GetComponentTypeHandle<CharacterInterpolation>(false);

            var singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new Singleton());

            state.RequireForUpdate(m_InterpolatedQuery);
            state.RequireForUpdate<Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.Time;
            ref var singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
            singleton.InterpolationDeltaTime = time.DeltaTime;
            singleton.LastTimeRememberedInterpolationTransforms = time.ElapsedTime;

            m_TransformType.Update(ref state);
            m_CharacterInterpolationType.Update(ref state);

            var job = new CharacterInterpolationRememberTransformJob
            {
                TransformType = m_TransformType,
                CharacterInterpolationType = m_CharacterInterpolationType,
            };
            state.Dependency = job.ScheduleParallel(m_InterpolatedQuery, state.Dependency);
        }

        [BurstCompile]
        public unsafe struct CharacterInterpolationRememberTransformJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformType;
            public ComponentTypeHandle<CharacterInterpolation> CharacterInterpolationType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // No enabled comps support for interpolation
                Assert.IsFalse(useEnabledMask);

                var chunkTransforms = chunk.GetNativeArray(ref TransformType);
                var chunkCharacterInterpolations = chunk.GetNativeArray(ref CharacterInterpolationType);

                var chunkInterpolationsPtr = chunkCharacterInterpolations.GetUnsafePtr();
                var chunkCount = chunk.Count;
                var sizeCharacterInterpolation = UnsafeUtility.SizeOf<CharacterInterpolation>();
                var sizeTransform = UnsafeUtility.SizeOf<LocalTransform>();
                var sizePosition = UnsafeUtility.SizeOf<float3>();
                var sizeScale = UnsafeUtility.SizeOf<float>();
                var sizeRotation = UnsafeUtility.SizeOf<quaternion>();
                var sizeByte = UnsafeUtility.SizeOf<byte>();

                // Efficiently copy all position & rotation to the "from" rigidtransform in the character interpolation component
                {
                    // Copy positions
                    UnsafeUtility.MemCpyStride((void*)((long)chunkInterpolationsPtr + sizeRotation),
                        sizeCharacterInterpolation,
                        chunkTransforms.GetUnsafeReadOnlyPtr(),
                        sizeTransform,
                        sizePosition,
                        chunkCount);

                    // Copy rotations
                    UnsafeUtility.MemCpyStride(chunkInterpolationsPtr,
                        sizeCharacterInterpolation,
                        (void*)((long)chunkTransforms.GetUnsafeReadOnlyPtr() + sizePosition + sizeScale),
                        sizeTransform,
                        sizeRotation,
                        chunkCount);

                    // Reset interpolation skippings [DefaultByte -> InterpolationSkipping]
                    UnsafeUtility.MemCpyStride((void*)((long)chunkInterpolationsPtr + sizeRotation + sizePosition),
                        sizeCharacterInterpolation,
                        (void*)((long)chunkInterpolationsPtr + sizePosition + sizeRotation + sizeByte),
                        sizeCharacterInterpolation,
                        sizeByte,
                        chunkCount);
                }
            }
        }
    } 

    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(LocalToWorldSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [BurstCompile]
    public partial struct CharacterInterpolationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CharacterInterpolation>();
            state.RequireForUpdate<CharacterInterpolationRememberTransformSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingletonRW<CharacterInterpolationRememberTransformSystem.Singleton>().ValueRO;

            if (singleton.LastTimeRememberedInterpolationTransforms <= 0f) return;

            var fixedTimeStep = singleton.InterpolationDeltaTime;
            if (fixedTimeStep == 0f) return;

            var deltaTime = (float)(SystemAPI.Time.ElapsedTime - singleton.LastTimeRememberedInterpolationTransforms);
            var normalizedTimeAhead = math.clamp(deltaTime / fixedTimeStep, 0f, 1f);

            var job = new CharacterInterpolationJob { NormalizedTimeAhead = normalizedTimeAhead };
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(KinematicCharacterBody))]
        public partial struct CharacterInterpolationJob : IJobEntity
        {
            public float NormalizedTimeAhead;

            private void Execute(ref CharacterInterpolation interpolation, ref LocalToWorld localToWorld, in LocalTransform transform)
            {
                var targetTransform = new RigidTransform(transform.Rotation, transform.Position);

                var interpolatedRot = targetTransform.rot;
                if (interpolation.InterpolateRotation == 1 && !interpolation.ShouldSkipNextRotationInterpolation())
                {
                    interpolatedRot = math.slerp(interpolation.InterpolationFromTransform.rot,
                        targetTransform.rot, NormalizedTimeAhead);
                }

                var interpolatedPos = targetTransform.pos;
                if (interpolation.InterpolatePosition == 1 && !interpolation.ShouldSkipNextPositionInterpolation())
                {
                    interpolatedPos = math.lerp(interpolation.InterpolationFromTransform.pos,
                        targetTransform.pos, NormalizedTimeAhead);
                }

                localToWorld.Value = new float4x4(interpolatedRot, interpolatedPos);
            }
        }
    }
}

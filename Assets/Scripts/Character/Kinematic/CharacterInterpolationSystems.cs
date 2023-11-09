using Character.Utilities;
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
        private ComponentTypeHandle<CharacterInterpolation> m_InterpolationType;
        private EntityQuery m_InterpolatedQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_InterpolatedQuery = KinematicCharacterUtilities.GetInterpolatedCharacterQueryBuilder().Build(ref state);

            m_TransformType = state.GetComponentTypeHandle<LocalTransform>(true);
            m_InterpolationType = state.GetComponentTypeHandle<CharacterInterpolation>(false);

            var singletonEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(singletonEntity, new Singleton());

            state.RequireForUpdate(m_InterpolatedQuery);
            state.RequireForUpdate<Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_TransformType.Update(ref state);
            m_InterpolationType.Update(ref state);

            var time = SystemAPI.Time;
            ref var singleton = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW;
            singleton.InterpolationDeltaTime = time.DeltaTime;
            singleton.LastTimeRememberedInterpolationTransforms = time.ElapsedTime;

            var job = new CharacterInterpolationRememberTransformJob
            {
                TransformType = m_TransformType,
                InterpolationType = m_InterpolationType,
            };
            state.Dependency = job.ScheduleParallel(m_InterpolatedQuery, state.Dependency);
        }

        [BurstCompile]
        public unsafe struct CharacterInterpolationRememberTransformJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformType;
            public ComponentTypeHandle<CharacterInterpolation> InterpolationType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // No enabled comps support for interpolation
                Assert.IsFalse(useEnabledMask);

                var chunkTransforms = chunk.GetNativeArray(ref TransformType);
                var chunkCharacterInterpolations = chunk.GetNativeArray(ref InterpolationType);

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
                    UnsafeUtility.MemCpyStride(
                        (void*)((long)chunkInterpolationsPtr + sizeRotation),
                        sizeCharacterInterpolation,
                        chunkTransforms.GetUnsafeReadOnlyPtr(),
                        sizeTransform,
                        sizePosition,
                        chunkCount);
                    
                    // Copy rotations
                    UnsafeUtility.MemCpyStride(
                        chunkInterpolationsPtr,
                        sizeCharacterInterpolation,
                        (void*)((long)chunkTransforms.GetUnsafeReadOnlyPtr() + sizePosition + sizeScale),
                        sizeTransform,
                        sizeRotation,
                        chunkCount);
                    
                    // Reset interpolation skippings [DefaultByte -> InterpolationSkipping]
                    UnsafeUtility.MemCpyStride(
                        (void*)((long)chunkInterpolationsPtr + sizeRotation + sizePosition),
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
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingletonRW<CharacterInterpolationRememberTransformSystem.Singleton>().ValueRO;
    
            if (singleton.LastTimeRememberedInterpolationTransforms <= 0f) return;

            var fixedTimeStep = singleton.InterpolationDeltaTime;
            if (fixedTimeStep == 0f) return;

            var timeAheadOfLastFixedUpdate = (float)(SystemAPI.Time.ElapsedTime - singleton.LastTimeRememberedInterpolationTransforms);
            var normalizedTimeAhead = math.clamp(timeAheadOfLastFixedUpdate / fixedTimeStep, 0f, 1f);
            
            var job = new CharacterInterpolationJob { NormalizedTimeAhead = normalizedTimeAhead };
            job.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(KinematicCharacterBody))]
        public partial struct CharacterInterpolationJob : IJobEntity
        {
            public float NormalizedTimeAhead;

            private void Execute(ref CharacterInterpolation characterInterpolation, ref LocalToWorld localToWorld, in LocalTransform transform)
            {
                var targetTransform = new RigidTransform(transform.Rotation, transform.Position);

                var interpolatedRot = targetTransform.rot;
                if (characterInterpolation.InterpolateRotation == 1)
                {
                    if (!characterInterpolation.ShouldSkipNextRotationInterpolation())
                    {
                        interpolatedRot = math.slerp(characterInterpolation.InterpolationFromTransform.rot, targetTransform.rot, NormalizedTimeAhead);
                    }
                }
            
                var interpolatedPos = targetTransform.pos;
                if (characterInterpolation.InterpolatePosition == 1)
                {
                    if (!characterInterpolation.ShouldSkipNextPositionInterpolation())
                    {
                        interpolatedPos = math.lerp(characterInterpolation.InterpolationFromTransform.pos, targetTransform.pos, NormalizedTimeAhead);
                    }
                }
                
                localToWorld.Value = new float4x4(interpolatedRot, interpolatedPos);
            }
        }
    }
}
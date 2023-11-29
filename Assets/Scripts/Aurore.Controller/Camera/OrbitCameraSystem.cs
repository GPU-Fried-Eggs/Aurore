using Character;
using Character.Kinematic;
using Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Utilities;

namespace Camera
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial struct OrbitCameraSystem : ISystem
    {
        public struct CameraObstructionHitsCollector : ICollector<ColliderCastHit>
        {
            public ColliderCastHit ClosestHit;

            private float m_ClosestHitFraction;
            private float3 m_CameraDirection;
            private Entity m_FollowedCharacter;
            private DynamicBuffer<OrbitCameraIgnoredEntityBufferElement> m_IgnoredEntitiesBuffer;

            public bool EarlyOutOnFirstHit => false;

            public float MaxFraction => 1f;

            public int NumHits { get; private set; }

            public CameraObstructionHitsCollector(Entity followedCharacter,
                DynamicBuffer<OrbitCameraIgnoredEntityBufferElement> ignoredEntitiesBuffer,
                float3 cameraDirection)
            {
                NumHits = 0;
                ClosestHit = default;

                m_ClosestHitFraction = float.MaxValue;
                m_CameraDirection = cameraDirection;
                m_FollowedCharacter = followedCharacter;
                m_IgnoredEntitiesBuffer = ignoredEntitiesBuffer;
            }

            public bool AddHit(ColliderCastHit hit)
            {
                if (m_FollowedCharacter == hit.Entity) return false;

                if (math.dot(hit.SurfaceNormal, m_CameraDirection) < 0f || !PhysicsUtilities.IsCollidable(hit.Material))
                    return false;

                for (var i = 0; i < m_IgnoredEntitiesBuffer.Length; i++)
                    if (m_IgnoredEntitiesBuffer[i].Entity == hit.Entity)
                        return false;

                // Process valid hit
                if (hit.Fraction < m_ClosestHitFraction)
                {
                    m_ClosestHitFraction = hit.Fraction;
                    ClosestHit = hit;
                }

                NumHits++;

                return true;
            }
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<OrbitCamera, OrbitCameraControl>().Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            var job = new OrbitCameraJob
            {
                TimeData = SystemAPI.Time,
                PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
                LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(false),
                CharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(true),
                CharacterDataLookup = SystemAPI.GetComponentLookup<CharacterData>(true),
                CharacterStateMachineLookup = SystemAPI.GetComponentLookup<CharacterStateMachine>(true),
                CustomGravityLookup = SystemAPI.GetComponentLookup<CustomGravity>(true)
            };
            job.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct OrbitCameraJob : IJobEntity
        {
            public TimeData TimeData;
            [ReadOnly] public PhysicsWorld PhysicsWorld;

            public ComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<KinematicCharacterBody> CharacterBodyLookup;
            [ReadOnly] public ComponentLookup<CharacterData> CharacterDataLookup;
            [ReadOnly] public ComponentLookup<CharacterStateMachine> CharacterStateMachineLookup;
            [ReadOnly] public ComponentLookup<CustomGravity> CustomGravityLookup;

            private void Execute(Entity entity,
                ref LocalTransform localTransform,
                ref OrbitCamera orbitCamera,
                in OrbitCameraControl cameraControl,
                in DynamicBuffer<OrbitCameraIgnoredEntityBufferElement> ignoredEntitiesBuffer)
            {
                var elapsedTime = (float)TimeData.ElapsedTime;

                if (LocalToWorldLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out var characterLTW) &&
                    CustomGravityLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out var customGravity) &&
                    CharacterDataLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out var characterData) &&
                    CharacterStateMachineLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out var characterStateMachine))
                {
                    #region Camera target handling

                    characterStateMachine.GetCameraParameters(characterStateMachine.CurrentState, in characterData,
                        out var selectedCameraTarget, out var calculateUpFromGravity);

                    var selectedCameraTargetTransform =
                        LocalToWorldLookup.TryGetComponent(selectedCameraTarget, out var camTargetLTW)
                            ? new RigidTransform(camTargetLTW.Rotation, camTargetLTW.Position)
                            : new RigidTransform(characterLTW.Rotation, characterLTW.Position);

                    if (calculateUpFromGravity)
                    {
                        selectedCameraTargetTransform.rot =
                            MathUtilities.CreateRotationWithUpPriority(math.normalizesafe(-customGravity.Gravity),
                                math.mul(selectedCameraTargetTransform.rot, math.forward()));
                    }

                    // Detect transition
                    if (orbitCamera.ActiveCameraTarget != selectedCameraTarget ||
                        orbitCamera.PreviousCalculateUpFromGravity != calculateUpFromGravity)
                    {
                        orbitCamera.CameraTargetTransitionStartTime = elapsedTime;
                        orbitCamera.CameraTargetTransitionFromTransform = orbitCamera.CameraTargetTransform;
                        orbitCamera.ActiveCameraTarget = selectedCameraTarget;
                        orbitCamera.PreviousCalculateUpFromGravity = calculateUpFromGravity;
                    }

                    // Update transitions
                    if (elapsedTime < orbitCamera.CameraTargetTransitionStartTime + orbitCamera.CameraTargetTransitionTime)
                    {
                        var previousCameraTargetPosition =
                            LocalToWorldLookup.TryGetComponent(orbitCamera.PreviousCameraTarget, out var previousCamTargetLTW)
                                ? previousCamTargetLTW.Position
                                : characterLTW.Position;

                        var transitionRatio = math.saturate(
                            (elapsedTime - orbitCamera.CameraTargetTransitionStartTime) / orbitCamera.CameraTargetTransitionTime);
                        orbitCamera.CameraTargetTransform.pos = math.lerp(previousCameraTargetPosition,
                            selectedCameraTargetTransform.pos,
                            transitionRatio);
                        orbitCamera.CameraTargetTransform.rot = math.slerp(orbitCamera.CameraTargetTransitionFromTransform.rot,
                            selectedCameraTargetTransform.rot,
                            transitionRatio);
                    }
                    else
                    {
                        orbitCamera.CameraTargetTransform = selectedCameraTargetTransform;
                        orbitCamera.PreviousCameraTarget = orbitCamera.ActiveCameraTarget;
                    }

                    #endregion

                    var cameraTargetUp = math.mul(orbitCamera.CameraTargetTransform.rot, math.up());

                    #region Rotation

                    localTransform.Rotation = quaternion.LookRotationSafe(orbitCamera.PlanarForward, cameraTargetUp);

                    // Handle rotating the camera along with character's parent entity (moving platform)
                    if (orbitCamera.RotateWithCharacterParent &&
                        CharacterBodyLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out var characterBody))
                    {
                        var forwardFromRotation = MathUtilities.GetForwardFromRotation(localTransform.Rotation);
                        var projectOnPlane = MathUtilities.ProjectOnPlane(forwardFromRotation, cameraTargetUp);
                        KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(ref localTransform.Rotation,
                            characterBody.RotationFromParent, TimeData.DeltaTime, characterBody.LastPhysicsUpdateDeltaTime);
                        orbitCamera.PlanarForward = math.normalizesafe(projectOnPlane);
                    }

                    // Yaw
                    var yawAngleChange = cameraControl.LookDegreesDelta.x * orbitCamera.RotationSpeed;
                    var yawRotation = quaternion.Euler(cameraTargetUp * math.radians(yawAngleChange));
                    orbitCamera.PlanarForward = math.rotate(yawRotation, orbitCamera.PlanarForward);

                    // Pitch
                    orbitCamera.PitchAngle += -cameraControl.LookDegreesDelta.y * orbitCamera.RotationSpeed;
                    orbitCamera.PitchAngle = math.clamp(orbitCamera.PitchAngle, orbitCamera.MinVAngle, orbitCamera.MaxVAngle);
                    var pitchRotation = quaternion.Euler(math.right() * math.radians(orbitCamera.PitchAngle));

                    // Final rotation
                    localTransform.Rotation = quaternion.LookRotationSafe(orbitCamera.PlanarForward, cameraTargetUp);
                    localTransform.Rotation = math.mul(localTransform.Rotation, pitchRotation);

                    #endregion

                    var cameraForward = MathUtilities.GetForwardFromRotation(localTransform.Rotation);

                    // Distance input
                    var distanceMovementSpeed = cameraControl.ZoomDelta * orbitCamera.DistanceMovementSpeed;
                    orbitCamera.TargetDistance = math.clamp(orbitCamera.TargetDistance + distanceMovementSpeed, orbitCamera.MinDistance, orbitCamera.MaxDistance);
                    var sharpnessInterpolant = MathUtilities.GetSharpnessInterpolant(orbitCamera.DistanceMovementSharpness, TimeData.DeltaTime);
                    orbitCamera.SmoothedTargetDistance = math.lerp(orbitCamera.SmoothedTargetDistance, orbitCamera.TargetDistance, sharpnessInterpolant);

                    // Obstructions
                    if (orbitCamera.ObstructionRadius > 0f)
                    {
                        var obstructionCheckDistance = orbitCamera.SmoothedTargetDistance;

                        var collector = new CameraObstructionHitsCollector(cameraControl.FollowedCharacterEntity,
                            ignoredEntitiesBuffer,
                            cameraForward);

                        PhysicsWorld.SphereCastCustom(orbitCamera.CameraTargetTransform.pos,
                            orbitCamera.ObstructionRadius,
                            -cameraForward,
                            obstructionCheckDistance,
                            ref collector,
                            CollisionFilter.Default,
                            QueryInteraction.IgnoreTriggers);

                        var newObstructedDistance = obstructionCheckDistance;
                        if (collector.NumHits > 0)
                        {
                            newObstructedDistance = obstructionCheckDistance * collector.ClosestHit.Fraction;

                            // Redo cast with the interpolated body transform to prevent FixedUpdate jitter in obstruction detection
                            if (orbitCamera.PreventFixedUpdateJitter)
                            {
                                var hitBody = PhysicsWorld.Bodies[collector.ClosestHit.RigidBodyIndex];
                                if (LocalToWorldLookup.TryGetComponent(hitBody.Entity, out var hitBodyLocalToWorld))
                                {
                                    var lookRotation = quaternion.LookRotationSafe(hitBodyLocalToWorld.Forward, hitBodyLocalToWorld.Up);
                                    hitBody.WorldFromBody = new RigidTransform(lookRotation, hitBodyLocalToWorld.Position);

                                    collector = new CameraObstructionHitsCollector(cameraControl.FollowedCharacterEntity,
                                        ignoredEntitiesBuffer,
                                        cameraForward);

                                    hitBody.SphereCastCustom(orbitCamera.CameraTargetTransform.pos,
                                        orbitCamera.ObstructionRadius,
                                        -cameraForward,
                                        obstructionCheckDistance,
                                        ref collector,
                                        CollisionFilter.Default,
                                        QueryInteraction.IgnoreTriggers);

                                    if (collector.NumHits > 0)
                                    {
                                        newObstructedDistance = obstructionCheckDistance * collector.ClosestHit.Fraction;
                                    }
                                }
                            }
                        }

                        // Update current distance based on obstructed distance
                        if (orbitCamera.ObstructedDistance < newObstructedDistance)
                        {
                            // Move outer
                            orbitCamera.ObstructedDistance = math.lerp(orbitCamera.ObstructedDistance,
                                newObstructedDistance,
                                MathUtilities.GetSharpnessInterpolant(orbitCamera.ObstructionOuterSmoothingSharpness,
                                    TimeData.DeltaTime));
                        }
                        else if (orbitCamera.ObstructedDistance > newObstructedDistance)
                        {
                            // Move inner
                            orbitCamera.ObstructedDistance = math.lerp(orbitCamera.ObstructedDistance,
                                newObstructedDistance,
                                MathUtilities.GetSharpnessInterpolant(orbitCamera.ObstructionInnerSmoothingSharpness,
                                    TimeData.DeltaTime));
                        }
                    }
                    else
                    {
                        orbitCamera.ObstructedDistance = orbitCamera.SmoothedTargetDistance;
                    }

                    // Calculate final camera position from targetposition + rotation + distance
                    localTransform.Position = orbitCamera.CameraTargetTransform.pos + (-cameraForward * orbitCamera.ObstructedDistance);

                    // Manually calculate the LocalToWorld since this is updating after the Transform systems, and the LtW is what rendering uses
                    LocalToWorldLookup[entity] = new LocalToWorld
                    {
                        Value = new float4x4(localTransform.Rotation, localTransform.Position)
                    };
                }
            }
        }
    }
}

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Interactive
{
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    [BurstCompile]
    public partial struct MovingPlatformSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            if (deltaTime <= 0f) return;

            var job = new MovingPlatformJob
            {
                Time = (float)SystemAPI.Time.ElapsedTime,
                InvDeltaTime = 1f / deltaTime,
            };
            job.Schedule();
        }

        [BurstCompile]
        public partial struct MovingPlatformJob : IJobEntity
        {
            public float Time;
            public float InvDeltaTime;

            private void Execute(Entity entity,
                ref PhysicsVelocity physicsVelocity,
                in PhysicsMass physicsMass,
                in LocalTransform localTransform,
                in MovingPlatform movingPlatform)
            {
                var normalizedTranslationAxis = math.normalizesafe(movingPlatform.TranslationAxis);
                var translationOffset = math.sin(Time * movingPlatform.TranslationSpeed) * movingPlatform.TranslationAmplitude;
                var targetPos = movingPlatform.OriginalPosition + normalizedTranslationAxis * translationOffset;

                var normalizedRotationAxis = math.normalizesafe(movingPlatform.RotationAxis);
                var rotationAngle = movingPlatform.RotationSpeed * Time;
                var rotationFromRotation = quaternion.Euler(normalizedRotationAxis * rotationAngle);

                var normalizedOscillationAxis = math.normalizesafe(movingPlatform.OscillationAxis);
                var oscillationAngle = math.sin(Time * movingPlatform.OscillationSpeed) * movingPlatform.OscillationAmplitude;
                var rotationFromOscillation = quaternion.Euler(normalizedOscillationAxis * oscillationAngle);

                var totalRotation = math.mul(rotationFromRotation, rotationFromOscillation);
                var targetRot = math.mul(totalRotation, movingPlatform.OriginalRotation);

                var targetTransform = new RigidTransform(targetRot, targetPos);

                physicsVelocity = PhysicsVelocity.CalculateVelocityToTarget(in physicsMass,
                    localTransform.Position, localTransform.Rotation,
                    in targetTransform, InvDeltaTime);
            }
        }
    }
}
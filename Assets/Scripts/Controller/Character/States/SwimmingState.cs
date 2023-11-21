using Character.Kinematic;
using Player;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Character.States
{
    public struct SwimmingState : ICharacterState
    {
        public void OnStateEnter(CharacterState previousState, ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext, in CharacterAspect aspect)
        {
            throw new System.NotImplementedException();
        }

        public void OnStateExit(CharacterState nextState, ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext, in CharacterAspect aspect)
        {
            throw new System.NotImplementedException();
        }

        public void OnStatePhysicsUpdate(ref CharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            throw new System.NotImplementedException();
        }

        public void OnStateVariableUpdate(ref CharacterUpdateContext context, ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect)
        {
            throw new System.NotImplementedException();
        }

        public void GetCameraParameters(in CharacterData character, out Entity cameraTarget, out bool calculateUpFromGravity)
        {
            throw new System.NotImplementedException();
        }

        public void GetMoveVectorFromPlayerInput(in PlayerInputs inputs, quaternion cameraRotation, out float3 moveVector)
        {
            throw new System.NotImplementedException();
        }

        public static unsafe bool DetectWaterZones(ref CharacterUpdateContext context,
            ref KinematicCharacterUpdateContext baseContext,
            in CharacterAspect aspect,
            out float3 directionToWaterSurface,
            out float waterSurfaceDistance)
        {
            directionToWaterSurface = default;
            waterSurfaceDistance = 0f;
        
            ref var physicsCollider = ref aspect.KinematicAspect.PhysicsCollider.ValueRW;
            ref var character = ref aspect.Character.ValueRW;
            ref var characterPosition = ref aspect.KinematicAspect.LocalTransform.ValueRW.Position;
            ref var characterRotation = ref aspect.KinematicAspect.LocalTransform.ValueRW.Rotation;

            var characterRigidTransform = new RigidTransform(characterRotation, characterPosition);
            var swimmingDetectionPointWorldPosition = math.transform(characterRigidTransform, character.LocalSwimmingDetectionPoint);
            var waterDetectionFilter = new CollisionFilter
            {
                BelongsTo = physicsCollider.ColliderPtr->GetCollisionFilter().BelongsTo,
                CollidesWith = character.WaterPhysicsCategory.Value,
            };

            var pointInput = new PointDistanceInput
            {
                Filter = waterDetectionFilter,
                MaxDistance = character.WaterDetectionDistance,
                Position = swimmingDetectionPointWorldPosition,
            };

            if (baseContext.PhysicsWorld.CalculateDistance(pointInput, out var closestHit))
            {
                directionToWaterSurface = closestHit.SurfaceNormal; // always goes in the direction of decolliding from the target collider
                waterSurfaceDistance = closestHit.Distance; // positive means above surface
                return true;
            }

            return false;
        }
    }
}
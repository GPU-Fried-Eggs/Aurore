using System.Runtime.CompilerServices;
using Character.Kinematic;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Physics.Extensions;
using Unity.Transforms;

namespace Character.Utilities
{
    public static class PhysicsUtilities
    {
        /// <summary>
        /// Gets the face normal of a hit triangle in a collision mesh
        /// </summary>
        /// <param name="hitBody"> The hit body </param>
        /// <param name="colliderKey"> The hit collider key</param>
        /// <param name="faceNormal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool GetHitFaceNormal(RigidBody hitBody, ColliderKey colliderKey, out float3 faceNormal)
        {
            faceNormal = default;

            if (hitBody.Collider.Value.GetLeaf(colliderKey, out var hitChildCollider))
            {
                var colliderType = hitChildCollider.Collider->Type;

                if (colliderType == ColliderType.Triangle || colliderType == ColliderType.Quad)
                {
                    var verticesAccessor = ((PolygonCollider*)hitChildCollider.Collider)->Vertices;
                    var localFaceNormal = math.normalizesafe(math.cross(verticesAccessor[1] - verticesAccessor[0], verticesAccessor[2] - verticesAccessor[0]));
                    faceNormal = math.rotate(hitBody.WorldFromBody, localFaceNormal);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if a hit body index has mass and velocity components, simply based on PhysicsWorld information
        /// </summary>
        /// <param name="physicsWorld"> The Physics World of the body </param>
        /// <param name="rigidbodyIndex"> The body index </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DoesBodyHavePhysicsVelocityAndMass(in PhysicsWorld physicsWorld, int rigidbodyIndex)
        {
            return rigidbodyIndex < physicsWorld.NumDynamicBodies;
        }

        /// <summary>
        /// Determines if a body is dynamic
        /// </summary>
        /// <param name="physicsWorld"> The Physics World of the body </param>
        /// <param name="rigidbodyIndex"> The body index </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBodyDynamic(in PhysicsWorld physicsWorld, int rigidbodyIndex)
        {
            if (DoesBodyHavePhysicsVelocityAndMass(in physicsWorld, rigidbodyIndex))
            {
                if (physicsWorld.MotionVelocities[rigidbodyIndex].InverseMass > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if the specified physics body has a given physics body tag
        /// </summary>
        /// <param name="physicsWorld"> The Physics World of the body </param>
        /// <param name="bodyIndex"> The body index </param>
        /// <param name="tag"> The physics body tag to check for </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasPhysicsTag(in PhysicsWorld physicsWorld, int bodyIndex, CustomPhysicsBodyTags tag)
        {
            if (tag.Value > CustomPhysicsBodyTags.Nothing.Value)
            {
                if ((physicsWorld.Bodies[bodyIndex].CustomTags & tag.Value) > 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Determines if the specified physics material has a given physics material tag
        /// </summary>
        /// <param name="hitMaterial"> The hit physics body material </param>
        /// <param name="tag"> The physics material tag to check for </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasPhysicsTag(Material hitMaterial, PhysicsCustomTags tag)
        {
            return (hitMaterial.CustomTags & tag.Value) == tag.Value;
        }

        /// <summary>
        /// Reconstructs physics velocity and mass components for a specified body, based on information stored in the Physics World
        /// </summary>
        /// <param name="physicsWorld"> The physics world </param>
        /// <param name="rigidbodyIndex"> The rigidbody index </param>
        /// <param name="transform"> The body's transform </param>
        /// <param name="physicsVelocity"> The body's physics velocity </param>
        /// <param name="physicsMass"> The body's physics mass </param>
        /// <returns> If the components were found successfully </returns>
        public static bool GetBodyComponents(in PhysicsWorld physicsWorld, int rigidbodyIndex, out LocalTransform transform, out PhysicsVelocity physicsVelocity, out PhysicsMass physicsMass)
        {
            if (rigidbodyIndex >= 0 && rigidbodyIndex < physicsWorld.MotionVelocities.Length)
            {
                var rigidbody = physicsWorld.Bodies[rigidbodyIndex];
                var motionVelocity = physicsWorld.MotionVelocities[rigidbodyIndex];
                var motionData = physicsWorld.MotionDatas[rigidbodyIndex];

                transform = new LocalTransform
                {
                    Position = rigidbody.WorldFromBody.pos,
                    Rotation = rigidbody.WorldFromBody.rot,
                    Scale = rigidbody.Scale,
                };

                physicsVelocity = new PhysicsVelocity
                {
                    Linear = motionVelocity.LinearVelocity,
                    Angular = motionVelocity.AngularVelocity,
                };

                physicsMass = new PhysicsMass
                {
                    Transform = motionData.BodyFromMotion,
                    InverseInertia = motionVelocity.InverseInertia,
                    InverseMass = motionVelocity.InverseMass,
                    AngularExpansionFactor = motionVelocity.AngularExpansionFactor,
                };

                return true;
            }

            transform = default;
            physicsVelocity = default;
            physicsMass = default;
            return false;
        }

        /// <summary>
        /// Determines if the physics material has a collideable collision response
        /// </summary>
        /// <param name="material"> The physics material </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCollidable(in Material material)
        {
            return material.CollisionResponse is CollisionResponsePolicy.Collide or CollisionResponsePolicy.CollideRaiseCollisionEvents;
        }

        /// <summary>
        /// Sets a collider's collision response
        /// </summary>
        /// <param name="rigidBody"> The rigidbody to change </param>
        /// <param name="colliderKey"> The collider key representing the collider to change </param>
        /// <param name="collisionResponse"> The desired collision response </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool SetCollisionResponse(RigidBody rigidBody, ColliderKey colliderKey, CollisionResponsePolicy collisionResponse)
        {
            if (rigidBody.Collider.Value.GetLeaf(colliderKey, out var leafCollider))
            {
                leafCollider.Collider->SetCollisionResponse(collisionResponse);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets a collider's collision response
        /// </summary>
        /// <param name="rigidBody"> The rigidbody to change </param>
        /// <param name="collisionResponse"> The desired collision response </param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetCollisionResponse(RigidBody rigidBody, CollisionResponsePolicy collisionResponse)
        {
            ((Collider*)rigidBody.Collider.GetUnsafePtr())->SetCollisionResponse(collisionResponse);
        }

        /// <summary>
        /// Solves a collision between two bodies and outputs the impulses to apply on each body
        /// </summary>
        /// <param name="physicsVelA"> Body A's physics velocity </param>
        /// <param name="physicsVelB"> Body B's physics velocity </param>
        /// <param name="physicsMassA"> Body A's physics mass </param>
        /// <param name="physicsMassB"> Body B's physics mass </param>
        /// <param name="transformA"> Body A's transform </param>
        /// <param name="transformB"> Body B's transform </param>
        /// <param name="collisionPoint"> The collision point between the two bodies </param>
        /// <param name="collisionNormalBToA"> The collision normal, from body B towards body A </param>
        /// <param name="impulseOnA"> The impulse to apply on body A </param>
        /// <param name="impulseOnB"> The impulse to apply on body B </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SolveCollisionImpulses(in PhysicsVelocity physicsVelA,
            in PhysicsVelocity physicsVelB,
            in PhysicsMass physicsMassA,
            in PhysicsMass physicsMassB,
            in RigidTransform transformA,
            in RigidTransform transformB,
            float3 collisionPoint,
            float3 collisionNormalBToA,
            out float3 impulseOnA,
            out float3 impulseOnB)
        {
            impulseOnA = default;
            impulseOnB = default;

            var pointVelocityA = physicsVelA.GetLinearVelocity(physicsMassA, transformA.pos, transformA.rot, collisionPoint);
            var pointVelocityB = physicsVelB.GetLinearVelocity(physicsMassB, transformB.pos, transformB.rot, collisionPoint);

            var centerOfMassA = physicsMassA.GetCenterOfMassWorldSpace(transformA.pos, transformA.rot);
            var centerOfMassB = physicsMassA.GetCenterOfMassWorldSpace(transformB.pos, transformB.rot);
            var centerOfMassAToPoint = collisionPoint - centerOfMassA;
            var centerOfMassBToPoint = collisionPoint - centerOfMassB;

            var relativeVelocityAToB = pointVelocityB - pointVelocityA;
            var relativeVelocityOnNormal = math.dot(relativeVelocityAToB, collisionNormalBToA);

            if (relativeVelocityOnNormal > 0f)
            {
                var crossA = math.cross(centerOfMassAToPoint, collisionNormalBToA);
                var crossB = math.cross(collisionNormalBToA, centerOfMassBToPoint);
                var angularA = math.mul(new Math.MTransform(transformA).InverseRotation, crossA).xyz;
                var angularB = math.mul(new Math.MTransform(transformB).InverseRotation, crossB).xyz;
                var temp = angularA * angularA * physicsMassA.InverseInertia + angularB * angularB * physicsMassB.InverseInertia;
                var invEffectiveMass = temp.x + temp.y + temp.z + (physicsMassA.InverseMass + physicsMassB.InverseMass);

                if (invEffectiveMass > 0f)
                {
                    var effectiveMass = 1f / invEffectiveMass;

                    var impulseScale = -relativeVelocityOnNormal * effectiveMass;
                    var totalImpulse = collisionNormalBToA * impulseScale;

                    impulseOnA = -totalImpulse;
                    impulseOnB = totalImpulse;
                }
            }
        }

        /// <summary>
        /// Builds a physics mass struct based on a character's mass properties
        /// </summary>
        /// <param name="storedCharacterData"> The character component that stores character data </param>
        /// <returns> The resulting physics mass </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PhysicsMass GetKinematicCharacterPhysicsMass(KinematicCharacterStoredData storedCharacterData)
        {
            return new PhysicsMass
            {
                AngularExpansionFactor = 0f,
                InverseInertia = float3.zero,
                InverseMass = storedCharacterData.SimulateDynamicBody ? (1f / storedCharacterData.Mass) : 0f,
                Transform = new RigidTransform(quaternion.identity, float3.zero),
            };
        }

        /// <summary>
        /// Builds a physics mass struct based on a character's mass properties
        /// </summary>
        /// <param name="characterData"> The character properties component </param>
        /// <returns> The resulting physics mass </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PhysicsMass GetKinematicCharacterPhysicsMass(KinematicCharacterData characterData)
        {
            return new PhysicsMass
            {
                AngularExpansionFactor = 0f,
                InverseInertia = float3.zero,
                InverseMass = characterData.SimulateDynamicBody ? (1f / characterData.Mass) : 0f,
                Transform = new RigidTransform(quaternion.identity, float3.zero),
            };
        }
    }
}

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Utilities;

namespace Character.Kinematic
{
    public readonly partial struct KinematicCharacterAspect
    {
        /// <summary>
        /// Casts the character collider and only returns the closest collideable hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="characterRotation"> The rotation of the character</param>
        /// <param name="characterScale"> The uniform scale of the character</param>
        /// <param name="direction"> The direction of the case </param>
        /// <param name="length"> The length of the cast </param>
        /// <param name="onlyObstructingHits"> Should the cast only detect hits whose normal is opposed to the direction of the cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="hit"> The closest detected hit </param>
        /// <param name="hitDistance"> The distance of the closest detected hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit was detected</returns>
        public bool CastColliderClosestCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            float3 characterPosition,
            quaternion characterRotation,
            float characterScale,
            float3 direction,
            float length,
            bool onlyObstructingHits,
            bool ignoreDynamicBodies,
            out ColliderCastHit hit,
            out float hitDistance)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterPhysicsCollider = PhysicsCollider.ValueRO;

            var castInput = new ColliderCastInput(characterPhysicsCollider.Value, characterPosition,
                characterPosition + (direction * length), characterRotation, characterScale);

            baseContext.TmpColliderCastHits.Clear();
            var collector = new AllHitsCollector<ColliderCastHit>(1f, ref baseContext.TmpColliderCastHits);
            baseContext.PhysicsWorld.CastCollider(castInput, ref collector);

            if (FilterColliderCastHitsForClosestCollisions(in processor, ref context, ref baseContext,
                    ref baseContext.TmpColliderCastHits, onlyObstructingHits, direction,
                    ignoreDynamicBodies, out var closestHit))
            {
                hit = closestHit;
                hitDistance = length * hit.Fraction;
                return true;
            }

            hit = default;
            hitDistance = default;
            return false;
        }

        /// <summary>
        /// Casts the character collider and returns all collideable hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="characterRotation"> The rotation of the character</param>
        /// <param name="characterScale"> The uniform scale of the character</param>
        /// <param name="direction"> The direction of the case </param>
        /// <param name="length"> The length of the cast </param>
        /// <param name="onlyObstructingHits"> Should the cast only detect hits whose normal is opposed to the direction of the cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="hits"> All valid detected hits </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit was detected </returns>
        public bool CastColliderAllCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            float3 characterPosition,
            quaternion characterRotation,
            float characterScale,
            float3 direction,
            float length,
            bool onlyObstructingHits,
            bool ignoreDynamicBodies,
            out NativeList<ColliderCastHit> hits)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterPhysicsCollider = PhysicsCollider.ValueRO;
            hits = baseContext.TmpColliderCastHits;

            var castInput = new ColliderCastInput(characterPhysicsCollider.Value, characterPosition,
                characterPosition + (direction * length), characterRotation, characterScale);

            baseContext.TmpColliderCastHits.Clear();
            var collector = new AllHitsCollector<ColliderCastHit>(1f, ref baseContext.TmpColliderCastHits);
            baseContext.PhysicsWorld.CastCollider(castInput, ref collector);

            return FilterColliderCastHitsForAllCollisions(in processor, ref context, ref baseContext,
                ref baseContext.TmpColliderCastHits, onlyObstructingHits, direction, ignoreDynamicBodies);
        }

        /// <summary>
        /// Casts a ray and only returns the closest collideable hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="startPoint"> The cast start point </param>
        /// <param name="direction"> The direction of the case </param>
        /// <param name="length"> The length of the cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="hit"> The detected hit </param>
        /// <param name="hitDistance"> The distance of the detected hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit was detected </returns>
        public bool RaycastClosestCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            float3 startPoint,
            float3 direction,
            float length,
            bool ignoreDynamicBodies,
            out RaycastHit hit,
            out float hitDistance)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterPhysicsCollider = PhysicsCollider.ValueRO;

            var castInput = new RaycastInput
            {
                Start = startPoint,
                End = startPoint + (direction * length),
                Filter = characterPhysicsCollider.Value.Value.GetCollisionFilter(),
            };

            baseContext.TmpRaycastHits.Clear();
            var collector = new AllHitsCollector<RaycastHit>(1f, ref baseContext.TmpRaycastHits);
            baseContext.PhysicsWorld.CastRay(castInput, ref collector);

            if (FilterRaycastHitsForClosestCollisions(in processor, ref context, ref baseContext,
                    ref baseContext.TmpRaycastHits, ignoreDynamicBodies, out var closestHit))
            {
                hit = closestHit;
                hitDistance = length * hit.Fraction;
                return true;
            }

            hit = default;
            hitDistance = default;
            return false;
        }

        /// <summary>
        /// Casts a ray and returns all collideable hits
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="startPoint"> The cast start point </param>
        /// <param name="direction"> The direction of the case </param>
        /// <param name="length"> The length of the cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="hits"> The detected hits </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit was detected </returns>
        public bool RaycastAllCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            float3 startPoint,
            float3 direction,
            float length,
            bool ignoreDynamicBodies,
            out NativeList<RaycastHit> hits)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterPhysicsCollider = PhysicsCollider.ValueRO;
            hits = baseContext.TmpRaycastHits;

            var castInput = new RaycastInput
            {
                Start = startPoint,
                End = startPoint + (direction * length),
                Filter = characterPhysicsCollider.Value.Value.GetCollisionFilter(),
            };

            baseContext.TmpRaycastHits.Clear();
            var collector = new AllHitsCollector<RaycastHit>(1f, ref baseContext.TmpRaycastHits);
            baseContext.PhysicsWorld.CastRay(castInput, ref collector);

            return FilterRaycastHitsForAllCollisions(in processor, ref context, ref baseContext,
                ref baseContext.TmpRaycastHits, ignoreDynamicBodies);
        }

        /// <summary>
        /// Calculates distance from the character collider and only returns the closest collideable hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="characterRotation"> The rotation of the character</param>
        /// <param name="characterScale"> The uniform scale of the character</param>
        /// <param name="maxDistance"> The direction of the case </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="hit"> The closest detected hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit was detected</returns>
        public bool CalculateDistanceClosestCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            float3 characterPosition,
            quaternion characterRotation,
            float characterScale,
            float maxDistance,
            bool ignoreDynamicBodies,
            out DistanceHit hit)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterPhysicsCollider = PhysicsCollider.ValueRO;

            var distanceInput = new ColliderDistanceInput(characterPhysicsCollider.Value, maxDistance,
                math.RigidTransform(characterRotation, characterPosition), characterScale);

            baseContext.TmpDistanceHits.Clear();
            var collector = new AllHitsCollector<DistanceHit>(distanceInput.MaxDistance, ref baseContext.TmpDistanceHits);
            baseContext.PhysicsWorld.CalculateDistance(distanceInput, ref collector);

            if (FilterDistanceHitsForClosestCollisions(in processor, ref context, ref baseContext,
                    ref baseContext.TmpDistanceHits, ignoreDynamicBodies, out var closestHit))
            {
                hit = closestHit;
                return true;
            }

            hit = default;
            return false;
        }

        /// <summary>
        /// Calculates distance from the character collider and only returns all collideable hits
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="characterPosition"> The position of the character </param>
        /// <param name="characterRotation"> The rotation of the character</param>
        /// <param name="characterScale"> The uniform scale of the character</param>
        /// <param name="maxDistance"> The direction of the case </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="hits"> The detected hits </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit was detected</returns>
        public unsafe bool CalculateDistanceAllCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            float3 characterPosition,
            quaternion characterRotation,
            float characterScale,
            float maxDistance,
            bool ignoreDynamicBodies,
            out NativeList<DistanceHit> hits)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            var characterPhysicsCollider = PhysicsCollider.ValueRO;
            hits = baseContext.TmpDistanceHits;

            var distanceInput = new ColliderDistanceInput(characterPhysicsCollider.Value, maxDistance,
                math.RigidTransform(characterRotation, characterPosition), characterScale);

            baseContext.TmpDistanceHits.Clear();
            var collector = new AllHitsCollector<DistanceHit>(distanceInput.MaxDistance, ref baseContext.TmpDistanceHits);
            baseContext.PhysicsWorld.CalculateDistance(distanceInput, ref collector);

            return FilterDistanceHitsForAllCollisions(in processor, ref context, ref baseContext,
                ref baseContext.TmpDistanceHits, ignoreDynamicBodies);
        }

        /// <summary>
        /// Filters a list of hits for ground probing and returns the closest valid hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="castDirection"> The direction of the ground probing cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="closestHit"> The closest detected hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterColliderCastHitsForGroundProbing<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref NativeList<ColliderCastHit> hits,
            float3 castDirection,
            bool ignoreDynamicBodies,
            out ColliderCastHit closestHit)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            closestHit = default;
            closestHit.Fraction = float.MaxValue;

            for (var i = hits.Length - 1; i >= 0; i--)
            {
                var hitAccepted = false;
                var hit = hits[i];
                if (hit.Entity != Entity)
                {
                    // ignore hits if we're going away from them
                    var dotRatio = math.dot(hit.SurfaceNormal, castDirection);
                    if (dotRatio < -k_DotProductSimilarityEpsilon)
                    {
                        if (!ignoreDynamicBodies ||
                            !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                        {
                            if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                            {
                                hitAccepted = true;

                                if (hit.Fraction < closestHit.Fraction)
                                {
                                    closestHit = hit;
                                }
                            }
                        }
                    }
                }

                if (!hitAccepted) hits.RemoveAtSwapBack(i);
            }

            return closestHit.Entity != Entity.Null;
        }

        /// <summary>
        /// Filters a list of hits for character movement and returns the closest valid hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="characterIsKinematic"> Is the character kinematic (as opposed to simulated dynamic) </param>
        /// <param name="castDirection"> The direction of the ground probing cast </param>
        /// <param name="ignoredEntity"> An optional Entity to force ignore </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="closestHit"> The closest detected hit </param>
        /// <param name="foundAnyOverlaps"> Whether any overlaps were found with other colliders </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterColliderCastHitsForMove<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref NativeList<ColliderCastHit> hits,
            bool characterIsKinematic,
            float3 castDirection,
            Entity ignoredEntity,
            bool ignoreDynamicBodies,
            out ColliderCastHit closestHit,
            out bool foundAnyOverlaps)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            foundAnyOverlaps = false;
            closestHit = default;
            closestHit.Fraction = float.MaxValue;
            var dotRatioOfSelectedHit = float.MaxValue;

            for (var i = hits.Length - 1; i >= 0; i--)
            {
                var hit = hits[i];
                if (hit.Entity == ignoredEntity) continue;
                if (hit.Entity != Entity)
                {
                    if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                    {
                        var hitBodyIsDynamic = PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex);

                        // Remember overlaps (must always include dynamic hits or hits we move away from)
                        if (hit.Fraction <= 0f || (characterIsKinematic && hitBodyIsDynamic))
                            foundAnyOverlaps = true;

                        if (ignoreDynamicBodies && hitBodyIsDynamic) continue;

                        // ignore hits if we're going away from them
                        var dotRatio = math.dot(hit.SurfaceNormal, castDirection);
                        if (dotRatio < -k_DotProductSimilarityEpsilon)
                        {
                            // only accept closest hit so far
                            if (hit.Fraction <= closestHit.Fraction)
                            {
                                // Accept hit if it's the new closest one, or if equal distance but more obstructing
                                var isCloserThanPreviousSelectedHit = hit.Fraction < closestHit.Fraction;
                                if (isCloserThanPreviousSelectedHit || dotRatio < dotRatioOfSelectedHit)
                                {
                                    closestHit = hit;
                                    dotRatioOfSelectedHit = dotRatio;
                                }
                            }
                        }
                    }
                }
            }

            return closestHit.Entity != Entity.Null;
        }

        /// <summary>
        /// Filters a list of hits and returns the closest valid hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="onlyObstructingHits"> Should the cast only detect hits whose normal is opposed to the direction of the cast </param>
        /// <param name="castDirection"> The direction of the ground probing cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="closestHit"> The closest detected hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterColliderCastHitsForClosestCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref NativeList<ColliderCastHit> hits,
            bool onlyObstructingHits,
            float3 castDirection,
            bool ignoreDynamicBodies,
            out ColliderCastHit closestHit)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            closestHit = default;
            closestHit.Fraction = float.MaxValue;

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.Fraction > closestHit.Fraction) continue;
                if (hit.Entity != Entity)
                {
                    if (!ignoreDynamicBodies ||
                        !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                    {
                        // ignore hits if we're going away from them
                        var dotRatio = math.dot(hit.SurfaceNormal, castDirection);
                        if (!onlyObstructingHits || dotRatio < -k_DotProductSimilarityEpsilon)
                        {
                            if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                            {
                                closestHit = hit;
                            }
                        }
                    }
                }
            }

            return closestHit.Entity != Entity.Null;
        }

        /// <summary>
        /// Filters a list of hits and keeps only valid hits
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="onlyObstructingHits"> Should the cast only detect hits whose normal is opposed to the direction of the cast </param>
        /// <param name="castDirection"> The direction of the ground probing cast </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterColliderCastHitsForAllCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref NativeList<ColliderCastHit> hits,
            bool onlyObstructingHits,
            float3 castDirection,
            bool ignoreDynamicBodies)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            for (var i = hits.Length - 1; i >= 0; i--)
            {
                var hitAccepted = false;
                var hit = hits[i];
                if (hit.Entity != Entity)
                {
                    if (!ignoreDynamicBodies ||
                        !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                    {
                        // ignore hits if we're going away from them
                        var dotRatio = math.dot(hit.SurfaceNormal, castDirection);
                        if (!onlyObstructingHits || dotRatio < -k_DotProductSimilarityEpsilon)
                        {
                            if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                            {
                                hitAccepted = true;
                            }
                        }
                    }
                }

                if (!hitAccepted) hits.RemoveAtSwapBack(i);
            }

            return hits.Length > 0;
        }

        /// <summary>
        /// Filters a list of hits for overlap resolution, and keeps only valid hits. Also returns a variety of closest hits
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="closestHit"> The closest valid hit </param>
        /// <param name="closestDynamicHit"> The closest valid dynamic hit </param>
        /// <param name="closestNonDynamicHit"> The closest valid non-dynamic hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        public void FilterDistanceHitsForSolveOverlaps<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref NativeList<DistanceHit> hits,
            out DistanceHit closestHit,
            out DistanceHit closestDynamicHit,
            out DistanceHit closestNonDynamicHit)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            closestHit = default;
            closestHit.Fraction = float.MaxValue;
            closestDynamicHit = default;
            closestDynamicHit.Fraction = float.MaxValue;
            closestNonDynamicHit = default;
            closestNonDynamicHit.Fraction = float.MaxValue;

            for (var i = hits.Length - 1; i >= 0; i--)
            {
                var hitAccepted = false;
                var hit = hits[i];
                if (hit.Entity != Entity)
                {
                    if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                    {
                        var isBodyDynamic = PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex);

                        if (hit.Distance < closestHit.Distance) closestHit = hit;

                        switch (isBodyDynamic)
                        {
                            case true when hit.Distance < closestDynamicHit.Distance:
                                closestDynamicHit = hit;
                                break;
                            case false when hit.Distance < closestNonDynamicHit.Distance:
                                closestNonDynamicHit = hit;
                                break;
                        }

                        // Keep all dynamic hits in the list (and only those)
                        if (isBodyDynamic) hitAccepted = true;
                    }
                }

                if (!hitAccepted) hits.RemoveAtSwapBack(i);
            }
        }

        /// <summary>
        /// Filters a list of hits and returns the closest valid hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="closestHit"> The closest valid hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterDistanceHitsForClosestCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref NativeList<DistanceHit> hits,
            bool ignoreDynamicBodies,
            out DistanceHit closestHit)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            closestHit = default;
            closestHit.Fraction = float.MaxValue;

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.Distance >= closestHit.Distance) continue;
                if (hit.Entity != Entity)
                {
                    if (!ignoreDynamicBodies ||
                        !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                    {
                        if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                        {
                            closestHit = hit;
                        }
                    }
                }
            }

            return closestHit.Entity != Entity.Null;
        }

        /// <summary>
        /// Filters a list of hits and returns all valid hits
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterDistanceHitsForAllCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref NativeList<DistanceHit> hits,
            bool ignoreDynamicBodies)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            for (var i = hits.Length - 1; i >= 0; i--)
            {
                var hitAccepted = false;
                var hit = hits[i];
                if (hit.Entity != Entity)
                {
                    if (!ignoreDynamicBodies ||
                        !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                    {
                        if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                        {
                            hitAccepted = true;
                        }
                    }
                }

                if (!hitAccepted) hits.RemoveAtSwapBack(i);
            }

            return hits.Length > 0;
        }

        /// <summary>
        /// Filters a list of hits and returns the closest valid hit
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <param name="closestHit"> The closest valid hit </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterRaycastHitsForClosestCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref NativeList<RaycastHit> hits,
            bool ignoreDynamicBodies,
            out RaycastHit closestHit)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            closestHit = default;
            closestHit.Fraction = float.MaxValue;

            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.Fraction >= closestHit.Fraction) continue;
                if (hit.Entity != Entity)
                {
                    if (!ignoreDynamicBodies ||
                        !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                    {
                        if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                        {
                            closestHit = hit;
                        }
                    }
                }
            }

            return closestHit.Entity != Entity.Null;
        }

        /// <summary>
        /// Filters a list of hits and returns all valid hits
        /// </summary>
        /// <param name="processor"> The struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </param>
        /// <param name="context"> The user context struct holding global data meant to be accessed during the character update </param>
        /// <param name="baseContext"> The built-in context struct holding global data meant to be accessed during the character update </param>
        /// <param name="hits"> The list of hits to filter </param>
        /// <param name="ignoreDynamicBodies"> Should the cast ignore dynamic bodies </param>
        /// <typeparam name="T"> The type of the struct implementing <see cref="IKinematicCharacterProcessor{C}"/> </typeparam>
        /// <typeparam name="C"> The type of the user-created context struct </typeparam>
        /// <returns> Whether or not any valid hit remains </returns>
        public bool FilterRaycastHitsForAllCollisions<T, C>(in T processor,
            ref C context,
            ref KinematicCharacterUpdateContext baseContext,
            ref NativeList<RaycastHit> hits,
            bool ignoreDynamicBodies)
            where T : unmanaged, IKinematicCharacterProcessor<C>
            where C : unmanaged
        {
            for (var i = hits.Length - 1; i >= 0; i--)
            {
                var hitAccepted = false;
                var hit = hits[i];
                if (hit.Entity != Entity)
                {
                    if (!ignoreDynamicBodies ||
                        !PhysicsUtilities.IsBodyDynamic(in baseContext.PhysicsWorld, hit.RigidBodyIndex))
                    {
                        if (processor.CanCollideWithHit(ref context, ref baseContext, new BasicHit(hit)))
                        {
                            hitAccepted = true;
                        }
                    }
                }

                if (!hitAccepted) hits.RemoveAtSwapBack(i);
            }

            return hits.Length > 0;
        }
    }
}

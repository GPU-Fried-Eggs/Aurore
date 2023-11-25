using System.Collections.Generic;
using Character.Kinematic;
using Physics;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Manager
{
    public partial class DefaultVariantSystem : DefaultVariantSystemBase
    {
        protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
        {
            defaultVariants.Add(typeof(KinematicCharacterBody), Rule.ForAll(typeof(KinematicCharacterBody_DefaultVariant)));
            defaultVariants.Add(typeof(CharacterInterpolation), Rule.ForAll(typeof(CharacterInterpolation_GhostVariant)));
            defaultVariants.Add(typeof(TrackedTransform), Rule.ForAll(typeof(TrackedTransform_DefaultVariant)));
        }
    }

    [GhostComponentVariation(typeof(KinematicCharacterBody))]
    [GhostComponent]
    public struct KinematicCharacterBody_DefaultVariant
    {
        [GhostField] public float3 RelativeVelocity;
        [GhostField] public bool IsGrounded;

        [GhostField] public Entity ParentEntity;
        [GhostField] public float3 ParentLocalAnchorPoint;
        [GhostField] public float3 ParentVelocity;
    }

    // Character interpolation must only exist on predicted clients:
    // - for remote interpolated ghost characters, interpolation is handled by netcode.
    // - for server, interpolation is superfluous.
    [GhostComponentVariation(typeof(CharacterInterpolation))]
    [GhostComponent(PrefabType = GhostPrefabType.PredictedClient)]
    public struct CharacterInterpolation_GhostVariant { }

    [GhostComponentVariation(typeof(TrackedTransform))]
    [GhostComponent]
    public struct TrackedTransform_DefaultVariant
    {
        [GhostField] public RigidTransform CurrentFixedRateTransform;
    }
}

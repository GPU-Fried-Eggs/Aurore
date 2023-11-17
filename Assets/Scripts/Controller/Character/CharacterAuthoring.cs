using Character.Hyper;
using Character.Kinematic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Utilities;

namespace Character
{
    [DisallowMultipleComponent]
    public class CharacterAuthoring : MonoBehaviour
    {
        public AuthoringKinematicCharacterData CharacterData = AuthoringKinematicCharacterData.GetDefault();
        public CharacterData Character = default;

        [Header("References")]
        public GameObject MeshPrefab;
        public GameObject DefaultCameraTarget;
        public GameObject SwimmingCameraTarget;
        public GameObject MeshRoot;

        [Header("Debug")]
        public bool DebugStandingGeometry;
        public bool DebugSwimmingGeometry;

        private class CharacterBaker : Baker<CharacterAuthoring>
        {
            public override void Bake(CharacterAuthoring authoring)
            {
                KinematicCharacterUtilities.BakeCharacter(this, authoring, authoring.CharacterData);

                authoring.Character.DefaultCameraTargetEntity = GetEntity(authoring.DefaultCameraTarget, TransformUsageFlags.Dynamic);
                authoring.Character.SwimmingCameraTargetEntity = GetEntity(authoring.SwimmingCameraTarget, TransformUsageFlags.Dynamic);
                authoring.Character.MeshRootEntity = GetEntity(authoring.MeshRoot, TransformUsageFlags.Dynamic);

                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, authoring.Character);
                AddComponent(entity, new CharacterControl());
                AddComponent(entity, new CharacterStateMachine());
                AddComponentObject(entity, new CharacterHybridData { MeshPrefab = authoring.MeshPrefab });
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (DebugStandingGeometry)
            {
                Gizmos.color = Color.cyan;
                DrawCapsuleGizmo(Character.StandingGeometry);
            }
            if (DebugSwimmingGeometry)
            {
                Gizmos.color = Color.cyan;
                DrawCapsuleGizmo(Character.SwimmingGeometry);
            }
        }

        private void DrawCapsuleGizmo(CapsuleGeometryDefinition capsuleGeo)
        {
            var characterTransform = new RigidTransform(transform.rotation, transform.position);
            float3 characterUp = transform.up;
            float3 characterFwd = transform.forward;
            float3 characterRight = transform.right;
            var capsuleCenter = math.transform(characterTransform, capsuleGeo.Center);
            var halfHeight = capsuleGeo.Height * 0.5f;

            var bottomHemiCenter = capsuleCenter - (characterUp * (halfHeight - capsuleGeo.Radius));
            var topHemiCenter = capsuleCenter + (characterUp * (halfHeight - capsuleGeo.Radius));

            Gizmos.DrawWireSphere(bottomHemiCenter, capsuleGeo.Radius);
            Gizmos.DrawWireSphere(topHemiCenter, capsuleGeo.Radius);

            Gizmos.DrawLine(bottomHemiCenter + (characterFwd * capsuleGeo.Radius), topHemiCenter + (characterFwd * capsuleGeo.Radius));
            Gizmos.DrawLine(bottomHemiCenter - (characterFwd * capsuleGeo.Radius), topHemiCenter - (characterFwd * capsuleGeo.Radius));
            Gizmos.DrawLine(bottomHemiCenter + (characterRight * capsuleGeo.Radius), topHemiCenter + (characterRight * capsuleGeo.Radius));
            Gizmos.DrawLine(bottomHemiCenter - (characterRight * capsuleGeo.Radius), topHemiCenter - (characterRight * capsuleGeo.Radius));
        }
        
    }
}
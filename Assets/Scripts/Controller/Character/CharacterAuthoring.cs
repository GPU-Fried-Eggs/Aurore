using Character.Hybrid;
using Character.Kinematic;
using Unity.Entities;
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
        public GameObject ClimbingCameraTarget;
        public GameObject CrouchingCameraTarget;
        public GameObject MeshRoot;

        public GameObject SwimmingDetectionPoint;

        private class CharacterBaker : Baker<CharacterAuthoring>
        {
            public override void Bake(CharacterAuthoring authoring)
            {
                KinematicCharacterUtilities.BakeCharacter(this, authoring, authoring.CharacterData);

                authoring.Character.DefaultCameraTargetEntity = GetEntity(authoring.DefaultCameraTarget, TransformUsageFlags.Dynamic);
                authoring.Character.SwimmingCameraTargetEntity = GetEntity(authoring.SwimmingCameraTarget, TransformUsageFlags.Dynamic);
                authoring.Character.ClimbingCameraTargetEntity = GetEntity(authoring.ClimbingCameraTarget, TransformUsageFlags.Dynamic);
                authoring.Character.CrouchingCameraTargetEntity = GetEntity(authoring.CrouchingCameraTarget, TransformUsageFlags.Dynamic);
                authoring.Character.MeshRootEntity = GetEntity(authoring.MeshRoot, TransformUsageFlags.Dynamic);

                authoring.Character.LocalSwimmingDetectionPoint = authoring.SwimmingDetectionPoint.transform.localPosition;

                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, authoring.Character);
                AddComponent(entity, new CharacterControl());
                AddComponent(entity, new CharacterStateMachine());
                AddComponentObject(entity, new CharacterHybridData { MeshPrefab = authoring.MeshPrefab });
            }
        }
    }
}
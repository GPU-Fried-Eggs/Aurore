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

        public GameObject DefaultCameraTarget;

        private class CharacterBaker : Baker<CharacterAuthoring>
        {
            public override void Bake(CharacterAuthoring authoring)
            {
                KinematicCharacterUtilities.BakeCharacter(this, authoring, authoring.CharacterData);

                authoring.Character.DefaultCameraTargetEntity = GetEntity(authoring.DefaultCameraTarget, TransformUsageFlags.Dynamic);

                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, authoring.Character);
                AddComponent(entity, new CharacterControl());
            }
        }
    }
}
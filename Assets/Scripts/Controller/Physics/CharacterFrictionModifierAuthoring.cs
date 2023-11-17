using Unity.Entities;
using UnityEngine;

namespace Physics
{
    public class CharacterFrictionModifierAuthoring : MonoBehaviour
    {
        public float Friction = 1f;

        private class CharacterFrictionModifierBaker : Baker<CharacterFrictionModifierAuthoring>
        {
            public override void Bake(CharacterFrictionModifierAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new CharacterFrictionModifier
                {
                    Friction = authoring.Friction
                });
            }
        }
    }
}

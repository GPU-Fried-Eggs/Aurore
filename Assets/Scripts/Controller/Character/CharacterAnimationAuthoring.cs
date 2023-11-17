using Unity.Entities;
using UnityEngine;

namespace Character
{
    [DisallowMultipleComponent]
    public class CharacterAnimationAuthoring : MonoBehaviour
    {
        private class CharacterAnimationBaker : Baker<CharacterAnimationAuthoring>
        {
            public override void Bake(CharacterAnimationAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
            
                AddComponent(entity, new CharacterAnimation
                {
                    IdleClip = 0,
                    RunClip = 1,
                    SprintClip = 2,
                    InAirClip = 3,
                    SwimmingIdleClip = 4,
                    SwimmingMoveClip = 5
                });
            }
        }
    }
}
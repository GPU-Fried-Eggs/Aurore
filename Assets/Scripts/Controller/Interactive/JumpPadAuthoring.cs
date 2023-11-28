using Unity.Entities;
using UnityEngine;

namespace Interactive
{
    public class JumpPadAuthoring : MonoBehaviour
    {
        public JumpPad JumpPad;

        private class JumpPadBaker : Baker<JumpPadAuthoring>
        {
            public override void Bake(JumpPadAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, authoring.JumpPad);
            }
        }
    }
}
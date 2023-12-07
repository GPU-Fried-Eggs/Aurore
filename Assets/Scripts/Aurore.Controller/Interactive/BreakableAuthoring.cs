using Unity.Entities;
using UnityEngine;

namespace Interactive
{
    public class BreakableAuthoring : MonoBehaviour
    {
        public Breakable Breakable = Breakable.GetDefault();

        private class BreakableBaker : Baker<BreakableAuthoring>
        {
            public override void Bake(BreakableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, authoring.Breakable);
            }
        }
    }
}
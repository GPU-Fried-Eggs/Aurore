using Unity.Entities;
using UnityEngine;

public class SpawnPointAuthoring : MonoBehaviour
{
    private class SpawnPointBaker : Baker<SpawnPointAuthoring>
    {
        public override void Bake(SpawnPointAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new SpawnPoint());
        }
    }
}
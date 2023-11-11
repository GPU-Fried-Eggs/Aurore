using Unity.Entities;
using UnityEngine;

namespace Controller.Character
{
    [DisallowMultipleComponent]
    public class CharacterAuthoring : MonoBehaviour
    {
        private class CharacterBaker : Baker<CharacterAuthoring>
        {
            public override void Bake(CharacterAuthoring authoring)
            {
            }
        }
    }
}
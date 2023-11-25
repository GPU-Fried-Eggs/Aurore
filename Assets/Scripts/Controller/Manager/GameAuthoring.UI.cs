using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Manager
{
    public class GameUIAuthoring : MonoBehaviour
    {
        [Header("UI Document")]
        public UIDocument MenuDocument;

        private void Start()
        {
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<GameUISystem>().SetUIReferences(this);
        }
    }
}

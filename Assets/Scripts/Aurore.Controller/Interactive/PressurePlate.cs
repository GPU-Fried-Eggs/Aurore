using Unity.Entities;

namespace Interactive
{
    public struct PressurePlate : IComponentData
    {
        public bool Activated;
    }
    
    public struct PressurePlateHandleData
    {
        public bool Activated;
        public EntityCommandBuffer EntityCommandBuffer; // for destroy or create
    }
}
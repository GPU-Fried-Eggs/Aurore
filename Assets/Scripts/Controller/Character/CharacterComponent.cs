using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Controller.Character
{
    [Serializable]
    [GhostComponent]
    public struct CharacterComponent : IComponentData
    {
        
    }

    [Serializable]
    public struct CharacterControl : IComponentData
    {
        public float3 MoveVector;
    
        public bool JumpHeld;
        public bool RollHeld;
        public bool SprintHeld;
    
        public bool JumpPressed;
        public bool DashPressed;
        public bool CrouchPressed;
        public bool RopePressed;
        public bool ClimbPressed;
        public bool FlyNoCollisionsPressed;
    }
}
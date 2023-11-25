using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player
{
    [Serializable]
    [GhostComponent]
    public struct PlayerData : IComponentData
    {
        [GhostField] public FixedString128Bytes Name;
        [GhostField] public Entity ControlledCharacter;
        [GhostField] public Entity ControlledCamera;
    }

    [Serializable]
    public struct PlayerInputs : IInputComponentData
    {
        public float2 Move;
        public float2 Look;
        public float CameraZoom;

        public bool SprintHeld;
        public bool JumpHeld;

        public InputEvent JumpPressed;
        public InputEvent GodModePressed;
    }
}

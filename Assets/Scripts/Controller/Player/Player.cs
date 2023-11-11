using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Controller.Player
{
    [Serializable]
    [GhostComponent]
    public struct PlayerData : IComponentData
    {
        [GhostField] public FixedString128Bytes Name;
        [GhostField] public Entity ControlledCharacter;
        
        public NetworkTick LastKnownCommandsTick;
        public PlayerCommands LastKnownCommands;
    }

    [Serializable]
    public struct PlayerCommands : IInputComponentData
    {
        public float2 Move;
        public float2 Look;
        public float CameraZoom;

        public bool JumpHeld;

        public InputEvent JumpPressed;
    }
}
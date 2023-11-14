using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Player
{
    [Serializable]
    public struct PlayerData : IComponentData
    {
        public Entity ControlledCharacter;
        public Entity ControlledCamera;
    }

    [Serializable]
    public struct PlayerInputs : IComponentData
    {
        public float2 Move;
        public float2 Look;
        public float CameraZoom;

        public bool JumpHeld;

        public FixedInputEvent JumpPressed;
    }

    public struct FixedInputEvent
    {
        private byte m_WasEverSet;
        private uint m_LastSetTick;
    
        public void Set(uint tick)
        {
            m_LastSetTick = tick;
            m_WasEverSet = 1;
        }
    
        public bool IsSet(uint tick)
        {
            if (m_WasEverSet == 1)
            {
                return tick == m_LastSetTick;
            }

            return false;
        }
    }
}
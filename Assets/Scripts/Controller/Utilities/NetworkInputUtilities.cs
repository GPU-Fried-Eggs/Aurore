using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Utilities
{
    public static class NetworkInputUtilities
    {
        public const float k_DefaultWrapAroundValue = 1000f;
    
        public static void GetCurrentAndPreviousTick(NetworkTime networkTimeSingleton,
            out NetworkTick currentTick,
            out NetworkTick previousTick)
        {
            currentTick = networkTimeSingleton.ServerTick;
            previousTick = currentTick;
            previousTick.Decrement();
        }

        public static void GetCurrentAndPreviousTickInputs<T>(DynamicBuffer<InputBufferData<T>> inputsBuffer,
            NetworkTick currentTick,
            NetworkTick previousTick,
            out T currentTickInputs,
            out T previousTickInputs)
            where T : unmanaged, IInputComponentData
        {
            currentTickInputs = default;
            previousTickInputs = default;

            if (inputsBuffer.GetDataAtTick(currentTick, out var currentTickInputData))
            {
                currentTickInputs = currentTickInputData.InternalInput;
            }
            if (inputsBuffer.GetDataAtTick(previousTick, out var previousTickInputData))
            {
                previousTickInputs = previousTickInputData.InternalInput;
            }
        }
    
        public static void AddInputDelta(ref float input,
            float addedDelta,
            float wrapAroundValue = k_DefaultWrapAroundValue)
        {
            input = math.fmod(input + addedDelta, wrapAroundValue);
        }
    
        public static float GetInputDelta(float currentTickValue,
            float previousTickValue,
            float wrapAroundValue = k_DefaultWrapAroundValue)
        {
            var delta = currentTickValue - previousTickValue;
        
            // When delta is very large, consider that the input has wrapped around
            if(math.abs(delta) > (wrapAroundValue * 0.5f))
            {
                delta += (math.sign(previousTickValue - currentTickValue) * wrapAroundValue);
            }

            return delta;
        }
    }
}
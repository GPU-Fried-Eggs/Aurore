using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

/// <summary> The collection of forwarding functions that can be executed in a burst job. </summary>
/// <typeparam name="T"> Is the void* data that will be passed to the ExecuteFunction. Also serves as a grouping mechanism for ReflectAll. </typeparam>
public unsafe struct Functions<T> where T : unmanaged
{
    [ReadOnly] private NativeArray<FunctionData> m_Functions;

    /// <summary> Initializes a new instance of the <see cref="Functions{T}"/> struct. </summary>
    /// <param name="functions"> The collection of functions. </param>
    internal Functions(NativeArray<FunctionData> functions)
    {
        this.m_Functions = functions;
    }

    /// <summary> Gets the number of functions for iterating. </summary>
    public int Length => this.m_Functions.Length;

    /// <summary> Call this in OnDestroy on the system to dispose memory. It also calls OnDestroy on all IFunction. </summary>
    /// <param name="state"> The system state. </param>
    public void OnDestroy(ref SystemState state)
    {
        foreach (var d in this.m_Functions)
        {
            if (d.DestroyFunction != IntPtr.Zero)
            {
                Marshal.GetDelegateForFunctionPointer<DestroyFunction>(d.DestroyFunction).Invoke(d.Target, ref state);
            }

            UnsafeUtility.FreeTracked(d.Target, Allocator.Persistent);
        }

        this.m_Functions.Dispose();
    }

    /// <summary> Call in OnUpdate to call OnUpdate on all IFunction. </summary>
    /// <param name="state"> The system state. </param>
    public void Update(ref SystemState state)
    {
        foreach (var d in this.m_Functions)
        {
            if (d.UpdateFunction.IsCreated)
            {
                d.UpdateFunction.Invoke(d.Target, ref state);
            }
        }
    }

    /// <summary> Call to execute a specific function. </summary>
    /// <param name="index"> The index of function to call. Should be positive and less than Length. </param>
    /// <param name="data"> The data to pass to the function. </param>
    /// <returns> A user defined value. Can use 0 as false for example. </returns>
    public int Execute(int index, ref T data)
    {
        ref var e = ref UnsafeUtility.ArrayElementAsRef<FunctionData>(this.m_Functions.GetUnsafePtr(), index);
        var ptr = UnsafeUtility.AddressOf(ref data);
        return e.ExecuteFunction.Invoke(e.Target, ptr);
    }
}

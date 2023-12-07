using System;
using Unity.Burst;

internal unsafe struct FunctionData
{
    public void* Target;

    public IntPtr DestroyFunction;
    public FunctionPointer<ExecuteFunction> ExecuteFunction;
    public FunctionPointer<UpdateFunction> UpdateFunction;
}
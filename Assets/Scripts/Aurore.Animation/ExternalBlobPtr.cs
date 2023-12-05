using System;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

//	Do not use this class in serialization contexts, because internal pointer will be invalidated!
public unsafe struct ExternalBlobPtr<T> where T : unmanaged
{
    [NativeDisableUnsafePtrRestriction] internal void* Ptr;

    public static ExternalBlobPtr<T> Create(ref T obj)
    {
        return new ExternalBlobPtr<T>
        {
            Ptr = UnsafeUtility.AddressOf(ref obj)
        };
    }

    public static ExternalBlobPtr<T> Create(ref BlobPtr<T> obj)
    {
        return new ExternalBlobPtr<T>
        {
            Ptr = obj.GetUnsafePtr()
        };
    }

    public ref T Value
    {
        get
        {
            ValidateNotNull();
            return ref UnsafeUtility.AsRef<T>(Ptr);
        }
    }

    public bool IsCreated => Ptr != null;

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    public void ValidateNotNull()
    {
        if (Ptr == null)
            throw new InvalidOperationException("The BlobAssetReference is null.");
    }
}
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public static class CollectionUtils
{
	public static unsafe NativeArray<T> AsArray<T>(this NativeSlice<T> v) where T: unmanaged
	{
		var ptr = v.GetUnsafePtr();
		return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, v.Length, Allocator.None);
	}

	public static unsafe UnsafeList<T> AsUnsafeList<T>(this NativeSlice<T> v) where T: unmanaged
	{
		var ptr = (T*)v.GetUnsafePtr();
		return new UnsafeList<T>(ptr, v.Length);
	}

	static void ValidateSpanCreationParameters<T>(this NativeList<T> v, int startIndex, int length) where T: unmanaged
	{
		if (startIndex >= v.Length)
		{
			throw new InvalidOperationException($"Requested span start index exceed list size (Start index {startIndex}, list length {v.Length})!");
		}

		if (startIndex + length > v.Length)
		{
			throw new InvalidOperationException($"Requested span exceed end of list (Start index {startIndex}, requested length {length}, list length {v.Length})!");
		}
	}

	public static unsafe Span<T> GetSpan<T>(this NativeList<T> v, int startIndex, int length) where T: unmanaged
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		ValidateSpanCreationParameters(v, startIndex, length);
#endif
		return new Span<T>(v.GetUnsafePtr() + startIndex, length);
	}

	public static unsafe ReadOnlySpan<T> GetReadOnlySpan<T>(this NativeList<T> v, int startIndex, int length) where T: unmanaged
	{
#if ENABLE_UNITY_COLLECTIONS_CHECKS
		ValidateSpanCreationParameters(v, startIndex, length);
#endif
		return new ReadOnlySpan<T>(v.GetUnsafeReadOnlyPtr() + startIndex, length);
	}

    public static unsafe Span<T> AsSpan<T>(this UnsafeList<T> l) where T: unmanaged
    {
	    return new Span<T>(l.Ptr, l.Length);
    }
}
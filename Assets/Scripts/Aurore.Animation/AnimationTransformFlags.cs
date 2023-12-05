using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public struct AnimationTransformFlags
{
	private UnsafeBitArray m_TransformFlags;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsTranslationSet(int index) => m_TransformFlags.IsSet(index * 4 + 0);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsRotationSet(int index) => m_TransformFlags.IsSet(index * 4 + 1);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsScaleSet(int index) => m_TransformFlags.IsSet(index * 4 + 2);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool IsAbsoluteTransform(int index) => m_TransformFlags.IsSet(index * 4 + 3);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SetTranslationFlag(int index) => m_TransformFlags.SetBitThreadSafe(index * 4 + 0);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SetRotationFlag(int index) => m_TransformFlags.SetBitThreadSafe(index * 4 + 1);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SetScaleFlag(int index) => m_TransformFlags.SetBitThreadSafe(index * 4 + 2);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SetAbsoluteTransformFlag(int index) => m_TransformFlags.SetBitThreadSafe(index * 4 + 3);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ResetAllFlags() => m_TransformFlags.Clear();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe UnsafeBitArray GetRigTransformFlagsInternal(void* ptr, int bufLenInBytes, int ulongOffset, int boneCount)
	{
		//	Each bone contains 4 bit flags - TRS and is bone already in absolute transform
		var sizeInUlongs = (boneCount * 4 >> 6) + 1;
		var sizeInBytes = sizeInUlongs * 8;
		var startPtr = (ulong*)ptr + ulongOffset;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
		if ((byte*)startPtr + sizeInBytes > (byte*)ptr + bufLenInBytes)
			throw new InvalidOperationException($"Buffer range error! Offset and/or count exceed buffer space!");
#endif

		return new UnsafeBitArray(startPtr, sizeInUlongs * 8, Allocator.None);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe AnimationTransformFlags CreateFromBufferRW(in NativeList<ulong> buf, int bufElementOffset, int boneCount)
	{
		var rwPtr = buf.GetUnsafePtr();

		return new AnimationTransformFlags
		{
			m_TransformFlags = GetRigTransformFlagsInternal(rwPtr, buf.Length * sizeof(ulong), bufElementOffset, boneCount)
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe AnimationTransformFlags CreateFromBufferRO(in NativeList<ulong> buf, int bufElementOffset, int boneCount)
	{
		var roPtr = buf.GetUnsafeReadOnlyPtr();

		return new AnimationTransformFlags
		{
			m_TransformFlags = GetRigTransformFlagsInternal(roPtr, buf.Length * sizeof(ulong), bufElementOffset, boneCount)
		};
	}
}
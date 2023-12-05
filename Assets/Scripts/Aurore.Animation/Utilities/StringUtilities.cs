using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Hash128 = Unity.Entities.Hash128;
using FixedStringName = Unity.Collections.FixedString512Bytes;

public static class FixedStringExtensions
{
	public static unsafe Hash128 CalculateHash128(in this FixedStringName s)
	{
		if (s.IsEmpty)
			return default;

		var hasher = new xxHash3.StreamingState();
		hasher.Update(s.GetUnsafePtr(), s.Length);
		var rv = new Hash128(hasher.DigestHash128());
		return rv;
	}

	public static unsafe uint CalculateHash32(in this FixedStringName s)
	{
		if (s.IsEmpty)
			return default;

		var rv = XXHash.Hash32(s.GetUnsafePtr(), s.Length);
		return rv;
	}
}

public static class BlobStringExtensions
{
	public static unsafe Hash128 CalculateHash128(ref this BlobString s)
	{
		var hasher = new xxHash3.StreamingState();
		//	BlobString internally is just BlobArray, so do a little C++ magic here and reinterpret BlobString as BlobArray (with hope that first member of former will remain as its data)
		ref var stringAsArr = ref UnsafeUtility.As<BlobString, BlobArray<byte>>(ref s);
		//	Ignoring trailing zero byte
		hasher.Update(stringAsArr.GetUnsafePtr(), stringAsArr.Length - 1);
		var rv = new Hash128(hasher.DigestHash128());
		return rv;
	}

	public static FixedStringName ToFixedString(ref this BlobString s)
	{
		var rv = new FixedStringName();
		s.CopyTo(ref rv);
		return rv;
	}
}
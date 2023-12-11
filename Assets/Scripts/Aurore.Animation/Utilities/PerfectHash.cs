using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Hash128 = Unity.Entities.Hash128;
using Random = Unity.Mathematics.Random;
	
public interface IPerfectHashedValue
{
	uint HashFunc(int prime, int key);

	uint InitialHash();
}
	
public struct UIntPerfectHashed: IPerfectHashedValue
{
	public uint Value;

	public UIntPerfectHashed(uint value)
	{
		Value = value;
	}

	public uint HashFunc(int prime, int key) => (uint)(key * Value % prime);

	public uint InitialHash() => Value;

	public static implicit operator UIntPerfectHashed(uint v) => new UIntPerfectHashed(v);
}

public struct Hash128PerfectHashed: IPerfectHashedValue
{
	public Hash128 Value;

	public Hash128PerfectHashed(Hash128 value)
	{
		Value = value;
	} 

	public uint HashFunc(int prime, int key) => (uint)(key * math.hash(Value.Value) % prime);

	public uint InitialHash() => math.hash(Value.Value);

	public static implicit operator Hash128PerfectHashed(Hash128 v) => new Hash128PerfectHashed(v);
}

internal static class PerfectHashPrimes
{
	public static NativeArray<int2> CreatePerfectHashPrimes(int numberOfPrimes = 0xffff)
	{
		var hashPrimesArray = new NativeArray<int2>(numberOfPrimes, Allocator.Temp);
		var randomGenerator = new Random((uint)numberOfPrimes);
		var currentPrimeNumber = PerfectHash<UIntPerfectHashed>.InitialPrime;

		for (var i = 0; i < numberOfPrimes; ++i)
		{
			currentPrimeNumber = NextPrime(currentPrimeNumber + 1);
			hashPrimesArray[i] = new int2(currentPrimeNumber, (int)(randomGenerator.NextUInt() & 0x7fffffff));
		}

		return hashPrimesArray;
	}

	private static bool IsPrime(int p)
	{
		if (p % 2 == 0) return false;
		for (var d = 3; d * d <= p; d += 2)
		{
			if (p % d == 0) return false;
		}
		return true;
	}

	private static int NextPrime(int p)
	{
		if (p <= 2) return 2;
		for (var l = p | 1; ; l += 2)
			if (IsPrime(l))
				return l;
	}
}

public class PerfectHash<T> where T: unmanaged, IPerfectHashedValue
{
	public const int InitialPrime = 100003;

	public static bool CreateMinimalPerfectHash(in NativeArray<T> dataArray, out NativeList<int2> seedValues, out NativeList<int> shuffleIndices)
	{
		var primesArray = PerfectHashPrimes.CreatePerfectHashPrimes();
		var dataSize = dataArray.Length;

		var buckets = new NativeArray<int>(dataSize * dataSize, Allocator.Temp).AsSpan();
		var bucketsCount = new NativeArray<int2>(dataSize, Allocator.Temp).AsSpan();
		var hashesArray = new NativeArray<uint>(dataSize, Allocator.Temp);
		seedValues = new NativeList<int2>(dataSize, Allocator.Temp);
		shuffleIndices = new NativeList<int>(dataSize, Allocator.Temp);

		for (var l = 0; l < bucketsCount.Length; ++l)
		{
			bucketsCount[l] = new int2(l, 0);
		}

		for (var i = 0; i < dataSize; ++i)
		{
			var v = dataArray[i];
			var h = v.InitialHash();
			var k = (int)(h % dataSize);
			buckets[k * dataSize + bucketsCount[k].y++] = i;

			hashesArray[i] = h;
		}

		//	Check for uniqueness of value hashes
		if (!CheckForUniqueness(hashesArray)) return false;

		//	Simple bubble sort
		for (var i = 0; i < bucketsCount.Length - 1; ++i)
		{
			for (var l = 0; l < bucketsCount.Length - i - 1; ++l)
			{
				if (bucketsCount[l].y < bucketsCount[l + 1].y)
				{
					(bucketsCount[l], bucketsCount[l + 1]) = (bucketsCount[l + 1], bucketsCount[l]);
				}
			}
		}

		var freeList = new NativeArray<int>(dataSize, Allocator.Temp).AsSpan();

		var seedValue = new int2(-dataSize, 0);
		for (var i = 0; i < dataSize; ++i)
		{
			seedValues.Add(seedValue);
			shuffleIndices.Add(-1);
		}

		var bucketIdx = 0;
		for (; bucketIdx < bucketsCount.Length && bucketsCount[bucketIdx].y > 1; ++bucketIdx)
		{
			var seed = 0;
			var l = 0;
			var bucketInfo = bucketsCount[bucketIdx];

			//	Skip buckets with less than two items
			ResetFreeList(ref freeList);

			const int maxNumIterations = 0xffff;

			int2 primeAndRnd = 0;
			while (l < bucketInfo.y && seed < maxNumIterations)
			{
				var itemIndex = buckets[bucketInfo.x * dataSize + l];
				var item = dataArray[itemIndex];
				primeAndRnd = primesArray[seed];
				var slotIndex = (int)(item.HashFunc(primeAndRnd.x, primeAndRnd.y) % dataSize);
				if (freeList[slotIndex] >= 0 || shuffleIndices[slotIndex] >= 0)
				{
					ResetFreeList(ref freeList);
					l = 0;
					seed++;
				}
				else
				{
					freeList[slotIndex] = itemIndex;
					l++;
				}
			}
			
			Assert.IsTrue(seed < maxNumIterations);

			seedValues[bucketInfo.x] = primeAndRnd;
			for (var k = 0; k < freeList.Length; ++k)
			{
				var f = freeList[k];
				if (f < 0) continue;

				shuffleIndices[k] = f;
			}
		}

		//	Add buckets with one element
		for (var i = bucketIdx; i < bucketsCount.Length && bucketsCount[i].y > 0; ++i)
		{
			var bucketInfo = bucketsCount[i];
			var bucketItemIndex = buckets[bucketInfo.x * dataSize];

			var freeSlotIndex = shuffleIndices.IndexOf(-1);
			var seedVal = new int2(-freeSlotIndex - 1, 0);
			seedValues[bucketInfo.x] = seedVal;
			Assert.IsTrue(shuffleIndices[freeSlotIndex] == -1);
			shuffleIndices[freeSlotIndex] = bucketItemIndex;
		}

		return true;
	}

	private static bool CheckForUniqueness(NativeArray<uint> hashesArray)
	{
		var origCount = hashesArray.Length;
		hashesArray.Sort();
		var uniqueCount = hashesArray.Unique();

		if (origCount > uniqueCount)
		{
			Debug.LogError($"Input values do not produce unique hashes. Check input array for duplicated values. Creation of perfect hash table is failed!");
			return false;
		}

		return true;
	}

	public static unsafe int QueryPerfectHashTable(in NativeList<int2> seedTable, T hashedValue)
	{
		var tablePtr = seedTable.GetUnsafePtr();
		var seedTableArray = new Span<int2>(tablePtr, seedTable.Length);
		var paramIdx = QueryPerfectHashTable(seedTableArray, hashedValue);
		return paramIdx;
	}

	public static unsafe int QueryPerfectHashTable(ref BlobArray<int2> seedTable, T hashedValue)
	{
		var tablePtr = seedTable.GetUnsafePtr();
		var seedTableArray = new Span<int2>(tablePtr, seedTable.Length);
		var paramIdx = QueryPerfectHashTable(seedTableArray, hashedValue);
		return paramIdx;
	}

	public static int QueryPerfectHashTable(ReadOnlySpan<int2> seedTable, T hashedValue)
	{
		var initialHash = hashedValue.InitialHash();
		var initialIdx = (int)(initialHash % seedTable.Length);
		var displacement = seedTable[initialIdx];
		if (displacement.x < 0) return -displacement.x - 1;

		var hashedIdx = hashedValue.HashFunc(displacement.x, displacement.y);
		return (int)(hashedIdx % seedTable.Length);
	}

	static void ResetFreeList(ref Span<int> freeList)
	{
		for (var k = 0; k < freeList.Length; ++k)
			freeList[k] = -1;
	}
}
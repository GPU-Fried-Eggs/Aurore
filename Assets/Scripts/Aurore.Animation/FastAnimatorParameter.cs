using System;
using Unity.Entities;
using FixedStringName = Unity.Collections.FixedString512Bytes;

#if AURORE_DEBUG
using UnityEngine;
#endif

public struct FastAnimatorParameter
{
	public FixedStringName ParamName;
	public uint Hash;

	public FastAnimatorParameter(FixedStringName name)
	{
		Hash = name.CalculateHash32();
		ParamName = name;
	}
	
	public FastAnimatorParameter(uint hash)
	{
		Hash = hash;
		ParamName = default;
	}

	bool GetRuntimeParameterDataInternal(int paramIdx, DynamicBuffer<AnimatorControllerParameterComponent> runtimeParameters, out ParameterValue outData)
	{
		bool isValid = paramIdx >= 0;

		if (isValid)
		{
			outData = runtimeParameters[paramIdx].Value;
		}
		else
		{
			outData = default;
		#if AURORE_DEBUG
			Debug.LogError($"Could find animator parameter with name {ParamName} in hash table! Returning default value!");
		#endif
		}
		return isValid;
	}

	public bool GetRuntimeParameterData(BlobAssetReference<ParameterPerfectHashTableBlob> cb, DynamicBuffer<AnimatorControllerParameterComponent> runtimeParameters, out ParameterValue outData)
	{
		var paramIdx = GetRuntimeParameterIndex(cb, runtimeParameters);
		return GetRuntimeParameterDataInternal(paramIdx, runtimeParameters, out outData);
	}

	public bool GetRuntimeParameterData(DynamicBuffer<AnimatorControllerParameterComponent> runtimeParameters, out ParameterValue outData)
	{
		var paramIdx = GetRuntimeParameterIndex(runtimeParameters);
		return GetRuntimeParameterDataInternal(paramIdx, runtimeParameters, out outData);
	}

	bool SetRuntimeParameterDataInternal(int paramIdx, DynamicBuffer<AnimatorControllerParameterComponent> runtimeParameters, in ParameterValue paramData)
	{
		bool isValid = paramIdx >= 0;

		if (isValid)
		{
			var p = runtimeParameters[paramIdx];
			p.Value = paramData;
			runtimeParameters[paramIdx] = p;
		}
	#if AURORE_DEBUG
		else
		{
			Debug.LogError($"Could find animator parameter with name {ParamName} in hash table! Setting value is failed!");
		}
	#endif
		return isValid;
	}

	public bool SetRuntimeParameterData(BlobAssetReference<ParameterPerfectHashTableBlob> cb, DynamicBuffer<AnimatorControllerParameterComponent> runtimeParameters, in ParameterValue paramData)
	{
		var paramIdx = GetRuntimeParameterIndex(cb, runtimeParameters);
		return SetRuntimeParameterDataInternal(paramIdx, runtimeParameters, paramData);
	}

	public bool SetRuntimeParameterData(DynamicBuffer<AnimatorControllerParameterComponent> runtimeParameters, in ParameterValue paramData)
	{
		var paramIdx = GetRuntimeParameterIndex(runtimeParameters);
		return SetRuntimeParameterDataInternal(paramIdx, runtimeParameters, paramData);
	}

	//	Linear search variant
	public static int GetRuntimeParameterIndex(uint hash, in ReadOnlySpan<AnimatorControllerParameterComponent> parameters)
	{
		for (int i = 0; i < parameters.Length; ++i)
		{
			var p = parameters[i];
			if (p.Hash == hash)
				return i;
		}
		return -1;
	}

	//	Perfect hash table variant
	public static int GetRuntimeParameterIndex(uint hash, in BlobAssetReference<ParameterPerfectHashTableBlob> cb, in ReadOnlySpan<AnimatorControllerParameterComponent> parameters)
	{
		ref var seedTable = ref cb.Value.SeedTable;
		var paramIdxShuffled = PerfectHash<UIntPerfectHashed>.QueryPerfectHashTable(ref seedTable, hash);

		if (paramIdxShuffled >= parameters.Length)
			return -1;

		var paramIdx = cb.Value.IndirectionTable[paramIdxShuffled];

		var p = parameters[paramIdx];
		if (p.Hash != hash)
			return -1;

		return paramIdx;
	}

	public unsafe int GetRuntimeParameterIndex(in BlobAssetReference<ParameterPerfectHashTableBlob> cb, in DynamicBuffer<AnimatorControllerParameterComponent> acpc)
	{
		var span = new ReadOnlySpan<AnimatorControllerParameterComponent>(acpc.GetUnsafePtr(), acpc.Length);
		return GetRuntimeParameterIndex(Hash, cb, span);
	}

	public unsafe int GetRuntimeParameterIndex(in DynamicBuffer<AnimatorControllerParameterComponent> acpc)
	{
		var span = new ReadOnlySpan<AnimatorControllerParameterComponent>(acpc.GetUnsafePtr(), acpc.Length);
		return GetRuntimeParameterIndex(Hash, span);
	}
}
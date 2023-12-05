using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct RuntimeAnimationData: IComponentData
{
    public NativeList<BoneTransform> AnimatedBonesBuffer;
    public NativeParallelHashMap<Entity, int2> EntityToDataOffsetMap;
    public NativeList<int3> BoneToEntityArr;
	public NativeList<ulong> BoneTransformFlagsHolderArr;

	public static RuntimeAnimationData MakeDefault()
	{
		return new RuntimeAnimationData
		{
			AnimatedBonesBuffer = new NativeList<BoneTransform>(Allocator.Persistent),
			EntityToDataOffsetMap = new NativeParallelHashMap<Entity, int2>(128, Allocator.Persistent),
			BoneToEntityArr = new NativeList<int3>(Allocator.Persistent),
			BoneTransformFlagsHolderArr = new NativeList<ulong>(Allocator.Persistent)
		};
	}

	public void Dispose()
	{
		AnimatedBonesBuffer.Dispose();
		EntityToDataOffsetMap.Dispose();
		BoneToEntityArr.Dispose();
		BoneTransformFlagsHolderArr.Dispose();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int2 CalculateBufferOffset(in NativeParallelHashMap<Entity, int2> entityToDataOffsetMap, Entity animatedRigEntity)
	{
		if (!entityToDataOffsetMap.TryGetValue(animatedRigEntity, out var offset))
			return -1;

		return offset;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ReadOnlySpan<BoneTransform> GetAnimationDataForRigRO(in NativeList<BoneTransform> animatedBonesBuffer, int offset, int length)
	{
		return animatedBonesBuffer.GetReadOnlySpan(offset, length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<BoneTransform> GetAnimationDataForRigRW(in NativeList<BoneTransform> animatedBonesBuffer, int offset, int length)
	{
		return animatedBonesBuffer.GetSpan(offset, length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ReadOnlySpan<BoneTransform> GetAnimationDataForRigRO(in NativeList<BoneTransform> animatedBonesBuffer, in NativeParallelHashMap<Entity, int2> entityToDataOffsetMap, in RigDefinitionComponent rdc, Entity animatedRigEntity)
	{
		var offset = CalculateBufferOffset(entityToDataOffsetMap, animatedRigEntity);
		if (offset.x < 0)
			return default;
			
		return GetAnimationDataForRigRO(animatedBonesBuffer, offset.x, rdc.RigBlob.Value.Bones.Length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<BoneTransform> GetAnimationDataForRigRW(in NativeList<BoneTransform> animatedBonesBuffer, in NativeParallelHashMap<Entity, int2> entityToDataOffsetMap, in RigDefinitionComponent rdc, Entity animatedRigEntity)
	{
		var offset = CalculateBufferOffset(entityToDataOffsetMap, animatedRigEntity);
		if (offset.x < 0)
			return default;
			
		return GetAnimationDataForRigRW(animatedBonesBuffer, offset.x, rdc.RigBlob.Value.Bones.Length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static AnimationTransformFlags GetAnimationTransformFlagsRO(in NativeList<int3> boneToEntityArr, in NativeList<ulong> boneTransformFlagsArr, int globalBoneIndex, int boneCount)
	{
		var boneInfo = boneToEntityArr[globalBoneIndex];
		var rv = AnimationTransformFlags.CreateFromBufferRO(boneTransformFlagsArr, boneInfo.z, boneCount);
		return rv;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static AnimationTransformFlags GetAnimationTransformFlagsRW(in NativeList<int3> boneToEntityArr, in NativeList<ulong> boneTransformFlagsArr, int globalBoneIndex, int boneCount)
	{
		var boneInfo = boneToEntityArr[globalBoneIndex];
		return AnimationTransformFlags.CreateFromBufferRW(boneTransformFlagsArr, boneInfo.z, boneCount);
	}
}
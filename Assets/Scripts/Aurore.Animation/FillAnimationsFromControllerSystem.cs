using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[DisableAutoCreation]
[BurstCompile]
[RequireMatchingQueriesForUpdate]
public partial struct FillAnimationsFromControllerSystem: ISystem
{
	private EntityQuery m_FillAnimationsBufferQuery;

	private BufferTypeHandle<AnimatorControllerLayerComponent> m_ControllerLayersBufferHandle;
	private BufferTypeHandle<AnimatorControllerParameterComponent> m_ControllerParametersBufferHandle;
	private BufferTypeHandle<AnimationToProcessComponent> m_AnimationToProcessBufferHandle;
	private EntityTypeHandle m_EntityTypeHandle;

	[BurstCompile]
	public void OnCreate(ref SystemState state)
	{
		var builder = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<AnimatorControllerLayerComponent, AnimationToProcessComponent>();

		m_FillAnimationsBufferQuery = state.GetEntityQuery(builder);

		m_ControllerLayersBufferHandle = state.GetBufferTypeHandle<AnimatorControllerLayerComponent>(true);
		m_ControllerParametersBufferHandle = state.GetBufferTypeHandle<AnimatorControllerParameterComponent>(true);
		m_AnimationToProcessBufferHandle = state.GetBufferTypeHandle<AnimationToProcessComponent>();
	}

	[BurstCompile]
	public void OnUpdate(ref SystemState state)
	{
		m_ControllerLayersBufferHandle.Update(ref state);
		m_ControllerParametersBufferHandle.Update(ref state);
		m_AnimationToProcessBufferHandle.Update(ref state);
		m_EntityTypeHandle.Update(ref state);

		var fillAnimationsBufferJob = new FillAnimationsBufferJob
		{
			ControllerLayersBufferHandle = m_ControllerLayersBufferHandle,
			ControllerParametersBufferHandle = m_ControllerParametersBufferHandle,
			AnimationToProcessBufferHandle = m_AnimationToProcessBufferHandle,
			EntityTypeHandle = m_EntityTypeHandle,
		};

		state.Dependency = fillAnimationsBufferJob.ScheduleParallel(m_FillAnimationsBufferQuery, state.Dependency);
	}
	
	[BurstCompile]
	private partial struct FillAnimationsBufferJob: IJobChunk
	{
		[ReadOnly] public BufferTypeHandle<AnimatorControllerLayerComponent> ControllerLayersBufferHandle;
		[ReadOnly] public BufferTypeHandle<AnimatorControllerParameterComponent> ControllerParametersBufferHandle;
		[ReadOnly] public EntityTypeHandle EntityTypeHandle;
	
		public BufferTypeHandle<AnimationToProcessComponent> AnimationToProcessBufferHandle;

		public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
		{
			var layerBuffers = chunk.GetBufferAccessor(ref ControllerLayersBufferHandle);
			var parameterBuffers = chunk.GetBufferAccessor(ref ControllerParametersBufferHandle);
			var animationsToProcessBuffers = chunk.GetBufferAccessor(ref AnimationToProcessBufferHandle);
			var entities = chunk.GetNativeArray(EntityTypeHandle);
	
			var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
	
			while (cee.NextEntityIndex(out var i))
			{
				var layers = layerBuffers[i].AsNativeArray();
				var parameters = parameterBuffers.Length > 0 ? parameterBuffers[i].AsNativeArray() : default;
				var entity = entities[i];
	
				var animsBuf = animationsToProcessBuffers[i];
	
				AddAnimationsForEntity(ref animsBuf, layers, entity, parameters);
			}
		}

		private void AnimationsPostSetup(Span<AnimationToProcessComponent> animations, ref LayerBlob layer, int layerIndex, float weightMultiplier, float layerWeight)
		{
			//	Set blending mode and adjust animations weight according to layer weight
			for (var k = 0; k < animations.Length; ++k)
			{
				var animation = animations[k];
				animation.BlendMode = layer.BlendingMode;
				animation.LayerWeight = layerWeight;
				animation.LayerIndex = layerIndex;
				animation.Weight *= weightMultiplier;
				if (layer.AvatarMask.Hash.IsValid)
					animation.AvatarMask = ExternalBlobPtr<AvatarMaskBlob>.Create(ref layer.AvatarMask);
				animations[k] = animation;
			}
		}

		private unsafe void AddAnimationsForEntity(ref DynamicBuffer<AnimationToProcessComponent> animations,
			in NativeArray<AnimatorControllerLayerComponent> runtimeLayers,
			Entity deformedEntity,
			in NativeArray<AnimatorControllerParameterComponent> runtimeParams)
		{
			if (deformedEntity == Entity.Null) return;
	
			animations.Clear();
	
			for (var i = 0; i < runtimeLayers.Length; ++i)
			{
				var animationCurIndex = animations.Length;
	
				var runtimeLayer = runtimeLayers[i];
				ref var cb = ref runtimeLayer.Controller;
				ref var lb = ref cb.Value.Layers[i];
				if (runtimeLayer.Weight == 0 || runtimeLayer.Rtd.SrcState.Id < 0)
					continue;
	
				ref var srcStateBlob = ref lb.States[runtimeLayer.Rtd.SrcState.Id];
	
				var srcStateWeight = 1.0f;
				var dstStateWeight = 0.0f;
	
				if (runtimeLayer.Rtd.ActiveTransition.Id >= 0)
				{
					dstStateWeight = runtimeLayer.Rtd.ActiveTransition.NormalizedDuration;
					srcStateWeight = (1 - dstStateWeight);
				}
	
				var srcStateTime = GetDurationTime(ref srcStateBlob, runtimeParams, runtimeLayer.Rtd.SrcState.NormalizedDuration);
	
				var dstStateAnimCount = 0;
				if (runtimeLayer.Rtd.DstState.Id >= 0)
				{
					ref var dstStateBlob = ref lb.States[runtimeLayer.Rtd.DstState.Id];
					var dstStateTime = GetDurationTime(ref dstStateBlob, runtimeParams, runtimeLayer.Rtd.DstState.NormalizedDuration);
					dstStateAnimCount = AddMotionForEntity(ref animations, ref dstStateBlob.Motion, runtimeParams, 1, dstStateTime);
				}
				var srcStateAnimCount = AddMotionForEntity(ref animations, ref srcStateBlob.Motion, runtimeParams, 1, srcStateTime);
	
				var animStartPtr = (AnimationToProcessComponent*)animations.GetUnsafePtr() + animationCurIndex;
				var dstAnimsSpan = new Span<AnimationToProcessComponent>(animStartPtr, dstStateAnimCount);
				var srcAnimsSpan = new Span<AnimationToProcessComponent>(animStartPtr + dstStateAnimCount, srcStateAnimCount);
	
				var dstLayerMultiplier = math.select(dstStateWeight, 1, srcStateAnimCount > 0);
				var srcLayerMultiplier = math.select(srcStateWeight, 1, dstStateAnimCount > 0);
				dstStateWeight = math.select(1, dstStateWeight, srcStateAnimCount > 0);
				srcStateWeight = math.select(1, srcStateWeight, dstStateAnimCount > 0);
	
				AnimationsPostSetup(dstAnimsSpan, ref lb, i, dstStateWeight, dstLayerMultiplier * runtimeLayer.Weight);
				AnimationsPostSetup(srcAnimsSpan, ref lb, i, srcStateWeight, srcLayerMultiplier * runtimeLayer.Weight);
			}
		}

		private void AddAnimationForEntity(ref DynamicBuffer<AnimationToProcessComponent> outAnims, ref MotionBlob mb, float weight, float normalizedStateTime)
		{
			var animation = new AnimationToProcessComponent();
	
			if (mb.AnimationBlob.IsValid)
				animation.Animation = ExternalBlobPtr<AnimationClipBlob>.Create(ref mb.AnimationBlob);
	
			animation.Weight = weight;
			animation.Time = normalizedStateTime;
			outAnims.Add(animation);
		}

		private void AddMotionsFromBlendtree(in NativeList<MotionIndexAndWeight> motionIndexAndWeights,
			ref DynamicBuffer<AnimationToProcessComponent> outAnims,
			in NativeArray<AnimatorControllerParameterComponent> runtimeParams,
			ref BlobArray<ChildMotionBlob> motions,
			float weight,
			float normalizedStateTime)
		{
			for (var i = 0; i < motionIndexAndWeights.Length; ++i)
			{
				var miw = motionIndexAndWeights[i];
				ref var childMotion = ref motions[miw.MotionIndex];
				AddMotionForEntity(ref outAnims, ref childMotion.Motion, runtimeParams, weight * miw.Weight, normalizedStateTime);
			}
		}

		private int AddMotionForEntity(ref DynamicBuffer<AnimationToProcessComponent> outAnims,
			ref MotionBlob motion,
			in NativeArray<AnimatorControllerParameterComponent> runtimeParams,
			float weight,
			float normalizedStateTime)
		{
			var startLen = outAnims.Length;
			var blendTreeMotionsAndWeights = new NativeList<MotionIndexAndWeight>(Allocator.Temp);
	
			switch (motion.MotionType)
			{
				case MotionBlob.Type.None:
					break;
				case MotionBlob.Type.AnimationClip:
					AddAnimationForEntity(ref outAnims, ref motion, weight, normalizedStateTime);
					break;
				case MotionBlob.Type.BlendTreeDirect:
					blendTreeMotionsAndWeights = AnimatorControllerJob.StateMachineProcessJob.GetBlendTreeDirectCurrentMotions(ref motion, runtimeParams);
					break;
				case MotionBlob.Type.BlendTree1D:
					blendTreeMotionsAndWeights = AnimatorControllerJob.StateMachineProcessJob.GetBlendTree1DCurrentMotions(ref motion, runtimeParams);
					break;
				case MotionBlob.Type.BlendTree2DSimpleDirectional:
					blendTreeMotionsAndWeights = AnimatorControllerJob.StateMachineProcessJob.GetBlendTree2DSimpleDirectionalCurrentMotions(ref motion, runtimeParams);
					break;
				case MotionBlob.Type.BlendTree2DFreeformCartesian:
					blendTreeMotionsAndWeights = AnimatorControllerJob.StateMachineProcessJob.GetBlendTree2DFreeformCartesianCurrentMotions(ref motion, runtimeParams);
					break;
				case MotionBlob.Type.BlendTree2DFreeformDirectional:
					blendTreeMotionsAndWeights = AnimatorControllerJob.StateMachineProcessJob.GetBlendTree2DFreeformDirectionalCurrentMotions(ref motion, runtimeParams);
					break;
			}
	
			if (blendTreeMotionsAndWeights.IsCreated)
			{
				AddMotionsFromBlendtree(blendTreeMotionsAndWeights, ref outAnims, runtimeParams, ref motion.BlendTree.Motions, weight, normalizedStateTime);
			}
	
			return outAnims.Length - startLen;
		}

		private float GetDurationTime(ref StateBlob state, in NativeArray<AnimatorControllerParameterComponent> runtimeParams, float normalizedDuration)
		{
			var timeDuration = normalizedDuration;
			if (state.TimeParameterIndex >= 0)
			{
				timeDuration = runtimeParams[state.TimeParameterIndex].FloatValue;
			}

			var stateCycleOffset = state.CycleOffset;
			if (state.CycleOffsetParameterIndex >= 0)
			{
				stateCycleOffset = runtimeParams[state.CycleOffsetParameterIndex].FloatValue;
			}

			timeDuration += stateCycleOffset;

			return timeDuration;
		}
	}	
}
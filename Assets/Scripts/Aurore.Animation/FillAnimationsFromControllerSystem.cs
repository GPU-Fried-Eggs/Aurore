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
	public void OnCreate(ref SystemState ss)
	{
		var builder = new EntityQueryBuilder(Allocator.Temp)
			.WithAllRW<AnimatorControllerLayerComponent>()
			.WithAllRW<AnimationToProcessComponent>();
		m_FillAnimationsBufferQuery = ss.GetEntityQuery(builder);

		m_ControllerLayersBufferHandle = ss.GetBufferTypeHandle<AnimatorControllerLayerComponent>();
		m_ControllerParametersBufferHandle = ss.GetBufferTypeHandle<AnimatorControllerParameterComponent>();
		m_AnimationToProcessBufferHandle = ss.GetBufferTypeHandle<AnimationToProcessComponent>();
	}

	[BurstCompile]
	public void OnUpdate(ref SystemState ss)
	{
		m_ControllerLayersBufferHandle.Update(ref ss);
		m_ControllerParametersBufferHandle.Update(ref ss);
		m_AnimationToProcessBufferHandle.Update(ref ss);
		m_EntityTypeHandle.Update(ref ss);

		var fillAnimationsBufferJob = new FillAnimationsBufferJob()
		{
			ControllerLayersBufferHandle = m_ControllerLayersBufferHandle,
			ControllerParametersBufferHandle = m_ControllerParametersBufferHandle,
			AnimationToProcessBufferHandle = m_AnimationToProcessBufferHandle,
			EntityTypeHandle = m_EntityTypeHandle,
		};

		ss.Dependency = fillAnimationsBufferJob.ScheduleParallel(m_FillAnimationsBufferQuery, ss.Dependency);
	}
	
	[BurstCompile]
	private partial struct FillAnimationsBufferJob: IJobChunk
	{
		public BufferTypeHandle<AnimatorControllerLayerComponent> ControllerLayersBufferHandle;
		public BufferTypeHandle<AnimatorControllerParameterComponent> ControllerParametersBufferHandle;
		public BufferTypeHandle<AnimationToProcessComponent> AnimationToProcessBufferHandle;
		[ReadOnly] public EntityTypeHandle EntityTypeHandle;
	
		public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
		{
			var layerBuffers = chunk.GetBufferAccessor(ref ControllerLayersBufferHandle);
			var parameterBuffers = chunk.GetBufferAccessor(ref ControllerParametersBufferHandle);
			var animationsToProcessBuffers = chunk.GetBufferAccessor(ref AnimationToProcessBufferHandle);
			var entities = chunk.GetNativeArray(EntityTypeHandle);
	
			var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
	
			while (cee.NextEntityIndex(out var i))
			{
				var layers = layerBuffers[i];
				var parameters = parameterBuffers.Length > 0 ? parameterBuffers[i] : default;
				var e = entities[i];
	
				var animsBuf = animationsToProcessBuffers[i];
	
				AddAnimationsForEntity(ref animsBuf, layers, e, parameters);
			}
		}

		private void AnimationsPostSetup(Span<AnimationToProcessComponent> animations, ref LayerBlob lb, int layerIndex, float weightMultiplier, float layerWeight)
		{
			//	Set blending mode and adjust animations weight according to layer weight
			for (int k = 0; k < animations.Length; ++k)
			{
				var a = animations[k];
				a.BlendMode = lb.BlendingMode;
				a.LayerWeight = layerWeight;
				a.LayerIndex = layerIndex;
				a.Weight *= weightMultiplier;
				if (lb.AvatarMask.Hash.IsValid)
					a.AvatarMask = ExternalBlobPtr<AvatarMaskBlob>.Create(ref lb.AvatarMask);
				animations[k] = a;
			}
		}

		private unsafe void AddAnimationsForEntity(ref DynamicBuffer<AnimationToProcessComponent> animations,
			in DynamicBuffer<AnimatorControllerLayerComponent> aclc,
			Entity deformedEntity,
			in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
		{
			if (deformedEntity == Entity.Null) return;
	
			animations.Clear();
	
			for (int i = 0; i < aclc.Length; ++i)
			{
				var animationCurIndex = animations.Length;
	
				ref var l = ref aclc.ElementAt(i);
				ref var cb = ref l.Controller;
				ref var lb = ref cb.Value.Layers[i];
				if (l.Weight == 0 || l.Rtd.SrcState.Id < 0)
					continue;
	
				ref var srcStateBlob = ref lb.States[l.Rtd.SrcState.Id];
	
				var srcStateWeight = 1.0f;
				var dstStateWeight = 0.0f;
	
				if (l.Rtd.ActiveTransition.Id >= 0)
				{
					ref var transitionBlob = ref srcStateBlob.Transitions[l.Rtd.ActiveTransition.Id];
					dstStateWeight = l.Rtd.ActiveTransition.NormalizedDuration;
					srcStateWeight = (1 - dstStateWeight);
				}
	
				var srcStateTime = GetDurationTime(ref srcStateBlob, runtimeParams, l.Rtd.SrcState.NormalizedDuration);
	
				var dstStateAnimCount = 0;
				if (l.Rtd.DstState.Id >= 0)
				{
					ref var dstStateBlob = ref lb.States[l.Rtd.DstState.Id];
					var dstStateTime = GetDurationTime(ref dstStateBlob, runtimeParams, l.Rtd.DstState.NormalizedDuration);
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
	
				AnimationsPostSetup(dstAnimsSpan, ref lb, i, dstStateWeight, dstLayerMultiplier * l.Weight);
				AnimationsPostSetup(srcAnimsSpan, ref lb, i, srcStateWeight, srcLayerMultiplier * l.Weight);
			}
		}

		private void AddAnimationForEntity(ref DynamicBuffer<AnimationToProcessComponent> outAnims, ref MotionBlob mb, float weight, float normalizedStateTime)
		{
			var atp = new AnimationToProcessComponent();
	
			if (mb.AnimationBlob.IsValid)
				atp.Animation = ExternalBlobPtr<AnimationClipBlob>.Create(ref mb.AnimationBlob);
	
			atp.Weight = weight;
			atp.Time = normalizedStateTime;
			outAnims.Add(atp);
		}

		private void AddMotionsFromBlendtree(in NativeList<MotionIndexAndWeight> miws,
			ref DynamicBuffer<AnimationToProcessComponent> outAnims,
			in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams,
			ref BlobArray<ChildMotionBlob> motions,
			float weight,
			float normalizedStateTime)
		{
			for (int i = 0; i < miws.Length; ++i)
			{
				var miw = miws[i];
				ref var m = ref motions[miw.MotionIndex];
				AddMotionForEntity(ref outAnims, ref m.Motion, runtimeParams, weight * miw.Weight, normalizedStateTime);
			}
		}

		private int AddMotionForEntity(ref DynamicBuffer<AnimationToProcessComponent> outAnims,
			ref MotionBlob mb,
			in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams,
			float weight,
			float normalizedStateTime)
		{
			var startLen = outAnims.Length;
			var blendTreeMotionsAndWeights = new NativeList<MotionIndexAndWeight>(Allocator.Temp);
	
			switch (mb.MotionType)
			{
				case MotionBlob.Type.None:
					break;
				case MotionBlob.Type.AnimationClip:
					AddAnimationForEntity(ref outAnims, ref mb, weight, normalizedStateTime);
					break;
				case MotionBlob.Type.BlendTreeDirect:
					blendTreeMotionsAndWeights = AnimatorControllerJob.StateMachineProcessJob.GetBlendTreeDirectCurrentMotions(ref mb, runtimeParams);
					break;
				case MotionBlob.Type.BlendTree1D:
					blendTreeMotionsAndWeights = AnimatorControllerJob.StateMachineProcessJob.GetBlendTree1DCurrentMotions(ref mb, runtimeParams);
					break;
				case MotionBlob.Type.BlendTree2DSimpleDirectional:
					blendTreeMotionsAndWeights = AnimatorControllerJob.StateMachineProcessJob.GetBlendTree2DSimpleDirectionalCurrentMotions(ref mb, runtimeParams);
					break;
				case MotionBlob.Type.BlendTree2DFreeformCartesian:
					blendTreeMotionsAndWeights = AnimatorControllerJob.StateMachineProcessJob.GetBlendTree2DFreeformCartesianCurrentMotions(ref mb, runtimeParams);
					break;
				case MotionBlob.Type.BlendTree2DFreeformDirectional:
					blendTreeMotionsAndWeights = AnimatorControllerJob.StateMachineProcessJob.GetBlendTree2DFreeformDirectionalCurrentMotions(ref mb, runtimeParams);
					break;
			}
	
			if (blendTreeMotionsAndWeights.IsCreated)
			{
				AddMotionsFromBlendtree(blendTreeMotionsAndWeights, ref outAnims, runtimeParams, ref mb.BlendTree.Motions, weight, normalizedStateTime);
			}
	
			return outAnims.Length - startLen;
		}

		private float GetDurationTime(ref StateBlob sb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams, float normalizedDuration)
		{
			var timeDuration = normalizedDuration;
			if (sb.TimeParameterIndex >= 0)
			{
				timeDuration = runtimeParams[sb.TimeParameterIndex].FloatValue;
			}
			var stateCycleOffset = sb.CycleOffset;
			if (sb.CycleOffsetParameterIndex >= 0)
			{
				stateCycleOffset = runtimeParams[sb.CycleOffsetParameterIndex].FloatValue;
			}
			timeDuration += stateCycleOffset;
			return timeDuration;
		}
	}	
}
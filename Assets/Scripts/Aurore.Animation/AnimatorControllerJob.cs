using System;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial struct AnimatorControllerJob
{
	[BurstCompile]
	public struct StateMachineProcessJob: IJobChunk
	{
		public float DeltaTime;
		public int FrameIndex;
		public BufferTypeHandle<AnimatorControllerLayerComponent> ControllerLayersBufferHandle;
		public BufferTypeHandle<AnimatorControllerParameterComponent> ControllerParametersBufferHandle;

#if AURORE_DEBUG
		public bool DoLogging;
#endif

		public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
		{
			var layerBuffers = chunk.GetBufferAccessor(ref ControllerLayersBufferHandle);
			var parameterBuffers = chunk.GetBufferAccessor(ref ControllerParametersBufferHandle);

			var cee = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

			while (cee.NextEntityIndex(out var i))
			{
				var layers = layerBuffers[i];
				var parameters = parameterBuffers.Length > 0 ? parameterBuffers[i].AsNativeArray() : default;

				ExecuteSingle(layers, parameters);
			}
		}

		private void ExecuteSingle(DynamicBuffer<AnimatorControllerLayerComponent> layerBuffer, NativeArray<AnimatorControllerParameterComponent> runtimeParams)
		{
			for (var i = 0; i < layerBuffer.Length; ++i)
			{
				ref var layer = ref layerBuffer.ElementAt(i);
#if AURORE_DEBUG
				//	Make state snapshot to compare it later and log differences
				var controllerDataPreSnapshot = layer;
#endif

				ProcessLayer(ref layer.Controller.Value, layer.LayerIndex, runtimeParams, DeltaTime, ref layer);

#if AURORE_DEBUG
				DoDebugLogging(controllerDataPreSnapshot, layer, FrameIndex);
#endif
			}
		}

		private RuntimeAnimatorData.StateRuntimeData InitRuntimeStateData(int stateID)
		{
			return new RuntimeAnimatorData.StateRuntimeData
			{
				Id = stateID,
				NormalizedDuration = 0
			};
		}

		private void ExitTransition(ref AnimatorControllerLayerComponent layer)
		{
			if (layer.Rtd.ActiveTransition.Id < 0) return;

			if (CheckTransitionExitConditions(layer.Rtd.ActiveTransition))
			{
				layer.Rtd.SrcState = layer.Rtd.DstState;
				layer.Rtd.DstState = layer.Rtd.ActiveTransition = RuntimeAnimatorData.StateRuntimeData.MakeDefault();
			}
		}

		private void EnterTransition(ref AnimatorControllerLayerComponent layer,
			ref LayerBlob layerBlob,
			NativeArray<AnimatorControllerParameterComponent> runtimeParams,
			float srcStateDurationFrameDelta,
			float curStateDuration)
		{
			if (layer.Rtd.ActiveTransition.Id >= 0)
				return;

			ref var currentState = ref layerBlob.States[layer.Rtd.SrcState.Id];

			for (var i = 0; i < currentState.Transitions.Length; ++i)
			{
				ref var transitionBlob = ref currentState.Transitions[i];
				var condition = CheckTransitionEnterExitTimeCondition(ref transitionBlob, layer.Rtd.SrcState, srcStateDurationFrameDelta) &&
				                CheckTransitionEnterConditions(ref transitionBlob, runtimeParams);
				if (condition)
				{
					var timeShouldBeInTransition = GetTimeInSecondsShouldBeInTransition(ref transitionBlob, layer.Rtd.SrcState, curStateDuration, srcStateDurationFrameDelta);
					layer.Rtd.ActiveTransition.Id	= i;
					layer.Rtd.ActiveTransition.NormalizedDuration = timeShouldBeInTransition / CalculateTransitionDuration(ref transitionBlob, curStateDuration);
					var dstStateDur = CalculateStateDuration(ref layerBlob.States[transitionBlob.TargetStateId], runtimeParams);
					layer.Rtd.DstState = InitRuntimeStateData(transitionBlob.TargetStateId);
					layer.Rtd.DstState.NormalizedDuration += timeShouldBeInTransition / dstStateDur + transitionBlob.Offset;
					break;
				}
			}
		}

		private void ProcessLayer(ref ControllerBlob controllerBlob, int layerIndex, NativeArray<AnimatorControllerParameterComponent> runtimeParams, float deltaTime, ref AnimatorControllerLayerComponent runtimeLayer)
		{
			ref var layer = ref controllerBlob.Layers[layerIndex];

			var currentStateID = runtimeLayer.Rtd.SrcState.Id;
			if (currentStateID < 0)
				currentStateID = layer.DefaultStateIndex;

			ref var currentState = ref layer.States[currentStateID];
			var curStateDuration = CalculateStateDuration(ref currentState, runtimeParams);

			if (Hint.Unlikely(runtimeLayer.Rtd.SrcState.Id < 0))
			{
				runtimeLayer.Rtd.SrcState = InitRuntimeStateData(layer.DefaultStateIndex);
			}

			var srcStateDurationFrameDelta = deltaTime / curStateDuration;
			runtimeLayer.Rtd.SrcState.NormalizedDuration += srcStateDurationFrameDelta;

			if (runtimeLayer.Rtd.DstState.Id >= 0)
			{
				var dstStateDuration = CalculateStateDuration(ref layer.States[runtimeLayer.Rtd.DstState.Id], runtimeParams);
				runtimeLayer.Rtd.DstState.NormalizedDuration += deltaTime / dstStateDuration;
			}

			if (runtimeLayer.Rtd.ActiveTransition.Id >= 0)
			{
				ref var currentTransitionBlob = ref currentState.Transitions[runtimeLayer.Rtd.ActiveTransition.Id];
				var transitionDuration = CalculateTransitionDuration(ref currentTransitionBlob, curStateDuration);
				runtimeLayer.Rtd.ActiveTransition.NormalizedDuration += deltaTime / transitionDuration;
			}

			ExitTransition(ref runtimeLayer);
			EnterTransition(ref runtimeLayer, ref layer, runtimeParams, srcStateDurationFrameDelta, curStateDuration);
			//	Check tranision exit conditions one more time in case of Enter->Exit sequence appeared in single frame
			ExitTransition(ref runtimeLayer);

			ProcessTransitionInterruptions();
		}

		//	p0 = (0,0)
		private static (float, float, float) CalculateBarycentric(float2 p1, float2 p2, float2 pt)
		{
			var np2 = new float2(0 - p2.y, p2.x - 0);
			var np1 = new float2(0 - p1.y, p1.x - 0);

			var l1 = math.dot(pt, np2) / math.dot(p1, np2);
			var l2 = math.dot(pt, np1) / math.dot(p2, np1);
			var l0 = 1 - l1 - l2;
			return (l0, l1, l2);
		}

		private static unsafe void HandleCentroidCase(ref NativeList<MotionIndexAndWeight> motionIndexAndWeights, float2 pt, ref BlobArray<ChildMotionBlob> childMotions)
		{
			if (math.any(pt)) return;

			var i = 0;
			for (; i < childMotions.Length && math.any(childMotions[i].Position2D); ++i) { }

			if (i < childMotions.Length)
			{
				var miw = new MotionIndexAndWeight { MotionIndex = i, Weight = 1 };
				motionIndexAndWeights.Add(miw);
			}
			else
			{
				var f = 1.0f / childMotions.Length;
				for (var l = 0; l < childMotions.Length; ++l)
				{
					var miw = new MotionIndexAndWeight { MotionIndex = l, Weight = f };
					motionIndexAndWeights.Add(miw);
				}
			}
		}

		public static unsafe NativeList<MotionIndexAndWeight> GetBlendTree2DSimpleDirectionalCurrentMotions(ref MotionBlob motionBlob, in NativeArray<AnimatorControllerParameterComponent> runtimeParams)
		{
			var motionIndexAndWeights = new NativeList<MotionIndexAndWeight>(Allocator.Temp);
			var pX = runtimeParams[motionBlob.BlendTree.BlendParameterIndex];
			var pY = runtimeParams[motionBlob.BlendTree.BlendParameterYIndex];
			var pt = new float2(pX.FloatValue, pY.FloatValue);
			ref var motions = ref motionBlob.BlendTree.Motions;

			if (motions.Length < 2)
			{
				if (motions.Length == 1)
					motionIndexAndWeights.Add(new MotionIndexAndWeight { Weight = 1, MotionIndex = 0 });
				return motionIndexAndWeights;
			}

			HandleCentroidCase(ref motionIndexAndWeights, pt, ref motions);
			if (motionIndexAndWeights.Length > 0)
				return motionIndexAndWeights;

			var centerPtIndex = -1;
			//	Loop over all directions and search for sector that contains requested point
			var dotProductsAndWeights = new NativeList<MotionIndexAndWeight>(motions.Length, Allocator.Temp);
			for (var i = 0; i < motions.Length; ++i)
			{
				ref var m = ref motions[i];
				var motionDir = m.Position2D;
				if (!math.any(motionDir))
				{
					centerPtIndex = i;
					continue;
				}
				var angle = math.atan2(motionDir.y, motionDir.x);
				var miw = new MotionIndexAndWeight { MotionIndex = i, Weight = angle };
				dotProductsAndWeights.Add(miw);
			}

			var ptAngle = math.atan2(pt.y, pt.x);

			dotProductsAndWeights.Sort();

			// Pick two closest points
			MotionIndexAndWeight d0 = default, d1 = default;
			var l = 0;
			for (; l < dotProductsAndWeights.Length; ++l)
			{
				var d = dotProductsAndWeights[l];
				if (d.Weight < ptAngle)
				{
					var ld0 = l == 0 ? dotProductsAndWeights.Length - 1 : l - 1;
					d1 = d;
					d0 = dotProductsAndWeights[ld0];
					break;
				}
			}

			//	Handle last sector
			if (l == dotProductsAndWeights.Length)
			{
				d0 = dotProductsAndWeights[dotProductsAndWeights.Length - 1];
				d1 = dotProductsAndWeights[0];
			}

			ref var m0 = ref motions[d0.MotionIndex];
			ref var m1 = ref motions[d1.MotionIndex];
			var p0 = m0.Position2D;
			var p1 = m1.Position2D;
			
			//	Barycentric coordinates for point pt in triangle <p0,p1,0>
			var (l0, l1, l2) = CalculateBarycentric(p0, p1, pt);

			var m0Weight = l1;
			var m1Weight = l2;
			if (l0 < 0)
			{
				var sum = m0Weight + m1Weight;
				m0Weight /= sum;
				m1Weight /= sum;
			}	

			l0 = math.saturate(l0);

			var evenlyDistributedMotionWeight = centerPtIndex < 0 ? 1.0f / motions.Length * l0 : 0;

			var miw0 = new MotionIndexAndWeight { MotionIndex = d0.MotionIndex, Weight = m0Weight + evenlyDistributedMotionWeight };
			motionIndexAndWeights.Add(miw0);

			var miw1 = new MotionIndexAndWeight { MotionIndex = d1.MotionIndex, Weight = m1Weight + evenlyDistributedMotionWeight };
			motionIndexAndWeights.Add(miw1);

			//	Add other motions of blend tree
			if (evenlyDistributedMotionWeight > 0)
			{
				for (var i = 0; i < motions.Length; ++i)
				{
					if (i != d0.MotionIndex && i != d1.MotionIndex)
					{
						var miw = new MotionIndexAndWeight { MotionIndex = i, Weight = evenlyDistributedMotionWeight };
						motionIndexAndWeights.Add(miw);
					}
				}
			}

			//	Add centroid motion
			if (centerPtIndex >= 0)
			{
				var miw = new MotionIndexAndWeight { MotionIndex = centerPtIndex, Weight = l0 };
				motionIndexAndWeights.Add(miw);
			}

			dotProductsAndWeights.Dispose();

			return motionIndexAndWeights;
		}

		public static unsafe NativeList<MotionIndexAndWeight> GetBlendTree2DFreeformCartesianCurrentMotions(ref MotionBlob motionBlob, in NativeArray<AnimatorControllerParameterComponent> runtimeParams)
		{
			var pX = runtimeParams[motionBlob.BlendTree.BlendParameterIndex];
			var pY = runtimeParams[motionBlob.BlendTree.BlendParameterYIndex];
			var p = new float2(pX.FloatValue, pY.FloatValue);
			ref var motions = ref motionBlob.BlendTree.Motions;
			Span<float> hpArr = stackalloc float[motions.Length];

			var hpSum = 0.0f;

			//	Calculate influence factors
			for (var i = 0; i < motions.Length; ++i)
			{
				var pi = motions[i].Position2D;
				var pip = p - pi;

				var w = 1.0f;

				for (var j = 0; j < motions.Length && w > 0; ++j)
				{
					if (i == j) continue;
					var pj = motions[j].Position2D;
					var pipj = pj - pi;
					var f = math.dot(pip, pipj) / math.lengthsq(pipj);
					var hj = math.max(1 - f, 0);
					w = math.min(hj, w);
				}
				hpSum += w;
				hpArr[i] = w;
			}

			var motionIndexAndWeights = new NativeList<MotionIndexAndWeight>(motions.Length, Allocator.Temp);
			//	Calculate weight functions
			for (var i = 0; i < motions.Length; ++i)
			{
				var w = hpArr[i] / hpSum;
				if (w > 0)
				{
					var miw = new MotionIndexAndWeight { MotionIndex = i, Weight = w };
					motionIndexAndWeights.Add(miw);
				}
			}

			return motionIndexAndWeights;
		}

		private static float CalcAngle(float2 a, float2 b)
		{
			var cross = a.x * b.y - a.y * b.x;
			var dot = math.dot(a, b);
			var tanA = new float2(cross, dot);

			return math.atan2(tanA.x, tanA.y);
		}

		private static float2 CalcAngleWeights(float2 i, float2 j, float2 s)
		{
			float2 weights = 0;
			if (!math. any(i))
			{
				weights.x = CalcAngle(j, s);
				weights.y = 0;
			}
			else if (!math.any(j))
			{
				weights.x = CalcAngle(i, s);
				weights.y = weights.x;
			}
			else
			{
				weights.x = CalcAngle(i, j);
				if (!math.any(s))
					weights.y = weights.x;
				else
					weights.y = CalcAngle(i, s);
			}

			return weights;
		}

		public static unsafe NativeList<MotionIndexAndWeight> GetBlendTree2DFreeformDirectionalCurrentMotions(ref MotionBlob motionBlob, in NativeArray<AnimatorControllerParameterComponent> runtimeParams)
		{
			var pX = runtimeParams[motionBlob.BlendTree.BlendParameterIndex];
			var pY = runtimeParams[motionBlob.BlendTree.BlendParameterYIndex];
			var p = new float2(pX.FloatValue, pY.FloatValue);
			var lp = math.length(p);

			ref var motions = ref motionBlob.BlendTree.Motions;
			Span<float> hpArr = stackalloc float[motions.Length];

			var hpSum = 0.0f;

			//	Calculate influence factors
			for (var i = 0; i < motions.Length; ++i)
			{
				var pi = motions[i].Position2D;
				var lpi = math.length(pi);

				var w = 1.0f;

				for (var j = 0; j < motions.Length && w > 0; ++j)
				{
					if (i == j) continue;
					var pj = motions[j].Position2D;
					var lpj = math.length(pj);

					var pRcpMiddle = math.rcp((lpj + lpi) * 0.5f);
					var lpip = (lp - lpi) * pRcpMiddle;
					var lpipj = (lpj - lpi) * pRcpMiddle;
					var angleWeights = CalcAngleWeights(pi, pj, p);

					var pip = new float2(lpip, angleWeights.y);
					var pipj = new float2(lpipj, angleWeights.x);

					var f = math.dot(pip, pipj) / math.lengthsq(pipj);
					var hj = math.saturate(1 - f);
					w = math.min(hj, w);
				}
				hpSum += w;
				hpArr[i] = w;	
			}

			var motionIndexAndWeights = new NativeList<MotionIndexAndWeight>(motions.Length, Allocator.Temp);
			//	Calculate weight functions
			for (var i = 0; i < motions.Length; ++i)
			{
				var w = hpArr[i] / hpSum;
				if (w > 0)
				{
					var miw = new MotionIndexAndWeight { MotionIndex = i, Weight = w };
					motionIndexAndWeights.Add(miw);
				}
			}

			return motionIndexAndWeights;
		}
		
		public static NativeList<MotionIndexAndWeight> GetBlendTree1DCurrentMotions(ref MotionBlob motionBlob, in NativeArray<AnimatorControllerParameterComponent> runtimeParams)
		{
			var blendTreeParameter = runtimeParams[motionBlob.BlendTree.BlendParameterIndex];
			ref var motions = ref motionBlob.BlendTree.Motions;
			var i0 = 0;
			var i1 = 0;
			var found = false;
			for (var i = 0; i < motions.Length && !found; ++i)
			{
				ref var m = ref motions[i];
				i0 = i1;
				i1 = i;
				if (m.Threshold > blendTreeParameter.FloatValue)
					found = true;
			}
			if (!found)
			{
				i0 = i1 = motions.Length - 1;
			}

			var motion0Threshold = motions[i0].Threshold;
			var motion1Threshold = motions[i1].Threshold;
			var f = i1 == i0 ? 0 : (blendTreeParameter.FloatValue - motion0Threshold) / (motion1Threshold - motion0Threshold);

			var motionIndexAndWeights = new NativeList<MotionIndexAndWeight>(2, Allocator.Temp);
			motionIndexAndWeights.Add(new MotionIndexAndWeight { MotionIndex = i0, Weight = 1 - f });
			motionIndexAndWeights.Add(new MotionIndexAndWeight { MotionIndex = i1, Weight = f });

			return motionIndexAndWeights;
		}

		public static NativeList<MotionIndexAndWeight> GetBlendTreeDirectCurrentMotions(ref MotionBlob motionBlob, in NativeArray<AnimatorControllerParameterComponent> runtimeParams)
		{
			ref var motions = ref motionBlob.BlendTree.Motions;
			var motionIndexAndWeights = new NativeList<MotionIndexAndWeight>(motions.Length, Allocator.Temp);

			var weightSum = 0.0f;
			for (var i = 0; i < motions.Length; ++i)
			{
				ref var cm = ref motions[i];
				var w = cm.DirectBlendParameterIndex >= 0 ? runtimeParams[cm.DirectBlendParameterIndex].FloatValue : 0;
				if (w > 0)
				{
					var miw = new MotionIndexAndWeight { MotionIndex = i, Weight = w };
					weightSum += miw.Weight;
					motionIndexAndWeights.Add(miw);
				}
			}

			if (motionBlob.BlendTree.NormalizeBlendValues && weightSum > 1)
			{
				for (var i = 0; i < motionIndexAndWeights.Length; ++i)
				{
					var miw = motionIndexAndWeights[i];
					miw.Weight = miw.Weight / weightSum;
					motionIndexAndWeights[i] = miw;
				}
			}

			return motionIndexAndWeights;
		}

		private unsafe float CalculateMotionDuration(ref MotionBlob motionBlob, in NativeArray<AnimatorControllerParameterComponent> runtimeParams, float weight)
		{
			if (weight == 0) return 0;

			NativeList<MotionIndexAndWeight> blendTreeMotionsAndWeights = default;
			switch (motionBlob.MotionType)
			{
				case MotionBlob.Type.None:
					return 1;
				case MotionBlob.Type.AnimationClip:
					return motionBlob.AnimationBlob.Value.Length * weight;
				case MotionBlob.Type.BlendTreeDirect:
					blendTreeMotionsAndWeights = GetBlendTreeDirectCurrentMotions(ref motionBlob, runtimeParams);
					break;
				case MotionBlob.Type.BlendTree1D:
					blendTreeMotionsAndWeights = GetBlendTree1DCurrentMotions(ref motionBlob, runtimeParams);
					break;
				case MotionBlob.Type.BlendTree2DSimpleDirectional:
					blendTreeMotionsAndWeights = GetBlendTree2DSimpleDirectionalCurrentMotions(ref motionBlob, runtimeParams);
					break;
				case MotionBlob.Type.BlendTree2DFreeformCartesian:
					blendTreeMotionsAndWeights = GetBlendTree2DFreeformCartesianCurrentMotions(ref motionBlob, runtimeParams);
					break;
				case MotionBlob.Type.BlendTree2DFreeformDirectional:
					blendTreeMotionsAndWeights = GetBlendTree2DFreeformDirectionalCurrentMotions(ref motionBlob, runtimeParams);
					break;
				default:
					Debug.Log($"Unsupported blend tree type");
					break;
			}

			var motionDuration = CalculateBlendTreeMotionDuration(blendTreeMotionsAndWeights, ref motionBlob.BlendTree.Motions, runtimeParams, weight);
			if (blendTreeMotionsAndWeights.IsCreated) blendTreeMotionsAndWeights.Dispose();
			
			return motionDuration;
		}

		private float CalculateBlendTreeMotionDuration(NativeList<MotionIndexAndWeight> motionIndexAndWeights, ref BlobArray<ChildMotionBlob> childMotionBlobs, in NativeArray<AnimatorControllerParameterComponent> runtimeParams, float weight)
		{
			if (!motionIndexAndWeights.IsCreated || motionIndexAndWeights.IsEmpty)
				return 1;

			var weightSum = 0.0f;
			for (var i = 0; i < motionIndexAndWeights.Length; ++i)
				weightSum += motionIndexAndWeights[i].Weight;

			//	If total weight less then 1, normalize weights
			if (Hint.Unlikely(weightSum < 1))
			{
				for (var i = 0; i < motionIndexAndWeights.Length; ++i)
				{
					var miw = motionIndexAndWeights[i];
					miw.Weight = miw.Weight / weightSum;
					motionIndexAndWeights[i] = miw;
				}
			}

			var duration = 0.0f;
			for (var i = 0; i < motionIndexAndWeights.Length; ++i)
			{
				var miw = motionIndexAndWeights[i];
				ref var m = ref childMotionBlobs[miw.MotionIndex];
				duration += CalculateMotionDuration(ref m.Motion, runtimeParams, weight * miw.Weight) / m.TimeScale;
			}

			return duration;
		}

		private float CalculateTransitionDuration(ref TransitionBlob transitionBlob, float curStateDuration)
		{
			var duration = transitionBlob.Duration;
			if (!transitionBlob.HasFixedDuration)
			{
				duration *= curStateDuration;
			}

			return math.max(duration, 0.0001f);
		}

		private float CalculateStateDuration(ref StateBlob stateBlob, in NativeArray<AnimatorControllerParameterComponent> runtimeParams)
		{
			var motionDuration = CalculateMotionDuration(ref stateBlob.Motion, runtimeParams, 1);
			var speedMultiplier = 1.0f;
			if (stateBlob.SpeedMultiplierParameterIndex >= 0)
			{
				speedMultiplier = runtimeParams[stateBlob.SpeedMultiplierParameterIndex].FloatValue;
			}

			return motionDuration / (stateBlob.Speed * speedMultiplier);
		}

		internal static float GetLoopAwareTransitionExitTime(float exitTime, float normalizedDuration, float speedSign)
		{
			var time = exitTime;
			if (exitTime <= 1.0f)
			{
				//	Unity animator logic and documentation mismatch. Documentation says that exit time loop condition should be when transition exitTime less then 1, but in practice it will loop when exitTime is less or equal(!) to 1.
				exitTime = math.min(exitTime, 0.9999f);
				var snd = normalizedDuration * speedSign;

				var f = math.frac(snd);
				time += (int)snd;
				if (f > exitTime)
					time += 1;
			}
			return time * speedSign;
		}

		private float GetTimeInSecondsShouldBeInTransition(ref TransitionBlob transitionBlob,
			RuntimeAnimatorData.StateRuntimeData curStateRuntimeData,
			float curStateDuration,
			float frameDeltaTime)
		{
			if (!transitionBlob.HasExitTime) return 0;

			//	This should be always less then curStateRTD.normalizedDuration
			var loopAwareExitTime = GetLoopAwareTransitionExitTime(transitionBlob.ExitTime, curStateRuntimeData.NormalizedDuration - frameDeltaTime, math.sign(frameDeltaTime));
			var loopDelta = curStateRuntimeData.NormalizedDuration - loopAwareExitTime;

			return loopDelta * curStateDuration;
		}

		private bool CheckTransitionEnterExitTimeCondition(ref TransitionBlob transitionBlob,
			RuntimeAnimatorData.StateRuntimeData curStateRuntimeData,
			float srcStateDurationFrameDelta)
		{
			var normalizedStateDuration = curStateRuntimeData.NormalizedDuration; 

			var noNormalConditions = transitionBlob.Conditions.Length == 0;
			if (!transitionBlob.HasExitTime) return !noNormalConditions;

			var l0 = normalizedStateDuration - srcStateDurationFrameDelta;
			var l1 = normalizedStateDuration;
			var speedSign = math.select(-1, 1, l0 < l1);

			var loopAwareExitTime = GetLoopAwareTransitionExitTime(transitionBlob.ExitTime, l0, speedSign);

			if (speedSign < 0) MathUtils.Swap(ref l0, ref l1);

			return loopAwareExitTime > l0 && loopAwareExitTime <= l1;
		}

		private bool CheckIntCondition(in AnimatorControllerParameterComponent param, ref ConditionBlob conditionBlob)
		{
			var hasCondition = true;
			switch (conditionBlob.ConditionMode)
			{
				case AnimatorConditionMode.Equals:
					if (param.IntValue != conditionBlob.Threshold.intValue) hasCondition = false;
					break;
				case AnimatorConditionMode.Greater:
					if (param.IntValue <= conditionBlob.Threshold.intValue) hasCondition = false;
					break;
				case AnimatorConditionMode.Less:
					if (param.IntValue >= conditionBlob.Threshold.intValue) hasCondition = false;
					break;
				case AnimatorConditionMode.NotEqual:
					if (param.IntValue == conditionBlob.Threshold.intValue) hasCondition = false;
					break;
				default:
					Debug.LogError($"Unsupported condition type for int parameter value!");
					break;
			}

			return hasCondition;
		}

		private bool CheckFloatCondition(in AnimatorControllerParameterComponent param, ref ConditionBlob conditionBlob)
		{
			var hasCondition = true;
			switch (conditionBlob.ConditionMode)
			{
				case AnimatorConditionMode.Greater:
					if (param.FloatValue <= conditionBlob.Threshold.floatValue) hasCondition = false;
					break;
				case AnimatorConditionMode.Less:
					if (param.FloatValue >= conditionBlob.Threshold.floatValue) hasCondition = false;
					break;
				default:
					Debug.LogError($"Unsupported condition type for int parameter value!");
					break;
			}

			return hasCondition;
		}

		private bool CheckBoolCondition(in AnimatorControllerParameterComponent param, ref ConditionBlob conditionBlob)
		{
			var hasCondition = true;
			switch (conditionBlob.ConditionMode)
			{
				case AnimatorConditionMode.If:
					hasCondition = param.BoolValue;
					break;
				case AnimatorConditionMode.IfNot:
					hasCondition = !param.BoolValue;
					break;
				default:
					Debug.LogError($"Unsupported condition type for int parameter value!");
					break;
			}

			return hasCondition;
		}

		private void ResetTriggers(ref TransitionBlob transitionBlob, NativeArray<AnimatorControllerParameterComponent> runtimeParams)
		{
			for (var i = 0; i < transitionBlob.Conditions.Length; ++i)
			{
				ref var condition = ref transitionBlob.Conditions[i];
				var param = runtimeParams[condition.ParamIdx];
				if (param.Type == ControllerParameterType.Trigger)
				{
					param.Value.boolValue = false;
					runtimeParams[condition.ParamIdx] = param;
				}
			}
		}

		private bool CheckTransitionEnterConditions(ref TransitionBlob transitionBlob, NativeArray<AnimatorControllerParameterComponent> runtimeParams)
		{
			if (transitionBlob.Conditions.Length == 0)
				return true;

			var hasCondition = true;
			var hasTriggers = false;
			for (var i = 0; i < transitionBlob.Conditions.Length && hasCondition; ++i)
			{
				ref var condition = ref transitionBlob.Conditions[i];
				var param = runtimeParams[condition.ParamIdx];

				switch (param.Type)
				{
					case ControllerParameterType.Float:
						hasCondition = CheckFloatCondition(param, ref condition);
						break;
					case ControllerParameterType.Int:
						hasCondition = CheckIntCondition(param, ref condition);
						break;
					case ControllerParameterType.Bool:
						hasCondition = CheckBoolCondition(param, ref condition);
						break;
					case ControllerParameterType.Trigger:
						hasCondition = CheckBoolCondition(param, ref condition);
						hasTriggers = true;
						break;
				}
			}

			if (hasTriggers && hasCondition)
				ResetTriggers(ref transitionBlob, runtimeParams);

			return hasCondition;
		}

		private bool CheckTransitionExitConditions(RuntimeAnimatorData.StateRuntimeData transitionRuntimeData)
		{
			return transitionRuntimeData.NormalizedDuration >= 1;
		}

		private void ProcessTransitionInterruptions()
		{
			// Not implemented yet
		}

		private void DoDebugLogging(AnimatorControllerLayerComponent prevData, AnimatorControllerLayerComponent curData, int frameIndex)
		{
		#if AURORE_DEBUG
			if (!DoLogging) return;

			ref var controllerBlob = ref curData.Controller.Value;
			ref var controllerBlobLayer = ref controllerBlob.Layers[curData.LayerIndex];
			ref var currentState = ref controllerBlobLayer.States[curData.Rtd.SrcState.Id];

			var layerName = controllerBlobLayer.Name.ToFixedString();
			var controllerName = controllerBlob.Name.ToFixedString();
			var curStateName = currentState.Name.ToFixedString();

			Debug.Log($"[{frameIndex}:{controllerName}:{layerName}] In state: '{curStateName}' with normalized duration: {curData.Rtd.SrcState.NormalizedDuration}");

			//	Exit transition event
			if (prevData.Rtd.ActiveTransition.Id >= 0 && curData.Rtd.ActiveTransition.Id != prevData.Rtd.ActiveTransition.Id)
			{
				ref var t = ref controllerBlobLayer.States[prevData.Rtd.SrcState.Id].Transitions[prevData.Rtd.ActiveTransition.Id];
				Debug.Log($"[{frameIndex}:{controllerName}:{layerName}] Exiting transition: '{t.Name.ToFixedString()}'");
			}

			//	Enter transition event
			if (curData.Rtd.ActiveTransition.Id >= 0)
			{
				ref var t = ref controllerBlobLayer.States[curData.Rtd.SrcState.Id].Transitions[curData.Rtd.ActiveTransition.Id];
				if (curData.Rtd.ActiveTransition.Id != prevData.Rtd.ActiveTransition.Id)
				{
					Debug.Log($"[{frameIndex}:{controllerName}:{layerName}] Entering transition: '{t.Name.ToFixedString()}' with time: {{curData.Rtd.ActiveTransition.NormalizedDuration}}");
				}
				else
				{
					Debug.Log($"[{frameIndex}:{controllerName}:{layerName}] In transition: '{t.Name.ToFixedString()}' with time: {curData.Rtd.ActiveTransition.NormalizedDuration}");
				}
				ref var dstState = ref controllerBlobLayer.States[curData.Rtd.DstState.Id];
				Debug.Log($"[{frameIndex}:{controllerName}:{layerName}] Target state: '{dstState.Name.ToFixedString()}' with time: {curData.Rtd.DstState.NormalizedDuration}");
			}
		#endif
		}
	}
}
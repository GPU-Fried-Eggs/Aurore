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
		public float DT;
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
				var parameters = parameterBuffers.Length > 0 ? parameterBuffers[i] : default;

				ExecuteSingle(ref layers, ref parameters);
			}
		}

		private void ExecuteSingle(ref DynamicBuffer<AnimatorControllerLayerComponent> aclc, ref DynamicBuffer<AnimatorControllerParameterComponent> acpc)
		{
			for (int i = 0; i < aclc.Length; ++i)
			{
				ref var acc = ref aclc.ElementAt(i);
#if AURORE_DEBUG
				//	Make state snapshot to compare it later and log differences
				var controllerDataPreSnapshot = acc;
#endif

				ProcessLayer(ref acc.Controller.Value, acc.LayerIndex, ref acpc, DT, ref acc);

#if AURORE_DEBUG
				DoDebugLogging(controllerDataPreSnapshot, acc, FrameIndex);
#endif
			}
		}

		private RuntimeAnimatorData.StateRuntimeData InitRuntimeStateData(int stateID)
		{
			var rv = new RuntimeAnimatorData.StateRuntimeData();
			rv.Id = stateID;
			rv.NormalizedDuration = 0;
			return rv;
		}

		private void ExitTransition(ref AnimatorControllerLayerComponent acc, ref LayerBlob layer)
		{
			if (acc.Rtd.ActiveTransition.Id >= 0)
			{
				ref var t = ref layer.States[acc.Rtd.SrcState.Id].Transitions[acc.Rtd.ActiveTransition.Id];
				ref var dstState = ref layer.States[acc.Rtd.DstState.Id];

				if (CheckTransitionExitConditions(acc.Rtd.ActiveTransition))
				{
					acc.Rtd.SrcState = acc.Rtd.DstState;
					acc.Rtd.DstState = acc.Rtd.ActiveTransition = RuntimeAnimatorData.StateRuntimeData.MakeDefault();
				}
			}
		}

		private void EnterTransition(ref AnimatorControllerLayerComponent acc,
			ref LayerBlob layer,
			ref DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams,
			float srcStateDurationFrameDelta,
			float curStateDuration)
		{
			if (acc.Rtd.ActiveTransition.Id >= 0)
				return;

			ref var currentState = ref layer.States[acc.Rtd.SrcState.Id];

			for (int i = 0; i < currentState.Transitions.Length; ++i)
			{
				ref var t = ref currentState.Transitions[i];
				var b = CheckTransitionEnterExitTimeCondition(ref t, acc.Rtd.SrcState, srcStateDurationFrameDelta) &&
						CheckTransitionEnterConditions(ref t, ref runtimeParams);
				if (b)
				{
					var timeShouldBeInTransition = GetTimeInSecondsShouldBeInTransition(ref t, acc.Rtd.SrcState, curStateDuration, srcStateDurationFrameDelta);
					acc.Rtd.ActiveTransition.Id	= i;
					acc.Rtd.ActiveTransition.NormalizedDuration = timeShouldBeInTransition / CalculateTransitionDuration(ref t, curStateDuration);
					var dstStateDur = CalculateStateDuration(ref layer.States[t.TargetStateId], runtimeParams) + t.Offset;
					acc.Rtd.DstState = InitRuntimeStateData(t.TargetStateId);
					acc.Rtd.DstState.NormalizedDuration += timeShouldBeInTransition / dstStateDur;
					break;
				}
			}
		}

		private void ProcessLayer(ref ControllerBlob c, int layerIndex, ref DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams, float dt, ref AnimatorControllerLayerComponent acc)
		{
			ref var layer = ref c.Layers[layerIndex];

			var currentStateID = acc.Rtd.SrcState.Id;
			if (currentStateID < 0)
				currentStateID = layer.DefaultStateIndex;

			ref var currentState = ref layer.States[currentStateID];
			var curStateDuration = CalculateStateDuration(ref currentState, runtimeParams);

			if (Hint.Unlikely(acc.Rtd.SrcState.Id < 0))
			{
				acc.Rtd.SrcState = InitRuntimeStateData(layer.DefaultStateIndex);
			}

			var srcStateDurationFrameDelta = dt / curStateDuration;
			acc.Rtd.SrcState.NormalizedDuration += srcStateDurationFrameDelta;

			if (acc.Rtd.DstState.Id >= 0)
			{
				var dstStateDuration = CalculateStateDuration(ref layer.States[acc.Rtd.DstState.Id], runtimeParams);
				acc.Rtd.DstState.NormalizedDuration += dt / dstStateDuration;
			}

			if (acc.Rtd.ActiveTransition.Id >= 0)
			{
				ref var currentTransitionBlob = ref currentState.Transitions[acc.Rtd.ActiveTransition.Id];
				var transitionDuration = CalculateTransitionDuration(ref currentTransitionBlob, curStateDuration);
				acc.Rtd.ActiveTransition.NormalizedDuration += dt / transitionDuration;
			}

			ExitTransition(ref acc, ref layer);
			EnterTransition(ref acc, ref layer, ref runtimeParams, srcStateDurationFrameDelta, curStateDuration);

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

		private static unsafe void HandleCentroidCase(ref NativeList<MotionIndexAndWeight> rv, float2 pt, ref BlobArray<ChildMotionBlob> mbArr)
		{
			if (math.any(pt))
				return;

			int i = 0;
			for (; i < mbArr.Length && math.any(mbArr[i].Position2D); ++i) { }

			if (i < mbArr.Length)
			{
				var miw = new MotionIndexAndWeight() { MotionIndex = i, Weight = 1 };
				rv.Add(miw);
			}
			else
			{
				var f = 1.0f / mbArr.Length;
				for (int l = 0; l < mbArr.Length; ++l)
				{
					var miw = new MotionIndexAndWeight() { MotionIndex = l, Weight = f };
					rv.Add(miw);
				}
			}
		}

		public static unsafe NativeList<MotionIndexAndWeight> GetBlendTree2DSimpleDirectionalCurrentMotions(ref MotionBlob mb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
		{
			var rv = new NativeList<MotionIndexAndWeight>(Allocator.Temp);
			var pX = runtimeParams[mb.BlendTree.BlendParameterIndex];
			var pY = runtimeParams[mb.BlendTree.BlendParameterYIndex];
			var pt = new float2(pX.FloatValue, pY.FloatValue);
			ref var motions = ref mb.BlendTree.Motions;

			if (motions.Length < 2)
			{
				if (motions.Length == 1)
					rv.Add(new MotionIndexAndWeight() { Weight = 1, MotionIndex = 0 });
				return rv;
			}

			HandleCentroidCase(ref rv, pt, ref motions);
			if (rv.Length > 0)
				return rv;

			var centerPtIndex = -1;
			//	Loop over all directions and search for sector that contains requested point
			var dotProductsAndWeights = new NativeList<MotionIndexAndWeight>(motions.Length, Allocator.Temp);
			for (int i = 0; i < motions.Length; ++i)
			{
				ref var m = ref motions[i];
				var motionDir = m.Position2D;
				if (!math.any(motionDir))
				{
					centerPtIndex = i;
					continue;
				}
				var angle = math.atan2(motionDir.y, motionDir.x);
				var miw = new MotionIndexAndWeight() { MotionIndex = i, Weight = angle };
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

			var miw0 = new MotionIndexAndWeight() { MotionIndex = d0.MotionIndex, Weight = m0Weight + evenlyDistributedMotionWeight };
			rv.Add(miw0);

			var miw1 = new MotionIndexAndWeight() { MotionIndex = d1.MotionIndex, Weight = m1Weight + evenlyDistributedMotionWeight };
			rv.Add(miw1);

			//	Add other motions of blend tree
			if (evenlyDistributedMotionWeight > 0)
			{
				for (int i = 0; i < motions.Length; ++i)
				{
					if (i != d0.MotionIndex && i != d1.MotionIndex)
					{
						var miw = new MotionIndexAndWeight() { MotionIndex = i, Weight = evenlyDistributedMotionWeight };
						rv.Add(miw);
					}
				}
			}

			//	Add centroid motion
			if (centerPtIndex >= 0)
			{
				var miw = new MotionIndexAndWeight() { MotionIndex = centerPtIndex, Weight = l0 };
				rv.Add(miw);
			}

			dotProductsAndWeights.Dispose();

			return rv;
		}

		public static unsafe NativeList<MotionIndexAndWeight> GetBlendTree2DFreeformCartesianCurrentMotions(ref MotionBlob mb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
		{
			var pX = runtimeParams[mb.BlendTree.BlendParameterIndex];
			var pY = runtimeParams[mb.BlendTree.BlendParameterYIndex];
			var p = new float2(pX.FloatValue, pY.FloatValue);
			ref var motions = ref mb.BlendTree.Motions;
			Span<float> hpArr = stackalloc float[motions.Length];

			var hpSum = 0.0f;

			//	Calculate influence factors
			for (int i = 0; i < motions.Length; ++i)
			{
				var pi = motions[i].Position2D;
				var pip = p - pi;

				var w = 1.0f;

				for (int j = 0; j < motions.Length && w > 0; ++j)
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

			var rv = new NativeList<MotionIndexAndWeight>(motions.Length, Allocator.Temp);
			//	Calculate weight functions
			for (int i = 0; i < motions.Length; ++i)
			{
				var w = hpArr[i] / hpSum;
				if (w > 0)
				{
					var miw = new MotionIndexAndWeight() { MotionIndex = i, Weight = w };
					rv.Add(miw);
				}
			}
			return rv;
		}

		private static float CalcAngle(float2 a, float2 b)
		{
			var cross = a.x * b.y - a.y * b.x;
			var dot = math.dot(a, b);
			var tanA = new float2(cross, dot);
			var rv = math.atan2(tanA.x, tanA.y);
			return rv;
		}

		private static float2 CalcAngleWeights(float2 i, float2 j, float2 s)
		{
			float2 rv = 0;
			if (!math. any(i))
			{
				rv.x = CalcAngle(j, s);
				rv.y = 0;
			}
			else if (!math.any(j))
			{
				rv.x = CalcAngle(i, s);
				rv.y = rv.x;
			}
			else
			{
				rv.x = CalcAngle(i, j);
				if (!math.any(s))
					rv.y = rv.x;
				else
					rv.y = CalcAngle(i, s);
			}
			return rv;
		}

		public static unsafe NativeList<MotionIndexAndWeight> GetBlendTree2DFreeformDirectionalCurrentMotions(ref MotionBlob mb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
		{
			var pX = runtimeParams[mb.BlendTree.BlendParameterIndex];
			var pY = runtimeParams[mb.BlendTree.BlendParameterYIndex];
			var p = new float2(pX.FloatValue, pY.FloatValue);
			var lp = math.length(p);

			ref var motions = ref mb.BlendTree.Motions;
			Span<float> hpArr = stackalloc float[motions.Length];

			var hpSum = 0.0f;

			//	Calculate influence factors
			for (int i = 0; i < motions.Length; ++i)
			{
				var pi = motions[i].Position2D;
				var lpi = math.length(pi);

				var w = 1.0f;

				for (int j = 0; j < motions.Length && w > 0; ++j)
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

			var rv = new NativeList<MotionIndexAndWeight>(motions.Length, Allocator.Temp);
			//	Calculate weight functions
			for (int i = 0; i < motions.Length; ++i)
			{
				var w = hpArr[i] / hpSum;
				if (w > 0)
				{
					var miw = new MotionIndexAndWeight() { MotionIndex = i, Weight = w };
					rv.Add(miw);
				}
			}
			return rv;
		}
		
		public static NativeList<MotionIndexAndWeight> GetBlendTree1DCurrentMotions(ref MotionBlob mb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
		{
			var blendTreeParameter = runtimeParams[mb.BlendTree.BlendParameterIndex];
			ref var motions = ref mb.BlendTree.Motions;
			var i0 = 0;
			var i1 = 0;
			bool found = false;
			for (int i = 0; i < motions.Length && !found; ++i)
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
			float f = i1 == i0 ? 0 : (blendTreeParameter.FloatValue - motion0Threshold) / (motion1Threshold - motion0Threshold);

			var rv = new NativeList<MotionIndexAndWeight>(2, Allocator.Temp);
			rv.Add(new MotionIndexAndWeight { MotionIndex = i0, Weight = 1 - f });
			rv.Add(new MotionIndexAndWeight { MotionIndex = i1, Weight = f });
			return rv;
		}

		public static NativeList<MotionIndexAndWeight> GetBlendTreeDirectCurrentMotions(ref MotionBlob mb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
		{
			ref var motions = ref mb.BlendTree.Motions;
			var rv = new NativeList<MotionIndexAndWeight>(motions.Length, Allocator.Temp);

			var weightSum = 0.0f;
			for (int i = 0; i < motions.Length; ++i)
			{
				ref var cm = ref motions[i];
				var w = cm.DirectBlendParameterIndex >= 0 ? runtimeParams[cm.DirectBlendParameterIndex].FloatValue : 0;
				if (w > 0)
				{
					var miw = new MotionIndexAndWeight() { MotionIndex = i, Weight = w };
					weightSum += miw.Weight;
					rv.Add(miw);
				}
			}

			if (mb.BlendTree.NormalizeBlendValues && weightSum > 1)
			{
				for (int i = 0; i < rv.Length; ++i)
				{
					var miw = rv[i];
					miw.Weight = miw.Weight / weightSum;
					rv[i] = miw;
				}
			}

			return rv;
		}

		private unsafe float CalculateMotionDuration(ref MotionBlob mb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams, float weight)
		{
			if (weight == 0) return 0;

			NativeList<MotionIndexAndWeight> blendTreeMotionsAndWeights = default;
			switch (mb.MotionType)
			{
			case MotionBlob.Type.None:
				return 1;
			case MotionBlob.Type.AnimationClip:
				return mb.AnimationBlob.Value.Length * weight;
			case MotionBlob.Type.BlendTreeDirect:
				blendTreeMotionsAndWeights = GetBlendTreeDirectCurrentMotions(ref mb, runtimeParams);
				break;
			case MotionBlob.Type.BlendTree1D:
				blendTreeMotionsAndWeights = GetBlendTree1DCurrentMotions(ref mb, runtimeParams);
				break;
			case MotionBlob.Type.BlendTree2DSimpleDirectional:
				blendTreeMotionsAndWeights = GetBlendTree2DSimpleDirectionalCurrentMotions(ref mb, runtimeParams);
				break;
			case MotionBlob.Type.BlendTree2DFreeformCartesian:
				blendTreeMotionsAndWeights = GetBlendTree2DFreeformCartesianCurrentMotions(ref mb, runtimeParams);
				break;
			case MotionBlob.Type.BlendTree2DFreeformDirectional:
				blendTreeMotionsAndWeights = GetBlendTree2DFreeformDirectionalCurrentMotions(ref mb, runtimeParams);
				break;
			default:
				Debug.Log($"Unsupported blend tree type");
				break;
			}

			var rv = CalculateBlendTreeMotionDuration(blendTreeMotionsAndWeights, ref mb.BlendTree.Motions, runtimeParams, weight);
			if (blendTreeMotionsAndWeights.IsCreated) blendTreeMotionsAndWeights.Dispose();
			
			return rv;
		}

		private float CalculateBlendTreeMotionDuration(NativeList<MotionIndexAndWeight> miwArr, ref BlobArray<ChildMotionBlob> motions, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams, float weight)
		{
			if (!miwArr.IsCreated || miwArr.IsEmpty)
				return 1;

			var weightSum = 0.0f;
			for (int i = 0; i < miwArr.Length; ++i)
				weightSum += miwArr[i].Weight;

			//	If total weight less then 1, normalize weights
			if (Hint.Unlikely(weightSum < 1))
			{
				for (int i = 0; i < miwArr.Length; ++i)
				{
					var miw = miwArr[i];
					miw.Weight = miw.Weight / weightSum;
					miwArr[i] = miw;
				}
			}

			var rv = 0.0f;
			for (int i = 0; i < miwArr.Length; ++i)
			{
				var miw = miwArr[i];
				ref var m = ref motions[miw.MotionIndex];
				rv += CalculateMotionDuration(ref m.Motion, runtimeParams, weight * miw.Weight) / m.TimeScale;
			}

			return rv;
		}

		private float CalculateTransitionDuration(ref TransitionBlob tb, float curStateDuration)
		{
			var rv = tb.Duration;
			if (!tb.HasFixedDuration)
			{
				rv *= curStateDuration;
			}
			return math.max(rv, 0.0001f);
		}

		private float CalculateStateDuration(ref StateBlob sb, in DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
		{
			var motionDuration = CalculateMotionDuration(ref sb.Motion, runtimeParams, 1);
			var speedMuliplier = 1.0f;
			if (sb.SpeedMultiplierParameterIndex >= 0)
			{
				speedMuliplier = runtimeParams[sb.SpeedMultiplierParameterIndex].FloatValue;
			}
			return motionDuration / (sb.Speed * speedMuliplier);
		}

		internal static float GetLoopAwareTransitionExitTime(float exitTime, float normalizedDuration, float speedSign)
		{
			var rv = exitTime;
			if (exitTime <= 1.0f)
			{
				//	Unity animator logic and documentation mismatch. Documentation says that exit time loop condition should be when transition exitTime less then 1, but in practice it will loop when exitTime is less or equal(!) to 1.
				exitTime = math.min(exitTime, 0.9999f);
				var snd = normalizedDuration * speedSign;

				var f = math.frac(snd);
				rv += (int)snd;
				if (f > exitTime)
					rv += 1;
			}
			return rv * speedSign;
		}

		private float GetTimeInSecondsShouldBeInTransition(ref TransitionBlob tb, RuntimeAnimatorData.StateRuntimeData curStateRtd, float curStateDuration, float frameDT)
		{
			if (!tb.HasExitTime) return 0;

			//	This should be always less then curStateRTD.normalizedDuration
			var loopAwareExitTime = GetLoopAwareTransitionExitTime(tb.ExitTime, curStateRtd.NormalizedDuration - frameDT, math.sign(frameDT));
			var loopDelta = curStateRtd.NormalizedDuration - loopAwareExitTime;
			var rv = loopDelta * curStateDuration;
			return rv;
		}

		private bool CheckTransitionEnterExitTimeCondition
		(
			ref TransitionBlob tb,
			RuntimeAnimatorData.StateRuntimeData curStateRuntimeData,
			float srcStateDurationFrameDelta
		)
		{
			var normalizedStateDuration = curStateRuntimeData.NormalizedDuration; 

			var noNormalConditions = tb.Conditions.Length == 0;
			if (!tb.HasExitTime) return !noNormalConditions;

			var l0 = normalizedStateDuration - srcStateDurationFrameDelta;
			var l1 = normalizedStateDuration;
			var speedSign = math.select(-1, 1, l0 < l1);

			var loopAwareExitTime = GetLoopAwareTransitionExitTime(tb.ExitTime, l0, speedSign);

			if (speedSign < 0)
				MathUtils.Swap(ref l0, ref l1);

			var rv = loopAwareExitTime > l0 && loopAwareExitTime <= l1;
			return rv;
		}

		private bool CheckIntCondition(in AnimatorControllerParameterComponent param, ref ConditionBlob c)
		{
			var rv = true;
			switch (c.ConditionMode)
			{
			case AnimatorConditionMode.Equals:
				if (param.IntValue != c.Threshold.intValue) rv = false;
				break;
			case AnimatorConditionMode.Greater:
				if (param.IntValue <= c.Threshold.intValue) rv = false;
				break;
			case AnimatorConditionMode.Less:
				if (param.IntValue >= c.Threshold.intValue) rv = false;
				break;
			case AnimatorConditionMode.NotEqual:
				if (param.IntValue == c.Threshold.intValue) rv = false;
				break;
			default:
				Debug.LogError($"Unsupported condition type for int parameter value!");
				break;
			}
			return rv;
		}

		private bool CheckFloatCondition(in AnimatorControllerParameterComponent param, ref ConditionBlob c)
		{
			var rv = true;
			switch (c.ConditionMode)
			{
			case AnimatorConditionMode.Greater:
				if (param.FloatValue <= c.Threshold.floatValue) rv = false;
				break;
			case AnimatorConditionMode.Less:
				if (param.FloatValue >= c.Threshold.floatValue) rv = false;
				break;
			default:
				Debug.LogError($"Unsupported condition type for int parameter value!");
				break;
			}
			return rv;
		}

		private bool CheckBoolCondition(in AnimatorControllerParameterComponent param, ref ConditionBlob c)
		{
			var rv = true;
			switch (c.ConditionMode)
			{
			case AnimatorConditionMode.If:
				rv = param.BoolValue;
				break;
			case AnimatorConditionMode.IfNot:
				rv = !param.BoolValue;
				break;
			default:
				Debug.LogError($"Unsupported condition type for int parameter value!");
				break;
			}
			return rv;
		}

		private void ResetTriggers(ref TransitionBlob tb, ref DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
		{
			for (int i = 0; i < tb.Conditions.Length; ++i)
			{
				ref var c = ref tb.Conditions[i];
				var param = runtimeParams[c.ParamIdx];
				if (param.Type == ControllerParameterType.Trigger)
				{
					param.Value.boolValue = false;
					runtimeParams[c.ParamIdx] = param;
				}
			}
		}

		private bool CheckTransitionEnterConditions(ref TransitionBlob tb, ref DynamicBuffer<AnimatorControllerParameterComponent> runtimeParams)
		{
			if (tb.Conditions.Length == 0)
				return true;

			var rv = true;
			var hasTriggers = false;
			for (int i = 0; i < tb.Conditions.Length && rv; ++i)
			{
				ref var c = ref tb.Conditions[i];
				var param = runtimeParams[c.ParamIdx];

				switch (param.Type)
				{
				case ControllerParameterType.Float:
					rv = CheckFloatCondition(param, ref c);
					break;
				case ControllerParameterType.Int:
					rv = CheckIntCondition(param, ref c);
					break;
				case ControllerParameterType.Bool:
					rv = CheckBoolCondition(param, ref c);
					break;
				case ControllerParameterType.Trigger:
					rv = CheckBoolCondition(param, ref c);
					hasTriggers = true;
					break;
				}
			}

			if (hasTriggers && rv)
				ResetTriggers(ref tb, ref runtimeParams);

			return rv;
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

			ref var c = ref curData.Controller.Value;
			ref var layer = ref c.Layers[curData.LayerIndex];
			ref var currentState = ref layer.States[curData.Rtd.SrcState.Id];

			var layerName = layer.Name.ToFixedString();
			var controllerName = c.Name.ToFixedString();
			var curStateName = currentState.Name.ToFixedString();

			Debug.Log($"[{frameIndex}:{controllerName}:{layerName}] In state: '{curStateName}' with normalized duration: {curData.Rtd.SrcState.NormalizedDuration}");

			//	Exit transition event
			if (prevData.Rtd.ActiveTransition.Id >= 0 && curData.Rtd.ActiveTransition.Id != prevData.Rtd.ActiveTransition.Id)
			{
				ref var t = ref layer.States[prevData.Rtd.SrcState.Id].Transitions[prevData.Rtd.ActiveTransition.Id];
				Debug.Log($"[{frameIndex}:{controllerName}:{layerName}] Exiting transition: '{t.Name.ToFixedString()}'");
			}

			//	Enter transition event
			if (curData.Rtd.ActiveTransition.Id >= 0)
			{
				ref var t = ref layer.States[curData.Rtd.SrcState.Id].Transitions[curData.Rtd.ActiveTransition.Id];
				if (curData.Rtd.ActiveTransition.Id != prevData.Rtd.ActiveTransition.Id)
				{
					Debug.Log($"[{frameIndex}:{controllerName}:{layerName}] Entering transition: '{t.Name.ToFixedString()}'");
				}
				else
				{
					ref var dstState = ref layer.States[curData.Rtd.DstState.Id];
					Debug.Log($"[{frameIndex}:{controllerName}:{layerName}] In transition: '{t.Name.ToFixedString()}' with time: {curData.Rtd.ActiveTransition.NormalizedDuration}");
					Debug.Log($"[{frameIndex}:{controllerName}:{layerName}] Target state: '{dstState.Name.ToFixedString()}' with time: {curData.Rtd.DstState.NormalizedDuration}");
				}
			}
		#endif
		}
	}
}
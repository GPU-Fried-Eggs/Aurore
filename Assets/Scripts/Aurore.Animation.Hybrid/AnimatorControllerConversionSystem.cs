#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;
#if AURORE_DEBUG
using UnityEngine;
#endif

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[RequireMatchingQueriesForUpdate]
public partial class AnimatorControllerConversionSystem: SystemBase
{
	private EntityQuery m_AnimatorsQuery;
	private ComponentLookup<RigDefinitionBakerComponent> m_RigDefComponentLookup;

	public struct AnimatorBlobAssets
	{
		public BlobAssetReference<ControllerBlob> ControllerBlob;
		public BlobAssetReference<ParameterPerfectHashTableBlob> ParametersPerfectHashTableBlob;
	}

	private struct AnimatorControllerBakerDataSorter: IComparer<AnimatorControllerBakerData>
	{
		public int Compare(AnimatorControllerBakerData a, AnimatorControllerBakerData b)
		{
			if (a.Hash < b.Hash) return -1;
			if (a.Hash > b.Hash) return 1;
			return 0;
		}
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		
		using var builder = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<AnimatorControllerBakerData>()
			.WithOptions(EntityQueryOptions.IncludePrefab);

		m_RigDefComponentLookup = GetComponentLookup<RigDefinitionBakerComponent>(true);

		m_AnimatorsQuery = GetEntityQuery(builder);
	}

	protected override void OnDestroy()
	{
		using var controllersData = m_AnimatorsQuery.ToComponentDataArray<AnimatorControllerBakerData>(Allocator.Temp);
		foreach (var c in controllersData) c.ControllerData.Dispose();
	}

	protected override void OnUpdate()
	{
		using var controllersData = m_AnimatorsQuery.ToComponentDataArray<AnimatorControllerBakerData>(Allocator.TempJob);
		using var entities = m_AnimatorsQuery.ToEntityArray(Allocator.TempJob);
		if (controllersData.Length == 0) return;

#if AURORE_DEBUG
		SystemAPI.TryGetSingleton<DebugConfigurationComponent>(out var dc);
		if (dc.logAnimatorBaking) Debug.Log($"=== [AnimatorControllerConversionSystem] BEGIN CONVERSION ===");
#endif

		//	Create blob assets
		using var blobAssetsArr = new NativeArray<AnimatorBlobAssets>(controllersData.Length, Allocator.TempJob);
		controllersData.Sort(new AnimatorControllerBakerDataSorter());

		var startIndex = 0;
		var startHash = controllersData[0].Hash;

		using var jobHandles = new NativeList<JobHandle>(controllersData.Length, Allocator.Temp);

		m_RigDefComponentLookup.Update(this);
		
		for (var i = 1; i <= controllersData.Length; ++i)
		{
			var cd = i < controllersData.Length ? controllersData[i] : default;
			if (cd.Hash != startHash)
			{
				var numDuplicates = i - startIndex;
				var blobAssetsSlice = new NativeSlice<AnimatorBlobAssets>(blobAssetsArr, startIndex, numDuplicates);
				var refController = controllersData[startIndex];
				var createBlobAssetsJob = new CreateBlobAssetsJob
				{
					InData = refController,
					OutBlobAssets = blobAssetsSlice,
#if AURORE_DEBUG
					DoLogging = dc.logAnimatorBaking,
#endif
				};

				var jobHandle = createBlobAssetsJob.Schedule();
				jobHandles.Add(jobHandle);

				startHash = cd.Hash;
				startIndex = i;

				DebugLogging(refController, numDuplicates);
			}
		}

		var combinedJh = JobHandle.CombineDependencies(jobHandles.AsArray());
		using var ecb = new EntityCommandBuffer(Allocator.TempJob);

		var createComponentDatasJob = new CreateComponentDatasJob
		{
			ECB = ecb.AsParallelWriter(),
			BakerData = controllersData,
			BlobAssets = blobAssetsArr
		};

		createComponentDatasJob.ScheduleBatch(controllersData.Length, 32, combinedJh).Complete();

		ecb.Playback(EntityManager);
		OnDestroy();

#if AURORE_DEBUG
		if (dc.logAnimatorBaking)
		{
			Debug.Log($"Total converted animator controllers: {controllersData.Length}");
			Debug.Log($"=== [AnimatorControllerConversionSystem] END CONVERSION ===");
		}
#endif
	}

	private void DebugLogging(AnimatorControllerBakerData a, int numDuplicates)
	{
#if AURORE_DEBUG
		SystemAPI.TryGetSingleton<DebugConfigurationComponent>(out var dc);
		if (!dc.logAnimatorBaking) return;

		Debug.Log($"Creating blob asset for animator: '{a.Name}'. Entities: {numDuplicates}. Clips: {a.ControllerData.AnimationClips.Length}. Parameters: {a.ControllerData.Parameters.Length}. Layers: {a.ControllerData.Layers.Length}");
#endif
	}

	[BurstCompile]
	public struct CreateBlobAssetsJob: IJob
	{
		[NativeDisableContainerSafetyRestriction]
		public NativeSlice<AnimatorBlobAssets> OutBlobAssets;
		public AnimatorControllerBakerData InData;

#if AURORE_DEBUG
		[ReadOnly] public bool DoLogging;
#endif

		private void AddTransitionBlob(RTP.Transition transition,
			UnsafeList<RTP.State> allStates,
			UnsafeList<RTP.Parameter> allParams,
			ref BlobBuilder builder,
			ref TransitionBlob transitionBlob)
		{
#if AURORE_DEBUG
			builder.AllocateString(ref transitionBlob.Name, ref transition.Name);
#endif

			var bbc = builder.Allocate(ref transitionBlob.Conditions, transition.Conditions.Length);
			for (var ci = 0; ci < transition.Conditions.Length; ++ci)
			{
				ref var cb = ref bbc[ci];
				var src = transition.Conditions[ci];
				cb.ConditionMode = src.ConditionMode;
				cb.ParamIdx = allParams.IndexOf(src.ParamName);
				cb.Threshold = src.Threshold;

#if AURORE_DEBUG
				builder.AllocateString(ref cb.Name, ref src.Name);
#endif
			}

			transitionBlob.Hash = transition.Name.CalculateHash32();
			transitionBlob.Duration = transition.Duration;
			transitionBlob.ExitTime = transition.ExitTime;
			transitionBlob.HasExitTime = transition.HasExitTime;
			transitionBlob.Offset = transition.Offset;
			transitionBlob.HasFixedDuration = transition.HasFixedDuration;
			transitionBlob.TargetStateId = allStates.IndexOf(transition.TargetStateHash);
		}

		private void AddChildMotionBlob(RTP.ChildMotion cm,
			ref BlobBuilder bb,
			ref ChildMotionBlob cmb,
			ref BlobBuilderArray<AnimationClipBlob> allAnims,
			in UnsafeList<RTP.Parameter> allParams)
		{
			cmb.Threshold = cm.Threshold;
			cmb.TimeScale = cm.TimeScale;
			cmb.Position2D = cm.Position2D;
			cmb.DirectBlendParameterIndex = allParams.IndexOf(cm.DirectBlendParameterName);
			AddMotionBlob(cm.Motion, ref bb, ref cmb.Motion, ref allAnims, allParams);
		}

		private void AddMotionBlob(RTP.Motion motion,
			ref BlobBuilder builder,
			ref MotionBlob motionBlob,
			ref BlobBuilderArray<AnimationClipBlob> allAnims,
			in UnsafeList<RTP.Parameter> allParams)
		{
#if AURORE_DEBUG
			builder.AllocateString(ref motionBlob.Name, ref motion.Name);
#endif

			motionBlob.MotionType = motion.Type;
			if (motion.AnimationIndex >= 0 && motion.Type == MotionBlob.Type.AnimationClip)
			{
				ref var ab = ref builder.SetPointer(ref motionBlob.AnimationBlob, ref allAnims[motion.AnimationIndex]);
			}

			if (motion.Type != MotionBlob.Type.None && motion.Type != MotionBlob.Type.AnimationClip)
			{
				ref var blendTreeBlob = ref motionBlob.BlendTree;
				var bbm = builder.Allocate(ref blendTreeBlob.Motions, motion.BlendTree.Motions.Length);
				for (var i = 0; i < bbm.Length; ++i)
				{
					AddChildMotionBlob(motion.BlendTree.Motions[i], ref builder, ref bbm[i], ref allAnims, allParams);
				}
				blendTreeBlob.BlendParameterIndex = allParams.IndexOf(motion.BlendTree.BlendParameterName);
				blendTreeBlob.BlendParameterYIndex = allParams.IndexOf(motion.BlendTree.BlendParameterYName);
				blendTreeBlob.NormalizeBlendValues = motion.BlendTree.NormalizeBlendValues;

#if AURORE_DEBUG
				builder.AllocateString(ref blendTreeBlob.Name, ref motion.BlendTree.Name);
#endif
			}
		}

		private void AddStateBlob(RTP.State state,
			ref BlobBuilder builder,
			ref StateBlob stateBlob,
			ref BlobBuilderArray<AnimationClipBlob> allAnims,
			UnsafeList<RTP.Transition> anyStateTransitions,
			UnsafeList<RTP.State> allStates,
			UnsafeList<RTP.Parameter> allParams)
		{
#if AURORE_DEBUG
			builder.AllocateString(ref stateBlob.Name, ref state.Name);
#endif

			stateBlob.Hash = state.Name.CalculateHash32();
			stateBlob.Speed = state.Speed;
			stateBlob.SpeedMultiplierParameterIndex = allParams.IndexOf(state.SpeedMultiplierParameter);
			stateBlob.TimeParameterIndex = allParams.IndexOf(state.TimeParameter);
			stateBlob.CycleOffset = state.CycleOffset;
			stateBlob.CycleOffsetParameterIndex = allParams.IndexOf(state.CycleOffsetParameter);

			var bbt = builder.Allocate(ref stateBlob.Transitions, state.Transitions.Length + anyStateTransitions.Length);

			//	Any state transitions are first priority
			for (var ti = 0; ti < anyStateTransitions.Length; ++ti)
			{
				var ast = anyStateTransitions[ti];
				//	Do not add transitions to self according to flag
				if (ast.CanTransitionToSelf || ast.TargetStateHash != state.HashCode)
					AddTransitionBlob(ast, allStates, allParams, ref builder, ref bbt[ti]);
			}

			for (var ti = 0; ti < state.Transitions.Length; ++ti)
			{
				var src = state.Transitions[ti];
				AddTransitionBlob(src, allStates, allParams, ref builder, ref bbt[ti + anyStateTransitions.Length]);
			}

			//	Add motion
			AddMotionBlob(state.Motion, ref builder, ref stateBlob.Motion, ref allAnims, allParams);
		}

		private void AddKeyFrameArray(UnsafeList<KeyFrame> kf, ref BlobBuilderArray<KeyFrame> outKf)
		{
			for (var i = 0; i < kf.Length; ++i)
			{
				outKf[i] = kf[i];
			}
		}

		private void AddBoneClipArr(ref BlobBuilder builder,
			ref BlobArray<BoneClipBlob> bonesBlob,
			in UnsafeList<RTP.BoneClip> inData,
			in NativeArray<Hash128> hashesArr)
		{
			var bonesArr = builder.Allocate(ref bonesBlob, inData.Length);
			for (var i = 0; i < bonesArr.Length; ++i)
			{
				ref var boneBlob = ref bonesArr[i];
				var boneInData = inData[i];

				var anmCurvesArr = builder.Allocate(ref boneBlob.AnimationCurves, boneInData.AnimationCurves.Length);
				for (var l = 0; l < boneInData.AnimationCurves.Length; ++l)
				{
					var anmCurveData = boneInData.AnimationCurves[l];
					ref var anmCurveBlob = ref anmCurvesArr[l];
					var keyFramesArr = builder.Allocate(ref anmCurveBlob.KeyFrames, anmCurveData.KeyFrames.Length);

					anmCurveBlob.ChannelIndex = anmCurveData.ChannelIndex;
					anmCurveBlob.BindingType = anmCurveData.BindingType;
					AddKeyFrameArray(anmCurveData.KeyFrames, ref keyFramesArr);
				}

#if AURORE_DEBUG
				builder.AllocateString(ref boneBlob.Name, ref boneInData.Name);
#endif
				boneBlob.Hash = hashesArr[i];
				boneBlob.IsHumanMuscleClip = boneInData.IsHumanMuscleClip;
			}
		}

		private NativeArray<Hash128> ConstructClipsHashes(in UnsafeList<RTP.BoneClip> boneClips)
		{
			var hashCodes = new NativeArray<Hash128>(boneClips.Length, Allocator.Temp);
			for (var i = 0; i < boneClips.Length; ++i)
			{
				hashCodes[i] = boneClips[i].NameHash;
			}

			return hashCodes;
		}

		private void AddAnimationClipBlob(RTP.AnimationClip animationClip, ref BlobBuilder builder, ref AnimationClipBlob animationClipBlob)
		{
#if AURORE_DEBUG
			builder.AllocateString(ref animationClipBlob.Name, ref animationClip.Name);
#endif

			animationClipBlob.Hash = animationClip.Hash;

			var boneHashes = ConstructClipsHashes(animationClip.Bones);
			var curveHashes = ConstructClipsHashes(animationClip.Curves);

			var boneReinterpretedHashes = boneHashes.Reinterpret<Hash128PerfectHashed>();
			PerfectHash<Hash128PerfectHashed>.CreateMinimalPerfectHash(boneReinterpretedHashes, out var seedValues, out var shuffleIndices);
			MathUtils.ShuffleArray(animationClip.Bones.AsSpan(), shuffleIndices.AsArray());
			MathUtils.ShuffleArray(boneHashes.AsSpan(), shuffleIndices.AsArray());

			var bonePerfectHashSeeds = builder.Allocate(ref animationClipBlob.BonesPerfectHashSeedTable, seedValues.Length);
			for (var i = 0; i < seedValues.Length; ++i) bonePerfectHashSeeds[i] = seedValues[i];
			
			AddBoneClipArr(ref builder, ref animationClipBlob.Bones, animationClip.Bones, boneHashes);
			AddBoneClipArr(ref builder, ref animationClipBlob.Curves, animationClip.Curves, curveHashes);

			animationClipBlob.Looped = animationClip.Looped;
			animationClipBlob.Length = animationClip.Length;
			animationClipBlob.LoopPoseBlend = animationClip.LoopPoseBlend;
			animationClipBlob.CycleOffset = animationClip.CycleOffset;
			animationClipBlob.AdditiveReferencePoseTime = animationClip.AdditiveReferencePoseTime;
			animationClipBlob.HasRootMotionCurves = animationClip.HasRootMotionCurves;
		}

		private void AddAvatarMaskBlob(RTP.AvatarMask avatarMask, ref BlobBuilder builder, ref AvatarMaskBlob avatarMaskBlob)
		{
			avatarMaskBlob.Hash = avatarMask.Hash;
			avatarMaskBlob.HumanBodyPartsAvatarMask = avatarMask.HumanBodyPartsAvatarMask;

			if (avatarMask.Name.Length != 0)
			{
#if AURORE_DEBUG
				builder.AllocateString(ref avatarMaskBlob.Name, ref avatarMask.Name);
#endif
			}

			var avatarMaskArr = builder.Allocate(ref avatarMaskBlob.IncludedBoneHashes, avatarMask.IncludedBonePaths.Length);
			for (var i = 0; i < avatarMaskArr.Length; ++i)
			{
				var ibp = avatarMask.IncludedBonePaths[i];
				avatarMaskArr[i] = ibp.CalculateHash128();
			}

#if AURORE_DEBUG
			var avatarMaskNameArr = builder.Allocate(ref avatarMaskBlob.IncludedBoneNames, avatarMask.IncludedBonePaths.Length);
			for (var i = 0; i < avatarMaskNameArr.Length; ++i)
			{
				var ibp = avatarMask.IncludedBonePaths[i];
				builder.AllocateString(ref avatarMaskNameArr[i], ref ibp);
			}
#endif
		}

		private void AddAllAnimationClips(ref BlobBuilder builder,
			ref ControllerBlob controllerBlob,
			in RTP.Controller controller,
			out BlobBuilderArray<AnimationClipBlob> animationClipBlobs)
		{
			animationClipBlobs = builder.Allocate(ref controllerBlob.AnimationClips, controller.AnimationClips.Length);
			for (var ai = 0; ai < controller.AnimationClips.Length; ++ai)
			{
				var src = controller.AnimationClips[ai];
				ref var clip = ref animationClipBlobs[ai];
				AddAnimationClipBlob(src, ref builder, ref clip);
			}
		}

		internal static BlobAssetReference<ParameterPerfectHashTableBlob> CreateParametersPerfectHashTableBlob(in NativeArray<uint> hashesArr)
		{
			var hashesReinterpretedArr = hashesArr.Reinterpret<UIntPerfectHashed>();
			PerfectHash<UIntPerfectHashed>.CreateMinimalPerfectHash(hashesReinterpretedArr, out var seedValues, out var shuffleIndices);

			using var bb2 = new BlobBuilder(Allocator.Temp);
			ref var ppb = ref bb2.ConstructRoot<ParameterPerfectHashTableBlob>();
			var bbh = bb2.Allocate(ref ppb.SeedTable, hashesArr.Length);
			for (var hi = 0; hi < hashesArr.Length; ++hi)
			{
				ref var paramRef = ref bbh[hi];
				paramRef = seedValues[hi];
			}
		
			var bbia = bb2.Allocate(ref ppb.IndirectionTable, shuffleIndices.Length);
			for (var ii = 0; ii < shuffleIndices.Length; ++ii)
			{
				ref var indirectionIndex = ref bbia[ii];
				indirectionIndex = shuffleIndices[ii];
			}

			seedValues.Dispose();
			shuffleIndices.Dispose();

			return bb2.CreateBlobAssetReference<ParameterPerfectHashTableBlob>(Allocator.Persistent);
		}

		private BlobAssetReference<ParameterPerfectHashTableBlob> AddAllParameters(ref BlobBuilder bb, ref ControllerBlob c, RTP.Controller data)
		{
			//	Create perfect hash table and indirection array
			var hashesArr = new NativeArray<uint>(data.Parameters.Length, Allocator.Temp);
			for (var l = 0; l < data.Parameters.Length; ++l)
			{
				hashesArr[l] = data.Parameters[l].Name.CalculateHash32();
			}

			//	Now place parameters in its original places as in authoring animator
			var bba = bb.Allocate(ref c.Parameters, data.Parameters.Length);
			for	(var pi = 0; pi < data.Parameters.Length; ++pi)
			{
				var src = data.Parameters[pi];
				ref var p = ref bba[pi];
				p.DefaultValue = src.DefaultValue;
#if AURORE_DEBUG
				bb.AllocateString(ref p.Name, ref src.Name);
#endif
				p.Hash = hashesArr[pi];
				p.Type = src.Type;
			}

			//	Create separate blob asset for perfect hash table, but only if number of parameters is big enough
			var rv = new BlobAssetReference<ParameterPerfectHashTableBlob>();
			if (data.Parameters.Length > 10)
				rv = CreateParametersPerfectHashTableBlob(hashesArr);

			hashesArr.Dispose();

			return rv;
		}

		private void AddAllLayers(ref BlobBuilder bb, ref ControllerBlob c, ref BlobBuilderArray<AnimationClipBlob> bbc, RTP.Controller data)
		{
			var bbl = bb.Allocate(ref c.Layers, data.Layers.Length);
			for (var li = 0; li < data.Layers.Length; ++li)
			{
				var src = data.Layers[li];
				ref var l = ref bbl[li];

#if AURORE_DEBUG
				bb.AllocateString(ref l.Name, ref src.Name);
#endif

				l.DefaultStateIndex = src.DefaultStateIndex;
				l.BlendingMode = src.BlendMode;

				// States
				var bbs = bb.Allocate(ref l.States, src.States.Length);
				for (var si = 0; si < src.States.Length; ++si)
				{
					var s = src.States[si];
					AddStateBlob(s, ref bb, ref bbs[si], ref bbc, src.AnyStateTransitions, src.States, data.Parameters);
				}

				if (src.AvatarMask.Hash.IsValid)
					AddAvatarMaskBlob(src.AvatarMask, ref bb, ref l.AvatarMask);
			}
		}

		public void Execute()
		{
			var data = InData.ControllerData;
			var bb = new BlobBuilder(Allocator.Temp);
			ref var c = ref bb.ConstructRoot<ControllerBlob>();

#if AURORE_DEBUG
			bb.AllocateString(ref c.Name, ref data.Name);
#endif

			AddAllAnimationClips(ref bb, ref c, data, out var bbc);
			var parameterPerfectHashTableBlob = AddAllParameters(ref bb, ref c, data);
			AddAllLayers(ref bb, ref c, ref bbc, data);

			var rv = bb.CreateBlobAssetReference<ControllerBlob>(Allocator.Persistent);

			//	Entire slice has same blob assets
			for (var i = 0; i < OutBlobAssets.Length; ++i)
			{
				OutBlobAssets[i] = new AnimatorBlobAssets { ControllerBlob = rv, ParametersPerfectHashTableBlob = parameterPerfectHashTableBlob };
			}

#if AURORE_DEBUG
			if (OutBlobAssets.Length > 0 && DoLogging)
				LogAnimatorBakeing(OutBlobAssets[0]);
#endif
		}

		private void LogAnimatorBakeing(AnimatorBlobAssets aba)
		{
#if AURORE_DEBUG
			Debug.Log($"BAKING Controller: {aba.ControllerBlob.Value.Name.ToFixedString()}");
			ref var cb = ref aba.ControllerBlob.Value;
			for (var i = 0; i < cb.Layers.Length; ++i)
			{
				ref var l = ref cb.Layers[i];
				Debug.Log($"-> Layer `{l.Name.ToFixedString()}` states count: {l.States.Length}, blend mode: {(int)l.BlendingMode}, default state index: {l.DefaultStateIndex}");

				for (var k = 0; k < l.States.Length; ++k)
				{
					ref var state = ref l.States[k];
					Debug.Log($"--> State `{state.Name.ToFixedString()}` motion: {state.Motion.Name.ToFixedString()}, cycle offset: {state.CycleOffset}, speed: {state.Speed}, speed param index: {state.SpeedMultiplierParameterIndex}, time param index: {state.TimeParameterIndex}, transitions count: {state.Transitions.Length}");

					for (var m = 0; m < state.Transitions.Length; ++m)
					{
						ref var tr = ref state.Transitions[m];
						Debug.Log($"---> Transition `{tr.Name.ToFixedString()}` duration: {tr.Duration}, offset: {tr.Offset}, has fixed duration: {tr.HasFixedDuration}, has exit time: {tr.HasExitTime}, exit time: {tr.ExitTime}, offset: {tr.Offset}, conditions count: {tr.Conditions.Length}");

						for (var n = 0; n < tr.Conditions.Length; ++n)
						{
							ref var cnd = ref tr.Conditions[n];
							Debug.Log($"----> Condition `{cnd.Name.ToFixedString()}` mode: {(int)cnd.ConditionMode}, param index: {cnd.ParamIdx}, threshold: {cnd.Threshold.floatValue}");
						}
					}
				}
			}

			ref var pms = ref cb.Parameters;
			Debug.Log($"Total parameters count: {pms.Length}");

			for (var i = 0; i < pms.Length; ++i)
			{
				ref var pm = ref pms[i];
				switch (pm.Type)
				{
				case ControllerParameterType.Int:
					Debug.Log($"Parameter `{pm.Name.ToFixedString()}` type: Int, default value: {pm.DefaultValue.intValue}");
					break;
				case ControllerParameterType.Float:
					Debug.Log($"Parameter `{pm.Name.ToFixedString()}` type: Float, default value: {pm.DefaultValue.floatValue}");
					break;
				case ControllerParameterType.Bool:
					Debug.Log($"Parameter `{pm.Name.ToFixedString()}` type: Bool, default value: {pm.DefaultValue.boolValue}");
					break;
				case ControllerParameterType.Trigger:
					Debug.Log($"Parameter `{pm.Name.ToFixedString()}` type: Trigger, default value: {pm.DefaultValue.boolValue}");
					break;
				}
			}

			ref var anms = ref cb.AnimationClips;
			Debug.Log($"Total animation clips: {anms.Length}");

			for (var i = 0; i < anms.Length; ++i)
			{
				ref var anm = ref anms[i];
				Debug.Log($"Animation `{anm.Name.ToFixedString()}`");
			}

			Debug.Log($"END Controller: {aba.ControllerBlob.Value.Name.ToFixedString()}");
#endif
		}
	}

	[BurstCompile]
	private struct CreateComponentDatasJob: IJobParallelForBatch
	{
		[ReadOnly]
		public NativeArray<AnimatorControllerBakerData> BakerData;
		[ReadOnly]
		public NativeArray<AnimatorBlobAssets> BlobAssets;

		public EntityCommandBuffer.ParallelWriter ECB;

		public void Execute(int startIndex, int count)
		{
			for (var i = startIndex; i < startIndex + count; ++i)
			{
				var bd = BakerData[i];
				var e = bd.TargetEntity;
				var ba = BlobAssets[i];

				var acc = new AnimatorControllerLayerComponent();
				acc.Rtd = RuntimeAnimatorData.MakeDefault();
				acc.Controller = ba.ControllerBlob;

				var buf = ECB.AddBuffer<AnimatorControllerLayerComponent>(startIndex, e);
				ref var cb = ref ba.ControllerBlob.Value;
				for (var k = 0; k < cb.Layers.Length; ++k)
				{
					acc.LayerIndex = k;
					acc.Weight = bd.ControllerData.Layers[k].Weight;
					buf.Add(acc);
				}

				//	Add animation to process buffer
				ECB.AddBuffer<AnimationToProcessComponent>(startIndex, e);

				if (cb.Parameters.Length > 0)
				{
					//	Add dynamic parameters
					var paramArray = ECB.AddBuffer<AnimatorControllerParameterComponent>(startIndex, e);
					for (var p = 0; p < cb.Parameters.Length; ++p)
					{
						ref var pm = ref cb.Parameters[p];
						var acpc = new AnimatorControllerParameterComponent
						{
							Value = pm.DefaultValue,
							Hash = pm.Hash,
							Type = pm.Type,
						};

#if AURORE_DEBUG
						pm.Name.CopyTo(ref acpc.Name);
#endif

						paramArray.Add(acpc);
					}

					if (ba.ParametersPerfectHashTableBlob.IsCreated)
					{
						//	Add perfect hash table used to fast runtime parameter value lookup
						var pht = new AnimatorControllerParameterIndexTableComponent
						{
							SeedTable = ba.ParametersPerfectHashTableBlob
						};
						ECB.AddComponent(startIndex, e, pht);
					}
				}
			}
		}
	}
}
#endif
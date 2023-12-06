#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FixedStringName = Unity.Collections.FixedString512Bytes;
using Hash128 = Unity.Entities.Hash128;

[TemporaryBakingType]
public struct AnimatorControllerBakerData: IComponentData
{
	public RTP.Controller ControllerData;
	public Entity TargetEntity;
	public Hash128 Hash;
#if AURORE_DEBUG
	public FixedStringName Name;
#endif
}

public class AnimatorControllerBaker: Baker<Animator>
{
	public override void Bake(Animator authoring)
	{
		//	Skip animators without rig definition
		if (!authoring.GetComponent<RigDefinitionAuthoring>()) return;

		if (authoring.runtimeAnimatorController == null)
		{
			Debug.LogWarning($"There is no controller attached to '{authoring.name}' animator. Skipping this object");
			return;
		}

		var runtimeAnimaController = GetRuntimeAnimatorController(authoring);
		var animController = GetAnimatorControllerFromRuntime(runtimeAnimaController);
		var animHashCodes = GatherUnityAnimationsHashCodes(runtimeAnimaController.animationClips);
		var controller = GenerateControllerComputationData(animController, animHashCodes);

		//	If AnimatorOverrideController is used, substitute animations
		var animOverrideController = authoring.runtimeAnimatorController as AnimatorOverrideController;
		var animClipsWithOverride = animOverrideController != null ? animOverrideController.animationClips : runtimeAnimaController.animationClips;
		var	animationClips = ConvertAllControllerAnimations(animClipsWithOverride, authoring);
		controller.AnimationClips = animationClips;

		var entity = GetEntity(TransformUsageFlags.Dynamic);
		var data = new AnimatorControllerBakerData
		{
			ControllerData = controller,
			TargetEntity = GetEntity(TransformUsageFlags.Dynamic),
			Hash = GetControllerHashCode(animController, authoring.avatar),
#if AURORE_DEBUG
			Name = authoring.name
#endif
		};

		MakeDependencies(animController, runtimeAnimaController);
		AddComponent(entity, data);
	}

	private Hash128 GetControllerHashCode(AnimatorController controller, Avatar avatar)
	{
		//	Need to make unique animator controller for every prefab because of uniqueness of hips and root animation tracks
		var controllerHashCode = (uint)controller.GetHashCode();
		var avatarHashCode = avatar != null ? (uint)avatar.GetHashCode() : 0u;

		return new Hash128(controllerHashCode, avatarHashCode, 0, 0);
	}

	private void MakeDependencies(AnimatorController controller, RuntimeAnimatorController runtimeController)
	{
		DependsOn(runtimeController);
		DependsOn(controller);

		var animationClips = runtimeController.animationClips;
		foreach (var clip in animationClips)
		{
			DependsOn(clip);
		}
	}

	private RuntimeAnimatorController GetRuntimeAnimatorController(Animator animator)
	{
		var runtimeController = animator.runtimeAnimatorController;
		//	Check for animator override controller
		var runtimeControllerOverriden = runtimeController as AnimatorOverrideController;
		if (runtimeControllerOverriden != null)
		{
			runtimeController = runtimeControllerOverriden.runtimeAnimatorController;
		}

		return runtimeController;
	}

	private AnimatorController GetAnimatorControllerFromRuntime(RuntimeAnimatorController runtimeController)
	{
		if (runtimeController == null) return null;
		var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(runtimeController));

		return controller;
	}

	private List<int> GatherUnityAnimationsHashCodes(AnimationClip[] allClips)
	{
		allClips = Deduplicate(allClips);

		var hashCodes = new List<int>();
		for (var i = 0; i < allClips.Length; ++i)
			hashCodes.Add(allClips[i].GetHashCode());

		return hashCodes;
	}

	private RTP.Controller GenerateControllerComputationData(AnimatorController ac, List<int> allClipsHashCodes)
	{
		var bakedController = new RTP.Controller();
		bakedController.Name = ac.name;
		bakedController.Parameters = GenerateControllerParametersComputationData(ac.parameters);

		bakedController.Layers = new UnsafeList<RTP.Layer>(ac.layers.Length, Allocator.Persistent);

		for (var i = 0; i < ac.layers.Length; ++i)
		{
			var l = ac.layers[i];
			var lOverriden = l.syncedLayerIndex >= 0 ? ac.layers[l.syncedLayerIndex] : l;
			var layerData = GenerateControllerLayerComputationData(lOverriden, l, allClipsHashCodes, i, bakedController.Parameters);
			if (!layerData.States.IsEmpty)
				bakedController.Layers.Add(layerData);
		}

		return bakedController;
	}

	private UnsafeList<RTP.Parameter> GenerateControllerParametersComputationData(AnimatorControllerParameter[] parameters)
	{
		var bakedParams = new UnsafeList<RTP.Parameter>(parameters.Length, Allocator.Persistent);
		for (var i = 0; i < parameters.Length; ++i)
		{
			var sourceParam = parameters[i];
			var bakedParam = new RTP.Parameter();

			switch (sourceParam.type)
			{
				case AnimatorControllerParameterType.Float:
					bakedParam.Type = ControllerParameterType.Float;
					bakedParam.DefaultValue.floatValue = sourceParam.defaultFloat;
					break;
				case AnimatorControllerParameterType.Int:
					bakedParam.Type = ControllerParameterType.Int;
					bakedParam.DefaultValue.intValue = sourceParam.defaultInt;
					break;
				case AnimatorControllerParameterType.Bool:
					bakedParam.Type = ControllerParameterType.Bool;
					bakedParam.DefaultValue.boolValue = sourceParam.defaultBool;
					break;
				case AnimatorControllerParameterType.Trigger:
					bakedParam.Type = ControllerParameterType.Trigger;
					bakedParam.DefaultValue.boolValue = sourceParam.defaultBool;
					break;
			}

			bakedParam.Name = sourceParam.name;
			bakedParams.Add(bakedParam);
		}

		return bakedParams;
	}

	private RTP.Layer GenerateControllerLayerComputationData(AnimatorControllerLayer controllerLayer,
		AnimatorControllerLayer aclOverriden,
		List<int> allClipsHashCodes,
		int layerIndex,
		in UnsafeList<RTP.Parameter> allParams)
	{
		var bakedLayer = new RTP.Layer();
		bakedLayer.Name = controllerLayer.name;

		var stateList = new UnsafeList<RTP.State>(128, Allocator.Persistent);
		var anyStateTransitions = new UnsafeList<RTP.Transition>(128, Allocator.Persistent);

		GenerateControllerStateMachineComputationData(controllerLayer.stateMachine, null, controllerLayer, aclOverriden, allClipsHashCodes, ref stateList, ref anyStateTransitions, allParams);
		bakedLayer.AvatarMask = AvatarMaskConversionSystem.PrepareAvatarMaskComputeData(controllerLayer.avatarMask);
		bakedLayer.States = stateList;

		var defaultState = controllerLayer.stateMachine.defaultState;

		bakedLayer.DefaultStateIndex = defaultState == null ? -1 : stateList.IndexOf(defaultState.GetHashCode());
		bakedLayer.AnyStateTransitions = anyStateTransitions;
		bakedLayer.Weight = layerIndex == 0 ? 1 : aclOverriden.defaultWeight;
		bakedLayer.BlendMode = (AnimationBlendingMode)aclOverriden.blendingMode;

		return bakedLayer;
	}

	private RTP.Condition GenerateControllerConditionComputationData(AnimatorCondition condition, in UnsafeList<RTP.Parameter> allParams)
	{
		var bakedCondition = new RTP.Condition();
		bakedCondition.ParamName = condition.parameter;

		var paramIdx = allParams.IndexOf(bakedCondition.ParamName);
		var param = allParams[paramIdx];

		switch (param.Type)
		{
			case ControllerParameterType.Int:
				bakedCondition.Threshold.intValue = (int)condition.threshold;
				break;
			case ControllerParameterType.Float:
				bakedCondition.Threshold.floatValue = condition.threshold;
				break;
			case ControllerParameterType.Bool:
			case ControllerParameterType.Trigger:
				bakedCondition.Threshold.boolValue = condition.threshold > 0;
				break;
		}

		bakedCondition.ConditionMode = (AnimatorConditionMode)condition.mode;
		bakedCondition.Name = $"{bakedCondition.ParamName} {bakedCondition.ConditionMode} {bakedCondition.Threshold}";

		return bakedCondition;
	}

	private RTP.Transition GenerateTransitionDataBetweenStates(AnimatorStateTransition stateTransition,
		string ownStateName,
		AnimatorState dstState,
		AnimatorCondition[] conditions,
		in UnsafeList<RTP.Parameter> allParams)
	{
		var bakedTransition = new RTP.Transition();

		bakedTransition.Duration = stateTransition.duration;
		bakedTransition.ExitTime = stateTransition.exitTime;
		bakedTransition.HasExitTime = stateTransition.hasExitTime;
		bakedTransition.HasFixedDuration = stateTransition.hasFixedDuration;
		bakedTransition.Offset = stateTransition.offset;
		bakedTransition.TargetStateHash = dstState.GetHashCode();
		bakedTransition.Conditions = new UnsafeList<RTP.Condition>(conditions.Length, Allocator.Persistent);
		bakedTransition.SoloFlag = stateTransition.solo;
		bakedTransition.MuteFlag = stateTransition.mute;
		bakedTransition.CanTransitionToSelf = stateTransition.canTransitionToSelf;

		bakedTransition.Name = stateTransition.name != "" ? stateTransition.name : $"{ownStateName} -> {dstState.name}";

		for (var i = 0; i < conditions.Length; ++i)
		{
			bakedTransition.Conditions.Add(GenerateControllerConditionComputationData(conditions[i], allParams));
		}

		return bakedTransition;
	}

	private NativeList<RTP.Transition> GenerateControllerTransitionComputationData(AnimatorStateTransition stateTransition,
		AnimatorStateMachine ourStateMachine,
		AnimatorStateMachine parentStateMachine,
		string ownStateName,
		in UnsafeList<RTP.Parameter> allParams)
	{
		//	Because exit and enter states of substatemachines can have several transitions with different conditions this function can generate several transitions
		var bakedTransitions = new NativeList<RTP.Transition>(Allocator.Temp);
		if (stateTransition.destinationState != null)
		{
			var outT = GenerateTransitionDataBetweenStates(stateTransition, ownStateName, stateTransition.destinationState, stateTransition.conditions, allParams);
			bakedTransitions.Add(outT);
		}
		else
		{
			if (stateTransition.destinationStateMachine == null)
			{
				//	This is exit state transition. Transition to parent state machine default state
				//	If there is no parent statemachine then go to own statemachine default state
				var targetState = parentStateMachine == null ? ourStateMachine.defaultState : parentStateMachine.defaultState;
				var outToParentSm = GenerateTransitionDataBetweenStates(stateTransition, ownStateName, targetState, stateTransition.conditions, allParams);
				bakedTransitions.Add(outToParentSm);
			}
			else
			{
				//	Generate transitions to every state connected with entry state
				var conditionsArr = new List<AnimatorCondition>(stateTransition.conditions);
				var initialConditionsLen = conditionsArr.Count;
				for (var i = 0; i < stateTransition.destinationStateMachine.entryTransitions.Length; ++i)
				{
					conditionsArr.RemoveRange(initialConditionsLen, conditionsArr.Count - initialConditionsLen);
					var e = stateTransition.destinationStateMachine.entryTransitions[i];
					conditionsArr.AddRange(e.conditions);
					var outEntryT = GenerateTransitionDataBetweenStates(stateTransition, ownStateName, e.destinationState, conditionsArr.ToArray(), allParams);
					bakedTransitions.Add(outEntryT);
				}

				//	Add transition to the default state of target state machine with lowest priority
				var outT = GenerateTransitionDataBetweenStates(stateTransition, ownStateName, stateTransition.destinationStateMachine.defaultState, stateTransition.conditions, allParams);
				bakedTransitions.Add(outT);
			}
		}

		return bakedTransitions;
	}

	private RTP.ChildMotion GenerateChildMotionComputationData(ChildMotion childMotion, List<int> allClipsHashCodes)
	{
		var bakedChildMotion = new RTP.ChildMotion();
		bakedChildMotion.Threshold = childMotion.threshold;
		bakedChildMotion.TimeScale = childMotion.timeScale;
		bakedChildMotion.DirectBlendParameterName = childMotion.directBlendParameter;
		//	Data for 2D blend trees
		bakedChildMotion.Position2D = childMotion.position;
		bakedChildMotion.Motion = GenerateMotionComputationData(childMotion.motion, allClipsHashCodes);

		return bakedChildMotion;
	}

	private RTP.Motion GenerateMotionComputationData(Motion motion, List<int> allClipsHashCodes)
	{
		var bakedMotion = new RTP.Motion();
		bakedMotion.AnimationIndex = -1;

		if (motion == null)
		{
			bakedMotion.Name = "NULL_MOTION";
			return bakedMotion;
		}

		bakedMotion.Name = motion.name;

		var animClip = motion as AnimationClip;
		if (animClip)
		{
			bakedMotion.AnimationIndex = allClipsHashCodes.IndexOf(animClip.GetHashCode());
			bakedMotion.Type = MotionBlob.Type.AnimationClip;
		}

		var blendTree = motion as BlendTree;
		if (blendTree)
		{
			bakedMotion.Type = blendTree.blendType switch
			{
				BlendTreeType.Simple1D => MotionBlob.Type.BlendTree1D,
				BlendTreeType.Direct => MotionBlob.Type.BlendTreeDirect,
				BlendTreeType.SimpleDirectional2D => MotionBlob.Type.BlendTree2DSimpleDirectional,
				BlendTreeType.FreeformDirectional2D => MotionBlob.Type.BlendTree2DFreeformDirectional,
				BlendTreeType.FreeformCartesian2D => MotionBlob.Type.BlendTree2DFreeformCartesian,
				_ => MotionBlob.Type.None
			};
			bakedMotion.BlendTree = new RTP.BlendTree();
			bakedMotion.BlendTree.Name = blendTree.name;
			bakedMotion.BlendTree.Motions = new UnsafeList<RTP.ChildMotion>(blendTree.children.Length, Allocator.Persistent);
			bakedMotion.BlendTree.BlendParameterName = blendTree.blendParameter;
			bakedMotion.BlendTree.BlendParameterYName = blendTree.blendParameterY;
			bakedMotion.BlendTree.NormalizeBlendValues = GetNormalizedBlendValuesProp(blendTree);
			for (var i = 0; i < blendTree.children.Length; ++i)
			{
				var child = blendTree.children[i];
				if (child.motion != null)
				{
					var childMotion = GenerateChildMotionComputationData(blendTree.children[i], allClipsHashCodes);
					bakedMotion.BlendTree.Motions.Add(childMotion);
				}
			}
		}

		return bakedMotion;
	}

	private bool GetNormalizedBlendValuesProp(BlendTree blendTree)
	{
		//	Hacky way to extract "Normalized Blend Values" prop
		var propertyFlag = false;

		using (var so = new SerializedObject(blendTree))
		{
			var p = so.FindProperty("m_NormalizedBlendValues");
			if (p != null) propertyFlag = p.boolValue;
		}

		return propertyFlag;
	}

	private RTP.State GenerateControllerStateComputationData(AnimatorState state,
		AnimatorStateMachine ourStateMachine,
		AnimatorStateMachine parentStateMachine,
		AnimatorControllerLayer layerOverriden,
		List<int> allClipsHashCodes,
		in UnsafeList<RTP.Parameter> allParams)
	{
		var bakedState = new RTP.State();
		bakedState.Name = state.name;
		bakedState.HashCode = state.GetHashCode();
		
		bakedState.Speed = state.speed;
		bakedState.SpeedMultiplierParameter = state.speedParameterActive ? state.speedParameter : "";
		bakedState.Transitions = new UnsafeList<RTP.Transition>(state.transitions.Length, Allocator.Persistent);

		for (var i = 0; i < state.transitions.Length; ++i)
		{
			var t = state.transitions[i];
			var generatedTransitions = GenerateControllerTransitionComputationData(t, ourStateMachine, parentStateMachine, state.name, allParams);
			foreach (var gt in generatedTransitions)
				bakedState.Transitions.Add(gt);
		}

		FilterSoloAndMuteTransitions(ref bakedState.Transitions);

		var motion = layerOverriden.GetOverrideMotion(state);
		if (motion == null)
			motion = state.motion;

		bakedState.Motion = GenerateMotionComputationData(motion, allClipsHashCodes);
		if (state.timeParameterActive)
			bakedState.TimeParameter = state.timeParameter;

		bakedState.CycleOffset = state.cycleOffset;
		if (state.cycleOffsetParameterActive)
			bakedState.CycleOffsetParameter = state.cycleOffsetParameter;

		return bakedState;
	}

	private void FilterSoloAndMuteTransitions(ref UnsafeList<RTP.Transition> transitions)
	{
		var hasSoloTransitions = false;
		var l = transitions.Length;
		for (var i = 0; i < l && !hasSoloTransitions; ++i)
		{
			hasSoloTransitions = transitions[i].SoloFlag;
		}

		for (var i = 0; i < l;)
		{
			var t = transitions[i];
			//	According to documentation mute flag has precedence
			if (t.MuteFlag)
			{
				transitions.RemoveAtSwapBack(i);
				--l;
			}
			else if (!t.SoloFlag && hasSoloTransitions)
			{
				transitions.RemoveAtSwapBack(i);
				--l;
			}
			else
			{
				++i;
			}
		}
	}

	private bool GenerateControllerStateMachineComputationData(AnimatorStateMachine stateMachine,
		AnimatorStateMachine parentStateMachine,
		AnimatorControllerLayer layer,
		AnimatorControllerLayer layerOverriden,
		List<int> allClipsHashCodes,
		ref UnsafeList<RTP.State> states,
		ref UnsafeList<RTP.Transition> anyStateTransitions,
		in UnsafeList<RTP.Parameter> allParams)
	{
		for (var k = 0; k < stateMachine.anyStateTransitions.Length; ++k)
		{
			var ast = stateMachine.anyStateTransitions[k];
			var stateName = "Any State";
			var generatedTransitions = GenerateControllerTransitionComputationData(ast, stateMachine, parentStateMachine, stateName, allParams);
			foreach (var gt in generatedTransitions)
				anyStateTransitions.Add(gt);
		}

		FilterSoloAndMuteTransitions(ref anyStateTransitions);

		for (var i = 0; i < stateMachine.states.Length; ++i)
		{
			var s = stateMachine.states[i];
			var generatedState = GenerateControllerStateComputationData(s.state, stateMachine, parentStateMachine, layerOverriden, allClipsHashCodes, allParams);
			states.Add(generatedState);
		}

		for (var j = 0; j < stateMachine.stateMachines.Length; ++j)
		{
			var sm = stateMachine.stateMachines[j];
			GenerateControllerStateMachineComputationData(sm.stateMachine, stateMachine, layer, layerOverriden, allClipsHashCodes, ref states, ref anyStateTransitions, allParams);
		}

		return true;
	}

	private AnimationClip[] Deduplicate(AnimationClip[] animationClips)
	{
		var dedupList = new List<AnimationClip>();
		using var dupSet = new NativeHashSet<int>(animationClips.Length, Allocator.Temp);

		foreach (var animationClip in animationClips)
		{
			if (!dupSet.Add(animationClip.GetHashCode())) continue;

			dedupList.Add(animationClip);
		}

		return dedupList.ToArray();
	}

	private UnsafeList<RTP.AnimationClip> ConvertAllControllerAnimations(AnimationClip[] animationClips, Animator animator)
	{
		animationClips = Deduplicate(animationClips);

		var bakedAnimClips = new UnsafeList<RTP.AnimationClip>(animationClips.Length, Allocator.Persistent);
		
		//	Need to make instance of object because when we will sample animations object placement can be modified.
		//	Also prefabs will not update its transforms
		
		var objectCopy = GameObject.Instantiate(animator.gameObject);
		objectCopy.hideFlags = HideFlags.HideAndDontSave;
		var animatorCopy = objectCopy.GetComponent<Animator>();

		foreach (var animationClip in animationClips)
			bakedAnimClips.Add(AnimationClipBaker.PrepareAnimationComputeData(animationClip, animatorCopy));
		
		GameObject.DestroyImmediate(objectCopy);

		return bakedAnimClips;
	}
}
#endif
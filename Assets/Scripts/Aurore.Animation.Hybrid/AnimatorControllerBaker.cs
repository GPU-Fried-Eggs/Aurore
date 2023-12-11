#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using AnimationClip = UnityEngine.AnimationClip;
using BlendTree = UnityEditor.Animations.BlendTree;
using ChildMotion = UnityEditor.Animations.ChildMotion;
using Hash128 = Unity.Entities.Hash128;
using Motion = UnityEngine.Motion;
#if AURORE_DEBUG
using FixedStringName = Unity.Collections.FixedString512Bytes;
#endif

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
	private struct TransitionPrototype
	{
		public AnimatorState DestinationState;
		public AnimatorStateMachine DestinationStateMachine;
		public float Duration;
		public float ExitTime;
		public bool HasExitTime;
		public bool HasFixedDuration;
		public float Offset;
		public bool Muted;
		public bool Solo;
		public bool CanTransitionToSelf;
		public string OwnStateName;
		public string Name;
		public AnimatorCondition[] Conditions;

		public TransitionPrototype(AnimatorStateTransition stateTransition, string ownStateName)
		{
			Duration = stateTransition.duration;
			ExitTime = stateTransition.exitTime;
			HasExitTime = stateTransition.hasExitTime;
			HasFixedDuration = stateTransition.hasFixedDuration;
			Offset = stateTransition.offset;
			Solo = stateTransition.solo;
			Muted = stateTransition.mute;
			CanTransitionToSelf = stateTransition.canTransitionToSelf;
			DestinationState = stateTransition.destinationState;
			Conditions = stateTransition.conditions;
			DestinationStateMachine = stateTransition.destinationStateMachine;
			OwnStateName = ownStateName;
			Name = stateTransition.name;
		}
	}

	private Dictionary<AnimatorStateMachine, AnimatorStateMachine> m_StateMachineParents;

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
		foreach (var clip in animationClips) DependsOn(clip);
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

	private RTP.Controller GenerateControllerComputationData(AnimatorController controller, List<int> allClipsHashCodes)
	{
		m_StateMachineParents = CreateParentsStateMachineDictionary(controller);

		var bakedController = new RTP.Controller();
		bakedController.Name = controller.name;
		bakedController.Parameters = GenerateControllerParametersComputationData(controller.parameters);

		bakedController.Layers = new UnsafeList<RTP.Layer>(controller.layers.Length, Allocator.Persistent);

		for (var i = 0; i < controller.layers.Length; ++i)
		{
			var layer = controller.layers[i];
			var layerOverriden = layer.syncedLayerIndex >= 0 ? controller.layers[layer.syncedLayerIndex] : layer;
			var layerData = GenerateControllerLayerComputationData(layerOverriden, layer, allClipsHashCodes, i, bakedController.Parameters);
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

	private RTP.Layer GenerateControllerLayerComputationData(AnimatorControllerLayer layer,
		AnimatorControllerLayer layerOverriden,
		List<int> allClipsHashCodes,
		int layerIndex,
		in UnsafeList<RTP.Parameter> allParams)
	{
		var bakedLayer = new RTP.Layer();
		bakedLayer.Name = layer.name;

		var stateList = new UnsafeList<RTP.State>(128, Allocator.Persistent);
		var anyStateTransitions = new UnsafeList<RTP.Transition>(128, Allocator.Persistent);

		GenerateControllerStateMachineComputationData(layer.stateMachine, layerOverriden, allClipsHashCodes, ref stateList, ref anyStateTransitions, allParams);
		bakedLayer.AvatarMask = AvatarMaskConversionSystem.PrepareAvatarMaskComputeData(layer.avatarMask);
		bakedLayer.States = stateList;

		var defaultState = layer.stateMachine.defaultState;

		bakedLayer.DefaultStateIndex = defaultState == null ? -1 : stateList.IndexOf(defaultState.GetHashCode());
		bakedLayer.AnyStateTransitions = anyStateTransitions;
		bakedLayer.Weight = layerIndex == 0 ? 1 : layerOverriden.defaultWeight;
		bakedLayer.BlendMode = (AnimationBlendingMode)layerOverriden.blendingMode;

		return bakedLayer;
	}

	private RTP.Condition GenerateControllerConditionComputationData(AnimatorCondition condition, in UnsafeList<RTP.Parameter> allParams)
	{
		var bakedCondition = new RTP.Condition();
		bakedCondition.ParamName = condition.parameter;

		var paramIdx = allParams.IndexOf(bakedCondition.ParamName);
		if (paramIdx < 0) return default;

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

	private RTP.Transition GenerateTransitionDataBetweenStates(in TransitionPrototype prototype, in UnsafeList<RTP.Parameter> allParams)
	{
		var bakedTransition = new RTP.Transition();

		bakedTransition.Duration = prototype.Duration;
		bakedTransition.ExitTime = prototype.ExitTime;
		bakedTransition.HasExitTime = prototype.HasExitTime;
		bakedTransition.HasFixedDuration = prototype.HasFixedDuration;
		bakedTransition.Offset = prototype.Offset;
		bakedTransition.TargetStateHash = prototype.DestinationState.GetHashCode();
		bakedTransition.Conditions = new UnsafeList<RTP.Condition>(prototype.Conditions.Length, Allocator.Persistent);
		bakedTransition.SoloFlag = prototype.Solo;
		bakedTransition.MuteFlag = prototype.Muted;
		bakedTransition.CanTransitionToSelf = prototype.CanTransitionToSelf;

		bakedTransition.Name = prototype.Name != "" ? prototype.Name : $"{prototype.OwnStateName} -> {prototype.DestinationState.name}";

		for (var i = 0; i < prototype.Conditions.Length; ++i)
		{
			var condition = prototype.Conditions[i];
			var createdCondition = GenerateControllerConditionComputationData(condition, allParams);
			if (!createdCondition.ParamName.IsEmpty)
				bakedTransition.Conditions.Add(createdCondition);
		}

		return bakedTransition;
	}

	private AnimatorCondition[] MergeConditions(AnimatorCondition[] a, AnimatorCondition[] b)
	{
		var animatorCondition = new AnimatorCondition[a.Length + b.Length];

		Array.Copy(a, animatorCondition, a.Length);
		Array.Copy(b, 0, animatorCondition, a.Length, b.Length);

		return animatorCondition;
	}

	private NativeArray<RTP.Transition> GenerateTransitionsToDestinationStateMachine(TransitionPrototype prototype,
		AnimatorStateMachine stateMachine,
		in UnsafeList<RTP.Parameter> allParams)
	{
		//	Generate transitions to every state connected with entry state
		var bakedTransitions = new NativeList<RTP.Transition>(Allocator.Temp);
		
		for (var i = 0; i < stateMachine.entryTransitions.Length; ++i)
		{
			var entryTransition = stateMachine.entryTransitions[i];
			var conditions = MergeConditions(prototype.Conditions, entryTransition.conditions);
			var mod = prototype;
			mod.DestinationState = entryTransition.destinationState;
			mod.Conditions = conditions;

			bakedTransitions.Add(GenerateTransitionDataBetweenStates(mod, allParams));
		}

		//	Add transition to the default state of target state machine with lowest priority
		prototype.DestinationState = stateMachine.defaultState;
		bakedTransitions.Add(GenerateTransitionDataBetweenStates(prototype, allParams));

		return bakedTransitions.AsArray();
	}

	private NativeArray<RTP.Transition> GenerateTransitionsToExitState(TransitionPrototype prototype,
		AnimatorStateMachine stateMachine,
		in UnsafeList<RTP.Parameter> allParams)
	{
		var bakedTransitions = new NativeList<RTP.Transition>(Allocator.Temp);
		
		var parentStateMachine = m_StateMachineParents[stateMachine];
		var transitions = parentStateMachine.GetStateMachineTransitions(stateMachine);
		for (var i = 0; i < transitions.Length; ++i)
		{
			var transition = transitions[i];
			var conditions = MergeConditions(prototype.Conditions, transition.conditions);

			var mod = prototype;
			mod.Conditions = conditions;
			mod.DestinationState = transition.destinationState;
			mod.DestinationStateMachine = transition.destinationStateMachine;
			mod.Muted = transition.mute;
			mod.Solo = transition.solo;
			mod.Name = transition.name;

			bakedTransitions.AddRange(GenerateControllerTransitionComputationData(mod, parentStateMachine, allParams).AsArray());
		}
		
		//	Add transition to the default state of target state machine with lowest priority
		var targetState = parentStateMachine == null ? stateMachine.defaultState : parentStateMachine.defaultState;
		prototype.DestinationState = targetState;
		bakedTransitions.Add(GenerateTransitionDataBetweenStates(prototype, allParams));

		return bakedTransitions.AsArray();
	}

	private NativeList<RTP.Transition> GenerateControllerTransitionComputationData(TransitionPrototype prototype,
		AnimatorStateMachine stateMachine,
		in UnsafeList<RTP.Parameter> allParams)
	{
		//	Because exit and enter states of substatemachines can have several transitions with different conditions this function can generate several transitions
		var bakedTransitions = new NativeList<RTP.Transition>(Allocator.Temp);
		if (prototype.DestinationState != null)
		{
			bakedTransitions.Add(GenerateTransitionDataBetweenStates(prototype, allParams));
		}
		else
		{
			if (prototype.DestinationStateMachine == null)
			{
				//	This is exit state transition.
				//	If parent state machine is null, behavior exactly the same as destination state machine transition.
				var parentStateMachine = m_StateMachineParents[stateMachine]; 
				if (parentStateMachine == null)
				{
					var dstSmTransitions = GenerateTransitionsToDestinationStateMachine(prototype, stateMachine, allParams);
					bakedTransitions.AddRange(dstSmTransitions);
				}
				//	Otherwise for parent state machine transitions separate "StateMachineTransitions" should be considered.
				else
				{
					var exitStateTransitions = GenerateTransitionsToExitState(prototype, stateMachine, allParams);
					bakedTransitions.AddRange(exitStateTransitions);
				}
			}
			else
			{
				var dstTransitions = GenerateTransitionsToDestinationStateMachine(prototype, prototype.DestinationStateMachine, allParams);
				bakedTransitions.AddRange(dstTransitions);
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

		using (var serializedObject = new SerializedObject(blendTree))
		{
			var property = serializedObject.FindProperty("m_NormalizedBlendValues");
			if (property != null) propertyFlag = property.boolValue;
		}

		return propertyFlag;
	}

	private RTP.State GenerateControllerStateComputationData(AnimatorState state,
		AnimatorStateMachine stateMachine,
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
			var stateTransition = state.transitions[i];
			var prototype = new TransitionPrototype(stateTransition, state.name);
			var generatedTransitions = GenerateControllerTransitionComputationData(prototype, stateMachine, allParams);
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

	private Dictionary<AnimatorStateMachine, AnimatorStateMachine> CreateParentsStateMachineDictionary(AnimatorController controller)
	{
		var dictionary = new Dictionary<AnimatorStateMachine, AnimatorStateMachine>();
		foreach (var controllerLayer in controller.layers)
		{
			FillParentsStateMachineDictionaryRecursively(controllerLayer.stateMachine, null, ref dictionary);	
		}

		return dictionary;
	}

	private void FillParentsStateMachineDictionaryRecursively(AnimatorStateMachine stateMachine, AnimatorStateMachine parentStateMachine, ref Dictionary<AnimatorStateMachine, AnimatorStateMachine> outDictionary)
	{
		if (stateMachine == null) return;
		
		outDictionary.Add(stateMachine, parentStateMachine);
		foreach (var childStateMachine in stateMachine.stateMachines)
		{
			FillParentsStateMachineDictionaryRecursively(childStateMachine.stateMachine, stateMachine, ref outDictionary);
		}
	}

	private bool GenerateControllerStateMachineComputationData(AnimatorStateMachine stateMachine,
		AnimatorControllerLayer layerOverriden,
		List<int> allClipsHashCodes,
		ref UnsafeList<RTP.State> states,
		ref UnsafeList<RTP.Transition> anyStateTransitions,
		in UnsafeList<RTP.Parameter> allParams)
	{
		for (var k = 0; k < stateMachine.anyStateTransitions.Length; ++k)
		{
			var transition = stateMachine.anyStateTransitions[k];
			var prototype = new TransitionPrototype(transition, "Any State");
			var generatedTransitions = GenerateControllerTransitionComputationData(prototype, stateMachine, allParams);
			foreach (var generatedTransition in generatedTransitions)
				anyStateTransitions.Add(generatedTransition);
		}

		FilterSoloAndMuteTransitions(ref anyStateTransitions);

		for (var i = 0; i < stateMachine.states.Length; ++i)
		{
			var childAnimatorState = stateMachine.states[i];
			var generatedState = GenerateControllerStateComputationData(childAnimatorState.state, stateMachine, layerOverriden, allClipsHashCodes, allParams);
			states.Add(generatedState);
		}

		for (var j = 0; j < stateMachine.stateMachines.Length; ++j)
		{
			var animatorStateMachine = stateMachine.stateMachines[j];
			GenerateControllerStateMachineComputationData(animatorStateMachine.stateMachine, layerOverriden, allClipsHashCodes, ref states, ref anyStateTransitions, allParams);
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
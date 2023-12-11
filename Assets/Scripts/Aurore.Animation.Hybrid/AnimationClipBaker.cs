#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using FixedStringName = Unity.Collections.FixedString512Bytes;
using Hash128 = Unity.Entities.Hash128;

[BurstCompile]
public partial class AnimationClipBaker
{
	private enum BoneType
	{
		Generic,
		MotionCurve,
		RootCurve
	}

	private struct ParsedCurveBinding
	{
		public BindingType BindingType;
		public short ChannelIndex;
		public BoneType BoneType;
		public FixedStringName BoneName;

		public bool IsValid() => BoneName.Length > 0;
	}

	private static ValueTuple<string, string> SplitPath(string path)
	{
		var paths = path.Split('/');
		Assert.IsTrue(paths.Length > 0);
		return (paths.Last(), paths.Length > 1 ? paths[^2] : "");
	}

	private static (BindingType, BoneType) PickGenericBindingTypeByString(string bindingString) => bindingString switch
	{
		"m_LocalPosition" => (BindingType.Translation, BoneType.Generic),
		//"MotionT" => (BindingType.Translation, BoneType.MotionCurve),
		//"RootT" => (BindingType.Translation, BoneType.RootCurve),
		"m_LocalRotation" => (BindingType.Quaternion, BoneType.Generic),
		//"MotionQ" => (BindingType.Quaternion, BoneType.MotionCurve),
		//"RootQ" => (BindingType.Quaternion, BoneType.RootCurve),
		"localEulerAngles" => (BindingType.EulerAngles, BoneType.Generic),
		"localEulerAnglesRaw" => (BindingType.EulerAngles, BoneType.Generic),
		"m_LocalScale" => (BindingType.Scale, BoneType.Generic),
		_ => (BindingType.Unknown, BoneType.Generic)
	};

	private static short ChannelIndexFromString(string c) => c switch
	{
		"x" => 0,
		"y" => 1,
		"z" => 2,
		"w" => 3,
		_ => 999
	};

	private static FixedStringName ConstructBoneClipName(ValueTuple<string, string> nameAndPath, BoneType type)
	{
		return nameAndPath.Item1.Length == 0 && nameAndPath.Item2.Length == 0
			? SpecialBones.unnamedRootBoneName
			: new FixedStringName(nameAndPath.Item1);
	}

	private static RTP.AnimationCurve PrepareAnimationCurve(Keyframe[] keyframes, ParsedCurveBinding curveBinding)
	{
		var bakedAnimCurve = new RTP.AnimationCurve();
		bakedAnimCurve.ChannelIndex = curveBinding.ChannelIndex;
		bakedAnimCurve.BindingType = curveBinding.BindingType;
		bakedAnimCurve.KeyFrames = new UnsafeList<KeyFrame>(keyframes.Length, Allocator.Persistent);

		foreach (var keyframe in keyframes)
		{
			bakedAnimCurve.KeyFrames.Add(new KeyFrame
			{
				Time = keyframe.time,
				InTan = keyframe.inTangent,
				OutTan = keyframe.outTangent,
				V = keyframe.value
			});
		}

		return bakedAnimCurve;
	}

	private static int GetOrCreateBoneClipHolder(ref UnsafeList<RTP.BoneClip> boneClips, in Hash128 nameHash, BindingType type)
	{
		var boneClipIndex = boneClips.IndexOf(nameHash);
		if (boneClipIndex < 0)
		{
			boneClipIndex = boneClips.Length;
			var boneClip = new RTP.BoneClip();
			boneClip.Name = "MISSING_BONE_NAME";
			boneClip.NameHash = nameHash;
			boneClip.IsHumanMuscleClip = type == BindingType.HumanMuscle;
			boneClip.AnimationCurves = new UnsafeList<RTP.AnimationCurve>(32, Allocator.Persistent);
			boneClips.Add(boneClip);
		}

		return boneClipIndex;
	}

	private static int GetOrCreateBoneClipHolder(ref UnsafeList<RTP.BoneClip> boneClips, in FixedStringName name, BindingType type)
	{
		//	Hash for generic curves must match parameter name hash which is 32 bit instead od 128
		var nameHash = type == BindingType.Unknown ? new Hash128(name.CalculateHash32(), 0, 0, 0) : name.CalculateHash128();
		var boneClipIndex = GetOrCreateBoneClipHolder(ref boneClips, nameHash, type);
		ref var boneClip = ref boneClips.ElementAt(boneClipIndex);
		boneClip.Name = name;

		return boneClipIndex;
	}

	private static RTP.BoneClip MakeBoneClipCopy(in RTP.BoneClip boneClip)
	{
		var bakedBoneClip = boneClip;
		bakedBoneClip.AnimationCurves = new UnsafeList<RTP.AnimationCurve>(boneClip.AnimationCurves.Length, Allocator.Persistent);
		for (var i = 0; i < boneClip.AnimationCurves.Length; ++i)
		{
			var inKeyFrame = boneClip.AnimationCurves[i].KeyFrames;
			var outKeyFrame = new UnsafeList<KeyFrame>(inKeyFrame.Length, Allocator.Persistent);
			for (var j = 0; j < inKeyFrame.Length; ++j) outKeyFrame.Add(inKeyFrame[j]);
			var animCurve = boneClip.AnimationCurves[i];
			animCurve.KeyFrames = outKeyFrame;
			bakedBoneClip.AnimationCurves.Add(animCurve);
		}

		return bakedBoneClip;
	}

	private static void DebugLogging(RTP.AnimationClip animationClip, bool hasRootCurves)
	{
#if AURORE_DEBUG
		var configuration = GameObject.FindObjectOfType<DebugConfigurationAuthoring>();
		var logClipBaking = configuration != null && configuration.logClipBaking;
		if (!logClipBaking) return;

		Debug.Log($"Baking animation clip '{animationClip.Name}'. Tracks: {animationClip.Bones.Length}. User curves: {animationClip.Curves.Length}. Length: {animationClip.Length}s. Looped: {animationClip.Looped}. Has root curves: {hasRootCurves}");
#endif
	}

	private static ParsedCurveBinding ParseGenericCurveBinding(EditorCurveBinding binding, AnimationClip animationClip)
	{
		var parsedBinding = new ParsedCurveBinding();

		var propNameData = binding.propertyName.Split('.');
		var propName = propNameData[0];
		var channel = propNameData.Length > 1 ? propNameData[1] : "";

		parsedBinding.ChannelIndex = ChannelIndexFromString(channel);
		(parsedBinding.BindingType, parsedBinding.BoneType) = PickGenericBindingTypeByString(propName);

		//	Ignore root curve if motion curve is present in clip
		if (parsedBinding.BoneType == BoneType.RootCurve && animationClip.hasMotionCurves)
			return parsedBinding;

		if (parsedBinding.BindingType != BindingType.Unknown)
		{
			var nameAndPath = SplitPath(binding.path);
			parsedBinding.BoneName = ConstructBoneClipName(nameAndPath, parsedBinding.BoneType);
		}
		else
		{
			parsedBinding.BoneName = new FixedStringName(propName);
		}

		return parsedBinding;
	}

	private static int GetHumanBoneIndexForHumanName(in HumanDescription humanDescription, FixedStringName humanBoneName)
	{
		var humanBoneIndexInAvatar = Array.FindIndex(humanDescription.human, x => x.humanName == humanBoneName);
		return humanBoneIndexInAvatar;
	}

	private static ParsedCurveBinding ParseHumanoidCurveBinding(EditorCurveBinding binding, Avatar avatar)
	{
		if (!s_HumanoidMappingTable.TryGetValue(binding.propertyName, out var rv))
			return rv;

		var humanDescription = avatar.humanDescription;
		var humanBoneIndexInAvatar = GetHumanBoneIndexForHumanName(humanDescription, rv.BoneName);
		if (humanBoneIndexInAvatar < 0)
			return rv;

		if (rv.BindingType == BindingType.HumanMuscle)
		{
			var humanBoneDef = humanDescription.human[humanBoneIndexInAvatar];
			rv.BoneName = humanBoneDef.boneName;
		}

		return rv;
	}

	private static ParsedCurveBinding ParseCurveBinding(AnimationClip animationClip, EditorCurveBinding binding, Avatar avatar)
	{
		return  animationClip.isHumanMotion
			? ParseHumanoidCurveBinding(binding, avatar)
			: ParseGenericCurveBinding(binding, animationClip);
	}

	private static void AddKeyFrameFromFloatValue(ref UnsafeList<KeyFrame> keyFrames, float2 key, float v)
	{
		keyFrames.Add(new KeyFrame
		{
			Time = key.x,
			InTan = key.y,
			OutTan = key.y,
			V = v
		});
	}

	[BurstCompile]
	private static void ComputeTangents(ref RTP.AnimationCurve ac)
	{
		for (var i = 0; i < ac.KeyFrames.Length; ++i)
		{
			var p0 = i == 0 ? ac.KeyFrames[0] : ac.KeyFrames[i - 1];
			var p1 = ac.KeyFrames[i];
			var p2 = i == ac.KeyFrames.Length - 1 ? ac.KeyFrames[i] : ac.KeyFrames[i + 1];

			var outV = math.normalizesafe(new float2(p2.Time, p2.V) - new float2(p1.Time, p1.V));
			var outTan = outV.x > 0.0001f ? outV.y / outV.x : 0;

			var inV = math.normalizesafe(new float2(p1.Time, p1.V) - new float2(p0.Time, p0.V));
			var inTan = inV.x > 0.0001f ? inV.y / inV.x : 0;

			var dt = math.abs(inTan) + math.abs(outTan);
			var f = dt > 0 ? math.abs(inTan) / dt : 0;

			var avgTan = math.lerp(inTan, outTan, f);

			var k = ac.KeyFrames[i];
			k.OutTan = avgTan;
			k.InTan = avgTan;
			ac.KeyFrames[i] = k;
		}
	}

	private static NativeList<float> CreateKeyframeTimes(float animationLength, float dt)
	{
		var numFrames = (int)math.ceil(animationLength / dt);

		var rv = new NativeList<float>(numFrames, Allocator.Temp);

		float curTime = 0;
		for (;;)
		{
			rv.Add(curTime);
			curTime += dt;
			if (curTime > animationLength)
			{
				rv.Add(animationLength);
				break;
			}
		}
		return rv;
	}

	private static void ReadCurvesFromTransform(Transform tr, NativeArray<RTP.AnimationCurve> animCurves, float time)
	{
		quaternion q = tr.localRotation;
		float3 t = tr.localPosition;

		var vArr = new NativeArray<float>(7, Allocator.Temp);
		vArr[0] = t.x;
		vArr[1] = t.y;
		vArr[2] = t.z;
		vArr[3] = q.value.x;
		vArr[4] = q.value.y;
		vArr[5] = q.value.z;
		vArr[6] = q.value.w;

		for (var l = 0; l < vArr.Length; ++l)
		{
			var keysArr = animCurves[l];
			AddKeyFrameFromFloatValue(ref keysArr.KeyFrames, time, vArr[l]);
			animCurves[l] = keysArr;
		}
	}

	private static void SetCurvesToAnimation(ref UnsafeList<RTP.BoneClip> outBoneClips, in Hash128 boneHash, NativeArray<RTP.AnimationCurve> animCurve)
	{
		var boneId = GetOrCreateBoneClipHolder(ref outBoneClips, boneHash, BindingType.Translation);
		ref var bc = ref outBoneClips.ElementAt(boneId);
		bc.DisposeCurves();

		for (var i = 0; i < animCurve.Length; ++i)
		{
			var hac = animCurve[i];
			ComputeTangents(ref hac);
			bc.AnimationCurves.Add(hac);
		}
	}

	private static void SampleUnityAnimation(AnimationClip animationClip, Animator animator, ValueTuple<Transform, Hash128>[] trs, bool applyRootMotion, ref UnsafeList<RTP.BoneClip> boneClips)
	{
		if (trs.Length == 0) return;
		
		var sampleAnimationFrameTime = 1 / 60.0f;
		var keysList = CreateKeyframeTimes(animationClip.length, sampleAnimationFrameTime);

		var channelDesc = new ValueTuple<BindingType, short>[]
		{
			(BindingType.Translation, 0),
			(BindingType.Translation, 1),
			(BindingType.Translation, 2),
			(BindingType.Quaternion, 0),
			(BindingType.Quaternion, 1),
			(BindingType.Quaternion, 2),
			(BindingType.Quaternion, 3),
		};
 
		var rac = animator.runtimeAnimatorController;
		var origPos = animator.transform.position;
		var origRot = animator.transform.rotation;
		var origRootMotion = animator.applyRootMotion;
		var prevAnmCulling = animator.cullingMode;
		
		animator.runtimeAnimatorController = null;
		animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
		animator.applyRootMotion = true;
		animator.transform.position = Vector3.zero;
		animator.transform.rotation = quaternion.identity;
		
		var animationCurves = new NativeArray<RTP.AnimationCurve>(channelDesc.Length * trs.Length, Allocator.Temp);
		for (var k = 0; k < animationCurves.Length; ++k)
		{
			animationCurves[k] = new RTP.AnimationCurve
			{
				BindingType = channelDesc[k % channelDesc.Length].Item1,
				ChannelIndex = channelDesc[k % channelDesc.Length].Item2,
				KeyFrames = new UnsafeList<KeyFrame>(keysList.Length, Allocator.Persistent)
			};
		}

		for (var i = 0; i < keysList.Length; ++i)
		{
			var time = keysList[i];
			var dt = i == 0 ? 0.0000001f : time - keysList[i - 1];
			animationClip.SampleAnimation(animator.gameObject, time);

			for (var l = 0; l < trs.Length; ++l)
			{
				var tr = trs[l].Item1;
				var curvesSpan = animationCurves.GetSubArray(l * channelDesc.Length, channelDesc.Length);
				ReadCurvesFromTransform(tr, curvesSpan, time);
			}
		}

		for (var l = 0; l < trs.Length; ++l)
		{
			var curvesSpan = animationCurves.GetSubArray(l * channelDesc.Length, channelDesc.Length);
			SetCurvesToAnimation(ref boneClips, trs[l].Item2, curvesSpan);
		}

		animator.cullingMode = prevAnmCulling;
		animator.runtimeAnimatorController = rac;
		animator.transform.position = origPos;
		animator.transform.rotation = origRot;
		animator.applyRootMotion = origRootMotion;
	}

	private static (Transform, Hash128) GetRootBoneTransform(Animator animator)
	{
		if (animator.avatar.isHuman)
		{
			var hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
			var humanDescription = animator.avatar.humanDescription;
			var humanBoneIndexInDesc = GetHumanBoneIndexForHumanName(humanDescription, "Hips");
			var rigHipsBoneName = new FixedStringName(humanDescription.human[humanBoneIndexInDesc].boneName).CalculateHash128();
			return (hipsTransform, rigHipsBoneName);
		}

		var rootBoneName =  animator.avatar.GetRootMotionNodeName();
		var rootBoneNameHash = new FixedStringName(rootBoneName).CalculateHash128();
		var rootBoneTransform = TransformUtilities.FindChildRecursively(animator.transform, rootBoneName);
		return (rootBoneTransform, rootBoneNameHash);
	}

	private static void SampleMissingCurves(AnimationClip animationClip, Animator animator, ref UnsafeList<RTP.BoneClip> boneClips)
	{
		var trs = new List<ValueTuple<Transform, Hash128>>();
		var entityRootTransform = animator.transform;
		var rootBoneTransformData = GetRootBoneTransform(animator);

		if (animator.isHuman) trs.Add(rootBoneTransformData);

		//	Sample curves for non-rootmotion animations
		SampleUnityAnimation(animationClip, animator, trs.ToArray(), false, ref boneClips);

		//	Sample root motion curves
		trs.Clear();

		var entityRootHash = SpecialBones.unnamedRootBoneName.CalculateHash128();
		AnimationProcessSystem.ComputeBoneAnimationJob.ModifyBoneHashForRootMotion(ref entityRootHash);
		trs.Add((entityRootTransform, entityRootHash));

		//	Modify bone hash to separate root motion tracks and ordinary tracks
		AnimationProcessSystem.ComputeBoneAnimationJob.ModifyBoneHashForRootMotion(ref rootBoneTransformData.Item2);
		trs.Add(rootBoneTransformData);

		SampleUnityAnimation(animationClip, animator, trs.ToArray(), true, ref boneClips);
	}
	
	public static RTP.AnimationClip PrepareAnimationComputeData(AnimationClip animationClip, Animator animator)
	{
		var acSettings = AnimationUtility.GetAnimationClipSettings(animationClip);

		var animClip = new RTP.AnimationClip();
		animClip.Name = animationClip.name;
		animClip.Bones = new UnsafeList<RTP.BoneClip>(100, Allocator.Persistent);
		animClip.Curves = new UnsafeList<RTP.BoneClip>(100, Allocator.Persistent);
		animClip.Length = animationClip.length;
		animClip.Looped = animationClip.isLooping;
		animClip.Hash = new Hash128((uint)animationClip.GetHashCode(), 0, 0, 0);
		animClip.LoopPoseBlend = acSettings.loopBlend;
		animClip.CycleOffset = acSettings.cycleOffset;
		animClip.AdditiveReferencePoseTime = acSettings.additiveReferencePoseTime;
		animClip.HasRootMotionCurves = animationClip.hasRootCurves || animationClip.hasMotionCurves;

		var bindings = AnimationUtility.GetCurveBindings(animationClip);

		foreach (var binding in bindings)
		{
			var ec = AnimationUtility.GetEditorCurve(animationClip, binding);
			var pb = ParseCurveBinding(animationClip, binding, animator.avatar);
			
			if (!pb.IsValid()) continue;

			var animCurve = PrepareAnimationCurve(ec.keys, pb);
			var isGenericCurve = pb.BindingType == BindingType.Unknown;

			var curveHolder = isGenericCurve ? animClip.Curves : animClip.Bones;

			if (pb.ChannelIndex < 0 && !isGenericCurve) continue;

			var boneId = GetOrCreateBoneClipHolder(ref curveHolder, pb.BoneName, pb.BindingType);
			var boneClip = curveHolder[boneId];
			boneClip.AnimationCurves.Add(animCurve);
			curveHolder[boneId] = boneClip;

			if (isGenericCurve)
				animClip.Curves = curveHolder;
			else
				animClip.Bones = curveHolder;
		}
		
		if (animator.avatar != null)
		{
			//	Sample root and hips curves and from unity animations. Maybe sometime I will figure out all RootT/RootQ and body pose generation formulas and this step could be replaced with generation.
			SampleMissingCurves(animationClip, animator, ref animClip.Bones);
			
			//	Because we have modified tracks we need to make animation hash unique
			animClip.Hash.Value.y = (uint)animator.avatar.GetHashCode();
		}

		DebugLogging(animClip, animationClip.hasMotionCurves || animationClip.hasRootCurves);

		return animClip;
	}
}
#endif
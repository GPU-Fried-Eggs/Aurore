using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Hash128 = Unity.Entities.Hash128;

public struct ControllerBlob
{
#if AURORE_DEBUG
	public BlobString Name;
#endif
	public BlobArray<LayerBlob> Layers;
	public BlobArray<ParameterBlob> Parameters;
	public BlobArray<AnimationClipBlob> AnimationClips;
}

public enum ControllerParameterType
{
	Int,
	Float,
	Bool,
	Trigger
}

[StructLayout(LayoutKind.Explicit)]
public struct ParameterValue
{
	[FieldOffset(0)] public float floatValue;
	[FieldOffset(0)] public int intValue;
	[FieldOffset(0)] public bool boolValue;

	public static implicit operator ParameterValue(float f) => new ParameterValue { floatValue = f };
	public static implicit operator ParameterValue(int i) => new ParameterValue { intValue = i };
	public static implicit operator ParameterValue(bool b) => new ParameterValue { boolValue = b };
}

public struct ParameterBlob
{
#if AURORE_DEBUG
	public BlobString Name;
#endif
	public uint Hash;
	public ParameterValue DefaultValue;
	public ControllerParameterType Type;
}

public struct ParameterPerfectHashTableBlob
{
	public BlobArray<int2> SeedTable;
	// We need indirection array to keep parameters in its original indices (as in authoring Unity.Animator)
	public BlobArray<int> IndirectionTable;
}

public enum AnimationBlendingMode
{
	Override = 0,
	Additive = 1
}

public enum AnimatorConditionMode
{
	If = 1,
	IfNot = 2,
	Greater = 3,
	Less = 4,
	Equals = 6,
	NotEqual = 7
}

public struct LayerBlob
{
#if AURORE_DEBUG
	public BlobString Name;
#endif
	public int DefaultStateIndex;
	public AnimationBlendingMode BlendingMode;
	public BlobArray<StateBlob> States;
	public AvatarMaskBlob AvatarMask;
}

public struct TransitionBlob
{
#if AURORE_DEBUG
	public BlobString Name;
#endif
	public uint Hash;
	public BlobArray<ConditionBlob> Conditions;
	public int TargetStateId;
	public float Duration;
	public float ExitTime;
	public float Offset;
	public bool HasExitTime;
	public bool HasFixedDuration;
}

public struct ConditionBlob
{
#if AURORE_DEBUG
	public BlobString Name;
#endif
	public int ParamIdx;
	public ParameterValue Threshold;
	public AnimatorConditionMode ConditionMode;
}

public struct MotionBlob
{
	public enum Type
	{
		None,
		AnimationClip,
		BlendTree1D,
		BlendTree2DSimpleDirectional,
		BlendTree2DFreeformDirectional,
		BlendTree2DFreeformCartesian,
		BlendTreeDirect
	}

#if AURORE_DEBUG
	public BlobString Name;
#endif
	public Type MotionType;
	public Hash128 AnimationHash;
	public BlobPtr<AnimationClipBlob> AnimationBlob;
	public BlendTreeBlob BlendTree;
}

public struct ChildMotionBlob
{
	public MotionBlob Motion;
	public float Threshold;
	public float TimeScale;
	public float2 Position2D;
	public int DirectBlendParameterIndex;
}

public struct BlendTreeBlob
{
#if AURORE_DEBUG
	public BlobString Name;
#endif
	public int BlendParameterIndex;
	public int BlendParameterYIndex;
	public bool NormalizeBlendValues;
	public BlobArray<ChildMotionBlob> Motions;
}

public struct StateBlob
{
#if AURORE_DEBUG
	public BlobString Name;
#endif
	public uint Hash;
	public float Speed;
	public int SpeedMultiplierParameterIndex;
	public int TimeParameterIndex;
	public float CycleOffset;
	public int CycleOffsetParameterIndex;
	public BlobArray<TransitionBlob> Transitions;
	public MotionBlob Motion;
}
using System;
using Unity.Collections;
using Unity.Entities;

public struct RuntimeAnimatorData
{
	public struct StateRuntimeData
	{
		public int Id;
		public float NormalizedDuration;

		public static StateRuntimeData MakeDefault()
		{
			return new StateRuntimeData
			{
				Id = -1,
				NormalizedDuration = 0
			};
		}
	}

	public StateRuntimeData SrcState;
	public StateRuntimeData DstState;
	public StateRuntimeData ActiveTransition;

	public static RuntimeAnimatorData MakeDefault()
	{
		return new RuntimeAnimatorData
		{
			SrcState = StateRuntimeData.MakeDefault(),
			DstState = StateRuntimeData.MakeDefault(),
			ActiveTransition = StateRuntimeData.MakeDefault()
		};
	}
}

public struct AnimatorControllerLayerComponent: IBufferElementData, IEnableableComponent
{
	public BlobAssetReference<ControllerBlob> Controller;
	public int LayerIndex;
	public float Weight;
	public RuntimeAnimatorData Rtd;
}

public struct AnimatorControllerParameterComponent: IBufferElementData
{
#if AURORE_DEBUG
	public FixedString64Bytes Name;
#endif
	public uint Hash;
	public ControllerParameterType Type;
	public ParameterValue Value;

	public float FloatValue
	{
		get => Value.floatValue;
		set => this.Value.floatValue = value;
	}

	public int IntValue
	{
		get => Value.intValue;
		set => this.Value.intValue= value;
	}

	public bool BoolValue
	{
		get => Value.boolValue;
		set => this.Value.boolValue = value;
	}

	public void SetTrigger()
	{
		Value.boolValue = true;
	}
}

public struct AnimatorControllerParameterIndexTableComponent: IComponentData
{
	public BlobAssetReference<ParameterPerfectHashTableBlob> SeedTable;
}

public struct MotionIndexAndWeight: IComparable<MotionIndexAndWeight>
{
	public int MotionIndex;
	public float Weight;

	public int CompareTo(MotionIndexAndWeight a)
	{
		if (Weight < a.Weight)
			return 1;
		if (Weight > a.Weight)
			return -1;

		return 0;
	}
}
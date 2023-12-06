using Unity.Entities;
#if AURORE_DEBUG
using FixedStringName = Unity.Collections.FixedString512Bytes;
#endif

public readonly partial struct AnimatorStateQueryAspect: IAspect
{
	private readonly DynamicBuffer<AnimatorControllerLayerComponent> m_LayersArr;

	public struct RuntimeStateInfo
	{
#if AURORE_DEBUG
		public FixedStringName Name;
#endif
		public uint Hash;
		public float NormalizedTime;
	}

	public struct RuntimeTransitionInfo
	{
#if AURORE_DEBUG
		public FixedStringName Name;
#endif
		public uint Hash;
		public float NormalizedTime;
	}

	public RuntimeStateInfo GetLayerCurrentStateInfo(int layerIndex)
	{
		if (m_LayersArr.Length <= layerIndex)
			return default;

		var layerRuntimeData = m_LayersArr[layerIndex];
		ref var layerBlob = ref layerRuntimeData.Controller.Value.Layers[layerIndex];
		var curStateID = layerRuntimeData.Rtd.SrcState.Id;

		if (curStateID < 0 || curStateID >= layerBlob.States.Length)
			return default;

		return new RuntimeStateInfo
		{
#if AURORE_DEBUG
			Name = layerBlob.States[curStateID].Name.ToFixedString(),	
#endif
			Hash = layerBlob.States[curStateID].Hash,
			NormalizedTime = layerRuntimeData.Rtd.SrcState.NormalizedDuration,
		};
	}

	public RuntimeTransitionInfo GetLayerCurrentTransitionInfo(int layerIndex)
	{
		if (m_LayersArr.Length <= layerIndex)
			return default;

		var layerRuntimeData = m_LayersArr[layerIndex];
		ref var layerBlob = ref layerRuntimeData.Controller.Value.Layers[layerIndex];
		var curTransitionID = layerRuntimeData.Rtd.ActiveTransition.Id;
		var curStateID = layerRuntimeData.Rtd.SrcState.Id;

		if (curTransitionID < 0 || curStateID < 0 || curStateID >= layerBlob.States.Length)
			return default;

		return new RuntimeTransitionInfo
		{
#if AURORE_DEBUG
			Name = layerBlob.States[curStateID].Transitions[curTransitionID].Name.ToFixedString(),	
#endif
			Hash = layerBlob.States[curStateID].Transitions[curTransitionID].Hash,
			NormalizedTime = layerRuntimeData.Rtd.ActiveTransition.NormalizedDuration
		};
	}
}
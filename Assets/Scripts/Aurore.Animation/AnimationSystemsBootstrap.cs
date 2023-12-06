using Unity.Entities;
using UnityEngine;

[CreateAfter(typeof(AnimationSystemGroup))]
public partial class AnimationSystemsBootstrap: SystemBase
{
	protected override void OnCreate()
	{
#if AURORE_DEBUG
		Debug.LogWarning("AURORE_DEBUG is defined. Performance may be reduced. Do not forget remove it in release builds.");
#endif

#if !UNITY_BURST_EXPERIMENTAL_ATOMIC_INTRINSICS
		Debug.LogError($"'UNITY_BURST_EXPERIMENTAL_ATOMIC_INTRINSICS' script symbol is not defined. This animation bootstrap will add it automatically");
		return;
#endif

		var sysGroup = World.GetOrCreateSystemManaged<AnimationSystemGroup>();
		var acs = World.CreateSystem<AnimatorControllerSystem<AnimatorControllerQuery>>();
		var facs = World.CreateSystem<FillAnimationsFromControllerSystem>();
		var aps = World.CreateSystem<AnimationProcessSystem>();
		var aas = World.CreateSystem<AnimationApplicationSystem>();
		var bvs = World.CreateSystem<BoneVisualizationSystem>();
		sysGroup.AddSystemToUpdateList(acs);
		sysGroup.AddSystemToUpdateList(facs);
		sysGroup.AddSystemToUpdateList(aps);
		sysGroup.AddSystemToUpdateList(aas);
		sysGroup.AddSystemToUpdateList(bvs);

		//	Remove bootstrap system from world
		this.Enabled = false;
	}

	protected override void OnUpdate() {}
}
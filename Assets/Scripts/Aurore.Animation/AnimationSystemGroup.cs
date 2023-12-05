using Unity.Entities;
using Unity.Transforms;

[UpdateBefore(typeof(TransformSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
public partial class AnimationSystemGroup: ComponentSystemGroup
{
	protected override void OnCreate()
	{
		base.OnCreate();
		EnableSystemSorting = false;
	}
}
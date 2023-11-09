using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Character.Kinematic
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial class KinematicCharacterPhysicsUpdateGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial class KinematicCharacterVariableUpdateGroup : ComponentSystemGroup { }
}
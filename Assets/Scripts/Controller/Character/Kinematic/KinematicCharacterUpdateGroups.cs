using Unity.Entities;
using Unity.Physics.Systems;

namespace Character.Kinematic
{
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial class KinematicCharacterPhysicsUpdateGroup : ComponentSystemGroup { }
}
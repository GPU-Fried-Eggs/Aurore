using Controller.Character.Kinematic;
using Unity.Burst;
using Unity.Entities;

namespace Controller.Character
{
    [UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup))]
    [BurstCompile]
    public partial struct CharacterSystem : ISystem
    {
        
    }
}
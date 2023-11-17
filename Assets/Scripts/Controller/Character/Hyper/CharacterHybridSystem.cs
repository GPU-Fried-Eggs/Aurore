using Character.Kinematic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Character.Hyper
{
    [AlwaysSynchronizeSystem]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    public partial class CharacterHybridSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingletonRW<EndSimulationEntityCommandBufferSystem.Singleton>().ValueRW
                .CreateCommandBuffer(World.Unmanaged);

            // Create
            foreach (var (characterAnimation, hybridData, entity) in SystemAPI
                         .Query<RefRW<CharacterAnimation>, CharacterHybridData>()
                         .WithNone<CharacterHybridLink>()
                         .WithEntityAccess())
            {
                var tmpObject = GameObject.Instantiate(hybridData.MeshPrefab);
                var animator = tmpObject.GetComponent<Animator>();

                ecb.AddComponent(entity, new CharacterHybridLink
                {
                    Object = tmpObject,
                    Animator = animator,
                });

                // Find the clipIndex param
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.name == "ClipIndex")
                    {
                        characterAnimation.ValueRW.ClipIndexParameterHash = parameter.nameHash;
                        break;
                    }
                }
            }

            // Update
            foreach (var (characterAnimation, characterBody, characterTransform, characterComponent,
                         characterStateMachine, characterControl, hybridLink, entity) in SystemAPI
                         .Query<
                             RefRW<CharacterAnimation>,
                             KinematicCharacterBody,
                             LocalTransform,
                             CharacterData,
                             CharacterStateMachine,
                             CharacterControl,
                             CharacterHybridLink>()
                         .WithEntityAccess())
            {
                if (hybridLink.Object)
                {
                    // Transform
                    var meshRootLTW = SystemAPI.GetComponent<LocalToWorld>(characterComponent.MeshRootEntity);
                    hybridLink.Object.transform.position = meshRootLTW.Position;
                    hybridLink.Object.transform.rotation = meshRootLTW.Rotation;

                    // Animation
                    if (hybridLink.Animator)
                    {
                        hybridLink.Animator.UpdateAnimation(ref characterAnimation.ValueRW,
                            in characterBody,
                            in characterComponent,
                            in characterStateMachine,
                            in characterControl,
                            in characterTransform);
                    }
                }
            }

            // Destroy
            foreach (var (hybridLink, entity) in SystemAPI.Query<CharacterHybridLink>()
                         .WithNone<CharacterHybridData>()
                         .WithEntityAccess())
            {
                GameObject.Destroy(hybridLink.Object);
                ecb.RemoveComponent<CharacterHybridLink>(entity);
            }
        }
    }
}

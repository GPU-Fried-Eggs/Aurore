using Unity.Entities;
using Unity.NetCode;
using Utilities;

namespace Manager
{
    public struct MoveToClientWorld : IComponentData { }

    public struct MoveToServerWorld : IComponentData { }

    public struct MoveToLocalWorld : IComponentData { }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class MoveLocalEntitiesToClientServerSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Move entities to clients
            var pendingMoveToClientQuery = SystemAPI.QueryBuilder().WithAll<MoveToClientWorld>().Build();
            if (pendingMoveToClientQuery.CalculateEntityCount() > 0)
            {
                // For each client world...
                var worlds =   World.All;
                foreach (var tmpWorld in worlds)
                {
                    if (WorldUtilities.IsValidAndCreated(tmpWorld) && (tmpWorld.IsClient() || tmpWorld.IsThinClient()))
                    {
                        WorldUtilities.CopyEntitiesToWorld(EntityManager, tmpWorld.EntityManager,
                            pendingMoveToClientQuery);
                    }
                }
            
                // Destroy entities in this world after copying them to all target worlds
                EntityManager.DestroyEntity(pendingMoveToClientQuery);
            }

            // Move entities to server
            var pendingMoveToServerQuery = SystemAPI.QueryBuilder().WithAll<MoveToServerWorld>().Build();
            if (pendingMoveToServerQuery.CalculateEntityCount() > 0)
            {
                // For each server world...
                var worlds = World.All;
                foreach (var tmpWorld in worlds)
                {
                    if (WorldUtilities.IsValidAndCreated(tmpWorld) && tmpWorld.IsServer())
                    {
                        WorldUtilities.CopyEntitiesToWorld(EntityManager, tmpWorld.EntityManager,
                            pendingMoveToServerQuery);
                    }
                }
            
                // Destroy entities in this world after copying them to all target worlds
                EntityManager.DestroyEntity(pendingMoveToServerQuery);
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class MoveClientServerEntitiesToLocalSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var pendingMoveToLocalQuery = SystemAPI.QueryBuilder().WithAll<MoveToLocalWorld>().Build();
            if (pendingMoveToLocalQuery.CalculateEntityCount() > 0)
            {
                var worlds = World.All;
                foreach (var tmpWorld in worlds)
                {
                    if (WorldUtilities.IsValidAndCreated(tmpWorld) && 
                        !(tmpWorld.IsClient() || tmpWorld.IsThinClient()) &&
                        !tmpWorld.IsServer() &&
                        tmpWorld.GetExistingSystemManaged<GameSystem>() != null)
                    {
                        WorldUtilities.CopyEntitiesToWorld(EntityManager, tmpWorld.EntityManager,
                            pendingMoveToLocalQuery);
                    }
            
                    // Destroy entities in this world after copying them to all target worlds
                    EntityManager.DestroyEntity(pendingMoveToLocalQuery);
                }
            }
        }
    }
}

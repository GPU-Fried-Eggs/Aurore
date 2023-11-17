using Unity.Entities;

namespace Utilities
{
    public static class WorldUtilities
    {
        public static bool IsValidAndCreated(World world)
        {
            return world is { IsCreated: true };
        }
    }
}
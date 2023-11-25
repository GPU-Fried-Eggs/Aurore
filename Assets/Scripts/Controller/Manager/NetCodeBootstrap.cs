using Unity.NetCode;
using UnityEngine.Scripting;

namespace Manager
{
    [Preserve]
    public class NetCodeBootstrap : ClientServerBootstrap
    {
        public override bool Initialize(string defaultWorldName)
        {
            CreateLocalWorld(defaultWorldName);
            return true;
        }
    }
}

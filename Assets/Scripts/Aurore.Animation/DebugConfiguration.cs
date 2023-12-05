using Unity.Entities;
using Unity.Mathematics;

public struct DebugConfigurationComponent : IComponentData
{
    public bool logRigDefinitionBaking;
    public bool logSkinnedMeshBaking;
    public bool logAnimatorBaking;
    public bool logClipBaking;

    public bool logAnimatorControllerProcesses;
    public bool logAnimationCalculationProcesses;

    public bool VisualizeAllRigs;
    public float4 ColorTri;
    public float4 ColorLines;

    public static DebugConfigurationComponent Default()
    {
        return new DebugConfigurationComponent
        {
            ColorTri = new float4(0, 1, 1, 0.3f),
            ColorLines = new float4(0, 1, 1, 1)
        };
    }
}
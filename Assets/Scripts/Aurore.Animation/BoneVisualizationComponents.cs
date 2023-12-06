using Unity.Entities;
using Unity.Mathematics;

public struct BoneVisualizationComponent: IComponentData
{
	public float4 ColorTri;
	public float4 ColorLines;
}
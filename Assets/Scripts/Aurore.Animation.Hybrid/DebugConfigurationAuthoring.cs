using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class DebugConfigurationAuthoring: MonoBehaviour
{
	[Header("Baking Systems")]
	public bool logRigDefinitionBaking;
	public bool logSkinnedMeshBaking;
	public bool logAnimatorBaking;
	public bool logClipBaking;

	[Header("Animator Controller System")]
	public bool logAnimatorControllerProcesses;

	[Header("Animation Process System")]
	public bool logAnimationCalculationProcesses;

	[Header("Bone Visualization")]
	public bool visualizeAllRigs;
	public Color boneColor = new Color(0, 1, 1, 0.3f);
	public Color outlineColor = new Color(0, 1, 1, 1);
	
	public class DebugConfigurationBaker: Baker<DebugConfigurationAuthoring>
	{
		public override void Bake(DebugConfigurationAuthoring authoring)
		{
			var entity = GetEntity(TransformUsageFlags.None);

			AddComponent(entity, new DebugConfigurationComponent
			{
				logAnimatorBaking = authoring.logAnimatorBaking,
				logAnimatorControllerProcesses = authoring.logAnimatorControllerProcesses,
				logAnimationCalculationProcesses = authoring.logAnimationCalculationProcesses,
				logClipBaking = authoring.logClipBaking,
				logRigDefinitionBaking = authoring.logRigDefinitionBaking,
				logSkinnedMeshBaking = authoring.logSkinnedMeshBaking,

				VisualizeAllRigs = authoring.visualizeAllRigs,
				ColorLines = new float4(authoring.outlineColor.r, authoring.outlineColor.g, authoring.outlineColor.b, authoring.outlineColor.a),
				ColorTri = new float4(authoring.boneColor.r, authoring.boneColor.g, authoring.boneColor.b, authoring.boneColor.a),
			});
		}
	}
}
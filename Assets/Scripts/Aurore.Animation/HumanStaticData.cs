using Unity.Mathematics;
using UnityEngine;

public static class HumanStaticData
{
	private const float k_CBodyDefaultMass = 82.5f;

	public static readonly float[] BodyPartsMasses = 
	{
		12 / k_CBodyDefaultMass,
		10 / k_CBodyDefaultMass,
		10 / k_CBodyDefaultMass,
		4 / k_CBodyDefaultMass,
		4 / k_CBodyDefaultMass,
		0.8f / k_CBodyDefaultMass,
		0.8f / k_CBodyDefaultMass,
		2.5f / k_CBodyDefaultMass,
		12 / k_CBodyDefaultMass,
		12 / k_CBodyDefaultMass,
		1 / k_CBodyDefaultMass,
		4 / k_CBodyDefaultMass,
		0.5f / k_CBodyDefaultMass,
		0.5f / k_CBodyDefaultMass,
		2 / k_CBodyDefaultMass,
		2 / k_CBodyDefaultMass,
		1.5f / k_CBodyDefaultMass,
		1.5f / k_CBodyDefaultMass,
		0.5f / k_CBodyDefaultMass,
		0.5f / k_CBodyDefaultMass,
		0.2f / k_CBodyDefaultMass,
		0.2f / k_CBodyDefaultMass,
	};

	public static readonly int4[] MassIndicesTable = 
	{
		new ((int)HumanBodyBones.LeftUpperLeg, (int)HumanBodyBones.RightUpperLeg, (int)HumanBodyBones.Spine, -1),
		new ((int)HumanBodyBones.LeftUpperLeg, (int)HumanBodyBones.LeftLowerLeg, -1, -1),
		new ((int)HumanBodyBones.RightUpperLeg, (int)HumanBodyBones.RightLowerLeg, -1, -1),
		new ((int)HumanBodyBones.LeftLowerLeg, (int)HumanBodyBones.LeftFoot, -1, -1),
		new ((int)HumanBodyBones.RightLowerLeg, (int)HumanBodyBones.RightFoot, -1, -1),
		new ((int)HumanBodyBones.LeftFoot, -1, -1, -1),
		new ((int)HumanBodyBones.RightFoot, -1, -1, -1),
		new ((int)HumanBodyBones.Spine, (int)HumanBodyBones.Chest, -1, -1),
		new ((int)HumanBodyBones.UpperChest, (int)HumanBodyBones.Chest, -1, -1),
		new ((int)HumanBodyBones.UpperChest, (int)HumanBodyBones.Neck, (int)HumanBodyBones.LeftShoulder, (int)HumanBodyBones.RightShoulder),
		new ((int)HumanBodyBones.Neck, (int)HumanBodyBones.Head, -1, 1),
		new ((int)HumanBodyBones.Head, -1, -1, 1),
		new ((int)HumanBodyBones.LeftShoulder, (int)HumanBodyBones.LeftUpperArm, -1, 1),
		new ((int)HumanBodyBones.RightShoulder, (int)HumanBodyBones.RightUpperArm, -1, 1),
		new ((int)HumanBodyBones.LeftLowerArm, (int)HumanBodyBones.LeftUpperArm, -1, 1),
		new ((int)HumanBodyBones.RightLowerArm, (int)HumanBodyBones.RightUpperArm, -1, 1),
		new ((int)HumanBodyBones.LeftLowerArm, (int)HumanBodyBones.LeftHand, -1, 1),
		new ((int)HumanBodyBones.RightLowerArm, (int)HumanBodyBones.RightHand, -1, 1),
		new ((int)HumanBodyBones.LeftHand, -1, -1, 1),
		new ((int)HumanBodyBones.RightHand, -1, -1, 1),
		new ((int)HumanBodyBones.LeftToes, -1, -1, 1),
		new ((int)HumanBodyBones.RightToes, -1, -1, 1),
	};
}
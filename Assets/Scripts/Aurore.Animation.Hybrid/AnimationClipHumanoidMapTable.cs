#if UNITY_EDITOR
using System.Collections.Generic;

public partial class AnimationClipBaker
{
	static Dictionary<string, ParsedCurveBinding> s_HumanoidMappingTable;
	static Dictionary<string, string> s_HumanoidMuscleNameFromCurveProperty;

	static AnimationClipBaker()
	{
		s_HumanoidMappingTable = new Dictionary<string, ParsedCurveBinding>
		{
			//	--- Head ---
			//	Neck
			{ "Neck Nod Down-Up",				new ParsedCurveBinding { BoneName = "Neck", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Neck Tilt Left-Right",			new ParsedCurveBinding { BoneName = "Neck", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Neck Turn Left-Right",			new ParsedCurveBinding { BoneName = "Neck", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Head
			{ "Head Nod Down-Up",				new ParsedCurveBinding { BoneName = "Head", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Head Tilt Left-Right",			new ParsedCurveBinding { BoneName = "Head", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Head Turn Left-Right",			new ParsedCurveBinding { BoneName = "Head", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Left Eye
			{ "Left Eye Down-Up",				new ParsedCurveBinding { BoneName = "LeftEye", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Left Eye In-Out",				new ParsedCurveBinding { BoneName = "LeftEye", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Right Eye
			{ "Right Eye Down-Up",				new ParsedCurveBinding { BoneName = "RightEye", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Right Eye In-Out",				new ParsedCurveBinding { BoneName = "RightEye", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Jaw
			{ "Jaw Close",						new ParsedCurveBinding { BoneName = "Jaw", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Jaw Left-Right",					new ParsedCurveBinding { BoneName = "Jaw", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},

			//	--- Body ---
			//	Spine
			{ "Spine Front-Back",				new ParsedCurveBinding { BoneName = "Spine", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Spine Left-Right",				new ParsedCurveBinding { BoneName = "Spine", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Spine Twist Left-Right",			new ParsedCurveBinding { BoneName = "Spine", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Chest
			{ "Chest Front-Back",				new ParsedCurveBinding { BoneName = "Chest", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Chest Left-Right",				new ParsedCurveBinding { BoneName = "Chest", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Chest Twist Left-Right",			new ParsedCurveBinding { BoneName = "Chest", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	UpperChest
			{ "UpperChest Front-Back",			new ParsedCurveBinding { BoneName = "UpperChest", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "UpperChest Left-Right",			new ParsedCurveBinding { BoneName = "UpperChest", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "UpperChest Twist Left-Right",	new ParsedCurveBinding { BoneName = "UpperChest", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},

			//	--- Left Arm ---
			//	LeftShoulder
			{ "Left Shoulder Down-Up",			new ParsedCurveBinding { BoneName = "LeftShoulder", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Left Shoulder Front-Back",		new ParsedCurveBinding { BoneName = "LeftShoulder", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	LeftUpperArm
			{ "Left Arm Down-Up",				new ParsedCurveBinding { BoneName = "LeftUpperArm", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Left Arm Front-Back",			new ParsedCurveBinding { BoneName = "LeftUpperArm", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Left Arm Twist In-Out",			new ParsedCurveBinding { BoneName = "LeftUpperArm", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	LeftLowerArm
			{ "Left Forearm Stretch",			new ParsedCurveBinding { BoneName = "LeftLowerArm", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Left Forearm Twist In-Out",		new ParsedCurveBinding { BoneName = "LeftLowerArm", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	LeftHand
			{ "Left Hand Down-Up",				new ParsedCurveBinding { BoneName = "LeftHand", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Left Hand In-Out",				new ParsedCurveBinding { BoneName = "LeftHand", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},

			//	--- Left Hand ---
			//	Thumb 1
			{ "LeftHand.Thumb.1 Stretched",		new ParsedCurveBinding { BoneName = "Left Thumb Proximal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "LeftHand.Thumb Spread",			new ParsedCurveBinding { BoneName = "Left Thumb Proximal", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Thumb 2
			{ "LeftHand.Thumb.2 Stretched",		new ParsedCurveBinding { BoneName = "Left Thumb Intermediate", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Thumb 3
			{ "LeftHand.Thumb.3 Stretched",		new ParsedCurveBinding { BoneName = "Left Thumb Distal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Index 1
			{ "LeftHand.Index.1 Stretched",		new ParsedCurveBinding { BoneName = "Left Index Proximal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "LeftHand.Index.Spread",			new ParsedCurveBinding { BoneName = "Left Index Proximal", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Index 2
			{ "LeftHand.Index.2 Stretched",		new ParsedCurveBinding { BoneName = "Left Index Intermediate", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Index 3
			{ "LeftHand.Index.3 Stretched",		new ParsedCurveBinding { BoneName = "Left Index Distal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Middle 1
			{ "LeftHand.Middle.1 Stretched",	new ParsedCurveBinding { BoneName = "Left Middle Proximal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "LeftHand.Middle.Spread",			new ParsedCurveBinding { BoneName = "Left Middle Proximal", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Middle 2
			{ "LeftHand.Middle.2 Stretched",	new ParsedCurveBinding { BoneName = "Left Middle Intermediate", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Middle 3
			{ "LeftHand.Middle.3 Stretched",	new ParsedCurveBinding { BoneName = "Left Middle Distal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Ring 1
			{ "LeftHand.Ring.1 Stretched",		new ParsedCurveBinding { BoneName = "Left Ring Proximal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "LeftHand.Ring.Spread",			new ParsedCurveBinding { BoneName = "Left Ring Proximal", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Ring 2
			{ "LeftHand.Ring.2 Stretched",		new ParsedCurveBinding { BoneName = "Left Ring Intermediate", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Ring 3
			{ "LeftHand.Ring.3 Stretched",		new ParsedCurveBinding { BoneName = "Left Ring Distal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Little 1
			{ "LeftHand.Little.1 Stretched",	new ParsedCurveBinding { BoneName = "Left Little Proximal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "LeftHand.Little.Spread",			new ParsedCurveBinding { BoneName = "Left Little Proximal", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Little 2
			{ "LeftHand.Little.2 Stretched",	new ParsedCurveBinding { BoneName = "Left Little Intermediate", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Little 3
			{ "LeftHand.Little.3 Stretched",	new ParsedCurveBinding { BoneName = "Left Little Distal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},

			//	--- Right Arm ---
			//	RightShoulder
			{ "Right Shoulder Down-Up",			new ParsedCurveBinding { BoneName = "RightShoulder", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Right Shoulder Front-Back",		new ParsedCurveBinding { BoneName = "RightShoulder", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	RightUpperArm
			{ "Right Arm Down-Up",				new ParsedCurveBinding { BoneName = "RightUpperArm", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Right Arm Front-Back",			new ParsedCurveBinding { BoneName = "RightUpperArm", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Right Arm Twist In-Out",			new ParsedCurveBinding { BoneName = "RightUpperArm", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	RightLowerArm
			{ "Right Forearm Stretch",			new ParsedCurveBinding { BoneName = "RightLowerArm", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Right Forearm Twist In-Out",		new ParsedCurveBinding { BoneName = "RightLowerArm", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	RightHand
			{ "Right Hand Down-Up",				new ParsedCurveBinding { BoneName = "RightHand", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Right Hand In-Out",				new ParsedCurveBinding { BoneName = "RightHand", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},

			//	--- Right Hand ---
			//	Thumb 1
			{ "RightHand.Thumb.1 Stretched",	new ParsedCurveBinding { BoneName = "Right Thumb Proximal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "RightHand.Thumb Spread",			new ParsedCurveBinding { BoneName = "Right Thumb Proximal", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Thumb 2
			{ "RightHand.Thumb.2 Stretched",	new ParsedCurveBinding { BoneName = "Right Thumb Intermediate", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Thumb 3
			{ "RightHand.Thumb.3 Stretched",	new ParsedCurveBinding { BoneName = "Right Thumb Distal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Index 1
			{ "RightHand.Index.1 Stretched",	new ParsedCurveBinding { BoneName = "Right Index Proximal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "RightHand.Index.Spread",			new ParsedCurveBinding { BoneName = "Right Index Proximal", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Index 2
			{ "RightHand.Index.2 Stretched",	new ParsedCurveBinding { BoneName = "Right Index Intermediate", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Index 3
			{ "RightHand.Index.3 Stretched",	new ParsedCurveBinding { BoneName = "Right Index Distal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Middle 1
			{ "RightHand.Middle.1 Stretched",	new ParsedCurveBinding { BoneName = "Right Middle Proximal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "RightHand.Middle.Spread",		new ParsedCurveBinding { BoneName = "Right Middle Proximal", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Middle 2
			{ "RightHand.Middle.2 Stretched",	new ParsedCurveBinding { BoneName = "Right Middle Intermediate", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Middle 3
			{ "RightHand.Middle.3 Stretched",	new ParsedCurveBinding { BoneName = "Right Middle Distal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Ring 1
			{ "RightHand.Ring.1 Stretched",		new ParsedCurveBinding { BoneName = "Right Ring Proximal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "RightHand.Ring.Spread",			new ParsedCurveBinding { BoneName = "Right Ring Proximal", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Ring 2
			{ "RightHand.Ring.2 Stretched",		new ParsedCurveBinding { BoneName = "Right Ring Intermediate", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Ring 3
			{ "RightHand.Ring.3 Stretched",		new ParsedCurveBinding { BoneName = "Right Ring Distal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Little 1
			{ "RightHand.Little.1 Stretched",	new ParsedCurveBinding { BoneName = "Right Little Proximal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "RightHand.Little.Spread",			new ParsedCurveBinding { BoneName = "Right Little Proximal", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Little 2
			{ "RightHand.Little.2 Stretched",	new ParsedCurveBinding { BoneName = "Right Little Intermediate", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	Little 3
			{ "RightHand.Little.3 Stretched",	new ParsedCurveBinding { BoneName = "Right Little Distal", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},

			//	--- Left Leg ---
			//	LeftUpperLeg
			{ "Left Upper Leg Front-Back",		new ParsedCurveBinding { BoneName = "LeftUpperLeg", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Left Upper Leg In-Out",			new ParsedCurveBinding { BoneName = "LeftUpperLeg", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Left Upper Leg Twist In-Out",	new ParsedCurveBinding { BoneName = "LeftUpperLeg", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	LeftLowerLeg
			{ "Left Lower Leg Stretch",			new ParsedCurveBinding { BoneName = "LeftLowerLeg", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Left Lower Leg Twist In-Out",	new ParsedCurveBinding { BoneName = "LeftLowerLeg", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	LeftFoot
			{ "Left Foot Up-Down",				new ParsedCurveBinding { BoneName = "LeftFoot", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Left Foot Twist In-Out",			new ParsedCurveBinding { BoneName = "LeftFoot", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	LeftToes
			{ "Left Toes Up-Down",				new ParsedCurveBinding { BoneName = "LeftHand", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},

			//	--- Right Leg ---
			//	RightUpperLeg
			{ "Right Upper Leg Front-Back",		new ParsedCurveBinding { BoneName = "RightUpperLeg", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Right Upper Leg In-Out",			new ParsedCurveBinding { BoneName = "RightUpperLeg", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Right Upper Leg Twist In-Out",	new ParsedCurveBinding { BoneName = "RightUpperLeg", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	RightLowerLeg
			{ "Right Lower Leg Stretch",		new ParsedCurveBinding { BoneName = "RightLowerLeg", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Right Lower Leg Twist In-Out",	new ParsedCurveBinding { BoneName = "RightLowerLeg", ChannelIndex = 0, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	RightFoot
			{ "Right Foot Up-Down",				new ParsedCurveBinding { BoneName = "RightFoot", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			{ "Right Foot Twist In-Out",		new ParsedCurveBinding { BoneName = "RightFoot", ChannelIndex = 1, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},
			//	RightToes
			{ "Right Toes Up-Down",				new ParsedCurveBinding { BoneName = "RightHand", ChannelIndex = 2, BindingType = BindingType.HumanMuscle, BoneType = BoneType.Generic }},

			// --- Special Bones ---
			/*
			{ "RootT.x",						new ParsedCurveBinding { boneName = SpecialBones.unnamedRootBoneName, channelIndex = 0, bindingType = BindingType.Translation, boneType = BoneType.RootCurve }},
			{ "RootT.y",						new ParsedCurveBinding { boneName = SpecialBones.unnamedRootBoneName, channelIndex = 1, bindingType = BindingType.Translation, boneType = BoneType.RootCurve }},
			{ "RootT.z",						new ParsedCurveBinding { boneName = SpecialBones.unnamedRootBoneName, channelIndex = 2, bindingType = BindingType.Translation, boneType = BoneType.RootCurve }},
			{ "RootQ.x",						new ParsedCurveBinding { boneName = SpecialBones.unnamedRootBoneName, channelIndex = 0, bindingType = BindingType.Quaternion, boneType = BoneType.RootCurve }},
			{ "RootQ.y",						new ParsedCurveBinding { boneName = SpecialBones.unnamedRootBoneName, channelIndex = 1, bindingType = BindingType.Quaternion, boneType = BoneType.RootCurve }},
			{ "RootQ.z",						new ParsedCurveBinding { boneName = SpecialBones.unnamedRootBoneName, channelIndex = 2, bindingType = BindingType.Quaternion, boneType = BoneType.RootCurve }},
			{ "RootQ.w",						new ParsedCurveBinding { boneName = SpecialBones.unnamedRootBoneName, channelIndex = 3, bindingType = BindingType.Quaternion, boneType = BoneType.RootCurve }},
			*/
		};
	}
}
#endif
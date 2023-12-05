using Unity.Collections;
using UnityEngine;
using FixedStringName = Unity.Collections.FixedString512Bytes;
using Hash128 = Unity.Entities.Hash128;

public class AvatarMaskConversionSystem
{
	public static RTP.AvatarMask PrepareAvatarMaskComputeData(AvatarMask avatarMask)
	{
		var bakedAvatarMask = new RTP.AvatarMask();
		if (avatarMask != null)
		{
			bakedAvatarMask.IncludedBonePaths = new (avatarMask.transformCount, Allocator.Persistent);
			bakedAvatarMask.Name = avatarMask.ToString();
			for (int i = 0; avatarMask != null && i < avatarMask.transformCount; ++i)
			{
				var bonePath = avatarMask.GetTransformPath(i);
				var boneActive = avatarMask.GetTransformActive(i);
				if (bonePath.Length == 0 || !boneActive) continue;
				var boneNames = bonePath.Split('/');
				var leafBoneName = new FixedStringName(boneNames[boneNames.Length - 1]);
				bakedAvatarMask.IncludedBonePaths.Add(leafBoneName);
#if AURORE_DEBUG
				Debug.Log($"Adding avatar mask bone '{leafBoneName}'");
#endif
			}
			bakedAvatarMask.Hash = new Hash128((uint)avatarMask.GetHashCode(), 12, 13, 14);

			//	Humanoid avatar mask
			var humanBodyPartsCount = (int)AvatarMaskBodyPart.LastBodyPart;
			bakedAvatarMask.HumanBodyPartsAvatarMask = 0;
			for (int i = 0; i < humanBodyPartsCount; ++i)
			{
				var ambp = (AvatarMaskBodyPart)i;
				if (avatarMask.GetHumanoidBodyPartActive(ambp))
					bakedAvatarMask.HumanBodyPartsAvatarMask |= 1u << i;
			}
		}

		return bakedAvatarMask;
	}
}
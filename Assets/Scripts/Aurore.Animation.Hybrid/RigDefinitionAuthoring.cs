using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using FixedStringName = Unity.Collections.FixedString512Bytes;

[TemporaryBakingType]
public struct RigDefinitionBakerComponent: IComponentData
{
	public RTP.RigDefinition RigDefData;
	public Entity TargetEntity;
	public bool ApplyRootMotion;
	public int Hash;
#if AURORE_DEBUG
	public FixedStringName Name;
#endif
}

[TemporaryBakingType]
public struct BoneEntitiesToRemove : IBufferElementData
{
	public Entity BoneEntity;
}

public class InternalSkeletonBone
{
	public string Name;
	public string ParentName;
	public Vector3 Position;
	public Quaternion Rotation;
	public Vector3 Scale;
}


[RequireComponent(typeof(Animator))]
public class RigDefinitionAuthoring: MonoBehaviour
{
    public AvatarMask boneStrippingMask;
    
	public class RigDefinitionBaker: Baker<RigDefinitionAuthoring>
	{
		private static FieldInfo s_ParentBoneNameField;
		
		static RigDefinitionBaker()
		{
			s_ParentBoneNameField = typeof(SkeletonBone).GetField("parentName", BindingFlags.NonPublic | BindingFlags.Instance);
		}

		public override void Bake(RigDefinitionAuthoring authoring)
		{
			var animator = GetComponent<Animator>();
			var entity = GetEntity(TransformUsageFlags.Dynamic);
			
			var processedRig = CreateRigDefinitionFromRigAuthoring(entity, authoring, animator);
			var component = new RigDefinitionBakerComponent
			{
				RigDefData = processedRig,
				TargetEntity = GetEntity(TransformUsageFlags.Dynamic),
				Hash = processedRig.GetHashCode(),
				ApplyRootMotion = animator.applyRootMotion,
#if AURORE_DEBUG
				Name = animator.name
#endif
			};
	
			DependsOn(authoring);
			AddComponent(entity, component);
		}

		private InternalSkeletonBone CreateSkeletonBoneFromTransform(Transform transform, string parentName)
		{
			return new InternalSkeletonBone
			{
				Name = transform.name,
				Position = transform.localPosition,
				Rotation = transform.localRotation,
				Scale = transform.localScale,
				ParentName = parentName
			};
		}

		private void TransformHierarchyWalk(Transform parent, List<InternalSkeletonBone> sb)
		{
			for (var i = 0; i < parent.childCount; ++i)
			{
				var child = parent.GetChild(i);
				var childTransform = child.transform;
				var bone = CreateSkeletonBoneFromTransform(childTransform, parent.name);
				sb.Add(bone);
	
				TransformHierarchyWalk(childTransform, sb);
			}
		}

		private List<InternalSkeletonBone> CreateAvatarFromObjectHierarchy(GameObject root)
		{
			//	Manually fill all bone transforms
			var sb = new List<InternalSkeletonBone>();
			var rootBone = CreateSkeletonBoneFromTransform(root.transform, "");
			sb.Add(rootBone);
	
			TransformHierarchyWalk(root.transform, sb);
			return sb;
		}

		private int GetRigRootBoneIndex(Animator animator, List<InternalSkeletonBone> rigBones)
		{
			var avatar = animator.avatar;
			if (avatar == null) return 0;
			
			var rootBoneName = avatar.GetRootMotionNodeName();
			if (animator.avatar.isHuman)
			{
				var humanDescription = animator.avatar.humanDescription;
				var humanBoneIndexInDesc = Array.FindIndex(humanDescription.human, x => x.humanName == "Hips");
				rootBoneName = humanDescription.human[humanBoneIndexInDesc].boneName;
			}

			return math.max(rigBones.FindIndex(x => x.Name == rootBoneName), 0);
		}

		private List<InternalSkeletonBone> CreateInternalRigRepresentation(Avatar avatar, RigDefinitionAuthoring rd)
		{
			if (avatar == null) return CreateAvatarFromObjectHierarchy(rd.gameObject);
			
			var skeleton = avatar.humanDescription.skeleton;
			var skeletonBones = new List<InternalSkeletonBone>();
			for (var i = 0; i < skeleton.Length; ++i)
			{
				var skeletonBone = skeleton[i];
				skeletonBones.Add(new InternalSkeletonBone
				{
					Name = skeletonBone.name,
					Position = skeletonBone.position,
					Rotation = skeletonBone.rotation,
					Scale = skeletonBone.scale,
					ParentName = (string)s_ParentBoneNameField.GetValue(skeletonBone)
				});
			}
	
			return skeletonBones;
		}

		private RTP.RigDefinition CreateRigDefinitionFromRigAuthoring(Entity rigEntity, RigDefinitionAuthoring rigDef, Animator animator)
		{
			var avatar = animator.avatar;
	
			var bakedRigDefinition = new RTP.RigDefinition();
			bakedRigDefinition.RigBones = new UnsafeList<RTP.RigBoneInfo>(60, Allocator.Persistent);
	
			bakedRigDefinition.Name = rigDef.gameObject.name;
			bakedRigDefinition.IsHuman = avatar != null && avatar.isHuman;
	
			var skeletonBones = CreateInternalRigRepresentation(avatar, rigDef);
			if (skeletonBones.Count == 0)
			{
				Debug.LogError($"Unity avatar '{avatar.name}' setup is incorrect.");
				return bakedRigDefinition;
			}
	
			for (var i = 0; i < skeletonBones.Count; ++i)
			{
				var ab = CreateRigBoneInfo(rigDef, skeletonBones, avatar, i);
				bakedRigDefinition.RigBones.Add(ab);
			}
			
			bakedRigDefinition.RootBoneIndex = GetRigRootBoneIndex(animator, skeletonBones);
			
			ProcessBoneStrippingMask(rigEntity, rigDef, bakedRigDefinition.RigBones);
	
			return bakedRigDefinition;
		}

		private RTP.RigBoneInfo.HumanRotationData GetHumanoidBoneRotationData(Avatar avatar, string boneName)
		{
			if (avatar == null || !avatar.isHuman)
				return RTP.RigBoneInfo.HumanRotationData.Identity;
	
			var humanDescription = avatar.humanDescription;
			var humanBoneInSkeletonIndex = Array.FindIndex(humanDescription.human, x => x.boneName == boneName);
			if (humanBoneInSkeletonIndex < 0)
				return RTP.RigBoneInfo.HumanRotationData.Identity;
				
			var humanBones = HumanTrait.BoneName;
			var humanBoneDef = humanDescription.human[humanBoneInSkeletonIndex];
			var humanBoneId = Array.FindIndex(humanBones, x => x == humanBoneDef.humanName);
			Debug.Assert(humanBoneId >= 0);
	
			var bakedRotationData = RTP.RigBoneInfo.HumanRotationData.Identity;
			bakedRotationData.PreRot = avatar.GetPreRotation(humanBoneId);
			bakedRotationData.PostRot = avatar.GetPostRotation(humanBoneId);
			bakedRotationData.Sign = avatar.GetLimitSign(humanBoneId);
			bakedRotationData.HumanRigIndex = humanBoneId;
	
			var minA = humanBoneDef.limit.min;
			var maxA = humanBoneDef.limit.max;
			if (humanBoneDef.limit.useDefaultValues)
			{
				minA.x = HumanTrait.GetMuscleDefaultMin(HumanTrait.MuscleFromBone(humanBoneId, 0));
				minA.y = HumanTrait.GetMuscleDefaultMin(HumanTrait.MuscleFromBone(humanBoneId, 1));
				minA.z = HumanTrait.GetMuscleDefaultMin(HumanTrait.MuscleFromBone(humanBoneId, 2));
	
				maxA.x = HumanTrait.GetMuscleDefaultMax(HumanTrait.MuscleFromBone(humanBoneId, 0));
				maxA.y = HumanTrait.GetMuscleDefaultMax(HumanTrait.MuscleFromBone(humanBoneId, 1));
				maxA.z = HumanTrait.GetMuscleDefaultMax(HumanTrait.MuscleFromBone(humanBoneId, 2));
			}
			bakedRotationData.MinAngle = math.radians(minA);
			bakedRotationData.MaxAngle = math.radians(maxA);
	
			return bakedRotationData;
		}

		private Entity GetEntityForBone(Transform transform, TransformUsageFlags boneFlags)
		{
			if (transform == null || transform.GetComponent<SkinnedMeshRenderer>() != null)
				return Entity.Null;
	
			return GetEntity(transform, boneFlags);
		}

		private RTP.RigBoneInfo CreateRigBoneInfo(RigDefinitionAuthoring rda, List<InternalSkeletonBone> skeletonBones, Avatar avatar, int boneIndex)
		{
			var boneIsObjectRoot = boneIndex == 0;
			var skeletonBone = skeletonBones[boneIndex];
			var t = boneIsObjectRoot ? rda.transform : TransformUtilities.FindChildRecursively(rda.transform, skeletonBone.Name);
	
			var name = skeletonBone.Name;
			// Special handling of hierarchy root
			if (boneIsObjectRoot)
			{
				name = SpecialBones.unnamedRootBoneName.ToString();
			}
	
			var parentBoneIndex = skeletonBones.FindIndex(x => x.Name == skeletonBone.ParentName);
	
			var pose = new BoneTransform
			{
				Position = skeletonBone.Position,
				Rotation = skeletonBone.Rotation,
				Scale = skeletonBone.Scale
			};
	
			//	Add humanoid avatar info
			var humanRotData = GetHumanoidBoneRotationData(avatar, name);
	
			var boneName = new FixedStringName(name);
			var boneHash = boneName.CalculateHash128();
			var boneTransformFlags = TransformUsageFlags.Dynamic;
			if (rda.boneStrippingMask != null && !boneIsObjectRoot)
				boneTransformFlags |= TransformUsageFlags.WorldSpace;

			return new RTP.RigBoneInfo
			{
				Name = boneName,
				Hash = boneHash,
				ParentBoneIndex = parentBoneIndex,
				RefPose = pose,
				BoneObjectEntity = GetEntityForBone(t, boneTransformFlags),
				HumanRotation = humanRotData,
			};
		}

		private void ProcessBoneStrippingMask(Entity rigEntity, RigDefinitionAuthoring rda, UnsafeList<RTP.RigBoneInfo> rigBones)
		{
			if (rda.boneStrippingMask == null) return;
	
			var avatarMask = rda.boneStrippingMask;
			var bonesToRemove = AddBuffer<BoneEntitiesToRemove>(rigEntity);
	        
			for (var i = 0; i < avatarMask.transformCount; ++i)
			{
				var isActive = avatarMask.GetTransformActive(i);
				if (isActive) continue;
				
				var path = avatarMask.GetTransformPath(i);
				var boneIndex = 0;
				for (; boneIndex < rigBones.Length && !path.EndsWith(rigBones[boneIndex].Name.ToString()); ++boneIndex) { }
	
				if (boneIndex < rigBones.Length)
				{
					bonesToRemove.Add(new BoneEntitiesToRemove { BoneEntity = rigBones[boneIndex].BoneObjectEntity});
				}
			}
		}
	}
}
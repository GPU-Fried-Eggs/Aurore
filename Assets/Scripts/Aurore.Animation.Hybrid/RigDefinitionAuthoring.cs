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

		public override void Bake(RigDefinitionAuthoring a)
		{
			var animator = GetComponent<Animator>();
			var e = GetEntity(TransformUsageFlags.Dynamic);
			
			var processedRig = CreateRigDefinitionFromRigAuthoring(e, a, animator);
			var acbd = new RigDefinitionBakerComponent
			{
				RigDefData = processedRig,
				TargetEntity = GetEntity(TransformUsageFlags.Dynamic),
				Hash = processedRig.GetHashCode(),
				ApplyRootMotion = animator.applyRootMotion,
			#if AURORE_DEBUG
				Name = a.name
			#endif
			};
	
			DependsOn(a);
			AddComponent(e, acbd);
		}

		private InternalSkeletonBone CreateSkeletonBoneFromTransform(Transform t, string parentName)
		{
			var bone = new InternalSkeletonBone();
			bone.Name = t.name;
			bone.Position = t.localPosition;
			bone.Rotation = t.localRotation;
			bone.Scale = t.localScale;
			bone.ParentName = parentName;
			return bone;
		}

		private void TransformHierarchyWalk(Transform parent, List<InternalSkeletonBone> sb)
		{
			for (int i = 0; i < parent.childCount; ++i)
			{
				var c = parent.GetChild(i);
				var ct = c.transform;
				var bone = CreateSkeletonBoneFromTransform(ct, parent.name);
				sb.Add(bone);
	
				TransformHierarchyWalk(ct, sb);
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

		private int GetRigRootBoneIndex(Animator anm, List<InternalSkeletonBone> rigBones)
		{
			var a = anm.avatar;
			if (a == null) return 0;
			
			var rootBoneName = a.GetRootMotionNodeName();
			if (anm.avatar.isHuman)
			{
				var hd = anm.avatar.humanDescription;
				var humanBoneIndexInDesc = Array.FindIndex(hd.human, x => x.humanName == "Hips");
				rootBoneName = hd.human[humanBoneIndexInDesc].boneName;
			}
			var rv = rigBones.FindIndex(x => x.Name == rootBoneName);
			return math.max(rv, 0);
		}

		private List<InternalSkeletonBone> CreateInternalRigRepresentation(Avatar avatar, RigDefinitionAuthoring rd)
		{
			if (avatar == null)
			{
				return CreateAvatarFromObjectHierarchy(rd.gameObject);
			}
			
			var skeleton = avatar.humanDescription.skeleton;
			var rv = new List<InternalSkeletonBone>();
			for (var i = 0; i < skeleton.Length; ++i)
			{
				var sb = skeleton[i];
				var isb = new InternalSkeletonBone
				{
					Name = sb.name,
					Position = sb.position,
					Rotation = sb.rotation,
					Scale = sb.scale,
					ParentName = (string)s_ParentBoneNameField.GetValue(sb)
				};
				rv.Add(isb);
			}
	
			return rv;
		}

		private RTP.RigDefinition CreateRigDefinitionFromRigAuthoring(Entity rigEntity, RigDefinitionAuthoring rigDef, Animator animator)
		{
			var avatar = animator.avatar;
	
			var rv = new RTP.RigDefinition();
			rv.RigBones = new UnsafeList<RTP.RigBoneInfo>(60, Allocator.Persistent);
	
			rv.Name = rigDef.gameObject.name;
			rv.IsHuman = avatar != null && avatar.isHuman;
	
			var skeletonBones = CreateInternalRigRepresentation(avatar, rigDef);
			if (skeletonBones.Count == 0)
			{
				Debug.LogError($"Unity avatar '{avatar.name}' setup is incorrect.");
				return rv;
			}
	
			for (var i = 0; i < skeletonBones.Count; ++i)
			{
				var ab = CreateRigBoneInfo(rigDef, skeletonBones, avatar, i);
				rv.RigBones.Add(ab);
			}
			
			rv.RootBoneIndex = GetRigRootBoneIndex(animator, skeletonBones);
			
			ProcessBoneStrippingMask(rigEntity, rigDef, rv.RigBones);
	
			return rv;
		}

		private RTP.RigBoneInfo.HumanRotationData GetHumanoidBoneRotationData(Avatar a, string boneName)
		{
			if (a == null || !a.isHuman)
				return RTP.RigBoneInfo.HumanRotationData.Identity();
	
			var hd = a.humanDescription;
			var humanBoneInSkeletonIndex = Array.FindIndex(hd.human, x => x.boneName == boneName);
			if (humanBoneInSkeletonIndex < 0)
				return RTP.RigBoneInfo.HumanRotationData.Identity();
				
			var humanBones = HumanTrait.BoneName;
			var humanBoneDef = hd.human[humanBoneInSkeletonIndex];
			var humanBoneId = Array.FindIndex(humanBones, x => x == humanBoneDef.humanName);
			Debug.Assert(humanBoneId >= 0);
	
			var rv = RTP.RigBoneInfo.HumanRotationData.Identity();
			rv.PreRot = a.GetPreRotation(humanBoneId);
			rv.PostRot = a.GetPostRotation(humanBoneId);
			rv.Sign = a.GetLimitSign(humanBoneId);
			rv.HumanRigIndex = humanBoneId;
	
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
			rv.MinAngle = math.radians(minA);
			rv.MaxAngle = math.radians(maxA);
	
			return rv;
		}

		private Entity GetEntityForBone(Transform t, TransformUsageFlags boneFlags)
		{
			if (t == null || t.GetComponent<SkinnedMeshRenderer>() != null)
				return Entity.Null;
	
			return GetEntity(t, boneFlags);
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
	
			var m = rda.boneStrippingMask;
			var bonesToRemove = AddBuffer<BoneEntitiesToRemove>(rigEntity);
	        
			for (int i = 0; i < m.transformCount; ++i)
			{
				var isActive = m.GetTransformActive(i);
				if (isActive) continue;
				
				var path = m.GetTransformPath(i);
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
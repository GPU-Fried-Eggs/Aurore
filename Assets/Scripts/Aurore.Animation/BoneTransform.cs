using Unity.Mathematics;
using Unity.Transforms;

public struct BoneTransform
{
	public float3 Position;
	public quaternion Rotation;
	public float3 Scale;

	public BoneTransform(LocalTransform transform)
	{
		Position = transform.Position;
		Rotation = transform.Rotation;
		Scale = transform.Scale;
	}

	public static readonly BoneTransform Identity = new() { Position = 0, Rotation = quaternion.identity, Scale = 1 };

	//	Multiply child with parent
	public static BoneTransform Multiply(in BoneTransform parent, in BoneTransform child)
	{
		var transform = new BoneTransform();
		transform.Position = math.mul(parent.Rotation, child.Position * parent.Scale) + parent.Position;
		transform.Rotation = math.mul(parent.Rotation, child.Rotation);
		transform.Scale = parent.Scale * child.Scale;
		return transform;
	}

	public static BoneTransform Inverse(in BoneTransform boneTransform)
	{
		var transform = new BoneTransform();
		transform.Rotation = math.inverse(boneTransform.Rotation);
		transform.Position = math.mul(transform.Rotation, -boneTransform.Position);
		transform.Scale = math.rcp(boneTransform.Scale);
		return transform;
	}

	public static BoneTransform TransformScale(in BoneTransform boneTransform, float3 scale)
	{
		return new BoneTransform
		{
			Position = boneTransform.Position * scale.x,
			Rotation = boneTransform.Rotation.value * scale.y,
			Scale = boneTransform.Scale * scale.z,
		};
	}

	public LocalTransform ToLocalTransformComponent() =>
		new() { Position = Position, Rotation = Rotation, Scale = Scale.x };

	public float4x4 ToFloat4X4() =>
		float4x4.TRS(Position, Rotation, Scale);
}

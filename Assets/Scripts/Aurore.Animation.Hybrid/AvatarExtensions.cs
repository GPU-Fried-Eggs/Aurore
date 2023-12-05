using System.Reflection;
using UnityEngine;

public static class AvatarExtensions
{
	static readonly MethodInfo s_GetPreRotationFn = typeof(Avatar).GetMethod("GetPreRotation", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo s_GetPostRotationFn = typeof(Avatar).GetMethod("GetPostRotation", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo s_GetLimitSignFn = typeof(Avatar).GetMethod("GetLimitSign", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo s_GetZyPostQFn = typeof(Avatar).GetMethod("GetZYPostQ", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo s_GetZyRollFn = typeof(Avatar).GetMethod("GetZYRoll", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly MethodInfo s_GetAxisLengthFn = typeof(Avatar).GetMethod("GetAxisLength", BindingFlags.NonPublic | BindingFlags.Instance);

    public static Quaternion GetZyPostQ(this Avatar a, int humanId, Quaternion parentQ, Quaternion q)
    {
		return (Quaternion)s_GetZyPostQFn.Invoke(a, new object[] {humanId, parentQ, q});
    }

    public static Quaternion GetZyRoll(this Avatar a, int humanId, Vector3 uvw)
    {
		return (Quaternion)s_GetZyRollFn.Invoke(a, new object[] {humanId, uvw});
    }

    public static float GetAxisLength(this Avatar a, int humanId)
    {
		return (float)s_GetAxisLengthFn.Invoke(a, new object[] {humanId});
    }

    public static Quaternion GetPreRotation(this Avatar a, int humanId)
    {
		return (Quaternion)s_GetPreRotationFn.Invoke(a, new object[] {humanId});
    }

    public static Quaternion GetPostRotation(this Avatar a, int humanId)
    {
		return (Quaternion)s_GetPostRotationFn.Invoke(a, new object[] {humanId});
    }

    public static Vector3 GetLimitSign(this Avatar a, int humanId)
    {
		return (Vector3)s_GetLimitSignFn.Invoke(a, new object[] {humanId});
    }

	public static string GetRootMotionNodeName(this Avatar a)
	{
		if (a == null) return "";

		var fi = typeof(HumanDescription).GetField("m_RootMotionBoneName", BindingFlags.NonPublic | BindingFlags.Instance);
		return fi == null ? "" : (string)fi.GetValue(a.humanDescription);
	}
}
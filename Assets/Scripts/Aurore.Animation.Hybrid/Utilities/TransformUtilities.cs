using UnityEngine;

public static class TransformUtilities
{
    public static Transform FindChildRecursively(Transform root, string name)
    {
        var rv = root.Find(name);
        if (rv != null) return rv;

        var childCount = root.childCount;
        for (var i = 0; i < childCount; ++i)
        {
            var c = root.GetChild(i);
            var crv = FindChildRecursively(c, name);
            if (crv != null) return crv;
        }
        return null;
    }
}
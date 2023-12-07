using System;
using System.Collections.Generic;
using System.Linq;

public static class ReflectionUtility
{
    /// <summary> Searches all assemblies to find all types that implement a type. </summary>
    /// <typeparam name="T"> The base type that is inherited from. </typeparam>
    /// <returns> All the types. </returns>
    public static IEnumerable<Type> GetAllImplementations<T>() where T : class
    {
        var type = typeof(T);

        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(t => t != type)
            .Where(t => !t.IsInterface && !t.IsAbstract)
            .Where(t => type.IsAssignableFrom(t));
    }
}
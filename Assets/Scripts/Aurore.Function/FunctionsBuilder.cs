using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

/// <summary> The builder for creating <see cref="Functions{T}"/>. </summary>
/// <typeparam name="T"> Is the void* data that will be passed to the ExecuteFunction. Also serves as a grouping mechanism for ReflectAll. </typeparam>
public unsafe struct FunctionsBuilder<T> : IDisposable where T : unmanaged
{
    private static Dictionary<Type, List<MethodInfo>> s_CachedReflectAll = new();

    private NativeHashSet<BuildData> m_Functions;

    /// <summary> Initializes a new instance of the <see cref="FunctionsBuilder{T}"/> struct. </summary>
    /// <param name="allocator"> The allocator to use for the builder. This should nearly always be <see cref="Allocator.Temp"/>. </param>
    public FunctionsBuilder(Allocator allocator)
    {
        this.m_Functions = new NativeHashSet<BuildData>(0, allocator);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.m_Functions.Dispose();
    }

    /// <summary> Find all implementations of <see cref="IFunction{T}"/>. </summary>
    /// <param name="state"> The system state passed to OnCreate. </param>
    /// <returns> Itself. </returns>
    public FunctionsBuilder<T> ReflectAll(ref SystemState state)
    {
        if (!s_CachedReflectAll.TryGetValue(typeof(T), out var cachedData))
        {
            s_CachedReflectAll[typeof(T)] = cachedData = new List<MethodInfo>();

            var baseMethod = typeof(FunctionsBuilder<T>).GetMethod(nameof(this.AddInternalDefault), BindingFlags.Instance | BindingFlags.NonPublic)!;

            var implementations = ReflectionUtility.GetAllImplementations<IFunction<T>>();
            foreach (var type in implementations)
            {
                if (!UnsafeUtility.IsUnmanaged(type)) continue;

                var genericMethod = baseMethod.MakeGenericMethod(type);
                cachedData.Add(genericMethod);
            }
        }

        fixed (void* ptr = &state)
        {
            foreach (var genericMethod in cachedData)
            {
                genericMethod.Invoke(this, new object[] { (IntPtr)ptr });
            }
        }

        return this;
    }

    /// <summary> Manually add an instance of <see cref="IFunction{T}"/>. </summary>
    /// <param name="state"> The system state passed to OnCreate. </param>
    /// <param name="function"> The instance </param>
    /// <typeparam name="TF"> The type of <see cref="IFunction{T}"/>. </typeparam>
    /// <returns> Itself. </returns>
    public FunctionsBuilder<T> Add<TF>(ref SystemState state, TF function) where TF : unmanaged, IFunction<T>
    {
        var hash = BurstRuntime.GetHashCode64<TF>();

        var buildData = new BuildData { Hash = hash };

        if (this.m_Functions.Contains(buildData))
        {
            return this;
        }

        var pinned = (TF*)UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<TF>(), UnsafeUtility.AlignOf<TF>(), Allocator.Persistent, 0);
        *pinned = function;
        pinned->OnCreate(ref state);

        var executeFunction = BurstCompiler.CompileFunctionPointer(pinned->ExecuteFunction);
        var updateFunction = default(FunctionPointer<UpdateFunction>);
        var destroyFunction = IntPtr.Zero;

        if (pinned->UpdateFunction != null)
        {
            updateFunction = BurstCompiler.CompileFunctionPointer(pinned->UpdateFunction!);
        }

        if (pinned->DestroyFunction != null)
        {
            destroyFunction = Marshal.GetFunctionPointerForDelegate(pinned->DestroyFunction);
        }

        buildData.FunctionData = new FunctionData
        {
            Target = pinned,
            DestroyFunction = destroyFunction,
            ExecuteFunction = executeFunction,
            UpdateFunction = updateFunction,
        };

        var result = this.m_Functions.Add(buildData);
        Assert.IsTrue(result);

        return this;
    }

    /// <summary> Manually create an instance of <see cref="IFunction{T}"/>. </summary>
    /// <param name="state"> The system state passed to OnCreate. </param>
    /// <typeparam name="TF"> The type of <see cref="IFunction{T}"/> to create. </typeparam>
    /// <returns> Itself. </returns>
    public FunctionsBuilder<T> Add<TF>(ref SystemState state) where TF : unmanaged, IFunction<T>
    {
        fixed (SystemState* ptr = &state)
        {
            return this.AddInternalDefault<TF>(ptr);
        }
    }

    /// <summary> Builds the <see cref="Functions{T}"/> to use with all the found <see cref="IFunction{T}"/>. </summary>
    /// <returns> A new instance of <see cref="Functions{T}"/>. </returns>
    public Functions<T> Build()
    {
        var array = new NativeArray<FunctionData>(this.m_Functions.Count, Allocator.Persistent);

        using var e = this.m_Functions.GetEnumerator();
        var index = 0;

        while (e.MoveNext())
        {
            array[index++] = e.Current.FunctionData;
        }

        return new Functions<T>(array);
    }

    private FunctionsBuilder<T> AddInternalDefault<TF>(SystemState* state) where TF : unmanaged, IFunction<T>
    {
        return this.Add<TF>(ref *state, default);
    }

    private struct BuildData : IEquatable<BuildData>
    {
        public long Hash;
        public FunctionData FunctionData;

        public bool Equals(BuildData other)
        {
            return this.Hash == other.Hash;
        }

        public override int GetHashCode()
        {
            return this.Hash.GetHashCode();
        }
    }
}

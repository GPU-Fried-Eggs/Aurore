using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using static AnimatorControllerJob;

[assembly: RegisterGenericSystemType(typeof(AnimatorControllerSystem<PredictedAnimatorControllerQuery>))]
[assembly: RegisterGenericSystemType(typeof(AnimatorControllerSystem<AnimatorControllerQuery>))]

[DisableAutoCreation]
[RequireMatchingQueriesForUpdate]
public partial struct AnimatorControllerSystem<T>: ISystem where T: IAnimatorControllerQueryCreator, new()
{
	private EntityQuery m_AnimatorControllerQuery;

	[BurstCompile]
	public void OnCreate(ref SystemState ss)
	{
		var queryCreator = new T();
		m_AnimatorControllerQuery = queryCreator.CreateQuery(ref ss);
	}

	[BurstCompile]
	public void OnUpdate(ref SystemState ss)
	{
		var dt = SystemAPI.Time.DeltaTime;
		var frameCount = Time.frameCount;

#if AURORE_DEBUG
		SystemAPI.TryGetSingleton<DebugConfigurationComponent>(out var dc);
#endif

		var controllerLayersBufferHandle = SystemAPI.GetBufferTypeHandle<AnimatorControllerLayerComponent>();
		var controllerParametersBufferHandle = SystemAPI.GetBufferTypeHandle<AnimatorControllerParameterComponent>();
		var stateMachineProcessJob = new StateMachineProcessJob()
		{
			ControllerLayersBufferHandle = controllerLayersBufferHandle,
			ControllerParametersBufferHandle = controllerParametersBufferHandle,
			DeltaTime = dt,
			FrameIndex = frameCount,
#if AURORE_DEBUG
			DoLogging = dc.logAnimatorControllerProcesses,
#endif
		};

		ss.Dependency = stateMachineProcessJob.ScheduleParallel(m_AnimatorControllerQuery, ss.Dependency);
	}
}

public interface IAnimatorControllerQueryCreator
{
	EntityQuery CreateQuery(ref SystemState ss);
}

public struct PredictedAnimatorControllerQuery: IAnimatorControllerQueryCreator
{
	public EntityQuery CreateQuery(ref SystemState ss)
	{
		var builder = new EntityQueryBuilder(Allocator.Temp)
			.WithAllRW<AnimatorControllerLayerComponent>();
		var animatorControllerQuery = ss.GetEntityQuery(builder);
		return animatorControllerQuery;
	}
}

public struct AnimatorControllerQuery: IAnimatorControllerQueryCreator
{
	public EntityQuery CreateQuery(ref SystemState ss)
	{
		var builder = new EntityQueryBuilder(Allocator.Temp)
			.WithAllRW<AnimatorControllerLayerComponent>();
		var animatorControllerQuery = ss.GetEntityQuery(builder);
		return animatorControllerQuery;
	}
}
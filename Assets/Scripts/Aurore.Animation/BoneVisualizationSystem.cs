using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

[DisableAutoCreation]
public partial class BoneVisualizationSystem: SystemBase
{
	private EntityQuery m_BoneVisualizeQuery;

	private struct BoneGPUData
	{
		public float3 Pos0, Pos1;
		public float4 ColorTri, ColorLines;
	}

	private ComputeBuffer m_BoneGPUDataCb;
	private NativeList<BoneGPUData> m_BoneGPUData;
	private Mesh m_BoneMesh;
	private Material m_BoneRendererMaterial;
	private Bounds m_BigBBox = new Bounds(Vector3.zero, Vector3.one * 100000000);
	private MaterialPropertyBlock m_Mpb;

	private int m_BoneDataBufferShaderID;
	private int m_IsLinesShaderID;

	private void CreateBoneMeshes()
	{
		m_BoneMesh = new Mesh();
		m_BoneMesh.subMeshCount = 2;

		var vtx = new Vector3[6];
		vtx[0] = new Vector3(0, 1, 0);
		vtx[5] = new Vector3(0, -1, 0);
		vtx[1] = new Vector3(-1, 0, 0);
		vtx[2] = new Vector3(1, 0, 0);
		vtx[3] = new Vector3(0, 0, -1);
		vtx[4] = new Vector3(0, 0, 1);

		for (var i = 0; i < vtx.Length; ++i)
			vtx[i] *= 0.1f;

		var triIdx = new int[]
		{
			0, 1, 4,
			0, 4, 2,
			0, 2, 3,
			0, 3, 1,

			5, 4, 1,
			5, 2, 4,
			5, 3, 2,
			5, 1, 3,
		};

		var lineIdx = new int[]
		{
			0, 1,
			0, 2, 
			0, 3,
			0, 4,
			5, 1,
			5, 2, 
			5, 3,
			5, 4,
			2, 4,
			1, 4,
			1, 3,
			2, 3,
		};

		m_BoneMesh.SetVertices(vtx);
		m_BoneMesh.SetIndices(triIdx, MeshTopology.Triangles, 0);
		m_BoneMesh.SetIndices(lineIdx, MeshTopology.Lines, 1);
	}

	private Material CreateBoneRendererMaterial()
	{
		return new Material(Shader.Find("BoneRenderer"))
		{
			enableInstancing = true
		};
	}

	protected override void OnCreate()
	{
		CreateBoneMeshes();
		m_BoneRendererMaterial = CreateBoneRendererMaterial();
		m_Mpb = new MaterialPropertyBlock();

		m_IsLinesShaderID = Shader.PropertyToID("isLines");
		m_BoneDataBufferShaderID = Shader.PropertyToID("boneDataBuf");

		var ecb0 = new EntityQueryBuilder(Allocator.Temp)
			.WithAll<RigDefinitionComponent,
#if !AURORE_DEBUG
			BoneVisualizationComponent,
#endif
			LocalTransform>();
		m_BoneVisualizeQuery = GetEntityQuery(ecb0);

		m_BoneGPUData = new NativeList<BoneGPUData>(Allocator.Persistent);

		RequireForUpdate(m_BoneVisualizeQuery);
	}

	protected override void OnDestroy()
	{
		m_BoneGPUDataCb?.Release();
	}

	private JobHandle PrepareGPUDataBuf(NativeList<BoneTransform> bonesBuffer, JobHandle dependsOn)
	{
		var resizeDataJob = new ResizeDataBuffersJob
		{
			BoneTransforms = bonesBuffer,
			BoneGPUData = m_BoneGPUData
		};

		return resizeDataJob.Schedule(dependsOn);
	}

	private void RenderBones()
	{
		if (m_BoneGPUData.IsEmpty) return;

		if (m_BoneGPUDataCb == null || m_BoneGPUDataCb.count < m_BoneGPUData.Length)
		{
			m_BoneGPUDataCb?.Release();
			m_BoneGPUDataCb = new ComputeBuffer(m_BoneGPUData.Length, UnsafeUtility.SizeOf<BoneGPUData>());
		}
		m_BoneGPUDataCb.SetData(m_BoneGPUData.AsArray());

		m_BoneRendererMaterial.SetBuffer(m_BoneDataBufferShaderID, m_BoneGPUDataCb);
		m_Mpb.Clear();
		m_Mpb.SetInt(m_IsLinesShaderID, 0);
		Graphics.DrawMeshInstancedProcedural(m_BoneMesh, 0, m_BoneRendererMaterial, m_BigBBox, m_BoneGPUData.Length, m_Mpb, ShadowCastingMode.Off);
		m_Mpb.SetInt(m_IsLinesShaderID, 1);
		Graphics.DrawMeshInstancedProcedural(m_BoneMesh, 1, m_BoneRendererMaterial, m_BigBBox, m_BoneGPUData.Length, m_Mpb, ShadowCastingMode.Off);
	}

	protected override void OnUpdate()
	{
		var entityArr = m_BoneVisualizeQuery.ToEntityListAsync(WorldUpdateAllocator, Dependency, out var entityArrJh);
		var rigDefArr = m_BoneVisualizeQuery.ToComponentDataListAsync<RigDefinitionComponent>(WorldUpdateAllocator, Dependency, out var rigDefArrJh);

		var runtimeData = SystemAPI.GetSingleton<RuntimeAnimationData>();
		var prepareDataJH = PrepareGPUDataBuf(runtimeData.AnimatedBonesBuffer, Dependency);

		var combinedJh = JobHandle.CombineDependencies(entityArrJh, rigDefArrJh, prepareDataJH);

#if AURORE_DEBUG
		SystemAPI.TryGetSingleton<DebugConfigurationComponent>(out var dcc);
#else
		DebugConfigurationComponent dcc = default;
#endif

		var boneVisualizeComponentLookup = SystemAPI.GetComponentLookup<BoneVisualizationComponent>(true);
		var prepareRenderDataJob = new PrepareRenderDataJob
		{
			BoneGPUData = m_BoneGPUData.AsParallelWriter(),
			EntityToDataOffsetMap = runtimeData.EntityToDataOffsetMap,
			BonePoses = runtimeData.AnimatedBonesBuffer,
			RigDefArr = rigDefArr,
			BoneVisComponentLookup = boneVisualizeComponentLookup,
			DebugConfig = dcc,
			EntityArr = entityArr
		};

		var jh = prepareRenderDataJob.Schedule(entityArr, 16, combinedJh);
		jh.Complete();

		RenderBones();
	}
	
	[BurstCompile]
	private struct PrepareRenderDataJob: IJobParallelForDefer
	{
		[ReadOnly] public NativeList<Entity> EntityArr;
		[ReadOnly] public NativeList<RigDefinitionComponent> RigDefArr;
		[ReadOnly] public NativeList<BoneTransform> BonePoses;
		[ReadOnly] public NativeParallelHashMap<Entity, int2> EntityToDataOffsetMap;
		[ReadOnly] public ComponentLookup<BoneVisualizationComponent> BoneVisComponentLookup;
		public DebugConfigurationComponent DebugConfig;

		[WriteOnly] public NativeList<BoneGPUData>.ParallelWriter BoneGPUData;

		public void Execute(int i)
		{
			var rd = RigDefArr[i];
			var e = EntityArr[i];
			var bt = RuntimeAnimationData.GetAnimationDataForRigRO(BonePoses, EntityToDataOffsetMap, rd, e);

			if (!BoneVisComponentLookup.TryGetComponent(e, out var bvc))
			{
				if (!DebugConfig.VisualizeAllRigs) return;

				bvc = new BoneVisualizationComponent
				{
					ColorLines = DebugConfig.ColorLines,
					ColorTri = DebugConfig.ColorTri
				};
			}

			var len = bt.Length;
        
			for (var l = rd.RigBlob.Value.RootBoneIndex; l < len; ++l)
			{
				var bgd = new BoneGPUData();
				ref var rb = ref rd.RigBlob.Value.Bones[l];

				if (rb.ParentBoneIndex < 0)
					continue;

				bgd.Pos0 = bt[l].Position;
				bgd.Pos1 = bt[rb.ParentBoneIndex].Position;
				bgd.ColorTri = bvc.ColorTri;
				bgd.ColorLines = bvc.ColorLines;

				if (math.any(math.abs(bgd.Pos0 - bgd.Pos1)))
					BoneGPUData.AddNoResize(bgd);
			}
		}
	}

	private struct ResizeDataBuffersJob: IJob
	{
		public NativeList<BoneGPUData> BoneGPUData;
		public NativeList<BoneTransform> BoneTransforms;

		public void Execute()
		{
			var totalBoneCount = BoneTransforms.Length;

			if (BoneGPUData.Capacity < totalBoneCount)
			{
				BoneGPUData.Capacity = totalBoneCount;
			}

			BoneGPUData.Clear();
		}
	}
}
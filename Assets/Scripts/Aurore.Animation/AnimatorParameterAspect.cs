using Unity.Entities;
using FixedStringName = Unity.Collections.FixedString512Bytes;

public readonly partial struct AnimatorParametersAspect: IAspect
{
	[Optional]
	readonly RefRO<AnimatorControllerParameterIndexTableComponent> m_IndexTable;
	readonly DynamicBuffer<AnimatorControllerParameterComponent> m_ParametersArr;

	public float GetFloatParameter(FastAnimatorParameter fp) => GetParameterValue(fp).floatValue;
	public int GetIntParameter(FastAnimatorParameter fp) => GetParameterValue(fp).intValue;
	public bool GetBoolParameter(FastAnimatorParameter fp) => GetParameterValue(fp).boolValue;
	public float GetFloatParameter(uint h) => GetParameterValue(h).floatValue;
	public int GetIntParameter(uint h) => GetParameterValue(h).intValue;
	public bool GetBoolParameter(uint h) => GetParameterValue(h).boolValue;
	public float GetFloatParameter(FixedStringName n) => GetParameterValue(n).floatValue;
	public int GetIntParameter(FixedStringName n) => GetParameterValue(n).intValue;
	public bool GetBoolParameter(FixedStringName n) => GetParameterValue(n).boolValue;

	public ParameterValue GetParameterValue(FastAnimatorParameter fp)
	{
		ParameterValue rv;
		if (m_IndexTable.IsValid)
			fp.GetRuntimeParameterData(m_IndexTable.ValueRO.SeedTable, m_ParametersArr, out rv);
		else
			fp.GetRuntimeParameterData(m_ParametersArr, out rv);
		return rv;
	}

	public ParameterValue GetParameterValue(uint parameterHash)
	{
		var fp = new FastAnimatorParameter()
		{
			Hash = parameterHash,
			ParamName = default,
		};
		return GetParameterValue(fp);
	}

	public ParameterValue GetParameterValue(FixedStringName parameterName)
	{
		var fp = new FastAnimatorParameter(parameterName);
		return GetParameterValue(fp);
	}

	public void SetParameterValue(FastAnimatorParameter fp, ParameterValue value)
	{
		if (m_IndexTable.IsValid)
			fp.SetRuntimeParameterData(m_IndexTable.ValueRO.SeedTable, m_ParametersArr, value);
		else
			fp.SetRuntimeParameterData(m_ParametersArr, value);
	}

	public void SetTrigger(FastAnimatorParameter fp)
	{
		SetParameterValue(fp, new ParameterValue() { boolValue = true });
	}

	public void SetParameterValueByIndex(int paramIndex, ParameterValue value)
	{
		m_ParametersArr.ElementAt(paramIndex).Value = value;
	}

	public void SetParameterValue(uint parameterHash, ParameterValue value)
	{
		var fp = new FastAnimatorParameter()
		{
			Hash = parameterHash,
			ParamName = default,
		};
		SetParameterValue(fp, value);
	}

	public void SetParameterValue(FixedStringName parameterName, ParameterValue value)
	{
		var fp = new FastAnimatorParameter(parameterName);
		SetParameterValue(fp, value);
	}

	public int GetParameterIndex(FastAnimatorParameter fp)
	{
		return m_IndexTable.IsValid
			? fp.GetRuntimeParameterIndex(m_IndexTable.ValueRO.SeedTable, m_ParametersArr)
			: fp.GetRuntimeParameterIndex(m_ParametersArr);
	}
	public bool HasParameter(FastAnimatorParameter fp)
	{
		return GetParameterIndex(fp) != -1;
	}

	public bool HasParameter(uint parameterHash)
	{
		var fp = new FastAnimatorParameter()
		{
			Hash = parameterHash,
			ParamName = default,
		};
		return HasParameter(fp);
	}

	public bool HasParameter(FixedStringName parameterName)
	{
		var fp = new FastAnimatorParameter(parameterName);
		return HasParameter(fp);
	}
	
	public int ParametersCount() => m_ParametersArr.Length;
}
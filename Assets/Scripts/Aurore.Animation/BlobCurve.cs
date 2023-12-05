using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

public static class BlobCurve
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float EvaluateBezierCurve(KeyFrame f0, KeyFrame f1, float l)
	{
		var dt = f1.Time - f0.Time;
		var m0 = f0.OutTan * dt;
		var m1 = f1.InTan * dt;

		var t2 = l * l;
		var t3 = t2 * l;

		var a = 2 * t3 - 3 * t2 + 1;
		var b = t3 - 2 * t2 + l;
		var c = t3 - t2;
		var d = -2 * t3 + 3 * t2;

		var rv = a * f0.V + b * m0 + c * m1 + d * f1.V;
		return rv;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe float SampleAnimationCurve(ref BlobArray<KeyFrame> kf, float time)
	{
		var arr = new ReadOnlySpan<KeyFrame>(kf.GetUnsafePtr(), kf.Length);

		return SampleAnimationCurveBinarySearch(arr, time);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe float SampleAnimationCurve(in UnsafeList<KeyFrame> kf, float time)
	{
		var arr = new ReadOnlySpan<KeyFrame>(kf.Ptr, kf.Length);

		return SampleAnimationCurveBinarySearch(arr, time);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float SampleAnimationCurveBinarySearch(in ReadOnlySpan<KeyFrame> kf, float time)
	{
		var startIndex = 0;
		var endIndex = kf.Length;
		var less = true;
		var greater = true;
		KeyFrame frame0 = default, frame1 = default;

		if (kf.Length < 3) return SampleAnimationCurveLinearSearch(kf, time);

		while(endIndex - startIndex >= 1 && (less || greater) && endIndex > 1)
		{
			var middleIndex = (endIndex + startIndex) / 2;
			frame1 = kf[middleIndex];
			frame0 = kf[middleIndex - 1];
			
			less = time < frame0.Time;
			greater = time > frame1.Time;

			startIndex = math.select(startIndex, middleIndex + 1, greater);
			endIndex = math.select(endIndex, middleIndex, less);
		}

		if (less) return kf[0].V;

		if (greater) return kf[^1].V;

		var f = (time - frame0.Time) / (frame1.Time - frame0.Time);
		return EvaluateBezierCurve(frame0, frame1, f);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float SampleAnimationCurveLinearSearch(in ReadOnlySpan<KeyFrame> kf, float time)
	{
		for (var i = 0; i < kf.Length; ++i)
		{
			var frame1 = kf[i];
			if (frame1.Time >= time)
			{
				if (i == 0) return kf[i].V;
				var frame0 = kf[i - 1];

				var f = (time - frame0.Time) / (frame1.Time - frame0.Time);
				return EvaluateBezierCurve(frame0, frame1, f);
			}
		}
		return kf[^1].V;
	}
}
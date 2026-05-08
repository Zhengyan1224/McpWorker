using System;
using System.Collections.Generic;
using System.Numerics;

namespace Zhengyan.HNSW;

public static class VectorUtils
{
	public static float Magnitude(IList<float> vector)
	{
		float num = 0f;
		for (int i = 0; i < vector.Count; i++)
		{
			num += vector[i] * vector[i];
		}
		return (float)Math.Sqrt(num);
	}

	public static void Normalize(IList<float> vector)
	{
		float num = 1f / Magnitude(vector);
		for (int i = 0; i < vector.Count; i++)
		{
			vector[i] *= num;
		}
	}

	public static float MagnitudeSIMD(float[] vector)
	{
		if (!Vector.IsHardwareAccelerated)
		{
			throw new NotSupportedException("NormalizeSIMD is not supported");
		}
		float num = 0f;
		int count = Vector<float>.Count;
		int num2 = vector.Length - count;
		int i;
		for (i = 0; i <= num2; i += Vector<float>.Count)
		{
			Vector<float> vector2 = new Vector<float>(vector, i);
			num += Vector.Dot(vector2, vector2);
		}
		for (; i < vector.Length; i++)
		{
			num += vector[i] * vector[i];
		}
		return (float)Math.Sqrt(num);
	}

	public static void NormalizeSIMD(float[] vector)
	{
		if (!Vector.IsHardwareAccelerated)
		{
			throw new NotSupportedException("NormalizeSIMD is not supported");
		}
		float num = 1f / MagnitudeSIMD(vector);
		int count = Vector<float>.Count;
		int num2 = vector.Length - count;
		int i;
		for (i = 0; i <= num2; i += count)
		{
			Vector<float> right = new Vector<float>(vector, i);
			Vector.Multiply(num, right).CopyTo(vector, i);
		}
		for (; i < vector.Length; i++)
		{
			vector[i] *= num;
		}
	}
}


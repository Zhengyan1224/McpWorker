using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Zhengyan.HNSW;

public static class CosineDistance
{
	private static readonly int _vs1 = Vector<float>.Count;

	private static readonly int _vs2 = 2 * Vector<float>.Count;

	private static readonly int _vs3 = 3 * Vector<float>.Count;

	private static readonly int _vs4 = 4 * Vector<float>.Count;

	public static float NonOptimized(float[] u, float[] v)
	{
		if (u.Length != v.Length)
		{
			throw new ArgumentException("Vectors have non-matching dimensions");
		}
		float num = 0f;
		float num2 = 0f;
		float num3 = 0f;
		for (int i = 0; i < u.Length; i++)
		{
			num += u[i] * v[i];
			num2 += u[i] * u[i];
			num3 += v[i] * v[i];
		}
		float num4 = num / (float)(Math.Sqrt(num2) * Math.Sqrt(num3));
		return 1f - num4;
	}

	public static float ForUnits(float[] u, float[] v)
	{
		if (u.Length != v.Length)
		{
			throw new ArgumentException("Vectors have non-matching dimensions");
		}
		float num = 0f;
		for (int i = 0; i < u.Length; i++)
		{
			num += u[i] * v[i];
		}
		return 1f - num;
	}

	public static float SIMD(float[] u, float[] v)
	{
		if (!Vector.IsHardwareAccelerated)
		{
			throw new NotSupportedException("SIMD version of CosineDistance is not supported");
		}
		if (u.Length != v.Length)
		{
			throw new ArgumentException("Vectors have non-matching dimensions");
		}
		float num = 0f;
		Vector2 value = default(Vector2);
		int count = Vector<float>.Count;
		int num2 = u.Length - count;
		int i;
		for (i = 0; i <= num2; i += count)
		{
			Vector<float> vector = new Vector<float>(u, i);
			Vector<float> vector2 = new Vector<float>(v, i);
			num += Vector.Dot(vector, vector2);
			value.X += Vector.Dot(vector, vector);
			value.Y += Vector.Dot(vector2, vector2);
		}
		for (; i < u.Length; i++)
		{
			num += u[i] * v[i];
			value.X += u[i] * u[i];
			value.Y += v[i] * v[i];
		}
		value = Vector2.SquareRoot(value);
		float num3 = value.X * value.Y;
		if (num3 == 0f)
		{
			return 1f;
		}
		float num4 = num / num3;
		return 1f - num4;
	}

	public static float SIMDForUnits(float[] u, float[] v)
	{
		return 1f - DotProduct(ref u, ref v);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float DotProduct(ref float[] lhs, ref float[] rhs)
	{
		float num = 0f;
		int num2 = lhs.Length;
		int num3 = 0;
		while (num2 >= _vs4)
		{
			num += Vector.Dot(new Vector<float>(lhs, num3), new Vector<float>(rhs, num3));
			num += Vector.Dot(new Vector<float>(lhs, num3 + _vs1), new Vector<float>(rhs, num3 + _vs1));
			num += Vector.Dot(new Vector<float>(lhs, num3 + _vs2), new Vector<float>(rhs, num3 + _vs2));
			num += Vector.Dot(new Vector<float>(lhs, num3 + _vs3), new Vector<float>(rhs, num3 + _vs3));
			if (num2 == _vs4)
			{
				return num;
			}
			num2 -= _vs4;
			num3 += _vs4;
		}
		if (num2 >= _vs2)
		{
			num += Vector.Dot(new Vector<float>(lhs, num3), new Vector<float>(rhs, num3));
			num += Vector.Dot(new Vector<float>(lhs, num3 + _vs1), new Vector<float>(rhs, num3 + _vs1));
			if (num2 == _vs2)
			{
				return num;
			}
			num2 -= _vs2;
			num3 += _vs2;
		}
		if (num2 >= _vs1)
		{
			num += Vector.Dot(new Vector<float>(lhs, num3), new Vector<float>(rhs, num3));
			if (num2 == _vs1)
			{
				return num;
			}
			num2 -= _vs1;
			num3 += _vs1;
		}
		if (num2 > 0)
		{
			while (num2 > 0)
			{
				num += lhs[num3] * rhs[num3];
				num3++;
				num2--;
			}
		}
		return num;
	}
}


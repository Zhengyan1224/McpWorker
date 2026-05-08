using System;

namespace Zhengyan.HNSW;

public static class DistanceUtils
{
	public static bool LowerThan<TDistance>(TDistance x, TDistance y) where TDistance : IComparable<TDistance>
	{
		return x.CompareTo(y) < 0;
	}

	public static bool GreaterThan<TDistance>(TDistance x, TDistance y) where TDistance : IComparable<TDistance>
	{
		return x.CompareTo(y) > 0;
	}

	public static bool IsEqual<TDistance>(TDistance x, TDistance y) where TDistance : IComparable<TDistance>
	{
		return x.CompareTo(y) == 0;
	}
}


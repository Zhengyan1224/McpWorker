using System.Collections.Generic;

namespace Zhengyan.HNSW;

public static class ReverseComparerExtensions
{
	public static ReverseComparer<T> Reverse<T>(this IComparer<T> comparer)
	{
		return new ReverseComparer<T>(comparer);
	}
}


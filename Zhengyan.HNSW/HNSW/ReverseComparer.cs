using System.Collections.Generic;

namespace Zhengyan.HNSW;

public class ReverseComparer<T> : IComparer<T>
{
	private readonly IComparer<T> Comparer;

	public ReverseComparer(IComparer<T> comparer)
	{
		Comparer = comparer;
	}

	public int Compare(T x, T y)
	{
		return Comparer.Compare(y, x);
	}
}


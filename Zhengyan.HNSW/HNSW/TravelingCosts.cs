using System;
using System.Collections.Generic;

namespace Zhengyan.HNSW;

public class TravelingCosts<TItem, TDistance> : IComparer<TItem>
{
	private static readonly Comparer<TDistance> DistanceComparer = Comparer<TDistance>.Default;

	private readonly Func<TItem, TItem, TDistance> Distance;

	public TItem Destination { get; }

	public TravelingCosts(Func<TItem, TItem, TDistance> distance, TItem destination)
	{
		Distance = distance;
		Destination = destination;
	}

	public TDistance From(TItem departure)
	{
		return Distance(departure, Destination);
	}

	public int Compare(TItem x, TItem y)
	{
		TDistance x2 = From(x);
		TDistance y2 = From(y);
		return DistanceComparer.Compare(x2, y2);
	}
}


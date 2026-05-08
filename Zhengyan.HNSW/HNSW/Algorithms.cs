using System;
using System.Collections.Generic;
using System.Linq;

namespace Zhengyan.HNSW;

internal class Algorithms
{
	internal class Algorithm3<TItem, TDistance> : Algorithm<TItem, TDistance> where TDistance : struct, IComparable<TDistance>
	{
		public Algorithm3(Graph<TItem, TDistance>.Core graphCore)
			: base(graphCore)
		{
		}

		internal override List<int> SelectBestForConnecting(List<int> candidatesIds, TravelingCosts<int, TDistance> travelingCosts, int layer)
		{
			int m = GetM(layer);
			BinaryHeap<int> binaryHeap = new BinaryHeap<int>(candidatesIds, travelingCosts);
			while (binaryHeap.Buffer.Count > m)
			{
				binaryHeap.Pop();
			}
			return binaryHeap.Buffer;
		}
	}

	internal sealed class Algorithm4<TItem, TDistance> : Algorithm<TItem, TDistance> where TDistance : struct, IComparable<TDistance>
	{
		public Algorithm4(Graph<TItem, TDistance>.Core graphCore)
			: base(graphCore)
		{
		}

		internal override List<int> SelectBestForConnecting(List<int> candidatesIds, TravelingCosts<int, TDistance> travelingCosts, int layer)
		{
			IComparer<int> comparer = travelingCosts.Reverse();
			int m = GetM(layer);
			BinaryHeap<int> binaryHeap = new BinaryHeap<int>(new List<int>(m + 1), travelingCosts);
			BinaryHeap<int> binaryHeap2 = new BinaryHeap<int>(candidatesIds, comparer);
			if (GraphCore.Parameters.ExpandBestSelection)
			{
				HashSet<int> hashSet = new HashSet<int>(binaryHeap2.Buffer);
				HashSet<int> hashSet2 = new HashSet<int>();
				foreach (int item in binaryHeap2.Buffer)
				{
					foreach (int item2 in GraphCore.Nodes[item][layer])
					{
						if (!hashSet.Contains(item2))
						{
							hashSet2.Add(item2);
							hashSet.Add(item2);
						}
					}
				}
				foreach (int item3 in hashSet2)
				{
					binaryHeap2.Push(item3);
				}
			}
			BinaryHeap<int> binaryHeap3 = new BinaryHeap<int>(new List<int>(binaryHeap2.Buffer.Count), comparer);
			while (binaryHeap2.Buffer.Any() && binaryHeap.Buffer.Count < m)
			{
				int num = binaryHeap2.Pop();
				int departure = binaryHeap.Buffer.FirstOrDefault();
				if (!binaryHeap.Buffer.Any() || DistanceUtils.LowerThan(travelingCosts.From(num), travelingCosts.From(departure)))
				{
					binaryHeap.Push(num);
				}
				else if (GraphCore.Parameters.KeepPrunedConnections)
				{
					binaryHeap3.Push(num);
				}
			}
			if (GraphCore.Parameters.KeepPrunedConnections)
			{
				while (binaryHeap3.Buffer.Any() && binaryHeap.Buffer.Count < m)
				{
					binaryHeap.Push(binaryHeap3.Pop());
				}
			}
			return binaryHeap.Buffer;
		}
	}

	internal abstract class Algorithm<TItem, TDistance> where TDistance : struct, IComparable<TDistance>
	{
		protected readonly Graph<TItem, TDistance>.Core GraphCore;

		protected readonly Func<int, int, TDistance> NodeDistance;

		public Algorithm(Graph<TItem, TDistance>.Core graphCore)
		{
			GraphCore = graphCore;
			NodeDistance = graphCore.GetDistance;
		}

		internal virtual Node NewNode(int nodeId, int maxLayer)
		{
			List<List<int>> list = new List<List<int>>(maxLayer + 1);
			for (int i = 0; i <= maxLayer; i++)
			{
				int capacity = GetM(i) + 1;
				list.Add(new List<int>(capacity));
			}
			return new Node
			{
				Id = nodeId,
				Connections = list
			};
		}

		internal abstract List<int> SelectBestForConnecting(List<int> candidatesIds, TravelingCosts<int, TDistance> travelingCosts, int layer);

		internal int GetM(int layer)
		{
			if (layer != 0)
			{
				return GraphCore.Parameters.M;
			}
			return 2 * GraphCore.Parameters.M;
		}

		internal void Connect(Node node, Node neighbour, int layer)
		{
			List<int> list = node[layer];
			list.Add(neighbour.Id);
			if (list.Count > GetM(layer))
			{
				TravelingCosts<int, TDistance> travelingCosts = new TravelingCosts<int, TDistance>(NodeDistance, node.Id);
				node[layer] = SelectBestForConnecting(list, travelingCosts, layer);
			}
		}
	}
}


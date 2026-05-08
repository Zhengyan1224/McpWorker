using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using MessagePack;

namespace Zhengyan.HNSW;

internal class Graph<TItem, TDistance> where TDistance : struct, IComparable<TDistance>
{
	internal class Core
	{
		private readonly Func<TItem, TItem, TDistance> Distance;

		private readonly DistanceCache<TDistance> DistanceCache;

		private long DistanceCalculationsCount;

		internal List<Node> Nodes { get; private set; }

		internal List<TItem> Items { get; private set; }

		internal Algorithms.Algorithm<TItem, TDistance> Algorithm { get; private set; }

		internal SmallWorld<TItem, TDistance>.Parameters Parameters { get; private set; }

		internal float DistanceCacheHitRate => (float)(DistanceCache?.HitCount ?? 0) / (float)DistanceCalculationsCount;

		internal Core(Func<TItem, TItem, TDistance> distance, SmallWorld<TItem, TDistance>.Parameters parameters)
		{
			Distance = distance;
			Parameters = parameters;
			int capacity = Math.Max(1024, parameters.InitialItemsSize);
			Nodes = new List<Node>(capacity);
			Items = new List<TItem>(capacity);
			switch (Parameters.NeighbourHeuristic)
			{
			case NeighbourSelectionHeuristic.SelectSimple:
				Algorithm = new Algorithms.Algorithm3<TItem, TDistance>(this);
				break;
			case NeighbourSelectionHeuristic.SelectHeuristic:
				Algorithm = new Algorithms.Algorithm4<TItem, TDistance>(this);
				break;
			}
			if (Parameters.EnableDistanceCacheForConstruction)
			{
				DistanceCache = new DistanceCache<TDistance>();
				DistanceCache.Resize(parameters.InitialDistanceCacheSize, overwrite: false);
			}
			DistanceCalculationsCount = 0L;
		}

		internal IReadOnlyList<int> AddItems(IReadOnlyList<TItem> items, IProvideRandomValues generator)
		{
			int count = items.Count;
			List<int> list = new List<int>();
			Items.AddRange(items);
			DistanceCache?.Resize(count, overwrite: false);
			int count2 = Nodes.Count;
			for (int i = 0; i < count; i++)
			{
				Nodes.Add(Algorithm.NewNode(count2 + i, RandomLayer(generator, Parameters.LevelLambda)));
				list.Add(count2 + i);
			}
			return list;
		}

		internal void ResizeDistanceCache(int newSize)
		{
			DistanceCache?.Resize(newSize, overwrite: true);
		}

		internal void Serialize(Stream stream)
		{
			MessagePackSerializer.Serialize(stream, Nodes);
		}

		internal void Deserialize(IReadOnlyList<TItem> items, Stream stream)
		{
			Nodes = MessagePackSerializer.Deserialize<List<Node>>(stream);
			Items.AddRange(items);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal TDistance GetDistance(int fromId, int toId)
		{
			DistanceCalculationsCount++;
			if (DistanceCache != null)
			{
				return DistanceCache.GetOrCacheValue(fromId, toId, GetDistanceSkipCache);
			}
			return Distance(Items[fromId], Items[toId]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private TDistance GetDistanceSkipCache(int fromId, int toId)
		{
			return Distance(Items[fromId], Items[toId]);
		}

		private static int RandomLayer(IProvideRandomValues generator, double lambda)
		{
			return (int)((0.0 - Math.Log(generator.NextFloat())) * lambda);
		}
	}

	internal struct Searcher(Core core)
	{
		private readonly Core Core = core;

		private readonly List<int> ExpansionBuffer = new List<int>();

		private readonly VisitedBitSet VisitedSet = new VisitedBitSet(core.Nodes.Count);

		internal int RunKnnAtLayer(int entryPointId, TravelingCosts<int, TDistance> targetCosts, List<int> resultList, int layer, int k, ref long version, long versionAtStart, Func<int, bool> keepResult)
		{
			IComparer<int> comparer = targetCosts.Reverse();
			BinaryHeap<int> binaryHeap = new BinaryHeap<int>(resultList, targetCosts);
			BinaryHeap<int> binaryHeap2 = new BinaryHeap<int>(ExpansionBuffer, comparer);
			if (keepResult(entryPointId))
			{
				binaryHeap.Push(entryPointId);
			}
			binaryHeap2.Push(entryPointId);
			VisitedSet.Add(entryPointId);
			try
			{
				int num = 1;
				while (binaryHeap2.Buffer.Count > 0)
				{
					GraphChangedException.ThrowIfChanged(ref version, versionAtStart);
					int num2 = binaryHeap2.Pop();
					int num3 = ((binaryHeap.Buffer.Count > 0) ? binaryHeap.Buffer[0] : (-1));
					if (num3 > 0 && DistanceUtils.GreaterThan(targetCosts.From(num2), targetCosts.From(num3)))
					{
						break;
					}
					List<int> list = Core.Nodes[num2][layer];
					for (int i = 0; i < list.Count; i++)
					{
						int num4 = list[i];
						if (VisitedSet.Contains(num4))
						{
							continue;
						}
						num3 = ((binaryHeap.Buffer.Count > 0) ? binaryHeap.Buffer[0] : (-1));
						if (binaryHeap.Buffer.Count < k || (num3 >= 0 && DistanceUtils.LowerThan(targetCosts.From(num4), targetCosts.From(num3))))
						{
							binaryHeap2.Push(num4);
							if (keepResult(num4))
							{
								binaryHeap.Push(num4);
							}
							if (binaryHeap.Buffer.Count > k)
							{
								binaryHeap.Pop();
							}
						}
						num++;
						VisitedSet.Add(num4);
					}
				}
				ExpansionBuffer.Clear();
				VisitedSet.Clear();
				return num;
			}
			catch (Exception)
			{
				GraphChangedException.ThrowIfChanged(ref version, versionAtStart);
				throw;
			}
		}
	}

	internal class VisitedBitSet
	{
		private int[] Buffer;

		internal VisitedBitSet(int nodesCount)
		{
			Buffer = new int[(nodesCount >> 5) + 1];
		}

		internal bool Contains(int nodeId)
		{
			int num = Buffer[nodeId >> 5];
			return ((1 << nodeId) & num) != 0;
		}

		internal void Add(int nodeId)
		{
			int num = 1 << (nodeId & 0x1F);
			Buffer[nodeId >> 5] |= num;
		}

		internal void Clear()
		{
			Array.Clear(Buffer, 0, Buffer.Length);
		}
	}

	private readonly Func<TItem, TItem, TDistance> Distance;

	internal Core GraphCore;

	private Node? EntryPoint;

	private long _version;

	internal SmallWorld<TItem, TDistance>.Parameters Parameters { get; }

	internal Graph(Func<TItem, TItem, TDistance> distance, SmallWorld<TItem, TDistance>.Parameters parameters)
	{
		Distance = distance;
		Parameters = parameters;
	}

	internal IReadOnlyList<int> AddItems(IReadOnlyList<TItem> items, IProvideRandomValues generator, IProgressReporter progressReporter)
	{
		if (items == null || !items.Any())
		{
			return Array.Empty<int>();
		}
		GraphCore = GraphCore ?? new Core(Distance, Parameters);
		int count = GraphCore.Items.Count;
		IReadOnlyList<int> result = GraphCore.AddItems(items, generator);
		Node node = EntryPoint ?? GraphCore.Nodes[0];
		Searcher searcher = new Searcher(GraphCore);
		Func<int, int, TDistance> distance = GraphCore.GetDistance;
		List<int> list = new List<int>(GraphCore.Algorithm.GetM(0) + 1);
		for (int i = count; i < GraphCore.Nodes.Count; i++)
		{
			long versionAtStart = Interlocked.Increment(ref _version);
			using (new ScopeLatencyTracker(EventSources.GraphBuildEventSource.Instance?.GraphInsertNodeLatencyReporter))
			{
				Node node2 = node;
				Node node3 = GraphCore.Nodes[i];
				TravelingCosts<int, TDistance> travelingCosts = new TravelingCosts<int, TDistance>(distance, i);
				for (int num = node2.MaxLayer; num > node3.MaxLayer; num--)
				{
					searcher.RunKnnAtLayer(node2.Id, travelingCosts, list, num, 1, ref _version, versionAtStart, (int _) => true);
					node2 = GraphCore.Nodes[list[0]];
					list.Clear();
				}
				for (int num2 = Math.Min(node3.MaxLayer, node.MaxLayer); num2 >= 0; num2--)
				{
					searcher.RunKnnAtLayer(node2.Id, travelingCosts, list, num2, Parameters.ConstructionPruning, ref _version, versionAtStart, (int _) => true);
					List<int> list2 = GraphCore.Algorithm.SelectBestForConnecting(list, travelingCosts, num2);
					for (int num3 = 0; num3 < list2.Count; num3++)
					{
						int num4 = list2[num3];
						versionAtStart = Interlocked.Increment(ref _version);
						GraphCore.Algorithm.Connect(node3, GraphCore.Nodes[num4], num2);
						versionAtStart = Interlocked.Increment(ref _version);
						GraphCore.Algorithm.Connect(GraphCore.Nodes[num4], node3, num2);
						if (DistanceUtils.LowerThan(travelingCosts.From(num4), travelingCosts.From(node2.Id)))
						{
							node2 = GraphCore.Nodes[num4];
						}
					}
					list.Clear();
				}
				if (node3.MaxLayer > node.MaxLayer)
				{
					node = node3;
				}
				EventSources.GraphBuildEventSource.Instance?.CoreGetDistanceCacheHitRateReporter?.Invoke(GraphCore.DistanceCacheHitRate);
			}
			progressReporter?.Progress(i - count, GraphCore.Nodes.Count - count);
		}
		EntryPoint = node;
		return result;
	}

	internal IList<SmallWorld<TItem, TDistance>.KNNSearchResult> KNearest(TItem destination, int k, Func<TItem, bool> filterItem = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		Node? entryPoint = EntryPoint;
		if (!entryPoint.HasValue)
		{
			return null;
		}
		Func<int, bool> keepResult = (int _) => true;
		if (filterItem != null)
		{
			Dictionary<int, bool> keepResults = new Dictionary<int, bool>();
			keepResult = delegate(int id)
			{
				if (keepResults.TryGetValue(id, out var value))
				{
					return value;
				}
				if (cancellationToken.IsCancellationRequested)
				{
					return false;
				}
				value = filterItem(GraphCore.Items[id]);
				keepResults[id] = value;
				return value;
			};
		}
		int num = 1024;
		while (true)
		{
			long num2 = Interlocked.Read(in _version);
			try
			{
				using (new ScopeLatencyTracker(EventSources.GraphSearchEventSource.Instance?.GraphKNearestLatencyReporter))
				{
					Node node = EntryPoint.Value;
					Searcher searcher = new Searcher(GraphCore);
					TravelingCosts<int, TDistance> targetCosts = new TravelingCosts<int, TDistance>(RuntimeDistance, -1);
					List<int> list = new List<int>(k + 1);
					int num3 = 0;
					for (int num4 = EntryPoint.Value.MaxLayer; num4 > 0; num4--)
					{
						num3 += searcher.RunKnnAtLayer(node.Id, targetCosts, list, num4, 1, ref _version, num2, keepResult);
						if (list.Count > 0)
						{
							node = GraphCore.Nodes[list[0]];
						}
						if (cancellationToken.IsCancellationRequested)
						{
							return list.Select((int id) => new SmallWorld<TItem, TDistance>.KNNSearchResult(id, GraphCore.Items[id], RuntimeDistance(id, -1))).ToList();
						}
						list.Clear();
					}
					num3 += searcher.RunKnnAtLayer(node.Id, targetCosts, list, 0, k, ref _version, num2, keepResult);
					EventSources.GraphSearchEventSource.Instance?.GraphKNearestVisitedNodesReporter?.Invoke(num3);
					return list.Select((int id) => new SmallWorld<TItem, TDistance>.KNNSearchResult(id, GraphCore.Items[id], RuntimeDistance(id, -1))).ToList();
				}
			}
			catch (GraphChangedException)
			{
				if (num > 0)
				{
					num--;
					continue;
				}
				throw;
			}
			catch (Exception)
			{
				if (num2 != Interlocked.Read(in _version) && num > 0)
				{
					num--;
					continue;
				}
				throw;
			}
		}
		TDistance RuntimeDistance(int x, int y)
		{
			int index = ((x >= 0) ? x : y);
			return Distance(destination, GraphCore.Items[index]);
		}
	}

	internal void Serialize(Stream stream)
	{
		GraphCore.Serialize(stream);
		MessagePackSerializer.Serialize(stream, EntryPoint);
	}

	internal void Deserialize(IReadOnlyList<TItem> items, Stream stream)
	{
		Core core = new Core(Distance, Parameters);
		core.Deserialize(items, stream);
		EntryPoint = MessagePackSerializer.Deserialize<Node>(stream);
		GraphCore = core;
	}

	internal string Print()
	{
		StringBuilder buffer = new StringBuilder();
		int layer = EntryPoint.Value.MaxLayer;
		while (layer >= 0)
		{
			StringBuilder stringBuilder = buffer;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(8, 1, stringBuilder);
			handler.AppendLiteral("[LEVEL ");
			handler.AppendFormatted(layer);
			handler.AppendLiteral("]");
			stringBuilder.AppendLine(ref handler);
			BFS(GraphCore, EntryPoint.Value, layer, delegate(Node node)
			{
				string value = string.Join(", ", node[layer]);
				StringBuilder stringBuilder2 = buffer;
				StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(8, 2, stringBuilder2);
				handler2.AppendLiteral("(");
				handler2.AppendFormatted(node.Id);
				handler2.AppendLiteral(") -> {");
				handler2.AppendFormatted(value);
				handler2.AppendLiteral("}");
				stringBuilder2.AppendLine(ref handler2);
			});
			buffer.AppendLine();
			int num = layer - 1;
			layer = num;
		}
		return buffer.ToString();
	}

	internal static void BFS(Core core, Node entryPoint, int layer, Action<Node> visitAction)
	{
		HashSet<int> hashSet = new HashSet<int>();
		Queue<int> queue = new Queue<int>(new int[1] { entryPoint.Id });
		while (queue.Any())
		{
			Node obj = core.Nodes[queue.Dequeue()];
			if (hashSet.Contains(obj.Id))
			{
				continue;
			}
			visitAction(obj);
			hashSet.Add(obj.Id);
			foreach (int item in obj[layer])
			{
				queue.Enqueue(item);
			}
		}
	}
}


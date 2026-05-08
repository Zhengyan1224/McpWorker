using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MessagePack;

namespace Zhengyan.HNSW;

public class SmallWorld<TItem, TDistance> where TDistance : struct, IComparable<TDistance>
{
	[MessagePackObject(true)]
	public class Parameters
	{
		public int M { get; set; }

		public double LevelLambda { get; set; }

		public NeighbourSelectionHeuristic NeighbourHeuristic { get; set; }

		public int ConstructionPruning { get; set; }

		public bool ExpandBestSelection { get; set; }

		public bool KeepPrunedConnections { get; set; }

		public bool EnableDistanceCacheForConstruction { get; set; }

		public int InitialDistanceCacheSize { get; set; }

		public int InitialItemsSize { get; set; }

		public Parameters()
		{
			M = 10;
			LevelLambda = 1.0 / Math.Log(M);
			NeighbourHeuristic = NeighbourSelectionHeuristic.SelectSimple;
			ConstructionPruning = 200;
			ExpandBestSelection = false;
			KeepPrunedConnections = false;
			EnableDistanceCacheForConstruction = true;
			InitialDistanceCacheSize = 1048576;
			InitialItemsSize = 1024;
		}
	}

	public class KNNSearchResult
	{
		public int Id { get; }

		public TItem Item { get; }

		public TDistance Distance { get; }

		internal KNNSearchResult(int id, TItem item, TDistance distance)
		{
			Id = id;
			Item = item;
			Distance = distance;
		}

		public override string ToString()
		{
			return $"I:{Id} Dist:{Distance:n2} [{Item}]";
		}
	}

	private const string SERIALIZATION_HEADER = "HNSW";

	private readonly Func<TItem, TItem, TDistance> Distance;

	private Graph<TItem, TDistance> Graph;

	private IProvideRandomValues Generator;

	private ReaderWriterLockSlim _rwLock;

	public IReadOnlyList<TItem> UnsafeItems => Graph?.GraphCore?.Items;

	public IReadOnlyList<TItem> Items
	{
		get
		{
			if (_rwLock != null)
			{
				_rwLock.EnterReadLock();
				try
				{
					return Graph.GraphCore.Items.ToList();
				}
				finally
				{
					_rwLock.ExitReadLock();
				}
			}
			return Graph?.GraphCore?.Items;
		}
	}

	public SmallWorld(Func<TItem, TItem, TDistance> distance, IProvideRandomValues generator, Parameters parameters, bool threadSafe = true)
	{
		Distance = distance;
		Graph = new Graph<TItem, TDistance>(Distance, parameters);
		Generator = generator;
		_rwLock = (threadSafe ? new ReaderWriterLockSlim() : null);
	}

	public IReadOnlyList<int> AddItems(IReadOnlyList<TItem> items, IProgressReporter progressReporter = null)
	{
		_rwLock?.EnterWriteLock();
		try
		{
			return Graph.AddItems(items, Generator, progressReporter);
		}
		finally
		{
			_rwLock?.ExitWriteLock();
		}
	}

	public IList<KNNSearchResult> KNNSearch(TItem item, int k, Func<TItem, bool> filterItem = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		_rwLock?.EnterReadLock();
		try
		{
			return Graph.KNearest(item, k, filterItem, cancellationToken);
		}
		finally
		{
			_rwLock?.ExitReadLock();
		}
	}

	public TItem GetItem(int index)
	{
		_rwLock?.EnterReadLock();
		try
		{
			return Items[index];
		}
		finally
		{
			_rwLock?.ExitReadLock();
		}
	}

	public void SerializeGraph(Stream stream)
	{
		if (Graph == null)
		{
			throw new InvalidOperationException("The graph does not exist");
		}
		_rwLock?.EnterReadLock();
		try
		{
			MessagePackSerializer.Serialize(stream, SERIALIZATION_HEADER);
			MessagePackSerializer.Serialize(stream, Graph.Parameters);
			Graph.Serialize(stream);
		}
		finally
		{
			_rwLock?.ExitReadLock();
		}
	}

	public static SmallWorld<TItem, TDistance> DeserializeGraph(IReadOnlyList<TItem> items, Func<TItem, TItem, TDistance> distance, IProvideRandomValues generator, Stream stream, bool threadSafe = true)
	{
		long position = stream.Position;
		string text;
		try
		{
			text = MessagePackSerializer.Deserialize<string>(stream);
		}
		catch (Exception innerException)
		{
			if (stream.CanSeek)
			{
				stream.Position = position;
			}
			throw new InvalidDataException("Invalid header found in stream, data is corrupted or invalid", innerException);
		}
		if (text != SERIALIZATION_HEADER)
		{
			if (stream.CanSeek)
			{
				stream.Position = position;
			}
			throw new InvalidDataException("Invalid header found in stream, data is corrupted or invalid");
		}
		Parameters parameters = MessagePackSerializer.Deserialize<Parameters>(stream);
		parameters.InitialDistanceCacheSize = 0;
		SmallWorld<TItem, TDistance> smallWorld = new SmallWorld<TItem, TDistance>(distance, generator, parameters, threadSafe);
		smallWorld.Graph.Deserialize(items, stream);
		return smallWorld;
	}

	public string Print()
	{
		return Graph.Print();
	}

	public void ResizeDistanceCache(int newSize)
	{
		Graph.GraphCore.ResizeDistanceCache(newSize);
	}
}


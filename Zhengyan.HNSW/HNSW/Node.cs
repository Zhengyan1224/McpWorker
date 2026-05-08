using System.Collections.Generic;
using MessagePack;

namespace Zhengyan.HNSW;

[MessagePackObject(false)]
public struct Node
{
	[Key(0)]
	public List<List<int>> Connections;

	[Key(1)]
	public int Id;

	[IgnoreMember]
	public int MaxLayer => Connections.Count - 1;

	public List<int> this[int layer]
	{
		get
		{
			return Connections[layer];
		}
		set
		{
			Connections[layer] = value;
		}
	}
}


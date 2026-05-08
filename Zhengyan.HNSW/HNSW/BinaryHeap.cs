using System;
using System.Collections.Generic;

namespace Zhengyan.HNSW;

internal struct BinaryHeap<T>
{
	internal IComparer<T> Comparer;

	internal List<T> Buffer;

	internal bool Any => Buffer.Count > 0;

	internal BinaryHeap(List<T> buffer)
		: this(buffer, Comparer<T>.Default)
	{
	}

	internal BinaryHeap(List<T> buffer, IComparer<T> comparer)
	{
		Buffer = buffer ?? throw new ArgumentNullException("buffer");
		Comparer = comparer;
		for (int i = 1; i < Buffer.Count; i++)
		{
			SiftUp(i);
		}
	}

	internal void Push(T item)
	{
		Buffer.Add(item);
		SiftUp(Buffer.Count - 1);
	}

	internal T Pop()
	{
		if (Buffer.Count > 0)
		{
			T result = Buffer[0];
			Buffer[0] = Buffer[Buffer.Count - 1];
			Buffer.RemoveAt(Buffer.Count - 1);
			SiftDown(0);
			return result;
		}
		throw new InvalidOperationException("Heap is empty");
	}

	private void SiftDown(int i)
	{
		while (i < Buffer.Count)
		{
			int num = (i << 1) + 1;
			int num2 = num + 1;
			if (num < Buffer.Count)
			{
				int num3 = ((num2 < Buffer.Count && Comparer.Compare(Buffer[num], Buffer[num2]) < 0) ? num2 : num);
				if (Comparer.Compare(Buffer[num3], Buffer[i]) > 0)
				{
					Swap(i, num3);
					i = num3;
					continue;
				}
				break;
			}
			break;
		}
	}

	private void SiftUp(int i)
	{
		while (i > 0)
		{
			int num = i - 1 >> 1;
			if (Comparer.Compare(Buffer[i], Buffer[num]) > 0)
			{
				Swap(i, num);
				i = num;
				continue;
			}
			break;
		}
	}

	private void Swap(int i, int j)
	{
		T value = Buffer[i];
		Buffer[i] = Buffer[j];
		Buffer[j] = value;
	}
}


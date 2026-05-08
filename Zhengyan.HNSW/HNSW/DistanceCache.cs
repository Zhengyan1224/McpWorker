using System;
using System.Runtime.CompilerServices;

namespace Zhengyan.HNSW;

internal class DistanceCache<TDistance> where TDistance : struct
{
	private TDistance[] _values;

	private long[] _keys;

	internal int HitCount;

	internal DistanceCache()
	{
	}

	internal void Resize(int pointsCount, bool overwrite)
	{
		if (pointsCount <= 0)
		{
			pointsCount = 1024;
		}
		long num = (long)pointsCount * (long)(pointsCount + 1) >> 1;
		num = ((num < DistanceCacheLimits.MaxArrayLength) ? num : DistanceCacheLimits.MaxArrayLength);
		if (_keys == null || num > _keys.Length || overwrite)
		{
			int start = 0;
			if (_keys == null || overwrite)
			{
				_keys = new long[(int)num];
				_values = new TDistance[(int)num];
			}
			else
			{
				start = _keys.Length;
				Array.Resize(ref _keys, (int)num);
				Array.Resize(ref _values, (int)num);
			}
			Span<long> span = _keys.AsSpan();
			span = span.Slice(start);
			span.Fill(-1L);
			Span<TDistance> span2 = _values.AsSpan();
			span2 = span2.Slice(start);
			span2.Fill(default(TDistance));
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal TDistance GetOrCacheValue(int fromId, int toId, Func<int, int, TDistance> getter)
	{
		long num = MakeKey(fromId, toId);
		int num2 = (int)(num & (_keys.Length - 1));
		if (_keys[num2] == num)
		{
			HitCount++;
			return _values[num2];
		}
		TDistance val = getter(fromId, toId);
		_keys[num2] = num;
		_values[num2] = val;
		return val;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void SetValue(int fromId, int toId, TDistance distance)
	{
		long num = MakeKey(fromId, toId);
		int num2 = (int)(num & (_keys.Length - 1));
		_keys[num2] = num;
		_values[num2] = distance;
	}

	private static long MakeKey(int fromId, int toId)
	{
		if (fromId <= toId)
		{
			return ((long)toId * (long)(toId + 1) >> 1) + fromId;
		}
		return ((long)fromId * (long)(fromId + 1) >> 1) + toId;
	}
}


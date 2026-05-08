using System;
using System.Runtime.CompilerServices;

namespace Zhengyan.HNSW;

internal static class ThreadSafeFastRandom
{
	private static readonly Random _global = new Random();

	[ThreadStatic]
	private static FastRandom _local;

	private static int GetGlobalSeed()
	{
		lock (_global)
		{
			return _global.Next();
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Next()
	{
		FastRandom fastRandom = _local;
		if (fastRandom == null)
		{
			fastRandom = (_local = new FastRandom(GetGlobalSeed()));
		}
		return fastRandom.Next();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Next(int maxValue)
	{
		FastRandom fastRandom = _local;
		if (fastRandom == null)
		{
			fastRandom = (_local = new FastRandom(GetGlobalSeed()));
		}
		int num;
		do
		{
			num = fastRandom.Next(maxValue);
		}
		while (num == maxValue);
		return num;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Next(int minValue, int maxValue)
	{
		FastRandom fastRandom = _local;
		if (fastRandom == null)
		{
			fastRandom = (_local = new FastRandom(GetGlobalSeed()));
		}
		return fastRandom.Next(minValue, maxValue);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float NextFloat()
	{
		FastRandom fastRandom = _local;
		if (fastRandom == null)
		{
			fastRandom = (_local = new FastRandom(GetGlobalSeed()));
		}
		return fastRandom.NextFloat();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void NextFloats(Span<float> buffer)
	{
		FastRandom fastRandom = _local;
		if (fastRandom == null)
		{
			fastRandom = (_local = new FastRandom(GetGlobalSeed()));
		}
		fastRandom.NextFloats(buffer);
	}
}


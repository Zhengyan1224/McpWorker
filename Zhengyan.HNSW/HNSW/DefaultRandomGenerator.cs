using System;
using System.Runtime.CompilerServices;

namespace Zhengyan.HNSW;

public sealed class DefaultRandomGenerator : IProvideRandomValues
{
	public static DefaultRandomGenerator Instance { get; } = new DefaultRandomGenerator(allowParallel: true);

	public static DefaultRandomGenerator DisableThreading { get; } = new DefaultRandomGenerator(allowParallel: false);

	public bool IsThreadSafe { get; }

	private DefaultRandomGenerator(bool allowParallel)
	{
		IsThreadSafe = allowParallel;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int Next(int minValue, int maxValue)
	{
		return ThreadSafeFastRandom.Next(minValue, maxValue);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public float NextFloat()
	{
		return ThreadSafeFastRandom.NextFloat();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void NextFloats(Span<float> buffer)
	{
		ThreadSafeFastRandom.NextFloats(buffer);
	}
}


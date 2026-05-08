using System;
using System.Runtime.CompilerServices;

namespace Zhengyan.HNSW;

public interface IProvideRandomValues
{
	bool IsThreadSafe { get; }

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	float NextFloat();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	void NextFloats(Span<float> buffer);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	int Next(int minValue, int maxValue);
}


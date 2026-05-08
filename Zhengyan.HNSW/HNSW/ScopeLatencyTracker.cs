using System;
using System.Diagnostics;

namespace Zhengyan.HNSW;

internal struct ScopeLatencyTracker(Action<float> callback) : IDisposable
{
	private long StartTimestamp = ((callback != null) ? Stopwatch.GetTimestamp() : 0);

	private Action<float> LatencyCallback = callback;

	public void Dispose()
	{
		if (LatencyCallback != null)
		{
			long num = (Stopwatch.GetTimestamp() - StartTimestamp) / 10;
			LatencyCallback(num);
		}
	}
}


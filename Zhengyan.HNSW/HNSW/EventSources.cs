using System;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;

namespace Zhengyan.HNSW;

public static class EventSources
{
	[EventSource(Name = "HNSW.Net.Graph.Build")]
	[ComVisible(false)]
	public class GraphBuildEventSource : EventSource
	{
		public static readonly GraphBuildEventSource Instance = new GraphBuildEventSource();

		internal Action<float> CoreGetDistanceCacheHitRateReporter { get; }

		internal Action<float> GraphInsertNodeLatencyReporter { get; }

		private GraphBuildEventSource()
			: base(EventSourceSettings.EtwSelfDescribingEventFormat)
		{
			GraphBuildEventSource source = this;
			EventCounter coreGetDistanceCacheHitRate = new EventCounter("GetDistance.CacheHitRate", this);
			CoreGetDistanceCacheHitRateReporter = delegate(float value)
			{
				WriteMetricIfEnabled(source, coreGetDistanceCacheHitRate, value);
			};
			EventCounter graphInsertNodeLatency = new EventCounter("InsertNode.Latency", this);
			GraphInsertNodeLatencyReporter = delegate(float value)
			{
				WriteMetricIfEnabled(source, graphInsertNodeLatency, value);
			};
		}
	}

	[EventSource(Name = "HNSW.Net.Graph.Search")]
	[ComVisible(false)]
	public class GraphSearchEventSource : EventSource
	{
		public static readonly GraphSearchEventSource Instance = new GraphSearchEventSource();

		internal Action<float> GraphKNearestLatencyReporter { get; }

		internal Action<float> GraphKNearestVisitedNodesReporter { get; }

		private GraphSearchEventSource()
			: base(EventSourceSettings.EtwSelfDescribingEventFormat)
		{
			GraphSearchEventSource source = this;
			EventCounter graphKNearestLatency = new EventCounter("KNearest.Latency", this);
			GraphKNearestLatencyReporter = delegate(float value)
			{
				WriteMetricIfEnabled(source, graphKNearestLatency, value);
			};
			EventCounter graphKNearestVisitedNodes = new EventCounter("KNearest.VisitedNodes", this);
			GraphKNearestVisitedNodesReporter = delegate(float value)
			{
				WriteMetricIfEnabled(source, graphKNearestVisitedNodes, value);
			};
		}
	}

	internal static void WriteMetricIfEnabled(EventSource source, EventCounter counter, float value)
	{
		if (source.IsEnabled())
		{
			counter.WriteMetric(value);
		}
	}
}


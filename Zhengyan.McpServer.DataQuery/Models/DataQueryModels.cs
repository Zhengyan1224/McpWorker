namespace Zhengyan.McpServer.DataQuery.Models;

public class DataQueryResult
{
    public string SourceFile { get; set; } = string.Empty;

    public int RowNumber { get; set; }

    public double Similarity { get; set; }

    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class DataQueryResponse
{
    public string Query { get; set; } = string.Empty;

    public int RequestedTopN { get; set; }

    public double MinSimilarity { get; set; }

    public int ReturnedCount { get; set; }

    public int CandidateCount { get; set; }

    public int TotalAvailableRecords { get; set; }

    public string SearchMode { get; set; } = string.Empty;

    public IReadOnlyList<string> FilteredSourceFiles { get; set; } = [];

    public IReadOnlyList<string> MatchedSourceFiles { get; set; } = [];

    public IReadOnlyList<DataQueryResult> Results { get; set; } = [];
}

public class DataSourceInfo
{
    public string SourceFile { get; set; } = string.Empty;

    public int RecordCount { get; set; }
}

public class DataQueryCacheManifest
{
    public List<DataFileSignature> DataFiles { get; set; } = [];

    public bool EmbeddingEnabled { get; set; }

    public string EmbeddingModel { get; set; } = string.Empty;

    public string EmbeddingEndpoint { get; set; } = string.Empty;

    public int MaxTextLengthPerRecord { get; set; }

    public int RecordCount { get; set; }
}

public class DataFileSignature
{
    public string Name { get; set; } = string.Empty;

    public long Length { get; set; }

    public long LastWriteTimeUtcTicks { get; set; }
}

public class CachedDataRecord
{
    public string CacheKey { get; set; } = string.Empty;

    public string SourceFile { get; set; } = string.Empty;

    public int RowNumber { get; set; }

    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string SearchText { get; set; } = string.Empty;
}

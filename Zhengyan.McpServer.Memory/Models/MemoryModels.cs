namespace Zhengyan.McpServer.Memory.Models;

public class MemoryRecord
{
    public string Id { get; set; } = string.Empty;

    public string Scope { get; set; } = "default";

    public string Content { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public double Importance { get; set; } = 0.5;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? LastAccessedAtUtc { get; set; }

    public int AccessCount { get; set; }
}

public class MemoryRecallResult
{
    public string Id { get; set; } = string.Empty;

    public string Scope { get; set; } = "default";

    public double Similarity { get; set; }

    public string Content { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public double Importance { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? LastAccessedAtUtc { get; set; }

    public int AccessCount { get; set; }
}

public class MemoryRecallResponse
{
    public string Query { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public IReadOnlyList<string> Tags { get; set; } = [];

    public int RequestedTopN { get; set; }

    public double MinSimilarity { get; set; }

    public int ReturnedCount { get; set; }

    public int CandidateCount { get; set; }

    public int TotalAvailableMemories { get; set; }

    public string SearchMode { get; set; } = "none";

    public IReadOnlyList<MemoryRecallResult> Results { get; set; } = [];
}

public class RememberMemoryResult
{
    public bool Created { get; set; }

    public string Message { get; set; } = string.Empty;

    public bool VectorSearchAvailable { get; set; }

    public string SearchMode { get; set; } = "lexical";

    public MemoryRecord Memory { get; set; } = new();
}

public class ForgetMemoryResult
{
    public bool Success { get; set; }

    public string MemoryId { get; set; } = string.Empty;

    public int RemainingCount { get; set; }

    public bool VectorSearchAvailable { get; set; }

    public string SearchMode { get; set; } = "lexical";

    public string Message { get; set; } = string.Empty;
}

public class MemoryIndexRebuildResult
{
    public bool Success { get; set; }

    public int MemoryCount { get; set; }

    public bool VectorSearchAvailable { get; set; }

    public string SearchMode { get; set; } = "lexical";

    public string Message { get; set; } = string.Empty;
}

public class MemoryIndexManifest
{
    public bool EmbeddingEnabled { get; set; }

    public string EmbeddingModel { get; set; } = string.Empty;

    public string EmbeddingEndpoint { get; set; } = string.Empty;

    public int MaxTextLengthPerMemory { get; set; }

    public int MemoryCount { get; set; }

    public string RecordFingerprint { get; set; } = string.Empty;
}

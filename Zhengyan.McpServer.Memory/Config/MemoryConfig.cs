using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Zhengyan.McpServer.Memory.Config;

public class MemoryConfig
{
    public string StorageDirectoryPath { get; set; } = "./storage";

    public int DefaultTopN { get; set; } = 5;

    public int MaxTopN { get; set; } = 20;

    public int DefaultListLimit { get; set; } = 20;

    public int MaxListLimit { get; set; } = 100;

    public int EmbeddingBatchSize { get; set; } = 32;

    public int MaxMemoriesForEmbedding { get; set; } = 10000;

    public int ApproximateSearchCandidateMultiplier { get; set; } = 6;

    public EmbeddingConfig Embedding { get; set; } = new();

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });
    }
}

public class EmbeddingConfig
{
    public bool Enabled { get; set; } = true;

    public string Model { get; set; } = "bge_m3";

    public string Endpoint { get; set; } = "http://10.10.40.102:32730/openapi/373cd113-3840-4b83-99bd-b228847cfc7b/v1/embeddings";

    public string? ApiKey { get; set; } = "Vjdgwco4QeETBgqqD7iNPjfqn8yKpxvokTYyUR3eWkQ";

    public int TimeoutSeconds { get; set; } = 60;

    public int MaxTextLengthPerMemory { get; set; } = 2000;
}

using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ModelContextProtocol.Server;
using Serilog;
using Zhengyan.McpServer.Memory.Services;

namespace Zhengyan.McpServer.Memory.Tools;

[McpServerToolType]
public static class MemoryTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    [McpServerTool(Name = "remember_memory"), Description("Store a persistent long-term memory item for future recall. Use it for stable facts, preferences, commitments, or outcomes worth remembering.")]
    public static async Task<string> RememberMemoryAsync(
        IMemoryService memoryService,
        CancellationToken cancellationToken,
        [Description("The memory content to store. Use a concise but information-complete sentence or paragraph.")] string content,
        [Description("Optional memory scope or namespace, for example default, user_profile, project_alpha, or agent_xyz.")] string scope = "default",
        [Description("Optional comma-separated tags, for example preference,profile,meeting.")] string tags = "",
        [Description("Optional JSON object string for structured metadata, for example {\"user\":\"alice\",\"source\":\"chat\"}.")] string metadataJson = "",
        [Description("Optional short summary of the memory.")] string summary = "",
        [Description("Optional importance score between 0 and 1. Higher values make the memory rank slightly higher in recall.")] double importance = 0.5)
    {
        try
        {
            var parsedTags = ParseTags(tags);
            var parsedMetadata = ParseMetadata(metadataJson);
            Log.Information("[MemoryTool] remember_memory scope={Scope} tags={Tags} metadataKeys={MetadataKeys}", scope, parsedTags.Count == 0 ? "NONE" : string.Join(",", parsedTags), parsedMetadata.Count == 0 ? "NONE" : string.Join(",", parsedMetadata.Keys));
            var result = await memoryService.RememberAsync(content, scope, parsedTags, parsedMetadata, summary, importance, cancellationToken);
            var payload = JsonSerializer.Serialize(result, JsonOptions);
            Log.Debug("[MemoryTool] remember_memory result={Result}", payload);
            return payload;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MemoryTool] remember_memory failed");
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "recall_memory"), Description("Search long-term memory semantically and return the most relevant stored memories.")]
    public static async Task<string> RecallMemoryAsync(
        IMemoryService memoryService,
        CancellationToken cancellationToken,
        [Description("The natural language query to search memories for.")] string query,
        [Description("How many memories to return. Default 5.")] int topN = 5,
        [Description("Optional scope filter. Leave empty to search all scopes.")] string scope = "",
        [Description("Optional comma-separated tag filter. All specified tags must be present on a memory item.")] string tags = "",
        [Description("Optional minimum similarity threshold between 0 and 1. Results below this score are discarded.")] double minSimilarity = 0)
    {
        try
        {
            var parsedTags = ParseTags(tags);
            Log.Information("[MemoryTool] recall_memory query={Query} topN={TopN} scope={Scope} tags={Tags} minSimilarity={MinSimilarity}", query, topN, string.IsNullOrWhiteSpace(scope) ? "ALL" : scope, parsedTags.Count == 0 ? "NONE" : string.Join(",", parsedTags), minSimilarity);
            var result = await memoryService.RecallAsync(query, topN, scope, parsedTags, minSimilarity, cancellationToken);
            var payload = JsonSerializer.Serialize(result, JsonOptions);
            Log.Debug("[MemoryTool] recall_memory result={Result}", payload);
            return payload;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MemoryTool] recall_memory failed");
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "list_memories"), Description("List stored memories, ordered by most recently updated, optionally filtered by scope and tags.")]
    public static async Task<string> ListMemoriesAsync(
        IMemoryService memoryService,
        CancellationToken cancellationToken,
        [Description("Optional scope filter. Leave empty to list all scopes.")] string scope = "",
        [Description("Optional comma-separated tag filter. All specified tags must be present on a memory item.")] string tags = "",
        [Description("Maximum number of memories to return. Default 20.")] int limit = 20)
    {
        try
        {
            var parsedTags = ParseTags(tags);
            Log.Information("[MemoryTool] list_memories scope={Scope} tags={Tags} limit={Limit}", string.IsNullOrWhiteSpace(scope) ? "ALL" : scope, parsedTags.Count == 0 ? "NONE" : string.Join(",", parsedTags), limit);
            var memories = await memoryService.ListAsync(scope, parsedTags, limit, cancellationToken);
            var payload = JsonSerializer.Serialize(new
            {
                scope = scope?.Trim() ?? string.Empty,
                tags = parsedTags,
                returned_count = memories.Count,
                results = memories
            }, JsonOptions);
            Log.Debug("[MemoryTool] list_memories result={Result}", payload);
            return payload;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MemoryTool] list_memories failed");
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "forget_memory"), Description("Delete a stored memory by its memory ID.")]
    public static async Task<string> ForgetMemoryAsync(
        IMemoryService memoryService,
        CancellationToken cancellationToken,
        [Description("The exact memory ID to delete.")] string memoryId)
    {
        try
        {
            Log.Information("[MemoryTool] forget_memory memoryId={MemoryId}", memoryId);
            var result = await memoryService.ForgetAsync(memoryId, cancellationToken);
            var payload = JsonSerializer.Serialize(result, JsonOptions);
            Log.Debug("[MemoryTool] forget_memory result={Result}", payload);
            return payload;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MemoryTool] forget_memory failed");
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "rebuild_memory_index"), Description("Reload the persisted memory store and rebuild the local vector index used for semantic recall.")]
    public static async Task<string> RebuildMemoryIndexAsync(
        IMemoryService memoryService,
        CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("[MemoryTool] rebuild_memory_index");
            var result = await memoryService.RebuildIndexAsync(cancellationToken);
            var payload = JsonSerializer.Serialize(result, JsonOptions);
            Log.Debug("[MemoryTool] rebuild_memory_index result={Result}", payload);
            return payload;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MemoryTool] rebuild_memory_index failed");
            return $"Error: {ex.Message}";
        }
    }

    private static IReadOnlyList<string> ParseTags(string rawTags)
    {
        if (string.IsNullOrWhiteSpace(rawTags))
        {
            return [];
        }

        return rawTags
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> ParseMetadata(string metadataJson)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return metadata;
        }

        using var document = JsonDocument.Parse(metadataJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("metadataJson must be a JSON object.");
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            metadata[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : property.Value.GetRawText();
        }

        return metadata;
    }
}

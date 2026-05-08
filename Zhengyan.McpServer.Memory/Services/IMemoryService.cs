using Zhengyan.McpServer.Memory.Models;

namespace Zhengyan.McpServer.Memory.Services;

public interface IMemoryService
{
    Task LoadAsync(CancellationToken cancellationToken = default);

    Task<RememberMemoryResult> RememberAsync(
        string content,
        string? scope = null,
        IReadOnlyCollection<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? summary = null,
        double? importance = null,
        CancellationToken cancellationToken = default);

    Task<MemoryRecallResponse> RecallAsync(
        string query,
        int? topN = null,
        string? scope = null,
        IReadOnlyCollection<string>? tags = null,
        double? minSimilarity = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryRecord>> ListAsync(
        string? scope = null,
        IReadOnlyCollection<string>? tags = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    Task<ForgetMemoryResult> ForgetAsync(
        string memoryId,
        CancellationToken cancellationToken = default);

    Task<MemoryIndexRebuildResult> RebuildIndexAsync(CancellationToken cancellationToken = default);
}

using Zhengyan.McpServer.DataQuery.Models;

namespace Zhengyan.McpServer.DataQuery.Services;

public interface IDataQueryService
{
    Task<IReadOnlyList<DataSourceInfo>> ListDataSourcesAsync(CancellationToken cancellationToken = default);

    Task<DataQueryResponse> QueryAsync(
        string query,
        int? topN = null,
        IReadOnlyCollection<string>? sourceFiles = null,
        double? minSimilarity = null,
        CancellationToken cancellationToken = default);

    Task ReloadAsync(CancellationToken cancellationToken = default);
}

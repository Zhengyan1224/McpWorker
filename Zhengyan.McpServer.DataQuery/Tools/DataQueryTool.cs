using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ModelContextProtocol.Server;
using Serilog;
using Zhengyan.McpServer.DataQuery.Services;

namespace Zhengyan.McpServer.DataQuery.Tools;

[McpServerToolType]
public static class DataQueryTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    [McpServerTool(Name = "list_data_sources"), Description("List available CSV data source files and the record count loaded from each source.")]
    public static async Task<string> ListDataSourcesAsync(
        IDataQueryService dataQueryService,
        CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("[DataQueryTool] list_data_sources");
            var sources = await dataQueryService.ListDataSourcesAsync(cancellationToken);
            var json = JsonSerializer.Serialize(sources, JsonOptions);
            Log.Debug("[DataQueryTool] list_data_sources result={Result}", json);
            return json;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[DataQueryTool] list_data_sources failed");
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "query_data"), Description("Recall CSV records related to the query text and return the top matching rows ordered by similarity.")]
    public static async Task<string> QueryDataAsync(
        IDataQueryService dataQueryService,
        CancellationToken cancellationToken,
        [Description("The natural language query used to recall related data rows.")] string query,
        [Description("How many similar rows to return. Default 5.")] int topN = 5,
        [Description("Optional CSV source file filter. Use an exact file name like 小区.csv or a comma-separated list like 小区.csv,问题指标表.csv.")] string sourceFiles = "",
        [Description("Optional minimum similarity threshold between 0 and 1. Rows below this score will be discarded. Default 0.")] double minSimilarity = 0)
    {
        try
        {
            var sourceFileFilter = ParseSourceFiles(sourceFiles);
            Log.Information("[DataQueryTool] query_data query={Query} topN={TopN} minSimilarity={MinSimilarity} sourceFiles={SourceFiles}", query, topN, minSimilarity, sourceFileFilter.Count == 0 ? "ALL" : string.Join(",", sourceFileFilter));
            var response = await dataQueryService.QueryAsync(query, topN, sourceFileFilter, minSimilarity, cancellationToken);
            var payload = new
            {
                query = response.Query,
                requested_top_n = response.RequestedTopN,
                min_similarity = response.MinSimilarity,
                returned_count = response.ReturnedCount,
                candidate_count = response.CandidateCount,
                total_available_records = response.TotalAvailableRecords,
                search_mode = response.SearchMode,
                filtered_source_files = response.FilteredSourceFiles,
                matched_source_files = response.MatchedSourceFiles,
                results = response.Results.Select(item =>
                {
                    var row = new Dictionary<string, object?>
                    {
                        ["_source_file"] = item.SourceFile,
                        ["_row_number"] = item.RowNumber,
                        ["_similarity"] = item.Similarity
                    };

                    foreach (var pair in item.Fields)
                    {
                        row[pair.Key] = pair.Value;
                    }

                    return row;
                })
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            Log.Debug("[DataQueryTool] query_data result={Result}", json);
            return json;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[DataQueryTool] query_data failed");
            return $"Error: {ex.Message}";
        }
    }

    private static IReadOnlyCollection<string> ParseSourceFiles(string rawSourceFiles)
    {
        if (string.IsNullOrWhiteSpace(rawSourceFiles))
        {
            return Array.Empty<string>();
        }

        return rawSourceFiles
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    [McpServerTool(Name = "rebuild_data_index"), Description("Reload CSV data, rebuild embeddings if enabled, and refresh the local vector cache/index files.")]
    public static async Task<string> RebuildDataIndexAsync(
        IDataQueryService dataQueryService,
        CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("[DataQueryTool] rebuild_data_index");
            await dataQueryService.ReloadAsync(cancellationToken);
            var payload = JsonSerializer.Serialize(new
            {
                success = true,
                message = "Data index rebuilt successfully."
            }, JsonOptions);
            Log.Debug("[DataQueryTool] rebuild_data_index result={Result}", payload);
            return payload;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[DataQueryTool] rebuild_data_index failed");
            return $"Error: {ex.Message}";
        }
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
namespace Zhengyan.KBServer.Tools;

[McpServerToolType]
public static class KnowledgeBaseTool
{
    [McpServerTool(Name = "SearchKnowledge"), Description("从知识库中搜索与用户问题相关的内容。")]
    public static async Task<string> SearchKnowledge(
        [Description("要检索的知识库名")] string dbName,
        [Description("要检索的相关问题")] string query,
        [Description("返回前K条数据")] int topK,
        IKnowledgeBaseService service,
        CancellationToken cancellationToken)
    {
        Log.Debug($"DBName: {dbName}, Query: {query}, TopK: {topK}");
        try
        {
            if (string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(query))
            {
                return "Invalid parameters";
            }

            var result = await service.SearchKnowledgeAsync(dbName, query, topK);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            var json = JsonSerializer.Serialize(result, options);
            Log.Debug($"Search Result: {json}");
            return json;
        }
        catch (Exception ex)
        {
            Log.Error($"Search Error: {ex.Message}\n{ex.StackTrace}");
            return $"Error: {ex.Message}";
        }

    }

    [McpServerTool(Name = "AddKnowledge"), Description("向知识库中添加新的知识内容。")]
    public static async Task<string> AddKnowledge(
        IKnowledgeBaseService service,
        CancellationToken cancellationToken,
        [Description("要添加的知识库名")] string dbName,
        [Description("要添加的内容")] string content,
        [Description("添加的知识的来源")] string source = "")
    {
        Log.Debug($"DBName: {dbName}, Content: {content}, Source: {source}");
        try
        {
            if (string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(content))
            {
                return "Invalid parameters";
            }

            var result = await service.AddKnowledgeAsync(dbName, new Models.KnowledgeContent[] { new Models.KnowledgeContent { Content = content, MetaData = new Dictionary<string, string> { { "Source", source } } } });
            
            Log.Debug($"Add Result: {result}");
            return $"添加成功{result}条。";
        }
        catch (Exception ex)
        {
            Log.Error($"Search Error: {ex.Message}\n{ex.StackTrace}");
            return $"Error: {ex.Message}";
        }

    }

}
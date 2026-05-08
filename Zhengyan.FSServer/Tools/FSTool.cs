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
using Zhengyan.FSServer.Services;
namespace Zhengyan.FSServer.Tools;

[McpServerToolType]
public static class FSTool
{
    [McpServerTool(Name = "ReadFile"), Description("从文件存储服务上读取文件内容，参数为文件相对路径，返回文件内容字符串。")]
    public static async Task<string> SearchKnowledge(
        [Description("文件的相对路径")] string relativePath,
        IFSService service,
        CancellationToken cancellationToken)
    {
        Log.Debug($"Relative Path: {relativePath}");
        try
        {
            var content = await service.ReadFileAsync(relativePath, cancellationToken);
            Log.Debug($"File Content: {content}");
            return $"File Content: {content}";
        }
        catch (Exception ex)
        {
            Log.Error($"Search Error: {ex.Message}\n{ex.StackTrace}");
            return $"Error: {ex.Message}";
        }

    }

}
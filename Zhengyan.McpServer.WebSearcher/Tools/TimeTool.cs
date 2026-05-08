using ModelContextProtocol.Server;
using System.ComponentModel;
namespace Zhengyan.McpServer.WebSearcher.Tools;

[McpServerToolType]
public static class TimeTool
{
    [McpServerTool, Description("Get the current time.")]
    public static string GetCurrentTime() => $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
}
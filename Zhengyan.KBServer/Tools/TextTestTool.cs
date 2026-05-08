using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Zhengyan.KBServer.Tools;

[McpServerToolType]
public static class TextTestTool
{
    [McpServerTool(Name = "Summarize"), Description("根据特定段落文本的内容进行总结")]
    public static Task<string> Summarize(
        [Description("待总结的内容")] string content,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"原始内容: {content}");
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult("Canceled");
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return Task.FromResult(string.Empty);
            }

            var normalizedContent = content.Replace("\r", " ").Replace("\n", " ").Trim();
            var summary = normalizedContent.Length <= 200
                ? normalizedContent
                : $"{normalizedContent[..200]}...";

            Console.WriteLine($"结果: {summary}");
            return Task.FromResult(summary);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Summarize Error: {ex.Message}\n{ex.StackTrace}");
            return Task.FromResult($"Error: {ex.Message}");
        }
    }
}

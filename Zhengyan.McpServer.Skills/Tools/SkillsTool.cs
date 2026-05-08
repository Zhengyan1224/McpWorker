using ModelContextProtocol.Server;
using Serilog;
using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Zhengyan.McpServer.Skills.Services;

namespace Zhengyan.McpServer.Skills.Tools;

[McpServerToolType]
public static class SkillsTool
{
    private const int MaxLogContentLength = 12000;

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private static readonly JsonSerializerOptions _logJsonSerializerOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    [McpServerTool(Name = "ListSkills"), Description("列出所有可用技能。")]
    public static Task<string> ListSkillsAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("关键词过滤，留空表示不过滤。")] string keyword = "")
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "ListSkills",
            arguments: new { keyword },
            action: async () =>
            {
                var skills = await skillsService.ListSkillsAsync(keyword, cancellationToken);
                return JsonSerializer.Serialize(skills, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "SearchSkills"), Description("按关键词检索技能。")]
    public static Task<string> SearchSkillsAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("检索关键词。")] string query,
        [Description("返回条数上限，默认 10。")] int topK = 10)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "SearchSkills",
            arguments: new { query, topK },
            action: async () =>
            {
                var skills = await skillsService.SearchSkillsAsync(query, topK, cancellationToken);
                return JsonSerializer.Serialize(skills, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "ReadSkill"), Description("读取指定技能的完整内容。")]
    public static Task<string> ReadSkillAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("技能 ID。")] string skillId,
        [Description("返回内容最大长度，<=0 使用默认限制。")] int maxContentLength = 0)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "ReadSkill",
            arguments: new { skillId, maxContentLength },
            action: async () =>
            {
                var detail = await skillsService.GetSkillAsync(
                    skillId,
                    maxContentLength > 0 ? maxContentLength : null,
                    cancellationToken);

                if (detail == null)
                {
                    return $"Skill not found: {skillId}";
                }

                return JsonSerializer.Serialize(detail, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "GetPathInfo"), Description("获取路径信息。")]
    public static Task<string> GetPathInfoAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("相对工作区路径，默认 '.'。")] string relativePath = ".")
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "GetPathInfo",
            arguments: new { relativePath },
            action: async () =>
            {
                var info = await skillsService.GetPathInfoAsync(relativePath, cancellationToken);
                return JsonSerializer.Serialize(info, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "ListFiles"), Description("列出文件或目录。")]
    public static Task<string> ListFilesAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("相对路径，默认 '.'。")] string relativePath = ".",
        [Description("是否递归。")] bool recursive = false)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "ListFiles",
            arguments: new { relativePath, recursive },
            action: async () =>
            {
                var files = await skillsService.ListFilesAsync(relativePath, recursive, cancellationToken);
                return JsonSerializer.Serialize(files, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "FindFiles"), Description("按通配符查找文件。")]
    public static Task<string> FindFilesAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("通配符，默认 '*'。")] string pattern = "*",
        [Description("检索目录，默认 '.'。")] string relativePath = ".",
        [Description("是否递归。")] bool recursive = true)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "FindFiles",
            arguments: new { pattern, relativePath, recursive },
            action: async () =>
            {
                var files = await skillsService.FindFilesAsync(pattern, relativePath, recursive, cancellationToken);
                return JsonSerializer.Serialize(files, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "ReadFile"), Description("读取文本文件内容。")]
    public static Task<string> ReadFileAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("文件路径。")] string relativePath,
        [Description("最大读取长度，<=0 使用默认限制。")] int maxLength = 0)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "ReadFile",
            arguments: new { relativePath, maxLength },
            action: async () =>
            {
                var file = await skillsService.ReadFileAsync(
                    relativePath,
                    maxLength > 0 ? maxLength : null,
                    cancellationToken);
                return JsonSerializer.Serialize(file, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "ReadFileLines"), Description("按行读取文件。")]
    public static Task<string> ReadFileLinesAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("文件路径。")] string relativePath,
        [Description("起始行号（从 1 开始）。")] int startLine = 1,
        [Description("读取行数。")] int lineCount = 200)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "ReadFileLines",
            arguments: new { relativePath, startLine, lineCount },
            action: async () =>
            {
                var fileLines = await skillsService.ReadFileLinesAsync(relativePath, startLine, lineCount, cancellationToken);
                return JsonSerializer.Serialize(fileLines, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "WriteFile"), Description("写入或追加文本到文件。")]
    public static Task<string> WriteFileAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("文件路径。")] string relativePath,
        [Description("写入文本。")] string content,
        [Description("是否追加写入。")] bool append = false)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "WriteFile",
            arguments: new { relativePath, append, contentLength = content?.Length ?? 0 },
            action: async () =>
            {
                var safeContent = content ?? string.Empty;
                var writeResult = await skillsService.WriteFileAsync(relativePath, safeContent, append, cancellationToken);
                return JsonSerializer.Serialize(writeResult, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "ReplaceInFile"), Description("搜索并替换文件文本。")]
    public static Task<string> ReplaceInFileAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("文件路径。")] string relativePath,
        [Description("待搜索文本。")] string searchText,
        [Description("替换文本。")] string replaceText,
        [Description("是否替换全部。")] bool replaceAll = true,
        [Description("是否区分大小写。")] bool caseSensitive = true)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "ReplaceInFile",
            arguments: new
            {
                relativePath,
                searchText,
                replaceTextLength = replaceText?.Length ?? 0,
                replaceAll,
                caseSensitive
            },
            action: async () =>
            {
                var safeReplaceText = replaceText ?? string.Empty;
                var replaceResult = await skillsService.ReplaceInFileAsync(
                    relativePath,
                    searchText,
                    safeReplaceText,
                    replaceAll,
                    caseSensitive,
                    cancellationToken);
                return JsonSerializer.Serialize(replaceResult, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "SearchText"), Description("搜索目录/文件中的文本。")]
    public static Task<string> SearchTextAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("检索关键词。")] string query,
        [Description("目录或文件路径，默认 '.'。")] string relativePath = ".",
        [Description("是否递归搜索目录。")] bool recursive = true,
        [Description("是否区分大小写。")] bool caseSensitive = false,
        [Description("最大返回条数，<=0 使用默认限制。")] int maxResults = 0)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "SearchText",
            arguments: new { query, relativePath, recursive, caseSensitive, maxResults },
            action: async () =>
            {
                var searchResult = await skillsService.SearchTextAsync(
                    query,
                    relativePath,
                    recursive,
                    caseSensitive,
                    maxResults > 0 ? maxResults : null,
                    cancellationToken);
                return JsonSerializer.Serialize(searchResult, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "CreateDirectory"), Description("创建目录。")]
    public static Task<string> CreateDirectoryAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("目录路径。")] string relativePath)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "CreateDirectory",
            arguments: new { relativePath },
            action: async () =>
            {
                var opResult = await skillsService.CreateDirectoryAsync(relativePath, cancellationToken);
                return JsonSerializer.Serialize(opResult, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "DeletePath"), Description("删除文件或目录（需 force=true）。")]
    public static Task<string> DeletePathAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("路径。")] string relativePath,
        [Description("是否递归删除目录。")] bool recursive = false,
        [Description("必须为 true 才执行删除。")] bool force = false)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "DeletePath",
            arguments: new { relativePath, recursive, force },
            action: async () =>
            {
                var opResult = await skillsService.DeletePathAsync(relativePath, recursive, force, cancellationToken);
                return JsonSerializer.Serialize(opResult, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "CopyPath"), Description("复制文件或目录。")]
    public static Task<string> CopyPathAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("源路径。")] string sourceRelativePath,
        [Description("目标路径。")] string destinationRelativePath,
        [Description("是否覆盖。")] bool overwrite = false)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "CopyPath",
            arguments: new { sourceRelativePath, destinationRelativePath, overwrite },
            action: async () =>
            {
                var transferResult = await skillsService.CopyPathAsync(sourceRelativePath, destinationRelativePath, overwrite, cancellationToken);
                return JsonSerializer.Serialize(transferResult, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "MovePath"), Description("移动文件或目录。")]
    public static Task<string> MovePathAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("源路径。")] string sourceRelativePath,
        [Description("目标路径。")] string destinationRelativePath,
        [Description("是否覆盖。")] bool overwrite = false)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "MovePath",
            arguments: new { sourceRelativePath, destinationRelativePath, overwrite },
            action: async () =>
            {
                var transferResult = await skillsService.MovePathAsync(sourceRelativePath, destinationRelativePath, overwrite, cancellationToken);
                return JsonSerializer.Serialize(transferResult, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "ExecuteCommand"), Description("执行系统命令。")]
    public static Task<string> ExecuteCommandAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("命令文本。")] string command,
        [Description("工作目录，默认 '.'。")] string workingDirectory = ".",
        [Description("超时秒数，<=0 使用默认值。")] int timeoutSeconds = 0)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "ExecuteCommand",
            arguments: new
            {
                command,
                workingDirectory,
                timeoutSeconds
            },
            action: async () =>
            {
                var commandResult = await skillsService.ExecuteCommandAsync(
                    command,
                    workingDirectory,
                    timeoutSeconds > 0 ? timeoutSeconds : null,
                    cancellationToken);
                return JsonSerializer.Serialize(commandResult, _jsonSerializerOptions);
            });
    }

    [McpServerTool(Name = "ReadSkillFile"), Description("按技能目录相对路径读取技能附属文件，例如 references、assets 或 scripts 下的文件。")]
    public static Task<string> ReadSkillFileAsync(
        ISkillsService skillsService,
        CancellationToken cancellationToken,
        [Description("技能 ID。")] string skillId,
        [Description("相对技能目录的文件路径，例如 references/report-template.md。")] string relativePath,
        [Description("最大读取长度，<=0 使用默认限制。")] int maxLength = 0)
    {
        return ExecuteToolWithLoggingAsync(
            toolName: "ReadSkillFile",
            arguments: new { skillId, relativePath, maxLength },
            action: async () =>
            {
                var file = await skillsService.ReadSkillFileAsync(
                    skillId,
                    relativePath,
                    maxLength > 0 ? maxLength : null,
                    cancellationToken);
                return JsonSerializer.Serialize(file, _jsonSerializerOptions);
            });
    }

    private static async Task<string> ExecuteToolWithLoggingAsync(string toolName, object? arguments, Func<Task<string>> action)
    {
        LogToolCall(toolName, arguments);
        try
        {
            var result = await action();
            LogToolReturn(toolName, result);
            return result;
        }
        catch (Exception ex)
        {
            var errorResult = $"Error: {ex.Message}";
            Log.Error(ex, "[SkillsTool] {ToolName} threw exception", toolName);
            LogToolReturn(toolName, errorResult);
            return errorResult;
        }
    }

    private static void LogToolCall(string toolName, object? arguments)
    {
        var argsJson = SerializeForLog(arguments);
        Log.Information("[SkillsTool] Call {ToolName} args={Arguments}", toolName, TruncateForLog(argsJson));
    }

    private static void LogToolReturn(string toolName, string result)
    {
        Log.Information("[SkillsTool] Return {ToolName} result={Result}", toolName, TruncateForLog(result));
    }

    private static string SerializeForLog(object? value)
    {
        try
        {
            return JsonSerializer.Serialize(value, _logJsonSerializerOptions);
        }
        catch
        {
            return value?.ToString() ?? string.Empty;
        }
    }

    private static string TruncateForLog(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (text.Length <= MaxLogContentLength)
        {
            return text;
        }

        return $"{text[..MaxLogContentLength]}... [truncated, originalLength={text.Length}]";
    }
}

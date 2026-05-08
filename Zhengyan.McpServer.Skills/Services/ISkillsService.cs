using Zhengyan.McpServer.Skills.Models;

namespace Zhengyan.McpServer.Skills.Services;

public interface ISkillsService
{
    Task<IReadOnlyList<SkillInfo>> ListSkillsAsync(string? keyword = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SkillInfo>> SearchSkillsAsync(string query, int topK = 10, CancellationToken cancellationToken = default);

    Task<SkillDetail?> GetSkillAsync(string skillId, int? maxContentLength = null, CancellationToken cancellationToken = default);

    Task<FileReadResult> ReadSkillFileAsync(string skillId, string relativePath, int? maxLength = null, CancellationToken cancellationToken = default);

    Task<PathInfoResult> GetPathInfoAsync(string relativePath = ".", CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileEntry>> ListFilesAsync(string relativePath = ".", bool recursive = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileEntry>> FindFilesAsync(string pattern = "*", string relativePath = ".", bool recursive = true, CancellationToken cancellationToken = default);

    Task<FileReadResult> ReadFileAsync(string relativePath, int? maxLength = null, CancellationToken cancellationToken = default);

    Task<FileLinesResult> ReadFileLinesAsync(string relativePath, int startLine = 1, int lineCount = 200, CancellationToken cancellationToken = default);

    Task<FileWriteResult> WriteFileAsync(string relativePath, string content, bool append = false, CancellationToken cancellationToken = default);

    Task<ReplaceTextResult> ReplaceInFileAsync(string relativePath, string searchText, string replaceText, bool replaceAll = true, bool caseSensitive = true, CancellationToken cancellationToken = default);

    Task<TextSearchResult> SearchTextAsync(string query, string relativePath = ".", bool recursive = true, bool caseSensitive = false, int? maxResults = null, CancellationToken cancellationToken = default);

    Task<PathOperationResult> CreateDirectoryAsync(string relativePath, CancellationToken cancellationToken = default);

    Task<PathOperationResult> DeletePathAsync(string relativePath, bool recursive = false, bool force = false, CancellationToken cancellationToken = default);

    Task<PathTransferResult> CopyPathAsync(string sourceRelativePath, string destinationRelativePath, bool overwrite = false, CancellationToken cancellationToken = default);

    Task<PathTransferResult> MovePathAsync(string sourceRelativePath, string destinationRelativePath, bool overwrite = false, CancellationToken cancellationToken = default);

    Task<CommandExecutionResult> ExecuteCommandAsync(string command, string workingDirectory = ".", int? timeoutSeconds = null, CancellationToken cancellationToken = default);
}

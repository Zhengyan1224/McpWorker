using Serilog;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Zhengyan.McpServer.Skills.Config;
using Zhengyan.McpServer.Skills.Models;

namespace Zhengyan.McpServer.Skills.Services;

public class SkillsService : ISkillsService
{
    private readonly SkillsConfig _skillsConfig;
    private readonly string _skillsRootPath;
    private readonly string _workspaceRootPath;

    public SkillsService(SkillsConfig skillsConfig)
    {
        _skillsConfig = skillsConfig ?? throw new ArgumentNullException(nameof(skillsConfig));
        _workspaceRootPath = Path.GetFullPath(_skillsConfig.WorkspaceRootPath);
        _skillsRootPath = Path.GetFullPath(_skillsConfig.SkillsRootPath);

        if (!Directory.Exists(_workspaceRootPath))
        {
            Directory.CreateDirectory(_workspaceRootPath);
        }

        if (!Directory.Exists(_skillsRootPath))
        {
            Directory.CreateDirectory(_skillsRootPath);
        }
    }

    public async Task<IReadOnlyList<SkillInfo>> ListSkillsAsync(string? keyword = null, CancellationToken cancellationToken = default)
    {
        var query = keyword?.Trim();
        var results = new List<SkillInfo>();

        foreach (var skillFilePath in EnumerateSkillEntryFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var skillItem = await ReadSkillItemAsync(skillFilePath, cancellationToken);
            if (skillItem == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(query) || MatchQuery(skillItem, query))
            {
                results.Add(ToSkillInfo(skillItem));
            }
        }

        return [.. results.OrderBy(x => x.ID, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<IReadOnlyList<SkillInfo>> SearchSkillsAsync(string query, int topK = 10, CancellationToken cancellationToken = default)
    {
        var keyword = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return await ListSkillsAsync(null, cancellationToken);
        }

        var take = topK <= 0 ? 10 : Math.Min(topK, 100);
        var candidates = new List<(SkillItem Item, int Score)>();

        foreach (var skillFilePath in EnumerateSkillEntryFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var skillItem = await ReadSkillItemAsync(skillFilePath, cancellationToken);
            if (skillItem == null)
            {
                continue;
            }

            var score = CalculateScore(skillItem, keyword);
            if (score > 0)
            {
                candidates.Add((skillItem, score));
            }
        }

        return candidates
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Item.ID, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Select(x => ToSkillInfo(x.Item))
            .ToArray();
    }

    public async Task<SkillDetail?> GetSkillAsync(string skillId, int? maxContentLength = null, CancellationToken cancellationToken = default)
    {
        var skillItem = await FindSkillItemAsync(skillId, cancellationToken);
        if (skillItem == null)
        {
            return null;
        }

        var contentLimit = maxContentLength.GetValueOrDefault(_skillsConfig.MaxContentLength);
        contentLimit = contentLimit <= 0 ? _skillsConfig.MaxContentLength : contentLimit;

        var content = skillItem.Content;
        var truncated = content.Length > contentLimit;
        if (truncated)
        {
            content = content[..contentLimit];
        }

        return new SkillDetail
        {
            ID = skillItem.ID,
            Name = skillItem.Name,
            Description = skillItem.Description,
            EntryFilePath = skillItem.EntryFilePath,
            SkillRootPath = skillItem.SkillRootPath,
            RelativePathRule = "Resolve relative paths mentioned in this skill against SkillRootPath. Prefer the ReadSkillFile tool for skill-local references.",
            ContentLength = skillItem.Content.Length,
            Truncated = truncated,
            Content = content
        };
    }

    public async Task<FileReadResult> ReadSkillFileAsync(string skillId, string relativePath, int? maxLength = null, CancellationToken cancellationToken = default)
    {
        var skillItem = await FindSkillItemAsync(skillId, cancellationToken);
        if (skillItem == null)
        {
            throw new FileNotFoundException($"Skill not found: {skillId}", skillId);
        }

        var absolutePath = ResolveSkillPath(skillItem, relativePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("File not found", relativePath);
        }

        var content = await File.ReadAllTextAsync(absolutePath, cancellationToken);
        var lengthLimit = maxLength.GetValueOrDefault(_skillsConfig.MaxFileReadLength);
        lengthLimit = lengthLimit <= 0 ? _skillsConfig.MaxFileReadLength : lengthLimit;

        var truncated = content.Length > lengthLimit;
        if (truncated)
        {
            content = content[..lengthLimit];
        }

        return new FileReadResult
        {
            Path = NormalizePathForClient(absolutePath),
            Length = content.Length,
            Truncated = truncated,
            Content = content
        };
    }

    public Task<PathInfoResult> GetPathInfoAsync(string relativePath = ".", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var absolutePath = ResolveWorkspacePath(relativePath);
        var normalizedPath = NormalizeWorkspaceRelativePath(absolutePath);

        if (File.Exists(absolutePath))
        {
            var fileInfo = new FileInfo(absolutePath);
            return Task.FromResult(new PathInfoResult
            {
                Path = normalizedPath,
                Exists = true,
                IsDirectory = false,
                Length = fileInfo.Length,
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
            });
        }

        if (Directory.Exists(absolutePath))
        {
            var directoryInfo = new DirectoryInfo(absolutePath);
            return Task.FromResult(new PathInfoResult
            {
                Path = normalizedPath,
                Exists = true,
                IsDirectory = true,
                Length = 0,
                LastWriteTimeUtc = directoryInfo.LastWriteTimeUtc
            });
        }

        return Task.FromResult(new PathInfoResult
        {
            Path = normalizedPath,
            Exists = false,
            IsDirectory = false,
            Length = 0,
            LastWriteTimeUtc = null
        });
    }

    public Task<IReadOnlyList<FileEntry>> ListFilesAsync(string relativePath = ".", bool recursive = false, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveWorkspacePath(relativePath);
        var results = new List<FileEntry>();

        if (File.Exists(absolutePath))
        {
            var fileInfo = new FileInfo(absolutePath);
            results.Add(new FileEntry
            {
                Path = NormalizeWorkspaceRelativePath(absolutePath),
                IsDirectory = false,
                Length = fileInfo.Length,
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
            });
            return Task.FromResult<IReadOnlyList<FileEntry>>(results);
        }

        if (!Directory.Exists(absolutePath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {relativePath}");
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var dir in Directory.GetDirectories(absolutePath, "*", searchOption))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (results.Count >= _skillsConfig.MaxListEntries)
            {
                break;
            }

            var directoryInfo = new DirectoryInfo(dir);
            results.Add(new FileEntry
            {
                Path = NormalizeWorkspaceRelativePath(dir),
                IsDirectory = true,
                Length = 0,
                LastWriteTimeUtc = directoryInfo.LastWriteTimeUtc
            });
        }

        if (results.Count < _skillsConfig.MaxListEntries)
        {
            foreach (var file in Directory.GetFiles(absolutePath, "*", searchOption))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (results.Count >= _skillsConfig.MaxListEntries)
                {
                    break;
                }

                var fileInfo = new FileInfo(file);
                results.Add(new FileEntry
                {
                    Path = NormalizeWorkspaceRelativePath(file),
                    IsDirectory = false,
                    Length = fileInfo.Length,
                    LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
                });
            }
        }

        return Task.FromResult<IReadOnlyList<FileEntry>>([.. results.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)]);
    }

    public Task<IReadOnlyList<FileEntry>> FindFilesAsync(string pattern = "*", string relativePath = ".", bool recursive = true, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveWorkspacePath(relativePath);
        if (File.Exists(absolutePath))
        {
            absolutePath = Path.GetDirectoryName(absolutePath) ?? absolutePath;
        }

        if (!Directory.Exists(absolutePath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {relativePath}");
        }

        pattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern;
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(absolutePath, pattern, searchOption)
            .Take(_skillsConfig.MaxListEntries);

        var results = new List<FileEntry>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(file);
            results.Add(new FileEntry
            {
                Path = NormalizeWorkspaceRelativePath(file),
                IsDirectory = false,
                Length = fileInfo.Length,
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
            });
        }

        return Task.FromResult<IReadOnlyList<FileEntry>>([.. results.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)]);
    }

    public async Task<FileReadResult> ReadFileAsync(string relativePath, int? maxLength = null, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveWorkspacePath(relativePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("File not found", relativePath);
        }

        var content = await File.ReadAllTextAsync(absolutePath, cancellationToken);
        var lengthLimit = maxLength.GetValueOrDefault(_skillsConfig.MaxFileReadLength);
        lengthLimit = lengthLimit <= 0 ? _skillsConfig.MaxFileReadLength : lengthLimit;

        var truncated = content.Length > lengthLimit;
        if (truncated)
        {
            content = content[..lengthLimit];
        }

        return new FileReadResult
        {
            Path = NormalizeWorkspaceRelativePath(absolutePath),
            Length = content.Length,
            Truncated = truncated,
            Content = content
        };
    }

    public async Task<FileLinesResult> ReadFileLinesAsync(string relativePath, int startLine = 1, int lineCount = 200, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveWorkspacePath(relativePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("File not found", relativePath);
        }

        var normalizedStartLine = Math.Max(1, startLine);
        var normalizedLineCount = lineCount <= 0 ? 200 : Math.Min(lineCount, _skillsConfig.MaxReadLines);
        var endLine = normalizedStartLine + normalizedLineCount - 1;
        var lines = new List<FileLineItem>();

        using var reader = new StreamReader(absolutePath);
        var lineNumber = 0;
        while (!reader.EndOfStream && lineNumber < endLine)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            lineNumber++;
            if (lineNumber < normalizedStartLine)
            {
                continue;
            }

            lines.Add(new FileLineItem
            {
                LineNumber = lineNumber,
                Content = line
            });
        }

        return new FileLinesResult
        {
            Path = NormalizeWorkspaceRelativePath(absolutePath),
            StartLine = normalizedStartLine,
            RequestedLineCount = normalizedLineCount,
            ReturnedLineCount = lines.Count,
            Lines = lines
        };
    }

    public async Task<FileWriteResult> WriteFileAsync(string relativePath, string content, bool append = false, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveWorkspacePath(relativePath);
        var directoryPath = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        if (append)
        {
            await File.AppendAllTextAsync(absolutePath, content ?? string.Empty, cancellationToken);
        }
        else
        {
            await File.WriteAllTextAsync(absolutePath, content ?? string.Empty, cancellationToken);
        }

        return new FileWriteResult
        {
            Path = NormalizeWorkspaceRelativePath(absolutePath),
            Appended = append,
            WrittenLength = content?.Length ?? 0
        };
    }

    public async Task<ReplaceTextResult> ReplaceInFileAsync(string relativePath, string searchText, string replaceText, bool replaceAll = true, bool caseSensitive = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            throw new ArgumentException("searchText cannot be empty.", nameof(searchText));
        }

        var absolutePath = ResolveWorkspacePath(relativePath);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("File not found", relativePath);
        }

        var originalContent = await File.ReadAllTextAsync(absolutePath, cancellationToken);
        var replacedCount = 0;
        string newContent;

        if (caseSensitive)
        {
            if (replaceAll)
            {
                replacedCount = CountOccurrences(originalContent, searchText, StringComparison.Ordinal);
                newContent = originalContent.Replace(searchText, replaceText, StringComparison.Ordinal);
            }
            else
            {
                var firstIndex = originalContent.IndexOf(searchText, StringComparison.Ordinal);
                if (firstIndex >= 0)
                {
                    replacedCount = 1;
                    newContent = $"{originalContent[..firstIndex]}{replaceText}{originalContent[(firstIndex + searchText.Length)..]}";
                }
                else
                {
                    newContent = originalContent;
                }
            }
        }
        else
        {
            var regex = new Regex(Regex.Escape(searchText), RegexOptions.IgnoreCase);
            if (replaceAll)
            {
                replacedCount = regex.Matches(originalContent).Count;
                newContent = regex.Replace(originalContent, replaceText);
            }
            else
            {
                replacedCount = regex.IsMatch(originalContent) ? 1 : 0;
                newContent = regex.Replace(originalContent, replaceText, 1);
            }
        }

        if (replacedCount > 0)
        {
            await File.WriteAllTextAsync(absolutePath, newContent, cancellationToken);
        }

        return new ReplaceTextResult
        {
            Path = NormalizeWorkspaceRelativePath(absolutePath),
            ReplacedCount = replacedCount
        };
    }

    public async Task<TextSearchResult> SearchTextAsync(string query, string relativePath = ".", bool recursive = true, bool caseSensitive = false, int? maxResults = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty.", nameof(query));
        }

        var absolutePath = ResolveWorkspacePath(relativePath);
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var fileList = new List<string>();

        if (File.Exists(absolutePath))
        {
            fileList.Add(absolutePath);
        }
        else if (Directory.Exists(absolutePath))
        {
            fileList.AddRange(Directory.GetFiles(absolutePath, "*", searchOption).Take(_skillsConfig.MaxSearchFiles));
        }
        else
        {
            throw new FileNotFoundException($"Path not found: {relativePath}");
        }

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var resultLimit = maxResults.GetValueOrDefault(_skillsConfig.MaxSearchResults);
        resultLimit = resultLimit <= 0 ? _skillsConfig.MaxSearchResults : Math.Min(resultLimit, _skillsConfig.MaxSearchResults);

        var result = new TextSearchResult
        {
            Query = query
        };

        foreach (var filePath in fileList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.ScannedFiles++;
            if (result.Matches.Count >= resultLimit)
            {
                result.Truncated = true;
                break;
            }

            try
            {
                using var reader = new StreamReader(filePath);
                var lineNumber = 0;
                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var line = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
                    lineNumber++;

                    var idx = line.IndexOf(query, comparison);
                    if (idx < 0)
                    {
                        continue;
                    }

                    result.Matches.Add(new TextSearchMatch
                    {
                        Path = NormalizeWorkspaceRelativePath(filePath),
                        LineNumber = lineNumber,
                        Column = idx + 1,
                        LineText = line
                    });

                    if (result.Matches.Count >= resultLimit)
                    {
                        result.Truncated = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"SearchText skip file '{filePath}': {ex.Message}");
            }
        }

        return result;
    }

    public Task<PathOperationResult> CreateDirectoryAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("relativePath cannot be empty.", nameof(relativePath));
        }

        var absolutePath = ResolveWorkspacePath(relativePath);
        Directory.CreateDirectory(absolutePath);

        return Task.FromResult(new PathOperationResult
        {
            Path = NormalizeWorkspaceRelativePath(absolutePath),
            Operation = "create_directory",
            Success = true,
            Message = "Directory created."
        });
    }

    public Task<PathOperationResult> DeletePathAsync(string relativePath, bool recursive = false, bool force = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!force)
        {
            return Task.FromResult(new PathOperationResult
            {
                Path = relativePath,
                Operation = "delete_path",
                Success = false,
                Message = "Deletion denied. Set force=true to confirm deletion."
            });
        }

        var absolutePath = ResolveWorkspacePath(relativePath);
        EnsureNotWorkspaceRoot(absolutePath);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
            return Task.FromResult(new PathOperationResult
            {
                Path = NormalizeWorkspaceRelativePath(absolutePath),
                Operation = "delete_file",
                Success = true,
                Message = "File deleted."
            });
        }

        if (Directory.Exists(absolutePath))
        {
            Directory.Delete(absolutePath, recursive);
            return Task.FromResult(new PathOperationResult
            {
                Path = NormalizeWorkspaceRelativePath(absolutePath),
                Operation = "delete_directory",
                Success = true,
                Message = "Directory deleted."
            });
        }

        return Task.FromResult(new PathOperationResult
        {
            Path = NormalizeWorkspaceRelativePath(absolutePath),
            Operation = "delete_path",
            Success = false,
            Message = "Path does not exist."
        });
    }

    public Task<PathTransferResult> CopyPathAsync(string sourceRelativePath, string destinationRelativePath, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourcePath = ResolveWorkspacePath(sourceRelativePath);
        var destinationPath = ResolveWorkspacePath(destinationRelativePath);
        EnsureNotWorkspaceRoot(sourcePath);

        if (File.Exists(sourcePath))
        {
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir) && !Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }
            File.Copy(sourcePath, destinationPath, overwrite);
        }
        else if (Directory.Exists(sourcePath))
        {
            if (IsSubPath(sourcePath, destinationPath))
            {
                throw new IOException("Destination cannot be inside source directory.");
            }

            if (Directory.Exists(destinationPath))
            {
                if (!overwrite)
                {
                    throw new IOException("Destination directory already exists.");
                }
                Directory.Delete(destinationPath, recursive: true);
            }

            CopyDirectory(sourcePath, destinationPath);
        }
        else
        {
            throw new FileNotFoundException($"Source path not found: {sourceRelativePath}");
        }

        return Task.FromResult(new PathTransferResult
        {
            SourcePath = NormalizeWorkspaceRelativePath(sourcePath),
            DestinationPath = NormalizeWorkspaceRelativePath(destinationPath),
            Overwritten = overwrite
        });
    }

    public Task<PathTransferResult> MovePathAsync(string sourceRelativePath, string destinationRelativePath, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourcePath = ResolveWorkspacePath(sourceRelativePath);
        var destinationPath = ResolveWorkspacePath(destinationRelativePath);
        EnsureNotWorkspaceRoot(sourcePath);

        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Source path not found: {sourceRelativePath}");
        }

        if (File.Exists(destinationPath))
        {
            if (!overwrite)
            {
                throw new IOException("Destination file already exists.");
            }
            File.Delete(destinationPath);
        }
        else if (Directory.Exists(destinationPath))
        {
            if (!overwrite)
            {
                throw new IOException("Destination directory already exists.");
            }
            Directory.Delete(destinationPath, recursive: true);
        }

        var destinationDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDir) && !Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, destinationPath);
        }
        else
        {
            if (IsSubPath(sourcePath, destinationPath))
            {
                throw new IOException("Destination cannot be inside source directory.");
            }

            Directory.Move(sourcePath, destinationPath);
        }

        return Task.FromResult(new PathTransferResult
        {
            SourcePath = NormalizeWorkspaceRelativePath(sourcePath),
            DestinationPath = NormalizeWorkspaceRelativePath(destinationPath),
            Overwritten = overwrite
        });
    }

    public async Task<CommandExecutionResult> ExecuteCommandAsync(string command, string workingDirectory = ".", int? timeoutSeconds = null, CancellationToken cancellationToken = default)
    {
        var cmd = command?.Trim();
        if (string.IsNullOrWhiteSpace(cmd))
        {
            throw new ArgumentException("Command cannot be empty.", nameof(command));
        }

        var workingDirectoryPath = ResolveWorkspacePath(workingDirectory);
        if (!Directory.Exists(workingDirectoryPath))
        {
            throw new DirectoryNotFoundException($"Working directory not found: {workingDirectory}");
        }

        var timeout = timeoutSeconds.GetValueOrDefault(_skillsConfig.MaxCommandTimeoutSeconds);
        timeout = timeout <= 0 ? _skillsConfig.MaxCommandTimeoutSeconds : Math.Min(timeout, _skillsConfig.MaxCommandTimeoutSeconds);

        using var process = new Process
        {
            StartInfo = CreateShellStartInfo(cmd, workingDirectoryPath)
        };

        process.Start();
        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        var timedOut = false;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeout));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            TryKillProcess(process);
        }

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        var (finalStdOut, stdoutTruncated) = Truncate(stdOut, _skillsConfig.MaxCommandOutputLength);
        var (finalStdErr, stderrTruncated) = Truncate(stdErr, _skillsConfig.MaxCommandOutputLength);

        return new CommandExecutionResult
        {
            Command = cmd,
            WorkingDirectory = NormalizeWorkspaceRelativePath(workingDirectoryPath),
            ExitCode = process.HasExited ? process.ExitCode : -1,
            TimedOut = timedOut,
            StdOutTruncated = stdoutTruncated,
            StdErrTruncated = stderrTruncated,
            StdOut = finalStdOut,
            StdErr = finalStdErr
        };
    }

    private static ProcessStartInfo CreateShellStartInfo(string command, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "powershell.exe";
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(command);
        }
        else
        {
            startInfo.FileName = "/bin/bash";
            startInfo.ArgumentList.Add("-lc");
            startInfo.ArgumentList.Add(command);
        }

        return startInfo;
    }

    private string ResolveWorkspacePath(string relativePath)
    {
        var path = string.IsNullOrWhiteSpace(relativePath) ? "." : relativePath;
        var absolutePath = Path.GetFullPath(Path.Combine(_workspaceRootPath, path));

        if (!IsSubPath(_workspaceRootPath, absolutePath))
        {
            throw new UnauthorizedAccessException($"Path '{relativePath}' is outside WorkspaceRootPath.");
        }

        return absolutePath;
    }

    private string NormalizeWorkspaceRelativePath(string absolutePath)
    {
        var relativePath = Path.GetRelativePath(_workspaceRootPath, absolutePath).Replace('\\', '/');
        return string.IsNullOrWhiteSpace(relativePath) || relativePath == "." ? "." : relativePath;
    }

    private string ResolveSkillPath(SkillItem skillItem, string relativePath)
    {
        var path = string.IsNullOrWhiteSpace(relativePath) ? "." : relativePath;
        var skillRootPath = Path.GetFullPath(Path.Combine(_skillsRootPath, skillItem.SkillRootPath));
        var absolutePath = Path.GetFullPath(Path.Combine(skillRootPath, path));

        if (!IsSubPath(skillRootPath, absolutePath))
        {
            throw new UnauthorizedAccessException($"Path '{relativePath}' is outside SkillRootPath.");
        }

        return absolutePath;
    }

    private string NormalizePathForClient(string absolutePath)
    {
        if (IsSubPath(_workspaceRootPath, absolutePath))
        {
            return NormalizeWorkspaceRelativePath(absolutePath);
        }

        if (IsSubPath(_skillsRootPath, absolutePath))
        {
            var relativePath = Path.GetRelativePath(_skillsRootPath, absolutePath).Replace('\\', '/');
            return string.IsNullOrWhiteSpace(relativePath) || relativePath == "." ? "." : relativePath;
        }

        return absolutePath.Replace('\\', '/');
    }

    private void EnsureNotWorkspaceRoot(string absolutePath)
    {
        if (string.Equals(
            Path.TrimEndingDirectorySeparator(absolutePath),
            Path.TrimEndingDirectorySeparator(_workspaceRootPath),
            StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Operation on workspace root is not allowed.");
        }
    }

    private static bool IsSubPath(string rootPath, string fullPath)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var normalizedPath = Path.GetFullPath(fullPath);

        if (string.Equals(normalizedRoot, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        normalizedRoot += Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var filePath in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(filePath);
            var destinationFile = Path.Combine(destinationDir, fileName);
            File.Copy(filePath, destinationFile, overwrite: true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var destinationSubDir = Path.Combine(destinationDir, dirName);
            CopyDirectory(subDir, destinationSubDir);
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to kill process: {ex.Message}");
        }
    }

    private static (string Value, bool Truncated) Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return (string.Empty, false);
        }

        if (maxLength <= 0 || value.Length <= maxLength)
        {
            return (value, false);
        }

        return ($"{value[..maxLength]}...", true);
    }

    private static int CountOccurrences(string source, string value, StringComparison comparison)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var count = 0;
        var index = 0;
        while (true)
        {
            var found = source.IndexOf(value, index, comparison);
            if (found < 0)
            {
                break;
            }

            count++;
            index = found + value.Length;
        }

        return count;
    }

    private IEnumerable<string> EnumerateSkillEntryFiles()
    {
        if (!Directory.Exists(_skillsRootPath) || string.IsNullOrWhiteSpace(_skillsConfig.EntryFileName))
        {
            return [];
        }

        return Directory.GetFiles(_skillsRootPath, _skillsConfig.EntryFileName, SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<SkillItem?> ReadSkillItemAsync(string skillFilePath, CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllTextAsync(skillFilePath, cancellationToken);
            var entryFilePath = Path.GetRelativePath(_skillsRootPath, skillFilePath).Replace('\\', '/');
            var skillId = BuildSkillId(entryFilePath);
            var fallbackName = BuildFallbackName(entryFilePath);
            var name = ExtractTitle(content, fallbackName);
            var description = ExtractDescription(content, _skillsConfig.PreviewLength);
            var skillRootPath = Path.GetDirectoryName(entryFilePath)?.Replace('\\', '/') ?? string.Empty;

            return new SkillItem
            {
                ID = skillId,
                Name = name,
                Description = description,
                EntryFilePath = entryFilePath,
                SkillRootPath = skillRootPath,
                Content = content
            };
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to read skill file '{skillFilePath}': {ex.Message}");
            return null;
        }
    }

    private string BuildSkillId(string entryFilePath)
    {
        var normalizedPath = entryFilePath.Replace('\\', '/');
        var fileName = Path.GetFileName(normalizedPath);
        if (string.Equals(fileName, _skillsConfig.EntryFileName, StringComparison.OrdinalIgnoreCase))
        {
            var folder = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(folder))
            {
                return folder;
            }
        }

        return Path.ChangeExtension(normalizedPath, null) ?? normalizedPath;
    }

    private static string BuildFallbackName(string entryFilePath)
    {
        var normalizedPath = entryFilePath.Replace('\\', '/');
        var folder = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(folder))
        {
            var segments = folder.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                return segments[^1];
            }
        }

        return Path.GetFileNameWithoutExtension(normalizedPath);
    }

    private async Task<SkillItem?> FindSkillItemAsync(string? skillId, CancellationToken cancellationToken)
    {
        var id = skillId?.Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (var skillFilePath in EnumerateSkillEntryFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var skillItem = await ReadSkillItemAsync(skillFilePath, cancellationToken);
            if (skillItem == null)
            {
                continue;
            }

            if (string.Equals(skillItem.ID, id, StringComparison.OrdinalIgnoreCase))
            {
                return skillItem;
            }
        }

        return null;
    }

    private static string ExtractTitle(string content, string fallbackName)
    {
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
            {
                return trimmed.TrimStart('#').Trim();
            }
        }

        return fallbackName;
    }

    private static string ExtractDescription(string content, int maxLength)
    {
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            return LimitLength(trimmed, maxLength);
        }

        var compact = content.Replace("\r", " ").Replace("\n", " ").Trim();
        return LimitLength(compact, maxLength);
    }

    private static bool MatchQuery(SkillItem skillItem, string query)
    {
        return skillItem.ID.Contains(query, StringComparison.OrdinalIgnoreCase)
            || skillItem.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || skillItem.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static int CalculateScore(SkillItem skillItem, string query)
    {
        var score = 0;
        if (skillItem.ID.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        if (skillItem.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 4;
        }

        if (skillItem.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }

        if (skillItem.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
        }

        return score;
    }

    private static SkillInfo ToSkillInfo(SkillItem skillItem)
    {
        return new SkillInfo
        {
            ID = skillItem.ID,
            Name = skillItem.Name,
            Description = skillItem.Description,
            EntryFilePath = skillItem.EntryFilePath
        };
    }

    private static string LimitLength(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (maxLength <= 0 || value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..maxLength]}...";
    }

    private class SkillItem
    {
        public string ID { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string EntryFilePath { get; set; } = string.Empty;

        public string SkillRootPath { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }
}

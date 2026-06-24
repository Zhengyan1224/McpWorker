namespace Zhengyan.McpServer.Skills.Models;

public class FileEntry
{
    public string Path { get; set; } = string.Empty;

    public bool IsDirectory { get; set; }

    public long Length { get; set; }

    public DateTime LastWriteTimeUtc { get; set; }
}

public class PathInfoResult
{
    public string Path { get; set; } = string.Empty;

    public bool Exists { get; set; }

    public bool IsDirectory { get; set; }

    public long Length { get; set; }

    public DateTime? LastWriteTimeUtc { get; set; }
}

public class PathOperationResult
{
    public string Path { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;
}

public class PathTransferResult
{
    public string SourcePath { get; set; } = string.Empty;

    public string DestinationPath { get; set; } = string.Empty;

    public bool Overwritten { get; set; }
}

public class FileReadResult
{
    public string Path { get; set; } = string.Empty;

    public int Length { get; set; }

    public bool Truncated { get; set; }

    public string Content { get; set; } = string.Empty;
}

public class FileWriteResult
{
    public string Path { get; set; } = string.Empty;

    public bool Appended { get; set; }

    public int WrittenLength { get; set; }
}

public class FileLineItem
{
    public int LineNumber { get; set; }

    public string Content { get; set; } = string.Empty;
}

public class FileLinesResult
{
    public string Path { get; set; } = string.Empty;

    public int StartLine { get; set; }

    public int RequestedLineCount { get; set; }

    public int ReturnedLineCount { get; set; }

    public List<FileLineItem> Lines { get; set; } = new();
}

public class TextSearchMatch
{
    public string Path { get; set; } = string.Empty;

    public int LineNumber { get; set; }

    public int Column { get; set; }

    public string LineText { get; set; } = string.Empty;
}

public class TextSearchResult
{
    public string Query { get; set; } = string.Empty;

    public int ScannedFiles { get; set; }

    public bool Truncated { get; set; }

    public List<TextSearchMatch> Matches { get; set; } = new();
}

public class ReplaceTextResult
{
    public string Path { get; set; } = string.Empty;

    public int ReplacedCount { get; set; }
}

public class CommandExecutionResult
{
    public string Command { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public int ExitCode { get; set; }

    public bool TimedOut { get; set; }

    public bool StdOutTruncated { get; set; }

    public bool StdErrTruncated { get; set; }

    public string StdOut { get; set; } = string.Empty;

    public string StdErr { get; set; } = string.Empty;
}

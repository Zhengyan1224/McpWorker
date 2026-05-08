namespace Zhengyan.McpServer.Ssh.Services;

public interface ISshService
{
    Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken);

    Task<string> UploadAsync(string localPath, string remotePath, CancellationToken cancellationToken);
    Task<string> DownloadAsync(string remotePath, string localPath, CancellationToken cancellationToken);
}
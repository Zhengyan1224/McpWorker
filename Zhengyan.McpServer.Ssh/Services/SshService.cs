using System.Text.RegularExpressions;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Zhengyan.McpServer.Ssh.Config;
using Zhengyan.McpServer.Ssh.Utils;

namespace Zhengyan.McpServer.Ssh.Services;

public class SshService : ISshService
{
    private readonly SshConfig _sshConfig;

    private List<Regex> whiteList;
    private List<Regex> blackList;

    public SshService(SshConfig sshConfig)
    {
        _sshConfig = sshConfig ?? throw new ArgumentNullException(nameof(sshConfig));
        Initialize();
    }

    private void Initialize()
    {
        if (!string.IsNullOrEmpty(_sshConfig.WhiteList))
        {
            whiteList = _sshConfig.WhiteList.Split(',').Select(pattern => new Regex(pattern.Trim())).ToList();
        }

        if (!string.IsNullOrEmpty(_sshConfig.BlackList))
        {
            blackList = _sshConfig.BlackList.Split(',').Select(pattern => new Regex(pattern.Trim())).ToList();
        }
    }

    public async Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken)
    {
        if (!IsCommandAllowed(command))
        {
            throw new InvalidOperationException($"Command '{command}' is not allowed.");
        }
        using (var client = SshUtil.CreateSshClient(_sshConfig))
        {
            try
            {
                client.Connect();
                using (var cmd = client.CreateCommand(command))
                {
                    await cmd.ExecuteAsync(cancellationToken);
                    return cmd.Result;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to execute command: {command}", ex);
            }
            finally
            {
                if (client.IsConnected)
                {
                    client.Disconnect();
                }
            }
        }
    }

    public async Task<string> UploadAsync(string localPath, string remotePath, CancellationToken cancellationToken)
    {
        using (var client = SshUtil.CreateSftpClient(_sshConfig))
        {
            try
            {
                await Task.Run(() =>
                {
                    client.Connect();
                });
                using (FileStream fs = File.Open(localPath, FileMode.Open, FileAccess.Read))
                {
                    cancellationToken.ThrowIfCancellationRequested(); // 在关键操作前检查取消状态
                    await Task.Run(() =>
                    {
                        client.UploadFile(fs, remotePath);
                    }, cancellationToken); // 将同步方法包装为可取消的任务
                }
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException($"Upload of file: {localPath} to {remotePath} was canceled");
            }
            catch
            {
                throw;
            }
            finally
            {
                if (client.IsConnected)
                {
                    await Task.Run(() =>
                    {
                        client.Disconnect();
                    });
                }
            }
        }
        return $"Successfully uploaded {localPath} to {remotePath}";
    }
    public async Task<string> DownloadAsync(string remotePath, string localPath, CancellationToken cancellationToken)
    {
        using (var client = SshUtil.CreateSftpClient(_sshConfig))
        {
            try
            {
                await Task.Run(() =>
                {
                    client.Connect();
                });
                using (FileStream fs = File.Open(localPath, FileMode.Create, FileAccess.Write))
                {
                    cancellationToken.ThrowIfCancellationRequested(); // 在关键操作前检查取消状态
                    await Task.Run(() =>
                    {
                        client.DownloadFile(remotePath, fs);
                    }, cancellationToken); // 将同步方法包装为可取消的任务
                }
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException($"Download of file: {localPath} to {remotePath} was canceled");
            }
            catch
            {
                throw;
            }
            finally
            {
                if (client.IsConnected)
                {
                    await Task.Run(() =>
                    {
                        client.Disconnect();
                    });
                }
            }
        }
        return $"Successfully downloaded {localPath} to {remotePath}";
    }

    public bool IsCommandAllowed(string command)
    {
        bool isAllowed = true;
        if (whiteList != null && whiteList.Count > 0)
        {
            isAllowed = IsCommandInWhiteList(command);
        }
        if (blackList != null && blackList.Count > 0)
        {
            isAllowed &= !IsCommandInBlackList(command);
        }
        return isAllowed;
    }

    private bool IsCommandInWhiteList(string command)
    {
        return whiteList.Any(regex => regex.IsMatch(command));
    }
    private bool IsCommandInBlackList(string command)
    {
        return blackList.Any(regex => regex.IsMatch(command));
    }
}
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using ModelContextProtocol.Server;
using Serilog;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Zhengyan.McpServer.Ssh.Services;
namespace Zhengyan.McpServer.Ssh.Tools;

[McpServerToolType]
public static class SshTool
{

    [McpServerTool(Name = "execute-command"), Description("Execute command on connected server and get output result")]
    public static async Task<string> ExecuteCommandAsync(ISshService sshService, CancellationToken cancellationToken, [Description("Command to execute")] string cmdString)
    {
        try
        {
            Log.Debug($"Executing command: {cmdString}");
            var ret = await sshService.ExecuteCommandAsync(cmdString, cancellationToken);
            Log.Debug($"Command result: {ret}");
            return ret;
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}\n{ex.StackTrace}");
            return $"Failed to execute command: {cmdString}, error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "upload"), Description("Upload file to connected server")]
    public static async Task<string> UploadAsync(ISshService sshService, CancellationToken cancellationToken, [Description("Local path")] string localPath, [Description("Remote path")] string remotePath)
    {
        try
        {
            Log.Debug($"Uploading file: {localPath} to {remotePath}");
            return await sshService.UploadAsync(localPath, remotePath, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}\n{ex.StackTrace}");
            return $"Failed to upload file: {localPath} to {remotePath}, error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "download"), Description("Download file from connected server")]
    public static async Task<string> ExecuteCommand(ISshService sshService, CancellationToken cancellationToken, [Description("Remote path")] string remotePath, [Description("Local path")] string localPath)
    {
        try
        {
            Log.Debug($"Downloading file: {remotePath} to {localPath}");
            return await sshService.DownloadAsync(remotePath, localPath, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}\n{ex.StackTrace}");
            return $"Failed to download file: {remotePath} to {localPath}, error: {ex.Message}";
        }
    }

}
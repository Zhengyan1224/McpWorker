using Microsoft.Extensions.DependencyInjection;
using Renci.SshNet;
using Zhengyan.McpServer.Ssh.Config;
using Zhengyan.McpServer.Ssh.Services;

namespace Zhengyan.McpServer.Ssh.Utils;
public static class SshUtil
{
    public static SshClient CreateSshClient(SshConfig sshConfig)
    {
        if (sshConfig == null)
        {
            throw new ArgumentNullException(nameof(sshConfig));
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(sshConfig.PrivateKey))
            {
                return new SshClient(sshConfig.Host, sshConfig.UserName, new PrivateKeyFile(sshConfig.PrivateKey, string.IsNullOrEmpty(sshConfig.Passphrase) ? null : sshConfig.Passphrase));
            }
            else
            {
                return new SshClient(sshConfig.Host, sshConfig.UserName, sshConfig.Password);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to create SSH client.", ex);
        }
    }

    public static SftpClient CreateSftpClient(SshConfig sshConfig)
    {
        if (sshConfig == null)
        {
            throw new ArgumentNullException(nameof(sshConfig));
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(sshConfig.PrivateKey))
            {
                return new SftpClient(sshConfig.Host, sshConfig.UserName, new PrivateKeyFile(sshConfig.PrivateKey, string.IsNullOrEmpty(sshConfig.Passphrase) ? null : sshConfig.Passphrase));
            }
            else
            {
                return new SftpClient(sshConfig.Host, sshConfig.UserName, sshConfig.Password);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to create SSH client.", ex);
        }
    }

    public static IServiceCollection AddSshConfig(this IServiceCollection services, SshConfig sshConfig)
    {
        if (sshConfig == null)
        {
            throw new ArgumentNullException(nameof(sshConfig));
        }

        services.AddSingleton(sshConfig);
        return services;
    }

    public static IServiceCollection AddSshService(this IServiceCollection services)
    {
        services.AddSingleton<ISshService, SshService>();
        return services;
    }
}
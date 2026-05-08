using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using Zhengyan.McpServer.Ssh.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using System.Collections;
using Zhengyan.Commons.Web;
using Zhengyan.McpServer.Ssh.Config;
using Zhengyan.McpServer.Ssh.Utils;
using Serilog;
using Zhengyan.Commons.Web.Extensions;

namespace Zhengyan.McpServer.Ssh;

public class Program
{
    /// <summary>
    /// Main entry point for the application.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static async Task<int> Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddProfiles("profiles")
            .AddCommandLine(args)
            .Build();
        config.ConfigureSerilog();
        var mode = config["mode"] ?? "stdio";
        var stateless = config.GetValue<bool?>("stateless") ?? false;

        // var envs = Environment.GetEnvironmentVariables();
        // Console.WriteLine("Environment Variables:");
        // foreach (DictionaryEntry env in envs)
        // {
        //     Console.WriteLine($"{env.Key}\t=\t{env.Value}");
        // }
        // Console.WriteLine("===================================");



        var sshConfig = new SshConfig();
        config.Bind(sshConfig);

        Log.Debug($"SSH Config:\n{sshConfig.ToString()}\n=======================================================");

        if (string.Equals("stdio", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunStdioServerAsync(args, sshConfig);
        }
        else if (string.Equals("sse", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunHttpServerAsync(args, sshConfig, enableLegacySse: true, stateless: stateless);
        }
        else if (string.Equals("streamablehttp", mode, StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals("http", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunHttpServerAsync(args, sshConfig, enableLegacySse: false, stateless: stateless);
        }
        else
        {
            Log.Error($"Unknown mode {mode}");
        }
        return 1;
    }

    private static async Task<int> RunStdioServerAsync(string[] args, SshConfig sshConfig)
    {
        try
        {
            Log.Information("Starting MCP Server [Stdio]...");
            var builder = Host.CreateEmptyApplicationBuilder(settings: null);
            builder.Services
                .AddSshConfig(sshConfig)
                .AddSshService()
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
            await builder.Build().RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error($"Host terminated unexpectedly : {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunHttpServerAsync(string[] args, SshConfig sshConfig, bool enableLegacySse, bool stateless)
    {
        try
        {
            var effectiveStateless = stateless;
            if (enableLegacySse && stateless)
            {
                Log.Warning("Legacy SSE requires stateful mode, forcing stateless = false.");
                effectiveStateless = false;
            }

            Log.Information($"Starting MCP Server [Http], LegacySse = {enableLegacySse}, Stateless = {effectiveStateless}...");
            var builder = WebApplication.CreateBuilder(args);
            builder.Services
                .AddSshConfig(sshConfig)
                .AddSshService()
                .AddMcpServer()
                .WithHttpTransport(options =>
                {
                    options.Stateless = effectiveStateless;
#pragma warning disable MCP9004
                    options.EnableLegacySse = enableLegacySse;
#pragma warning restore MCP9004
                })
                .WithToolsFromAssembly();

            var app = builder.Build();

            app.MapMcp("ssh");

            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error($"Host terminated unexpectedly : {ex.Message}");
            return 1;
        }
    }

}

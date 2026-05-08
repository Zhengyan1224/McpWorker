using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Zhengyan.Commons.Web;
using Zhengyan.Commons.Web.Extensions;
using Zhengyan.McpServer.DataQuery.Config;
using Zhengyan.McpServer.DataQuery.Services;
using Zhengyan.McpServer.DataQuery.Utils;

namespace Zhengyan.McpServer.DataQuery;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddProfiles("profiles")
            .AddCommandLine(args)
            .Build();

        config.ConfigureSerilog();
        var mode = (config["mode"] ?? "stdio").Trim();
        var stateless = config.GetValue<bool?>("stateless") ?? false;

        var dataQueryConfig = new DataQueryConfig();
        config.Bind(dataQueryConfig);

        Log.Debug($"DataQuery Config:\n{dataQueryConfig}\n=======================================================");

        if (string.Equals("buildcache", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunCacheBuildAsync(dataQueryConfig);
        }

        var ensureCacheExitCode = await EnsureCacheBuiltAsync(args, dataQueryConfig);
        if (ensureCacheExitCode != 0)
        {
            return ensureCacheExitCode;
        }

        if (string.Equals("stdio", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunStdioServerAsync(dataQueryConfig);
        }

        if (string.Equals("sse", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunHttpServerAsync(args, dataQueryConfig, enableLegacySse: true, stateless: stateless);
        }

        if (string.Equals("streamablehttp", mode, StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals("http", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunHttpServerAsync(args, dataQueryConfig, enableLegacySse: false, stateless: stateless);
        }

        Log.Error("Unknown mode {Mode}", mode);
        return 1;
    }

    private static async Task<int> EnsureCacheBuiltAsync(string[] args, DataQueryConfig dataQueryConfig)
    {
        if (DataQueryService.HasCompatibleCache(dataQueryConfig))
        {
            return 0;
        }

        Log.Information("Data query cache is missing or stale. Building it in a dedicated helper process before starting the server.");
        using var process = Process.Start(CreateCacheBuildStartInfo(args));
        if (process == null)
        {
            Log.Error("Failed to start the cache builder process.");
            return 1;
        }

        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            Log.Error("Cache builder process exited with code {ExitCode}.", process.ExitCode);
            return process.ExitCode;
        }

        if (!DataQueryService.HasCompatibleCache(dataQueryConfig))
        {
            Log.Error("Cache builder process completed, but the cache is still unavailable.");
            return 1;
        }

        return 0;
    }

    private static ProcessStartInfo CreateCacheBuildStartInfo(string[] args)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Current process path is unavailable.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            UseShellExecute = false
        };

        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(entryAssemblyPath))
            {
                throw new InvalidOperationException("Entry assembly path is unavailable.");
            }

            startInfo.ArgumentList.Add(entryAssemblyPath);
        }

        foreach (var arg in FilterCacheBuildArgs(args))
        {
            startInfo.ArgumentList.Add(arg);
        }

        startInfo.ArgumentList.Add("--mode=buildcache");
        return startInfo;
    }

    private static IEnumerable<string> FilterCacheBuildArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(arg, "--mode", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                continue;
            }

            yield return arg;
        }
    }

    private static async Task<int> RunCacheBuildAsync(DataQueryConfig dataQueryConfig)
    {
        try
        {
            Log.Information("Starting MCP Server cache build helper...");
            var dataQueryService = new DataQueryService(dataQueryConfig);
            await dataQueryService.ReloadAsync();
            Log.Information("Data query cache build completed.");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Cache build helper terminated unexpectedly");
            return 1;
        }
    }

    private static async Task<int> RunStdioServerAsync(DataQueryConfig dataQueryConfig)
    {
        try
        {
            Log.Information("Starting MCP Server [Stdio]...");
            var builder = Host.CreateEmptyApplicationBuilder(settings: null);
            await builder.Services
                .AddDataQueryServiceAsync(dataQueryConfig);
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();

            await builder.Build().RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Host terminated unexpectedly");
            return 1;
        }
    }

    private static async Task<int> RunHttpServerAsync(string[] args, DataQueryConfig dataQueryConfig, bool enableLegacySse, bool stateless)
    {
        try
        {
            var effectiveStateless = stateless;
            if (enableLegacySse && stateless)
            {
                Log.Warning("Legacy SSE requires stateful mode, forcing stateless = false.");
                effectiveStateless = false;
            }

            Log.Information("Starting MCP Server [Http], LegacySse = {EnableLegacySse}, Stateless = {Stateless}...", enableLegacySse, effectiveStateless);
            var builder = WebApplication.CreateBuilder(args);
            await builder.Services
                .AddDataQueryServiceAsync(dataQueryConfig);
            builder.Services
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
            app.MapMcp("dataquery");

            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Host terminated unexpectedly");
            return 1;
        }
    }
}

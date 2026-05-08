using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Zhengyan.Commons.Web;
using Zhengyan.Commons.Web.Extensions;
using Zhengyan.McpServer.Memory.Config;
using Zhengyan.McpServer.Memory.Utils;

namespace Zhengyan.McpServer.Memory;

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

        var memoryConfig = new MemoryConfig();
        config.Bind(memoryConfig);

        Log.Debug("Memory Config:\n{Config}\n=======================================================", memoryConfig);

        if (string.Equals("stdio", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunStdioServerAsync(memoryConfig);
        }

        if (string.Equals("sse", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunHttpServerAsync(args, memoryConfig, enableLegacySse: true, stateless: stateless);
        }

        if (string.Equals("streamablehttp", mode, StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals("http", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunHttpServerAsync(args, memoryConfig, enableLegacySse: false, stateless: stateless);
        }

        Log.Error("Unknown mode {Mode}", mode);
        return 1;
    }

    private static async Task<int> RunStdioServerAsync(MemoryConfig memoryConfig)
    {
        try
        {
            Log.Information("Starting MCP Server [Stdio]...");
            var builder = Host.CreateEmptyApplicationBuilder(settings: null);
            await builder.Services.AddMemoryServiceAsync(memoryConfig);
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

    private static async Task<int> RunHttpServerAsync(string[] args, MemoryConfig memoryConfig, bool enableLegacySse, bool stateless)
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
            await builder.Services.AddMemoryServiceAsync(memoryConfig);
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
            app.MapMcp("memory");

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

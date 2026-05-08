using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Zhengyan.Commons.Web;
using Zhengyan.Commons.Web.Extensions;
using Zhengyan.McpServer.Skills.Config;
using Zhengyan.McpServer.Skills.Utils;

namespace Zhengyan.McpServer.Skills;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddProfiles("profiles")
            .AddCommandLine(args)
            .Build();
        config.ConfigureSerilog();
        var mode = config["mode"] ?? "stdio";
        var stateless = config.GetValue<bool?>("stateless") ?? false;

        var skillsConfig = new SkillsConfig();
        config.Bind(skillsConfig);

        Log.Debug($"Skills Config:\n{skillsConfig}\n=======================================================");

        if (string.Equals("stdio", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunStdioServerAsync(skillsConfig);
        }

        if (string.Equals("sse", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunHttpServerAsync(args, skillsConfig, enableLegacySse: true, stateless: stateless);
        }

        if (string.Equals("streamablehttp", mode, StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals("http", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunHttpServerAsync(args, skillsConfig, enableLegacySse: false, stateless: stateless);
        }

        Log.Error($"Unknown mode {mode}");
        return 1;
    }

    private static async Task<int> RunStdioServerAsync(SkillsConfig skillsConfig)
    {
        try
        {
            Log.Information("Starting MCP Server [Stdio]...");
            var builder = Host.CreateEmptyApplicationBuilder(settings: null);
            builder.Services
                .AddSkillsService(skillsConfig)
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
            await builder.Build().RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error($"Host terminated unexpectedly : {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunHttpServerAsync(string[] args, SkillsConfig skillsConfig, bool enableLegacySse, bool stateless)
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
                .AddSkillsService(skillsConfig)
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
            app.MapMcp("skills");

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

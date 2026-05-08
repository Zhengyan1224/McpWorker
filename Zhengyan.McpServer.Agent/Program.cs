using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using Zhengyan.Commons.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using System.Collections;
using Serilog;
using Zhengyan.McpServer.Agent.Config;
using Zhengyan.McpServer.Agent.Services;
using System.Reflection;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Zhengyan.McpServer.Agent.Custom;
using Zhengyan.Commons.Web.Extensions;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace Zhengyan.McpServer.Agent;

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



        var agentConfig = new AgentConfig();
        config.Bind(agentConfig);


        Log.Debug($"Agent Config:\n{agentConfig.ToString()}\n=======================================================");

        if (string.Equals("stdio", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunStdioServerAsync(args, agentConfig);
        }
        else if (string.Equals("sse", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunHttpServerAsync(args, agentConfig, enableLegacySse: true, stateless: stateless);
        }
        else if (string.Equals("streamablehttp", mode, StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals("http", mode, StringComparison.CurrentCultureIgnoreCase))
        {
            return await RunHttpServerAsync(args, agentConfig, enableLegacySse: false, stateless: stateless);
        }
        else
        {
            Log.Error($"Unknown mode {mode}");
        }
        return 1;
    }


    private static async ValueTask<ListToolsResult> ListToolsHandlerAsync(RequestContext<ListToolsRequestParams> request, CancellationToken cancellationToken)
    {
        var agentConfig = request.Services.GetRequiredService<AgentConfig>();
        return new ListToolsResult()
        {
            Tools =
                    [
                        new Tool()
                        {
                            Name = "send_task",
                            Description = agentConfig.ToolDescription,
                            InputSchema = JsonSerializer.Deserialize<JsonElement>($@"
                                    {{
                                    ""type"": ""object"",
                                    ""properties"": {{
                                    ""instruction"": {{
                                        ""type"": ""string"",
                                        ""description"": ""{agentConfig.ArgumentDescription}""
                                    }}
                                    }},
                                    ""required"": [""instruction""]
                                }}
                                "),
                        }
                    ]
        };
    }

    private static async ValueTask<CallToolResult> CallToolHandlerAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
    {
        if (request.Params?.Name == "send_task")
        {
            if (request.Params.Arguments?.TryGetValue("instruction", out var instruction) is not true)
            {
                throw new McpException("Missing required argument 'instruction'");
            }

            IAgentService agentService = request.Services.GetRequiredService<IAgentService>();
            var ret = await agentService.SendTaskAsync(instruction.GetString(), cancellationToken);

            return new CallToolResult()
            {
                Content = [new TextContentBlock() { Text = ret }]
            };
        }

        throw new McpException($"Unknown tool: '{request.Params?.Name}'");
    }


    private static async Task<int> RunStdioServerAsync(string[] args, AgentConfig agentConfig)
    {
        try
        {
            Log.Information("Starting MCP Server [Stdio]...");
            var builder = Host.CreateEmptyApplicationBuilder(settings: null);
            builder.Services
                .AddSingleton(agentConfig)
                .AddSingleton<IAgentService, AgentService>()
                .AddMcpServer()
                .WithStdioServerTransport()
                // .WithTools([typeof(AgentTool)], [new McpServerToolCreateOptions { Description = agentConfig.ToolDescription }]);
                .WithListToolsHandler(ListToolsHandlerAsync)
                .WithCallToolHandler(CallToolHandlerAsync);
            await builder.Build().RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error($"Host terminated unexpectedly : {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunHttpServerAsync(string[] args, AgentConfig agentConfig, bool enableLegacySse, bool stateless)
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
                .AddSingleton(agentConfig)
                .AddSingleton<IAgentService, AgentService>()
                .AddMcpServer()
                .WithHttpTransport(options =>
                {
                    options.Stateless = effectiveStateless;
#pragma warning disable MCP9004
                    options.EnableLegacySse = enableLegacySse;
#pragma warning restore MCP9004
                })
                // .WithTools([typeof(AgentTool)], [new McpServerToolCreateOptions { Description = agentConfig.ToolDescription }]);
                .WithListToolsHandler(ListToolsHandlerAsync)
                .WithCallToolHandler(CallToolHandlerAsync);
            var app = builder.Build();

            app.MapMcp("agent");

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

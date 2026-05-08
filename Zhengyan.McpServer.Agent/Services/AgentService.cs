using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using Serilog;
using Zhengyan.McpServer.Agent.Config;

namespace Zhengyan.McpServer.Agent.Services;

public class AgentService : IAgentService
{
    private readonly ModelContextProtocol.Server.McpServer _thisServer;
    private readonly AgentConfig _agentConfig;


    public AgentService(ModelContextProtocol.Server.McpServer thisServer, AgentConfig agentConfig)
    {
        _thisServer = thisServer;
        _agentConfig = agentConfig;
    }

    
    public async Task<string> SendTaskAsync(string instruction, CancellationToken cancellationToken)
    {
        try
        {
            Log.Debug($"Instruction: {instruction}");
            List<ChatMessage> messages = string.IsNullOrWhiteSpace(_agentConfig.SystemPrompt) ? new() :
            [
                new(ChatRole.System, _agentConfig.SystemPrompt),
            ];

            messages.Add(new(ChatRole.User, instruction));

            ChatOptions options = new()
            {
                MaxOutputTokens = _agentConfig.MaxOutputTokens,
                Temperature = _agentConfig.Temperature,
                TopP = _agentConfig.TopP,
                TopK = _agentConfig.TopK,
            };

            var ret = await _thisServer.AsSamplingChatClient().GetResponseAsync(messages, options, cancellationToken);
            var result = ret.Text;
            Log.Debug($"Task result: {result}");
            return result;
        }
        catch (Exception ex)
        {
            Log.Error($"Error: {ex.Message}\n{ex.StackTrace}");
            return $"Error: {ex.Message}";
        }
    }
}

using ModelContextProtocol.Server;

namespace Zhengyan.McpServer.Agent.Services;
public interface IAgentService
{
    Task<string> SendTaskAsync(string instruction, CancellationToken cancellationToken);
}
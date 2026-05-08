using ModelContextProtocol.Server;

namespace Zhengyan.McpServer.Divine.Services;
public interface IDivineService
{
    string Divine(int num1,int num2,int num3);
    Task<string> InterpretAsync(string divination,string question, ModelContextProtocol.Server.McpServer thisServer,CancellationToken cancellationToken);
}

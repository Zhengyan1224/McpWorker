using ModelContextProtocol.Server;

namespace Zhengyan.McpServer.EightChar.Services;
public interface IEightCharCalculationService
{
    string Calculation(int year, int month, int day, int hour,int gender);
    Task<string> InterpretAsync(string eightChar,string question, ModelContextProtocol.Server.McpServer thisServer,CancellationToken cancellationToken);
}

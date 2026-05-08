using ModelContextProtocol.Client;
using Zhengyan.WebSearch;

public class QueryCertInfoMcpTest
{
    public static async Task Run(string[] args)
    {
        await using var mcpClient = await McpClientFactory.CreateAsync(new SseClientTransport(new()
        {
            Name = "QueryCertInfo MCP Server",
            Endpoint = new Uri("http://127.0.0.1:5002/sse"),
        }));

        var tools = await mcpClient.ListToolsAsync();
        foreach (var tool in tools)
        {
            Console.WriteLine($"{tool.Name}\t:\t({tool.Description})\t:\t{tool.JsonSchema}");
        }

        var result = await mcpClient.CallToolAsync("QueryCertInfo", 
            new Dictionary<string, object?>() { ["idCard"] = "430423199612014719"});
        Console.WriteLine("QueryCertInfo Result:");
        foreach(var rc in result.Content)
        {
            Console.WriteLine($"{rc.ToString()}");
        }
    }
}
using ModelContextProtocol.Client;
using Zhengyan.WebSearch;

public class SSHMcpTest
{
    public static async Task Run(string[] args)
    {
        await using var mcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
        {
            Name = "SSH MCP Server",
            Command = "/usr/bin/npx",
            Arguments = [
                "-y",
                "@fangjunjie/ssh-mcp-server",
                "--host",
                "127.0.0.1",
                "--port",
                "22",
                "--username",
                "zhengyan",
                "--password",
                "974400763"
            ],

        }));

        var tools = await mcpClient.ListToolsAsync();
        foreach (var tool in tools)
        {
            Console.WriteLine($"{tool.Name}\t:\t({tool.Description})\t:\t{tool.JsonSchema}");
        }

        
    }
}
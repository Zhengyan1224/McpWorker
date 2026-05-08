using ModelContextProtocol.Client;
using Zhengyan.WebSearch;

public class WebSearcherMcpTest
{
    public static async Task Run(string[] args)
    {
        await using var mcpClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
        {
            Name = "Web Searcher MCP Server",
            Command = "dotnet",
            Arguments = [
                "run",
                "--project",
                "../Zhengyan.McpServer.WebSearcher"
            ],
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["SEARCH_ENGINE"] = "bing",
                ["CRAWLER"] = "markdown"
            },
            WorkingDirectory = "../Zhengyan.McpServer.WebSearcher"
        }));

        var tools = await mcpClient.ListToolsAsync();
        foreach (var tool in tools)
        {
            Console.WriteLine($"{tool.Name}\t:\t({tool.Description})\t:\t{tool.JsonSchema}");
        }

        var result = await mcpClient.CallToolAsync("CrawlUrls", 
            new Dictionary<string, object?>() { ["urls"] = new string[]{
                "http://www.xiongan.gov.cn/2021-04/27/c_1211131221.htm",
                "https://www.nxzfgjj.com/"
                }
            });
        Console.WriteLine("CrawlUrls Result:");
        foreach(var rc in result.Content)
        {
            Console.WriteLine($"{rc.ToString()}");
        }
    }
}
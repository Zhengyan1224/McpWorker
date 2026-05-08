using ModelContextProtocol.Server;
using Serilog;
using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Zhengyan.McpServer.WebSearcher.Services;
using Zhengyan.WebSearch;
namespace Zhengyan.McpServer.WebSearcher.Tools;

[McpServerToolType]
public static class WebSearcherTool
{
    [McpServerTool, Description("After the keywords are entered, call the search engine to obtain the search results.")]
    public static async Task<string> SearchAsync(IWebSearcherService webSearcherService, CancellationToken cancellationToken, [Description("The keywords passed to the search engine. If there are multiple keywords, they can be separated by spaces.")] string keywords, [Description("The page number of the search results (starting from page 1). When the search results span multiple pages, you can specify this parameter to obtain the desired page of results.")] int page = 1)
    {
        if (string.IsNullOrWhiteSpace(keywords))
            return $"Keywords cannot be empty!";
        Log.Debug($"Keywords: {keywords}\tPage: {page}");
        var ret = JsonSerializer.Serialize(await webSearcherService.SearchAsync(keywords, page, cancellationToken), new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });
        Log.Debug($"Search result: {ret}");
        return ret;
    }

    [McpServerTool, Description("This function is used to crawl the content of a given collection of websites (urls) concurrently. After crawling, it serializes the results and returns them as a JSON - formatted string.")]
    public static async Task<string> CrawlUrlsAsync(IWebSearcherService webSearcherService, CancellationToken cancellationToken, [Description("The collection of website URLs to be crawled")] List<string> urls)
    {
        Log.Debug($"Urls: {string.Join(",", urls)}");
        var ret = JsonSerializer.Serialize(await webSearcherService.CrawlUrlsAsync(urls, cancellationToken), new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });
        Log.Debug($"Crawl result: {ret}");
        return ret;
    }
}
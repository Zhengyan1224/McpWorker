using Serilog;
using Zhengyan.WebSearch;
using Zhengyan.WebSearch.Crawler;
using Zhengyan.WebSearch.SearchEngine;

namespace Zhengyan.McpServer.WebSearcher.Services;

public class WebSearcherService : IWebSearcherService
{
    private readonly ISearchEngine _searchEngine;
    private readonly ICrawler _crawler;

    public WebSearcherService(ISearchEngine searchEngine, ICrawler crawler)
    {
        _searchEngine = searchEngine;
        _crawler = crawler;
    }

    public async Task<List<CrawlResult>> CrawlUrlsAsync(List<string> urls, CancellationToken cancellationToken = default)
    {
        Log.Debug($"Crawl urls: {string.Join(", ", urls)}");
        return await _crawler.CrawlUrlsAsync(urls, cancellationToken);
    }

    public async Task<SearchResult[]> SearchAsync(string keywords, int page = 1, CancellationToken cancellationToken = default)
    {
        keywords = keywords.Replace(" ", "%20").Replace(":", "%3A");
        Log.Debug($"Search keywords: {keywords}, page: {page}");
        return await _searchEngine.SearchAsync(keywords, page, cancellationToken);
    }
}

    
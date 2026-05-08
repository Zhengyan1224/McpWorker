using Zhengyan.WebSearch;
using Zhengyan.WebSearch.Crawler;
using Zhengyan.WebSearch.SearchEngine;

namespace Zhengyan.McpServer.WebSearcher.Services;

public interface IWebSearcherService
{

    /// <summary>
    /// 搜索
    /// </summary>
    /// <param name="keywords">关键词</param>
    /// <param name="page">页码（从1开始）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>搜索结果</returns>
    Task<SearchResult[]> SearchAsync(string keywords,  int page = 1, CancellationToken cancellationToken = default);


    /// <summary>
    /// 爬取网页
    /// </summary>
    /// <param name="urls">要爬取的URL列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>爬取结果</returns>
    Task<List<CrawlResult>> CrawlUrlsAsync(List<string> urls,CancellationToken cancellationToken = default);
}
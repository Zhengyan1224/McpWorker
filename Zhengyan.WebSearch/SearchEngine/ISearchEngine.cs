using System.Net;

namespace Zhengyan.WebSearch.SearchEngine;

public interface ISearchEngine
{
    /// <summary>
    /// 搜索引擎名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Cookie容器
    /// </summary>
    CookieContainer Cookies { get; }

    string BaseUrl { get; }

    HttpClient HttpClient { get; }

    /// <summary>
    /// 搜索方法
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="page">页码</param>
    /// <returns>搜索结果</returns>
    Task<SearchResult[]> SearchAsync(string keyword, int page = 1, CancellationToken cancellationToken = default);

    Task<string> InitializeAsync();
}
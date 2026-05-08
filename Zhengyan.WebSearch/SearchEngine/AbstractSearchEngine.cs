using System.Net;
using ReverseMarkdown.Converters;

namespace Zhengyan.WebSearch.SearchEngine;

public abstract class AbstractSearchEngine : ISearchEngine
{
    /// <summary>
    /// 搜索引擎名称
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Cookie容器
    /// </summary>
    public abstract CookieContainer Cookies { get; }

    public abstract string BaseUrl { get; }

    public abstract HttpClient HttpClient { get; }

    /// <summary>
    /// 搜索方法
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="page">页码</param>
    /// <returns>搜索结果</returns>
    public abstract Task<SearchResult[]> SearchAsync(string keyword, int page = 1, CancellationToken cancellationToken = default);

    public virtual CookieCollection GetCookies() => Cookies.GetCookies(new Uri(BaseUrl));

    public virtual string GetCookieString() => string.Join("; ", GetCookies().Cast<Cookie>().Select(c => $"{c.Name}={c.Value}"));

    public virtual async Task<string> InitializeAsync()
    {
        using (var response = await HttpClient.GetAsync(BaseUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            // var headers = response.Headers;
            // var content = await response.Content.ReadAsStringAsync(); // 释放连接
        }
        return GetCookieString();
    }
}
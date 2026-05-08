using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Common;
using HtmlAgilityPack;

namespace Zhengyan.WebSearch.SearchEngine
{
    public class BaiduSearchEngine : AbstractSearchEngine
    {
        private readonly HttpClient _client;
        private static readonly SemaphoreSlim _throttler = new SemaphoreSlim(5);
        private readonly CookieContainer _cookieContainer = new CookieContainer();

        public BaiduSearchEngine()
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                // AllowAutoRedirect = true,
                // AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = true,
                CookieContainer = _cookieContainer  // 启用Cookie容器
            };
            _client = new HttpClient(handler);
            _client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/68.0.3440.106 Safari/537.36");
            _client.DefaultRequestHeaders.Add("Referer", "https://www.baidu.com/");
            _client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
        }

        public override string Name => "Baidu";

        private readonly string baseUrl = "https://www.baidu.com/";
        public override string BaseUrl { get => baseUrl; }

        public override HttpClient HttpClient => _client;

        // Cookie访问器
        public override CookieContainer Cookies => _cookieContainer;

        public override async Task<SearchResult[]> SearchAsync(string keyword, int page = 1, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                throw new ArgumentException("关键词不能为空！", nameof(keyword));

            string encodedKeyword = Uri.EscapeDataString(keyword);
            string searchUrl = $"{BaseUrl}s?ie=utf-8&tn=baidu&wd={encodedKeyword}&pn={(page - 1) * 10}";
            Console.WriteLine(searchUrl);

            
            try
            {
                var response = await _client.GetAsync(searchUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                // 读取响应后会更新cookieContainer中的Cookie
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                // Console.WriteLine(html);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var resultNodes = doc.DocumentNode.SelectNodes("//div[@id='content_left']/div[contains(@class, 'c-container')]");
                if (resultNodes == null) return Array.Empty<SearchResult>();

                var baseResults = new List<BaseResult>();
                foreach (var node in resultNodes)
                {
                    var titleNode = node.SelectSingleNode(".//h3/a");
                    if (titleNode == null) continue;

                    var title = WebUtility.HtmlDecode(titleNode.InnerText.Trim());
                    var url = titleNode.GetAttributeValue("href", "").Trim();
                    var abstractNode = node.SelectSingleNode(".//div[contains(@class, 'c-abstract')]")
                                      ?? node.SelectSingleNode(".//div[@class='c-row']")
                                      ?? node.SelectSingleNode(".//div");

                    baseResults.Add(new BaseResult
                    {
                        Title = title,
                        Url = url,
                        Snippet = abstractNode != null ? WebUtility.HtmlDecode(abstractNode.InnerText.Trim()) : "无摘要"
                    });
                }

                var redirectTasks = new List<Task<SearchResult>>();
                foreach (var item in baseResults)
                {
                    redirectTasks.Add(ProcessResultWithRedirectAsync(item, cancellationToken));
                }

                var results = await Task.WhenAll(redirectTasks);
                return results.Where(r => r != null).ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"百度搜索错误: {ex.Message}");
                return Array.Empty<SearchResult>();
            }
        }

        private async Task<SearchResult> ProcessResultWithRedirectAsync(BaseResult baseResult, CancellationToken cancellationToken)
        {
            try
            {
                var url = baseResult.Url;

                // 处理百度跳转链接时携带Cookie
                if (url.StartsWith("http://www.baidu.com/link?url=") ||
                    url.StartsWith("https://www.baidu.com/link?url="))
                {
                    await _throttler.WaitAsync(cancellationToken);
                    try
                    {
                        url = await ResolveRedirectUrl(url, cancellationToken);
                    }
                    finally
                    {
                        _throttler.Release();
                    }
                }

                return new SearchResult
                {
                    Title = baseResult.Title,
                    Url = url,
                    Snippet = baseResult.Snippet
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> ResolveRedirectUrl(string redirectUrl, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await _client.GetAsync(redirectUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                return response.RequestMessage.RequestUri.ToString();
            }
            catch
            {
                return redirectUrl;
            }
        }

        private class BaseResult
        {
            public string Title { get; set; }
            public string Url { get; set; }
            public string Snippet { get; set; }
        }
    }
}
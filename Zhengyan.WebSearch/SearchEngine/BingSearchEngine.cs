using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Zhengyan.WebSearch.SearchEngine
{
    public class BingSearchEngine : AbstractSearchEngine
    {
        private readonly HttpClient _client;

        private readonly CookieContainer _cookieContainer = new CookieContainer();

        public BingSearchEngine()
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                // AllowAutoRedirect = true,
                // AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = true,
                CookieContainer = _cookieContainer  // 启用Cookie容器
            };
            _client = new HttpClient(handler);
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36 Edg/91.0.864.59");
            _client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
        }

        public override string Name => "Bing";

        private readonly string baseUrl = "https://www.bing.com/";
        public override string BaseUrl { get => baseUrl; }

        public override HttpClient HttpClient => _client;
        
        // Cookie访问器
        public override CookieContainer Cookies => _cookieContainer;

        public override async Task<SearchResult[]> SearchAsync(string keyword, int page = 1, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                throw new ArgumentException("关键词不能为空！", nameof(keyword));

            string encodedKeyword = Uri.EscapeDataString(keyword);
            string searchUrl = $"{BaseUrl}search?q={encodedKeyword}&first={(page - 1) * 10 + 1}";
            Console.WriteLine(searchUrl);
            try
            {
                HttpResponseMessage response = await _client.GetAsync(searchUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                string html = await response.Content.ReadAsStringAsync(cancellationToken);
                // Console.WriteLine(html);
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                var resultNodes = doc.DocumentNode.SelectNodes("//li[@class='b_algo']");

                if (resultNodes == null)
                    return Array.Empty<SearchResult>();

                SearchResult[] results = new SearchResult[resultNodes.Count];

                for (int i = 0; i < resultNodes.Count; i++)
                {
                    var node = resultNodes[i];

                    // 提取标题
                    var titleNode = node.SelectSingleNode(".//h2");
                    string title = titleNode?.InnerText?.Trim() ?? "无标题";

                    // 提取URL
                    var urlNode = titleNode?.SelectSingleNode(".//a");
                    string url = urlNode?.GetAttributeValue("href", "") ?? "";

                    // 提取摘要
                    var snippetNode = node.SelectSingleNode(".//p");
                    string snippet = snippetNode?.InnerText?.Trim() ?? "无摘要";

                    results[i] = new SearchResult
                    {
                        Title = title,
                        Url = url,
                        Snippet = snippet
                    };
                }

                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误: {ex.Message}");
                return Array.Empty<SearchResult>();
            }
        }
    }
}
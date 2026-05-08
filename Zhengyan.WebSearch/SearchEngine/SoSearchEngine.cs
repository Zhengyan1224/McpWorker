using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Zhengyan.WebSearch.SearchEngine
{
    public class SoSearchEngine : AbstractSearchEngine
    {
        private readonly HttpClient _client;
        private readonly CookieContainer _cookieContainer = new CookieContainer();
        public SoSearchEngine()
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                // AllowAutoRedirect = true,
                // AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = true,
                CookieContainer = _cookieContainer  // 启用Cookie容器
            };
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36 Edg/91.0.864.59");
            _client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
        }

        public override string Name => "360";

        public override CookieContainer Cookies => _cookieContainer;

        private readonly string baseUrl = "https://www.so.com/";
        public override string BaseUrl { get => baseUrl; }

        public override HttpClient HttpClient => _client;

        public override async Task<SearchResult[]> SearchAsync(string keyword, int page = 1, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                throw new ArgumentException("关键词不能为空！", nameof(keyword));
            string encodedKeyword = Uri.EscapeDataString(keyword);
            string searchUrl = $"{BaseUrl}s?q={encodedKeyword}&pn={(page - 1) * 10}";

            Console.WriteLine(searchUrl);
            try
            {
                HttpResponseMessage response = await _client.GetAsync(searchUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                string html = await response.Content.ReadAsStringAsync(cancellationToken);
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                // 360的搜索结果通常在<li class="res-list">中
                var resultNodes = doc.DocumentNode.SelectNodes("//li[@class='res-list']");

                if (resultNodes == null)
                    return Array.Empty<SearchResult>();

                SearchResult[] results = new SearchResult[resultNodes.Count];

                for (int i = 0; i < resultNodes.Count; i++)
                {
                    var node = resultNodes[i];

                    // 提取标题
                    var titleNode = node.SelectSingleNode(".//h3[@class='res-title']");
                    if (titleNode == null)
                    {
                        titleNode = node.SelectSingleNode(".//h3");
                    }
                    string title = titleNode?.InnerText?.Trim() ?? "无标题";

                    // 提取URL
                    var urlNode = node.SelectSingleNode(".//a[@data-mdurl]");
                    if (urlNode == null)
                    {
                        urlNode = node.SelectSingleNode(".//a");
                    }
                    string url = urlNode?.GetAttributeValue("data-mdurl", "") ?? "";

                    // 提取摘要
                    var snippetNode = node.SelectSingleNode(".//p[@class='res-desc']");
                    if (snippetNode == null)
                    {
                        snippetNode = node.SelectSingleNode(".//p");
                    }
                    string snippet = snippetNode?.InnerText?.Trim() ?? "无摘要";

                    // 确保URL和标题来自同一个搜索结果项
                    if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(title))
                    {
                        results[i] = new SearchResult
                        {
                            Title = title,
                            Url = url,
                            Snippet = snippet
                        };
                    }
                    else
                    {
                        results[i] = new SearchResult
                        {
                            Title = "无标题",
                            Url = "",
                            Snippet = snippet
                        };
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误: {ex.Message}");
                return Array.Empty<SearchResult>();
            }
        }

        public override async Task<string> InitializeAsync()
        {
            string cookieStr = "biz_huid=116JCLfFwFJmTQdmfXeNhLZr9AKihf2drKIM3dsPSS9Mw%3D; QiHooGUID=727BFD02DC6E73DF6126A173A0B4D419.1745227250551; __guid=15484592.3671357243562488000.1745227280994.9766; so-like-red=2; dpr=1; webp=1; so_huid=11M9oLRmcFuvC7RGQ17UN4P4VwMb%2Blfz82OihovsU1ous%3D; __huid=11M9oLRmcFuvC7RGQ17UN4P4VwMb%2Blfz82OihovsU1ous%3D; gtHuid=1; count=3; erules=p2-15%7Cp1-14%7Cp4-37%7Cecr-1%7Cp3-11%7Cecl-3%7Ckd-4";
            string? so_cookie = Environment.GetEnvironmentVariable("SO_COOKIE");
            if (!string.IsNullOrWhiteSpace(so_cookie))
            {
                cookieStr = so_cookie;
            }
            var _cookies = cookieStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var cookie in _cookies)
                Cookies.SetCookies(new Uri(BaseUrl), cookie.Trim());

            HttpClient.DefaultRequestHeaders.Add("cookie", cookieStr);
            return GetCookieString();
        }
    }
}
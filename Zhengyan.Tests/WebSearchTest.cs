using System.Net;
using System.Text;
using AngleSharp.Common;
using Zhengyan.WebSearch.Crawler;
using Zhengyan.WebSearch.SearchEngine;

public class WebSearchTest
{
    public static async Task Run(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Console.Write("请输入搜索关键词：");
        string keyword = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            Console.WriteLine("关键词不能为空！");
            return;
        }

        Console.Write("请输入页码（默认为1）：");
        if (!int.TryParse(Console.ReadLine(), out int page))
        {
            page = 1;
        }

        // var searchEngine = new BingSearchEngine();
        // var searchEngine = new BaiduSearchEngine();
        var searchEngine = new SoSearchEngine();
        Console.WriteLine(await searchEngine.InitializeAsync());

        PrintCookies(searchEngine);

        var results = await searchEngine.SearchAsync(keyword, page);

        PrintCookies(searchEngine);

        if (results.Length == 0)
        {
            results = await searchEngine.SearchAsync(keyword, page);
            PrintCookies(searchEngine);

        }
        Console.WriteLine($"\n搜索结果 ({results.Length} 条)：\n");

        foreach (var result in results)
        {
            Console.WriteLine($"标题: {result.Title}");
            Console.WriteLine($"URL: {result.Url}");
            Console.WriteLine($"摘要: {result.Snippet}\n");
            Console.WriteLine("------------------------------\n");
        }
        return;
        Console.WriteLine("开始抓取页面内容...");

        ICrawler crawler = new WebCrawler();
        // ICrawler crawler = new DocumentCrawler();
        var crawlResults = await crawler.CrawlUrlsAsync(results.Select(r => r.Url).Distinct());

        foreach (var crawlResult in crawlResults)
        {
            Console.WriteLine($"URL: {crawlResult.Url}");
            Console.WriteLine($"状态码: {(crawlResult.Success ? "成功" : "失败")}");
            Console.WriteLine($"标题: {crawlResult.Title}");
            Console.WriteLine($"内容: {crawlResult.Content}");
            Console.WriteLine($"错误: {crawlResult.ErrorMessage}");
            Console.WriteLine("------------------------------\n");
        }
    }

    public static void PrintCookies(AbstractSearchEngine searchEngine)
    {
        // cookie.SetCookies(new Uri("http://www.baidu.com/s?xxx"), "xxxxx");

        Console.WriteLine("--------------------------------------");
        Console.WriteLine($"Cookie: {searchEngine.GetCookieString()}");
        Console.WriteLine("--------------------------------------");
        
        // Console.WriteLine("======================================");
        // var cookies = searchEngine.Cookies.GetCookies(new Uri("https://www.so.com/s?q=%E7%A6%8F%E5%B7%9E%E5%A4%A9%E6%B0%94&pn=0"));
        // Console.WriteLine($"Cookie: {string.Join("; ", cookies.Cast<Cookie>().Select(c => $"{c.Name}={c.Value}"))}");
        // Console.WriteLine("======================================");
    }
}
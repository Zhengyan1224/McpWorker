using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Zhengyan.WebSearch.Crawler;

public class DocumentCrawler : ICrawler
{
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3";

    public DocumentCrawler()
    {

    }

    public virtual async Task<List<CrawlResult>> CrawlUrlsAsync(IEnumerable<string> urls,CancellationToken cancellationToken = default)
    {
        if (urls == null || urls.Count() == 0)
            return new List<CrawlResult>();

        var results = new List<CrawlResult>();

        // 使用SemaphoreSlim限制并发请求数量，避免过多请求导致资源耗尽
        var semaphore = new SemaphoreSlim(10); // 最大并发数为10

        // 创建一个任务列表
        var tasks = new List<Task>();

        foreach (var url in urls)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await CrawlSingleUrlAsync(url, cancellationToken);
                    results.Add(result);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        // 等待所有任务完成
        await Task.WhenAll(tasks);

        return results;
    }

    private async Task<CrawlResult> CrawlSingleUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            SmartReader.Article article = await SmartReader.Reader.ParseArticleAsync(url, UserAgent);

            return new CrawlResult
            {
                Url = url,
                Title = article.Title,
                Content = article.TextContent,
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new CrawlResult
            {
                Url = url,
                Title = null,
                Content = null,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }


}


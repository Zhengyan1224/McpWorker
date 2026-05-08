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
using Zhengyan.WebSearch.Extensions;

namespace Zhengyan.WebSearch.Crawler;

public class WebCrawler : ICrawler, IDisposable
{
    public HttpClient HttpClient { get; }

    public WebCrawler()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        ServicePointManager.Expect100Continue = false;
        ServicePointManager.DefaultConnectionLimit = 100;

        // 忽略SSL证书验证（仅用于测试）
        ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

        // 配置HttpClient
        HttpClientHandler handler = new HttpClientHandler();
        handler.UseProxy = false; // 关闭代理
        handler.AllowAutoRedirect = true; // 启用自动重定向
        handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate; // 启用自动解压缩

        // 启用HTTP/2
        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
        handler.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

        HttpClient client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30) // 设置请求超时时间
        };
        // 创建HttpClient实例
        HttpClient = client;
        // 设置用户代理，模拟浏览器访问
        HttpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");
    }

    public virtual async Task<List<CrawlResult>> CrawlUrlsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
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
            // 发送HTTP GET请求
            HttpResponseMessage response = await HttpClient.GetAsync(url, cancellationToken);

            // 确保请求成功
            response.EnsureSuccessStatusCode();

            // 读取响应内容
            // byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();

            // // 检测网页编码并转码
            string charset, html, title;
            (charset, html) = await DetectEncodingFromHtmlAsync(response.Content, cancellationToken);
            title = GetHtmlTitle(html);

            // Console.WriteLine("Html: " + html);

            return new CrawlResult
            {
                Url = url,
                Title = title,
                Content = html,
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

    // 检测网页编码并转码
    public static async Task<(string, string)> DetectEncodingFromHtmlAsync(HttpContent httpContent, CancellationToken cancellationToken = default)
    {
        var charset = httpContent.Headers.ContentType.CharSet;
        if (string.IsNullOrEmpty(charset))
        {
            charset = "utf-8"; // 默认编码
        }
        var content = await httpContent.ReadAsByteArrayAsync(cancellationToken);
        var html = charset.GetEncoding().GetString(content);
        var match = Regex.Match(html, @"charset=(?<charset>.+?)""", RegexOptions.IgnoreCase);
        if (!match.Success)
            return (charset, html);
        charset = match.Groups["charset"].Value;
        return (charset, charset.GetEncoding().GetString(content));
    }

    public static string GetHtmlTitle(string htmlContent)
    {
        if (string.IsNullOrEmpty(htmlContent))
            return string.Empty;

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // 查找<title>标签
            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            if (titleNode != null)
            {
                return titleNode.InnerText.Trim();
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            // Console.WriteLine($"获取标题时出错: {ex.Message}");
            return string.Empty;
        }
    }

    // 释放资源
    public void Dispose()
    {
        HttpClient?.Dispose();
    }
}


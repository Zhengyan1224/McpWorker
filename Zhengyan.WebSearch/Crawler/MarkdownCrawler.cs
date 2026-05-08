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
using ReverseMarkdown;

namespace Zhengyan.WebSearch.Crawler;

public class MarkdownCrawler : WebCrawler
{
    public HttpClient HttpClient { get; }

    public MarkdownCrawler() : base()
    {
    }

    public override async Task<List<CrawlResult>> CrawlUrlsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default)
    {
        var results = await base.CrawlUrlsAsync(urls, cancellationToken);

        var config = new Config
        {
            // 配置选项
            GithubFlavored = true, // 启用 GitHub 风格
            RemoveComments = true,  // 移除注释
            UnknownTags = Config.UnknownTagsOption.Drop, // 删除未知标签
        };

        var converter = new Converter(config);

        foreach (var result in results)
        {
            if (!string.IsNullOrWhiteSpace(result.Content))
            {
                // 将 HTML 转换为 Markdown
                var markdown = converter.Convert(result.Content);
                result.Content = markdown;
            }
        }
        return results;
    }
}


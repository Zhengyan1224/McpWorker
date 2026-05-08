using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Zhengyan.WebSearch.Crawler;

// 结果类，用于存储每个URL的爬取结果
public class CrawlResult
{
    public string Url { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });
    }
}
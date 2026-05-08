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

public interface ICrawler
{
    Task<List<CrawlResult>> CrawlUrlsAsync(IEnumerable<string> urls,CancellationToken cancellationToken = default);
}


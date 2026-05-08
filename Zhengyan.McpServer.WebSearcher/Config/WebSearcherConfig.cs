using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Zhengyan.McpServer.WebSearcher.Config;

public class WebSearcherConfig
{
    /// <summary>
    /// 搜索引擎（bing/360）
    /// </summary>
    public string SearchEngine { get; set; } = "bing";

    /// <summary>
    /// 爬虫（web/document/markdown）
    /// </summary>
    public string Crawler { get; set; } = "markdown";

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        });

    }
}
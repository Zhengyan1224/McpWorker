namespace Zhengyan.WebSearch.Crawler;

public static class CrawlerFactory
{
    private static readonly Dictionary<string, Type> CrawlerMap = new Dictionary<string, Type>();

    static CrawlerFactory()
    {
        CrawlerMap.Add("web", typeof(WebCrawler));
        CrawlerMap.Add("document", typeof(DocumentCrawler));
        CrawlerMap.Add("markdown", typeof(MarkdownCrawler));
    }

    public static ICrawler CreateCrawler(string crawlerName)
    {
        crawlerName = crawlerName.ToLower();
        if (CrawlerMap.TryGetValue(crawlerName, out var crawlerType))
        {
            return (ICrawler)Activator.CreateInstance(crawlerType);
        }
        return null;
    }
}
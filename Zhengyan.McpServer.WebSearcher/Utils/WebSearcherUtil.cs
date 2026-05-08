using System.Reflection.Emit;
using Microsoft.Extensions.DependencyInjection;
using Zhengyan.McpServer.WebSearcher.Config;
using Zhengyan.McpServer.WebSearcher.Services;
using Zhengyan.WebSearch;
using Zhengyan.WebSearch.Crawler;
using Zhengyan.WebSearch.SearchEngine;

namespace Zhengyan.McpServer.WebSearcher.Utils;

public static class WebSearcherUtil
{
    public static async Task<IServiceCollection> AddWebSearcherServiceAsync(this IServiceCollection services, WebSearcherConfig webSearcherConfig)
    {
        if (webSearcherConfig == null)
        {
            throw new ArgumentNullException(nameof(webSearcherConfig));
        }

        if (!string.IsNullOrWhiteSpace(webSearcherConfig.SearchEngine))
        {
            var searchEngineName = webSearcherConfig.SearchEngine;
            var searchEngine = SearchEngineFactory.CreateSearchEngine(searchEngineName);

            if (searchEngine != null)
            {
                await searchEngine.InitializeAsync();
            }
            else
            {
                throw new ArgumentNullException(nameof(webSearcherConfig.SearchEngine));
            }
            services.AddSingleton<ISearchEngine>(searchEngine);
        }
        else
        {
            throw new ArgumentNullException(nameof(webSearcherConfig.SearchEngine));
        }

        if (!string.IsNullOrWhiteSpace(webSearcherConfig.Crawler))
        {
            var crawlerName = webSearcherConfig.Crawler;
            var crawler = CrawlerFactory.CreateCrawler(crawlerName);
            if (crawler == null)
                throw new ArgumentNullException(nameof(webSearcherConfig.Crawler));
            services.AddSingleton<ICrawler>(crawler);
        }
        else
        {
            throw new ArgumentNullException(nameof(webSearcherConfig.Crawler));
        }

        services.AddSingleton(webSearcherConfig);

        services.AddSingleton<IWebSearcherService, WebSearcherService>();
        return services;
    }
}
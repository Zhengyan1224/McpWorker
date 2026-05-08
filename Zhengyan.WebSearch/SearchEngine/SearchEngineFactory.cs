using System.Reflection;

namespace Zhengyan.WebSearch.SearchEngine;

public static class SearchEngineFactory
{
    private static readonly Dictionary<string, Type> SearchEngineMap = new Dictionary<string, Type>();
    static SearchEngineFactory()
    {
        SearchEngineMap.Add("bing", typeof(BingSearchEngine));
        SearchEngineMap.Add("360", typeof(SoSearchEngine));
        SearchEngineMap.Add("baidu", typeof(BaiduSearchEngine));
    }

    public static ISearchEngine CreateSearchEngine(string searchEngineName)
    {
        searchEngineName = searchEngineName.ToLower();
        if (SearchEngineMap.TryGetValue(searchEngineName, out var searchEngineType))
        {
            return (ISearchEngine)Activator.CreateInstance(searchEngineType);
        }
        return null;
    }
}
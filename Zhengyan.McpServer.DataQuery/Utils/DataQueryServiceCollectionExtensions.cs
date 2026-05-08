using Microsoft.Extensions.DependencyInjection;
using Zhengyan.McpServer.DataQuery.Config;
using Zhengyan.McpServer.DataQuery.Services;

namespace Zhengyan.McpServer.DataQuery.Utils;

public static class DataQueryServiceCollectionExtensions
{
    public static async Task<IServiceCollection> AddDataQueryServiceAsync(this IServiceCollection services, DataQueryConfig dataQueryConfig)
    {
        if (dataQueryConfig == null)
        {
            throw new ArgumentNullException(nameof(dataQueryConfig));
        }

        var dataQueryService = new DataQueryService(dataQueryConfig);
        await dataQueryService.ReloadAsync();

        services.AddSingleton(dataQueryConfig);
        services.AddSingleton<IDataQueryService>(dataQueryService);
        return services;
    }
}

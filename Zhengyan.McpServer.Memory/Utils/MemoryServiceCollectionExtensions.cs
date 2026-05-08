using Microsoft.Extensions.DependencyInjection;
using Zhengyan.McpServer.Memory.Config;
using Zhengyan.McpServer.Memory.Services;

namespace Zhengyan.McpServer.Memory.Utils;

public static class MemoryServiceCollectionExtensions
{
    public static async Task<IServiceCollection> AddMemoryServiceAsync(this IServiceCollection services, MemoryConfig memoryConfig)
    {
        ArgumentNullException.ThrowIfNull(memoryConfig);

        var memoryService = new MemoryService(memoryConfig);
        await memoryService.LoadAsync();

        services.AddSingleton(memoryConfig);
        services.AddSingleton<IMemoryService>(memoryService);
        return services;
    }
}

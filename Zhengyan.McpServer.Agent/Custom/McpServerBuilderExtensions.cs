

using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Zhengyan.McpServer.Agent.Custom;

public static class McpServerBuilderExtensions
{
    public static IMcpServerBuilder WithTools(this IMcpServerBuilder builder, IEnumerable<Type> toolTypes, IEnumerable<McpServerToolCreateOptions?> options = null)
    {

        foreach (var item in toolTypes.Zip(options))
        {
            var toolType = item.First;
            var option = item.Second;
            if (toolType is not null)
            {
                foreach (var toolMethod in toolType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                {
                    if (toolMethod.GetCustomAttribute<McpServerToolAttribute>() is not null)
                    {

                        builder.Services.AddSingleton((Func<IServiceProvider, McpServerTool>)(toolMethod.IsStatic ?
                            services => { option.Services = services; return McpServerTool.Create(toolMethod, options: option); }
                        :
                            services => { option.Services = services; return McpServerTool.Create(toolMethod, toolType, option); }));
                    }
                }
            }
        }

        return builder;
    }
}
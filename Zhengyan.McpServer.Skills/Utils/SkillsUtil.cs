using Microsoft.Extensions.DependencyInjection;
using Zhengyan.McpServer.Skills.Config;
using Zhengyan.McpServer.Skills.Services;

namespace Zhengyan.McpServer.Skills.Utils;

public static class SkillsUtil
{
    public static IServiceCollection AddSkillsService(this IServiceCollection services, SkillsConfig skillsConfig)
    {
        if (skillsConfig == null)
        {
            throw new ArgumentNullException(nameof(skillsConfig));
        }

        services.AddSingleton(skillsConfig);
        services.AddHttpContextAccessor();
        services.AddSingleton<ISkillsService, SkillsService>();
        return services;
    }
}

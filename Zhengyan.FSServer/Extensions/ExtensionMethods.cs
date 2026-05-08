using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Zhengyan.Commons;
using Zhengyan.Commons.Web;
using Zhengyan.Commons.Web.Middlewares;
using Zhengyan.FSServer.Models;

namespace Zhengyan.FSServer.Extensions
{
    public static class ExtensionMethods
    {

        public static IServiceCollection AddStorageConfig(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<StorageConfig>(configuration.GetSection("Storage").Get<StorageConfig>());
            return services;
        }

        public static IEndpointConventionBuilder MapMcp(this IEndpointRouteBuilder app, IConfiguration configuration)
        {
            var routePrefix = configuration.GetSection("McpServer:RoutePrefix")?.Value;
            Console.WriteLine($"McpServer RoutePrefix: {routePrefix}");
            return app.MapMcp(routePrefix);
        }
    }
}
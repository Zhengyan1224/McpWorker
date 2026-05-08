using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Zhengyan.Commons;
using Zhengyan.Commons.Web;
using Zhengyan.Commons.Web.Extensions;
using Zhengyan.Commons.Web.Middlewares;
using Zhengyan.FSServer.Extensions;

namespace Zhengyan.FSServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // export LD_LIBRARY_PATH=/root/miniconda3/envs/glm3/lib/python3.9/site-packages/nvidia/cublas/lib:/root/miniconda3/envs/glm3/lib/python3.9/site-packages/nvidia/cuda_runtime/lib:$LD_LIBRARY_PATH
            
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Configuration.SetBasePath(builder.Environment.ContentRootPath)
            .AddProfiles("profiles");

            builder.Host.UseSerilog(builder.Configuration);

            builder.Services.AddSwaggerGen(builder.Configuration)
                .AddCentralRoutePrefix(builder.Configuration);

            builder.Services.AddAllowCors();

            builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

            builder.Services.AddStorageConfig(builder.Configuration);
            builder.Services.AddSingleton<Services.IFSService, Services.FSService>();

            var mode = builder.Configuration.GetValue<string>("McpServer:Mode")
                ?? builder.Configuration["mode"]
                ?? "sse";
            var stateless = builder.Configuration.GetValue<bool?>("McpServer:Stateless")
                ?? builder.Configuration.GetValue<bool?>("stateless")
                ?? false;

            var enableLegacySse = string.Equals("sse", mode, StringComparison.CurrentCultureIgnoreCase);
            if (!enableLegacySse &&
                !string.Equals("streamablehttp", mode, StringComparison.CurrentCultureIgnoreCase) &&
                !string.Equals("http", mode, StringComparison.CurrentCultureIgnoreCase))
            {
                Log.Warning($"Unknown MCP mode {mode}, fallback to SSE.");
                enableLegacySse = true;
            }

            var effectiveStateless = stateless;
            if (enableLegacySse && stateless)
            {
                Log.Warning("Legacy SSE requires stateful mode, forcing stateless = false.");
                effectiveStateless = false;
            }

            Log.Information($"Starting MCP transport, Mode = {mode}, LegacySse = {enableLegacySse}, Stateless = {effectiveStateless}");
            builder.Services.AddMcpServer()
                .WithHttpTransport(options =>
                {
                    options.Stateless = effectiveStateless;
#pragma warning disable MCP9004
                    options.EnableLegacySse = enableLegacySse;
#pragma warning restore MCP9004
                })
                .WithToolsFromAssembly();

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();


            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.ConfigureSwagger(builder.Configuration);
            app.UseStaticFiles(builder.Configuration);

            app.UseCors("AllowCors");

            app.MapControllers();

            app.MapMcp(builder.Configuration);
            // app.MapMcp();

            app.Run();
        }
    }
}

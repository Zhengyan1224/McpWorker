using Zhengyan.McpHost.Config;
using Zhengyan.McpHost.Middleware;
// using Zhengyan.McpHost.Services;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using Zhengyan.Commons.Web;
using Zhengyan.Commons.Web.Middlewares;
using System.Threading.Tasks;
using Zhengyan.McpHost.Services;
using Zhengyan.McpHost.Extensions;
using Zhengyan.Commons.Web.Extensions;

namespace Zhengyan.McpHost
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Configuration.SetBasePath(builder.Environment.ContentRootPath)
            .AddProfiles("profiles");

            builder.Host.UseSerilog(builder.Configuration);

            builder.Services.AddSwaggerGen(builder.Configuration)
                .AddCentralRoutePrefix(builder.Configuration);

            builder.Services.AddAllowCors();
            builder.Services.AddHttpClient();

            await builder.Services.AddGlobalObjectPoolService(builder.Configuration);
            builder.Services.AddSingleton<IMcpChatModelService, McpChatModelService>();

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();


            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.ConfigureSwagger(builder.Configuration);
            app.UseStaticFiles(builder.Configuration);
            // app.UseMiddleware<AllowCorsMiddleware>();
            app.UseCors("AllowCors");


            // app.UseAuthorization();
            // app.AddSecurity(builder.Configuration);
            // app.UseMiddleware<TypeConversionMiddleware>();
            app.MapControllers();

            app.Run();

        }
    }
}

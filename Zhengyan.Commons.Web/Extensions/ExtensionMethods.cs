using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Zhengyan.Commons.Web.Mvc;

namespace Zhengyan.Commons.Web.Extensions
{
    public static class ExtensionMethods
    {
        static ExtensionMethods()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static IConfigurationBuilder AddProfiles(this IConfigurationBuilder configurationBuilder, string profilesFolder)
        {
            profilesFolder = SystemUtils.GetAbsolutePath(profilesFolder);
            if (!Directory.Exists(profilesFolder))
            {
                // throw new DirectoryNotFoundException($"The folder '{profilesFolder}' does not exist.");
                // Log.Error($"The folder '{profilesFolder}' does not exist.");
                Console.WriteLine($"The folder '{profilesFolder}' does not exist.");
                return configurationBuilder;
            }
            string[] jsonFilePaths = Directory.GetFiles(profilesFolder, "*.json", SearchOption.TopDirectoryOnly);
            foreach (string jsonFilePath in jsonFilePaths)
            {
                Console.WriteLine($"Loading configuration from {jsonFilePath}");
                configurationBuilder.AddJsonFile(jsonFilePath, optional: true, reloadOnChange: true);
            }

            return configurationBuilder;
        }

        public static IApplicationBuilder ConfigureSwagger(this IApplicationBuilder applicationBuilder, IConfiguration configuration)
        {
            var configurationSection = configuration.GetSection("Swagger");
            if (configurationSection == null)
                return applicationBuilder;
            bool enbale = configurationSection.GetValue<bool>("Enable");
            if (enbale)
            {
                // applicationBuilder.UseSwagger(c => c.RouteTemplate = "sdserver/swagger/{documentName}/swagger.json");

                // applicationBuilder.UseSwaggerUI(c =>
                // {
                //     c.RoutePrefix = "sdserver/swagger"; // 自定义路径前缀，默认是 swagger
                //     c.SwaggerEndpoint("/sdserver/swagger/v1/swagger.json", "My API V1");
                //     // 其他Swagger UI配置...
                // });
                var routePrefix = configurationSection.GetValue<string>("RoutePrefix");
                applicationBuilder.UseSwagger(c => c.RouteTemplate = routePrefix + "/{documentName}/swagger.json");

                applicationBuilder.UseSwaggerUI(c =>
                {
                    c.RoutePrefix = routePrefix; // 自定义路径前缀，默认是 swagger
                    c.SwaggerEndpoint($"/{routePrefix}/v1/swagger.json", "API V1");
                    // 其他Swagger UI配置...
                });
            }
            return applicationBuilder;
        }

        public static IServiceCollection AddSwaggerGen(this IServiceCollection services, IConfiguration configuration)
        {
            var configurationSection = configuration.GetSection("Swagger");
            if (configurationSection == null)
                return services;
            return services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = configurationSection.GetValue<string>("Title"),
                    Version = configurationSection.GetValue<string>("Version"),
                    Description = configurationSection.GetValue<string>("Description")
                });
                var xmls = configurationSection.GetSection("XmlComments");
                if (xmls != null)
                    foreach (var xml in xmls.GetChildren())
                        c.IncludeXmlComments(xml.Value);
                c.OrderActionsBy(o => o.RelativePath); // 对action的名称进行排序，如果有多个，就可以看见效果了。

            });
        }

        public static IHostBuilder UseSerilog(this IHostBuilder builder, IConfiguration configuration)
        {
            configuration.ConfigureSerilog();

            return builder.UseSerilog();
        }

        public static void ConfigureSerilog(this IConfiguration configuration)
        {
            var configurationSection = configuration.GetSection("Logger");
            if (configurationSection == null)
                return;
            var logconfig = new LoggerConfiguration();

            var level = configurationSection.GetValue<string>("Level");
            if (!string.IsNullOrWhiteSpace(level))
                level = level.Trim().ToLower();
            switch (level)
            {
                case "debug":
                    logconfig.MinimumLevel.Debug();
                    break;
                case "info":
                    logconfig.MinimumLevel.Information();
                    break;
                case "error":
                    logconfig.MinimumLevel.Error();
                    break;
                case "warn":
                    logconfig.MinimumLevel.Warning();
                    break;
                case "fatal":
                    logconfig.MinimumLevel.Fatal();
                    break;
                case "verb":
                    logconfig.MinimumLevel.Verbose();
                    break;
                default:
                    throw new NotSupportedException($"Logger level \'{level}\' is not supported.");
            }

            var outputs = configurationSection.GetSection("Outputs").GetChildren();
            foreach (var output in outputs)
            {
                ConfigureLoggerOutput(logconfig, output);
            }

            Log.Logger = logconfig.CreateLogger();
        }

        private static void ConfigureLoggerOutput(LoggerConfiguration logconfig, IConfigurationSection? outputconfig)
        {
            string? type = outputconfig?.GetValue<string>("Type");
            if (string.IsNullOrWhiteSpace(type))
                return;
            type = type.Trim().ToLower();
            string defaultTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
            string? template = outputconfig?.GetValue<string>("Template");
            if (string.IsNullOrWhiteSpace(template))
                template = defaultTemplate;
            switch (type)
            {
                case "file":
                    {
                        string? path = outputconfig?.GetValue<string>("Path");
                        RollingInterval rollingInterval = RollingInterval.Infinite;
                        Enum.TryParse<RollingInterval>(outputconfig?.GetValue<string>("RollingInterval"), out rollingInterval);
                        long fileSizeLimitBytes = outputconfig.GetValue<long>("FileSizeLimitBytes");
                        Encoding encoding = Encoding.UTF8;
                        string encodingname = outputconfig.GetValue<string>("Encoding");
                        if (!string.IsNullOrWhiteSpace(encodingname))
                            encoding = Encoding.GetEncoding(encodingname);
                        int retainedFileCountLimit = outputconfig.GetValue<int>("RetainedFileCountLimit");
                        logconfig.WriteTo.File(path, outputTemplate: template,
                            rollingInterval: rollingInterval,
                            rollOnFileSizeLimit: fileSizeLimitBytes > 0,
                            fileSizeLimitBytes: fileSizeLimitBytes > 0 ? fileSizeLimitBytes : null,
                            encoding: encoding, retainedFileCountLimit: retainedFileCountLimit);
                        break;
                    }
                case "console":
                    {
                        logconfig.WriteTo.Console(outputTemplate: template);
                        break;
                    }
                default:
                    throw new NotSupportedException($"Output type \'{type}\' is not supported.");
            }
        }

        public static IApplicationBuilder UseStaticFiles(this IApplicationBuilder applicationBuilder, IConfiguration configuration)
        {
            var configurationSection = configuration.GetSection("StaticFiles");
            if (configurationSection == null)
                return applicationBuilder;
            DefaultFilesOptions defaultFilesOptions = new DefaultFilesOptions(); //UseDefaultFiles()必须先于UseStaticFiles()注册。
            defaultFilesOptions.DefaultFileNames.Clear();
            defaultFilesOptions.RequestPath = configurationSection.GetValue<string>("RequestPath");
            // defaultFilesOptions.DefaultFileNames.Add("login.html");
            var defaultFiles = configurationSection.GetSection("DefaultFiles");
            if (defaultFiles != null)
                foreach (var df in defaultFiles.GetChildren())
                    defaultFilesOptions.DefaultFileNames.Add(df.Value);

            applicationBuilder.UseDefaultFiles(defaultFilesOptions);

            var provider = new FileExtensionContentTypeProvider();
            // Add new mappings
            // provider.Mappings[".txt"] = "application/octet-stream";
            var contentTypeMappings = configurationSection.GetSection("ContentTypeMappings");
            if (contentTypeMappings != null)
                foreach (var ctm in contentTypeMappings.GetChildren())
                {
                    var fileExt = ctm.GetValue<string>("FileExtension");
                    var contentType = ctm.GetValue<string>("ContentType");
                    provider.Mappings[fileExt] = contentType;
                }

            applicationBuilder.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.GetFullPath(configurationSection.GetValue<string>("StaticFilesRoot"))),
                RequestPath = configurationSection.GetValue<string>("RequestPath"),
                OnPrepareResponse = ctx =>
                {
                    //ctx.Context.Response.Headers.Append("cache-control", $"public,max-age={cacheMaxAge}");
                },
                ContentTypeProvider = provider
            });
            return applicationBuilder;
        }

        public static void UseCentralRoutePrefix(this MvcOptions opts, IRouteTemplateProvider routeAttribute)
        {
            // 添加我们自定义 实现IApplicationModelConvention的RouteConvention
            opts.Conventions.Insert(0, new RouteConvention(routeAttribute));
        }

        public static IMvcBuilder AddCentralRoutePrefix(this IServiceCollection services, IConfiguration configuration)
        {
            var configurationSection = configuration.GetSection("WebApi");
            if (configurationSection == null)
                return services.AddMvc();
            return services.AddMvc(opt =>
            {
                opt.UseCentralRoutePrefix(new RouteAttribute(configurationSection.GetValue<string>("CentralRoutePrefix")));
                //opt.UseCentralRoutePrefix(new RouteAttribute("api/[controller]/[action]"));

            });
        }

        /// <summary>
        /// 添加允许跨域
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddAllowCors(this IServiceCollection services)
        {
            return services.AddCors(options =>
            {
                options.AddPolicy(name: "AllowCors",
                    policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
                );
            });
        }


        /// <summary>
        /// 验证高级别密码复杂度（必须包含数字、字母和特殊字符）
        /// </summary>
        /// <param name="password">密码</param>
        /// <returns>是否合法</returns>
        public static bool ValidateHighLevelPasswordComplexity(this string password)
        {
            //密码复杂度正则表达式
            var regex = new Regex(@"
                (?=.*[0-9])                     #必须包含数字
                (?=.*[a-zA-Z])                  #必须包含小写或大写字母
                (?=([\x21-\x7e]+)[^a-zA-Z0-9])  #必须包含特殊符号
                .{8,50}                         #至少8个字符，最多50个字符
                ", RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
            //校验密码是否符合
            return regex.IsMatch(password);
        }

        /// <summary>
        /// 验证低级别密码复杂度（必须包含数字和字母）
        /// </summary>
        /// <param name="password">密码</param>
        /// <returns>是否合法</returns>
        public static bool ValidateLowLevelPasswordComplexity(this string password)
        {
            //密码复杂度正则表达式
            var regex = new Regex(@"
                (?=.*[0-9])                     #必须包含数字
                (?=.*[a-zA-Z])                  #必须包含小写或大写字母
                .{8,50}                         #至少8个字符，最多50个字符
                ", RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
            //校验密码是否符合
            return regex.IsMatch(password);
        }
    }
}

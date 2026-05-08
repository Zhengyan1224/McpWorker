using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Zhengyan.Commons;
using Zhengyan.Commons.Web;
using Zhengyan.Commons.Web.Middlewares;
using Zhengyan.KBServer.Implements;
using Zhengyan.KnowledgeBase;
using Zhengyan.VectorDB;


namespace Zhengyan.KBServer.Extensions
{
    public static class ExtensionMethods
    {
        public static IServiceCollection AddTextEmbedding(this IServiceCollection services, IConfiguration configuration)
        {
            ITextEmbedder textEmbedding = configuration.GetTextEmbedding();

            services.AddSingleton<ITextEmbedder>(textEmbedding);
            return services;
        }

        public static ITextEmbedder? GetTextEmbedding(this IConfiguration configuration)
        {
            var configurationSection = configuration.GetSection("TextEmbedding");

            ITextEmbedder textEmbedding = configurationSection.CreateNewObject<ITextEmbedder>();
            // ITextEmbedding textEmbedding = new LLamaSharpTextEmbedding(configurationSection.Get<ObjectConfig>().Parameters[0].Value.ToString());


            return textEmbedding;
        }

        public static T? CreateNewObject<T>(this IConfigurationSection section) where T : class
        {
            var objConfig = section.Get<ObjectConfig>();
            string? typename = objConfig?.Type;
            if (string.IsNullOrWhiteSpace(typename))
            {
                throw new ArgumentException("Type is not specified in the configuration section.");
            }
            Type? type = Type.GetType(typename);
            if (type == null)
            {
                throw new ArgumentException($"Type '{typename}' not found.");
            }

            // return type.GetConstructor(objConfig.Parameters.ToParametersType())?.Invoke(objConfig.Parameters.ToParametersValue()) as T;
            var constructor = type.GetConstructor(objConfig.Parameters.ToParametersType());
            var obj = constructor.Invoke(objConfig.Parameters.ToParametersValue());
            return obj as T;
        }

        private static Type[] ToParametersType(this ParameterConfig[] parameters)
        {
            Type[] types = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                types[i] = parameters[i].ToParameterType();
            }
            return types;
        }

        private static object[] ToParametersValue(this ParameterConfig[] parameters)
        {
            object[] values = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                values[i] = parameters[i].Value;
            }
            return values;
        }



        public static IServiceCollection AddTextProcessor(this IServiceCollection services, IConfiguration configuration)
        {
            ITextProcessor textProcessor = configuration.GetTextProcessor();

            services.AddSingleton<ITextProcessor>(textProcessor);
            return services;
        }

        public static ITextProcessor? GetTextProcessor(this IConfiguration configuration)
        {
            var configurationSection = configuration.GetSection("TextProcessor");

            ITextProcessor? textProcessor = configurationSection.CreateNewObject<ITextProcessor>();
            return textProcessor;
        }

        public static IServiceCollection AddKnowledgeBaseManager(this IServiceCollection services, IConfiguration configuration)
        {
            KnowledgeBaseManager<TextFeaturesEnhancedKnowledgeBase> kbManager = configuration.GetKnowledgeBaseManager();

            services.AddSingleton<KnowledgeBaseManager<TextFeaturesEnhancedKnowledgeBase>>(kbManager);
            return services;
        }

        public static KnowledgeBaseManager<TextFeaturesEnhancedKnowledgeBase> GetKnowledgeBaseManager(this IConfiguration configuration)
        {
            var configurationSection = configuration.GetSection("KnowledgeBase");
            var storageBaseDir = configurationSection.GetValue<string>("StorageBaseDir");
            Enum.TryParse<TextFeaturesMode>(configurationSection.GetValue<string>("TextFeaturesMode"), true, out var textFeaturesMode);

            var kbManager = KnowledgeBaseManager<TextFeaturesEnhancedKnowledgeBase>.Create();
            kbManager.AddDependency("TextEmbedding", configuration.GetTextEmbedding());
            kbManager.AddDependency("TextProcessor", configuration.GetTextProcessor());
            kbManager.ConfigureKBInitBehavior((string name, string storageBaseDir, ConcurrentDictionary<string, object> dependencyCollection, LifeCycle lifeCycle) =>
            {
                try
                {
                    string storageDirectoryPath = Path.Combine(storageBaseDir, name);
                    TextFeaturesEnhancedKnowledgeBase kb = new TextFeaturesEnhancedKnowledgeBase(dependencyCollection.GetDependency<ITextEmbedder>("TextEmbedding"),
                        // lifeCycle == LifeCycle.Load ? LiteVectorDB.Load(storageDirectoryPath, true, true) : LiteVectorDB.CreateNew(storageDirectoryPath, true, true),
                        lifeCycle == LifeCycle.Load ? LiteVectorDBV2.Load(storageDirectoryPath, true, true) : LiteVectorDBV2.CreateNew(storageDirectoryPath, true, true),
                        dependencyCollection.GetDependency<ITextProcessor>("TextProcessor"), textFeaturesMode
                    );
                    return kb;
                }
                catch (Exception e)
                {
                    Log.Error("Load \'" + name + "\' throws {Exception}", e);
                    return null;
                }
            }).ConfigureKBDisposeBehavior((string name, TextFeaturesEnhancedKnowledgeBase kb) =>
            {
                kb.VectorDB.Delete();
                return true;
            }).ConfigureKBSaveBehavior((string name, TextFeaturesEnhancedKnowledgeBase kb) =>
            {
                kb.VectorDB.Save();
                return true;
            });

            return kbManager.Load(storageBaseDir);
        }


        public static IEndpointConventionBuilder MapMcp(this IEndpointRouteBuilder app, IConfiguration configuration)
        {
            var routePrefix = configuration.GetSection("McpServer:RoutePrefix")?.Value;
            Console.WriteLine($"McpServer RoutePrefix: {routePrefix}");
            return app.MapMcp(routePrefix);
        }
    }
}
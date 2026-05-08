#define SYNC_LOAD
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
using Zhengyan.McpHost.Config;
using OpenAI;
using Microsoft.Extensions.AI;
using System.ClientModel;
using Zhengyan.McpHost.Services;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Reflection;
using System.Collections;
using Zhengyan.McpHost.Custom;

namespace Zhengyan.McpHost.Extensions
{
    public static class ExtensionMethods
    {
        public static async Task<IServiceCollection> AddGlobalObjectPoolService(this IServiceCollection services, IConfiguration configuration)
        {
            GlobalObjectPoolService globalObjectPoolService = new GlobalObjectPoolService();

            var chatClientsConfig = configuration.GetSection("ChatClients").Get<ChatClientsConfig>();
            if (chatClientsConfig != null)
            {
                if (chatClientsConfig.Storage != null)
                {
                    var chatClientConfigs = LoadConfigs<ChatClientConfig>(chatClientsConfig.Storage);
                    chatClientsConfig.ChatClients.AddRange(chatClientConfigs);
                }

                Log.Debug($"ChatClients: {chatClientsConfig.ToString()}");

                foreach (var chatClientConfig in chatClientsConfig.ChatClients)
                {
                    globalObjectPoolService.AddChatClientConfig(chatClientConfig);
                }
                globalObjectPoolService.ChatClientsStorageConfig = chatClientsConfig.Storage;
            }

            var mcpClientsConfig = configuration.GetSection("McpClients").Get<McpClientsConfig>();
            if (mcpClientsConfig != null)
            {
                if (mcpClientsConfig.Storage != null)
                {
                    var mcpClientConfigs = LoadConfigs<McpClientConfig>(mcpClientsConfig.Storage);
                    mcpClientsConfig.McpClients.AddRange(mcpClientConfigs);
                }

                Log.Debug($"McpClients: {mcpClientsConfig.ToString()}");
                #if SYNC_LOAD
                foreach (var mcpClientConfig in mcpClientsConfig.McpClients)
                {
                    await globalObjectPoolService.AddMcpClientConfigAsync(mcpClientConfig);
                }
                #else
                await Parallel.ForEachAsync(mcpClientsConfig.McpClients, async (mcpClientConfig, cancellationToken) =>
                {
                    await globalObjectPoolService.AddMcpClientConfigAsync(mcpClientConfig);
                });
                #endif

                globalObjectPoolService.McpClientsStorageConfig = mcpClientsConfig.Storage;
            }
						Log.Debug($"加载MCP客户端配置完成，当前MCP客户端数量：{globalObjectPoolService.McpClientConfigs.Count}");
            Log.Debug($"加载MCP客户端配置完成，当前启用的MCP客户端数量：{globalObjectPoolService.McpClients.Count}");

            var agentsConfig = configuration.GetSection("Agents").Get<AgentsConfig>();
            if (agentsConfig != null)
            {
                if (agentsConfig.Storage != null)
                {
                    var agentConfigs = LoadConfigs<AgentConfig>(agentsConfig.Storage);
                    agentsConfig.Agents.AddRange(agentConfigs);
                }
                Log.Debug($"Agents: {agentsConfig.ToString()}");
                foreach (var agentConfig in agentsConfig.Agents)
                {
                    globalObjectPoolService.AddAgentConfig(agentConfig);
                }

                globalObjectPoolService.AgentsStorageConfig = agentsConfig.Storage;
            }


            return services.AddSingleton(typeof(GlobalObjectPoolService), globalObjectPoolService);
        }

        private static T LoadConfig<T>(string jsonfilepath)
        {
            if (string.IsNullOrWhiteSpace(jsonfilepath) || !File.Exists(jsonfilepath))
                return default(T);
            var json = File.ReadAllText(jsonfilepath, Encoding.UTF8);
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
            });
        }

        private static IEnumerable<T> LoadConfigs<T>(StorageConfig storageConfig)
        {
            if (storageConfig == null || string.IsNullOrWhiteSpace(storageConfig.StorageFolderPath) || !Directory.Exists(storageConfig.StorageFolderPath))
                return Enumerable.Empty<T>();
            var files = Directory.GetFiles(storageConfig.StorageFolderPath, $"*{storageConfig.FileExtension}", SearchOption.AllDirectories);
            var tList = new List<T>();
            foreach (var file in files)
            {
                tList.Add(LoadConfig<T>(file));
            }
            return tList;
        }


        /// <summary>
        /// 添加安全中间件
        /// </summary>
        /// <param name="application"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static WebApplication AddSecurity(this WebApplication application, IConfiguration configuration)
        {
            var securityConfig = configuration.GetSection("Security").Get<SecurityConfig>();
            if (securityConfig == null)
                return application;
            Log.Debug($"Security: {securityConfig.ToString()}");
            var apiKeyExpirations = securityConfig.ApiKeyExpirations;
            if (apiKeyExpirations == null)
                return application;
            if (apiKeyExpirations.Count == 0)
                return application;

            var staticRequestPath = configuration.GetValue<string>("StaticFiles:RequestPath");

            Log.Debug($"StaticFiles RequestPath: {staticRequestPath}");

            application.Use(async (context, next) =>
                {
                    var headersBuilder = new StringBuilder();
                    headersBuilder.Append("Request Path: " + context.Request.Path.Value + "\n");
                    headersBuilder.Append("Request Header:\n");

                    foreach (var header in context.Request.Headers)
                    {
                        headersBuilder.Append($"\t{header.Key}: {header.Value}\n");
                    }
                    Log.Debug(headersBuilder.ToString());

                    if (!string.IsNullOrWhiteSpace(staticRequestPath) && context.Request.Path.Value.StartsWith(staticRequestPath))
                    {
                        await next(context);
                        return;
                    }
                    // 先尝试取一下 Authorization
                    var found = context.Request.Headers.TryGetValue("Authorization", out var key);

                    // 不存在，尝试取一下 api-key
                    if (!found)
                    {
                        found = context.Request.Headers.TryGetValue("Api-Key", out key);
                    }

                    key = key.ToString().Split(" ")[^1];

                    if (found)
                    {
                        if (apiKeyExpirations.TryGetValue(key, out var expirationTime))
                        {
                            if (DateTime.TryParse(expirationTime, out var expiration))
                            {
                                if (expiration < DateTime.Now)
                                {
                                    context.Response.StatusCode = 401;
                                    await context.Response.WriteAsync("Unauthorized");
                                    return;
                                }
                                else
                                {
                                    // 继续执行
                                    await next(context);
                                    return;
                                }
                            }
                            else
                            {
                                context.Response.StatusCode = 401;
                                await context.Response.WriteAsync("Unauthorized");
                                return;
                            }
                        }

                    }
                    else
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Unauthorized");
                        return;
                    }
                });
            return application;
        }


        public static BinaryData GetChoiceValue(this ChatResponseUpdate chatResponseUpdate, string key)
        {
            if (TryGetCompatibleChoiceValue(chatResponseUpdate?.RawRepresentation, key, out var compatibleValue))
            {
                return compatibleValue;
            }

            var openai_sccu = chatResponseUpdate?.RawRepresentation as OpenAI.Chat.StreamingChatCompletionUpdate;
            var icd = openai_sccu != null ? GetNonPublicPropertyValue(openai_sccu, "InternalChoiceDelta") : null;
            var ard = icd != null ? GetNonPublicPropertyValue(icd, "SerializedAdditionalRawData") as IDictionary<string, BinaryData> : null;

            if (ard != null && ard.TryGetValue(key, out var value))
            {
                return value;
            }

            return TryGetChoiceValueFromObjectGraph(chatResponseUpdate?.RawRepresentation, key, out value)
                || TryGetChoiceValueFromObjectGraph(chatResponseUpdate, key, out value)
                ? value
                : null;
        }

        public static BinaryData GetChoiceValue(this ChatResponse chatResponse, string key)
        {
            if (TryGetCompatibleChoiceValue(chatResponse?.RawRepresentation, key, out var compatibleValue))
            {
                return compatibleValue;
            }

            ChatMessage openai_msg = (chatResponse?.Messages != null && chatResponse.Messages.Count > 0) ? chatResponse.Messages[0] : null;
            if (TryGetCompatibleChoiceValue(openai_msg?.RawRepresentation, key, out compatibleValue))
            {
                return compatibleValue;
            }

            var openai_cc = chatResponse?.RawRepresentation as OpenAI.Chat.ChatCompletion
                ?? openai_msg?.RawRepresentation as OpenAI.Chat.ChatCompletion;

            var directAdditionalRawData = openai_cc != null
                ? GetNonPublicPropertyValue(openai_cc, "SerializedAdditionalRawData") as IDictionary<string, BinaryData>
                : null;

            if (directAdditionalRawData != null && directAdditionalRawData.TryGetValue(key, out var directValue))
            {
                return directValue;
            }

            var choices = (openai_cc != null ? GetNonPublicPropertyValue(openai_cc, "Choices") : null) as IReadOnlyList<object>;

            var icd = (choices != null && choices.Count > 0) ? choices[0] : null;

            var msg = icd != null ? GetNonPublicPropertyValue(icd, "Message") : null;

            var ard = msg != null ? GetNonPublicPropertyValue(msg, "SerializedAdditionalRawData") as IDictionary<string, BinaryData> : null;

            if (ard != null && ard.TryGetValue(key, out var value))
            {
                return value;
            }

            return TryGetChoiceValueFromObjectGraph(chatResponse?.RawRepresentation, key, out value)
                || TryGetChoiceValueFromObjectGraph(openai_msg?.RawRepresentation, key, out value)
                || TryGetChoiceValueFromObjectGraph(chatResponse, key, out value)
                ? value
                : null;
        }


        private static object GetNonPublicPropertyValue(object obj, string propertyName)
        {
            return obj?.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj);
        }

        private static bool TryGetCompatibleChoiceValue(object rawRepresentation, string key, out BinaryData value)
        {
            if (rawRepresentation is ResponsesChoiceRawData rawData
                && rawData.Values.TryGetValue(key, out value))
            {
                return true;
            }

            if (rawRepresentation is ResponsesStreamingUpdateRawData streamingRawData
                && streamingRawData.Values.TryGetValue(key, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        private static bool TryGetChoiceValueFromObjectGraph(object? source, string key, out BinaryData value)
        {
            value = null;
            if (source == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return TryGetChoiceValueFromObjectGraphCore(
                source,
                key,
                new HashSet<object>(ReferenceEqualityComparer.Instance),
                depth: 0,
                out value);
        }

        private static bool TryGetChoiceValueFromObjectGraphCore(
            object? source,
            string key,
            HashSet<object> visited,
            int depth,
            out BinaryData value)
        {
            value = null;
            if (source == null || depth > 8)
            {
                return false;
            }

            if (source is JsonElement jsonElement)
            {
                return TryGetChoiceValueFromJsonElement(jsonElement, key, depth + 1, out value);
            }

            if (source is BinaryData binaryData)
            {
                return TryGetChoiceValueFromBinaryData(binaryData, key, depth + 1, out value);
            }

            if (source is string)
            {
                return false;
            }

            if (!source.GetType().IsValueType && !visited.Add(source))
            {
                return false;
            }

            if (source is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is string dictionaryKey
                        && string.Equals(dictionaryKey, key, StringComparison.OrdinalIgnoreCase)
                        && TryConvertToBinaryData(entry.Value, out value))
                    {
                        return true;
                    }

                    if (TryGetChoiceValueFromObjectGraphCore(entry.Value, key, visited, depth + 1, out value))
                    {
                        return true;
                    }
                }

                return false;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var property in source.GetType().GetProperties(flags))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                object? propertyValue;
                try
                {
                    propertyValue = property.GetValue(source);
                }
                catch
                {
                    continue;
                }

                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase)
                    && TryConvertToBinaryData(propertyValue, out value))
                {
                    return true;
                }

                if (TryGetChoiceValueFromObjectGraphCore(propertyValue, key, visited, depth + 1, out value))
                {
                    return true;
                }
            }

            foreach (var field in source.GetType().GetFields(flags))
            {
                object? fieldValue;
                try
                {
                    fieldValue = field.GetValue(source);
                }
                catch
                {
                    continue;
                }

                if (string.Equals(field.Name, key, StringComparison.OrdinalIgnoreCase)
                    && TryConvertToBinaryData(fieldValue, out value))
                {
                    return true;
                }

                if (TryGetChoiceValueFromObjectGraphCore(fieldValue, key, visited, depth + 1, out value))
                {
                    return true;
                }
            }

            if (source is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (TryGetChoiceValueFromObjectGraphCore(item, key, visited, depth + 1, out value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetChoiceValueFromBinaryData(BinaryData binaryData, string key, int depth, out BinaryData value)
        {
            value = null;
            var rawText = binaryData.ToString();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return false;
            }

            try
            {
                using var jsonDocument = JsonDocument.Parse(rawText);
                return TryGetChoiceValueFromJsonElement(jsonDocument.RootElement, key, depth + 1, out value);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryGetChoiceValueFromJsonElement(JsonElement element, string key, int depth, out BinaryData value)
        {
            value = null;
            if (depth > 8)
            {
                return false;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase)
                            && TryConvertToBinaryData(property.Value, out value))
                        {
                            return true;
                        }

                        if (TryGetChoiceValueFromJsonElement(property.Value, key, depth + 1, out value))
                        {
                            return true;
                        }
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        if (TryGetChoiceValueFromJsonElement(item, key, depth + 1, out value))
                        {
                            return true;
                        }
                    }
                    break;
            }

            return false;
        }

        private static bool TryConvertToBinaryData(object? source, out BinaryData value)
        {
            value = null;
            switch (source)
            {
                case null:
                    return false;
                case BinaryData binaryData:
                    value = binaryData;
                    return true;
                case JsonElement jsonElement when jsonElement.ValueKind != JsonValueKind.Null && jsonElement.ValueKind != JsonValueKind.Undefined:
                    value = BinaryData.FromString(jsonElement.ValueKind == JsonValueKind.String
                        ? jsonElement.GetString() ?? string.Empty
                        : jsonElement.GetRawText());
                    return true;
                case string text when !string.IsNullOrWhiteSpace(text):
                    value = BinaryData.FromString(text);
                    return true;
                case bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    value = BinaryData.FromString(JsonSerializer.Serialize(source));
                    return true;
                default:
                    return false;
            }
        }

    }
}

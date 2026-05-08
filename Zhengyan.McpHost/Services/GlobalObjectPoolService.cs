using System.ClientModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using OpenAI;
using Serilog;
using Zhengyan.McpHost.Config;
using Zhengyan.McpHost.Custom;

namespace Zhengyan.McpHost.Services
{
    public class GlobalObjectPoolService
    {
        public StorageConfig ChatClientsStorageConfig { get; set; }
        public Dictionary<string, ChatClientConfig> ChatClientConfigs { get; } = new();
        // public Dictionary<string, ChatClientConfig> SamplingChatClientConfigs { get; } = new();
        public Dictionary<string, IChatClient> ChatClients { get; } = new();

        public bool AddChatClientConfig(ChatClientConfig chatClientConfig, bool persistFlag = false)
        {
            try
            {
                bool ret = true;
                if (!chatClientConfig.AsSampling)
                {
                    IChatClient chatClient = CreateManagedChatClient(chatClientConfig);
                    ret &= this.ChatClients.TryAdd(chatClientConfig.ID, chatClient);
                }
                ret &= this.ChatClientConfigs.TryAdd(chatClientConfig.ID, chatClientConfig);
                if (ret && persistFlag)
                {
                    SaveConfig(chatClientConfig, ChatClientsStorageConfig);
                }
                return ret;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to add ChatClient config: {ex.Message}");
                return false;
            }
        }

        public bool UpdateChatClientConfig(ChatClientConfig chatClientConfig, bool persistFlag = false)
        {
            if (!this.ChatClientConfigs.TryGetValue(chatClientConfig.ID, out var existingConfig))
            {
                return false;
            }

            if (this.ChatClients.TryGetValue(chatClientConfig.ID, out var existingClient))
            {
                existingClient.Dispose();
                this.ChatClients.Remove(chatClientConfig.ID);
            }

            CopyChatClientConfig(existingConfig, chatClientConfig);
            if (!existingConfig.AsSampling)
            {
                this.ChatClients[existingConfig.ID] = CreateManagedChatClient(existingConfig);
            }

            if (persistFlag)
            {
                SaveConfig(existingConfig, ChatClientsStorageConfig);
            }

            return true;
        }

        public bool DeleteChatClientConfig(string chatClientID, bool persistFlag = false)
        {
            if (IsChatClientLocked(chatClientID))
            {
                Log.Error($"ChatClient: {chatClientID} is locked by agent or mcpclient, please unlock it first.");
                return false;
            }

            bool ret = this.ChatClients.Remove(chatClientID);
            ret &= this.ChatClientConfigs.Remove(chatClientID);
            if (ret && persistFlag)
            {
                DeleteConfig(chatClientID, ChatClientsStorageConfig);
            }
            return ret;
        }

        private bool IsChatClientLocked(string chatClientID)
        {
            foreach (var agentConfig in AgentConfigs.Values)
            {
                if (agentConfig.ChatClientID == chatClientID)
                {
                    return true;
                }
            }

            foreach (var mcpClientConfig in McpClientConfigs.Values)
            {
                if (mcpClientConfig.SamplingChatClientID == chatClientID)
                {
                    return true;
                }
            }
            return false;
        }

        internal static IChatClient CreateManagedChatClient(ChatClientConfig chatClientConfig, bool enableFunctionInvocation = true)
        {
            IChatClient managedChatClient = string.Equals(chatClientConfig.ApiMode, "responses", StringComparison.OrdinalIgnoreCase)
                ? new OpenAICompatibleResponsesChatClient(chatClientConfig.Endpoint, chatClientConfig.ApiKey, chatClientConfig.ModelId)
                : CreateOpenAIChatClient(chatClientConfig);

            var builder = managedChatClient.AsBuilder();
            if (enableFunctionInvocation)
            {
                builder.UseFunctionInvocation();
            }

            return builder.Build();
        }

        private static IChatClient CreateOpenAIChatClient(ChatClientConfig chatClientConfig)
        {
            var apiKeyCredential = new ApiKeyCredential(chatClientConfig.ApiKey);
            var aiClientOptions = new OpenAIClientOptions
            {
                Endpoint = new Uri(chatClientConfig.Endpoint)
            };

            var openAIClient = new OpenAIClient(apiKeyCredential, aiClientOptions);
            return openAIClient.GetChatClient(chatClientConfig.ModelId).AsIChatClient();
        }

        private bool IsMcpClientLocked(string mcpClientID)
        {
            foreach (var agentConfig in AgentConfigs.Values)
            {
                if (agentConfig.McpClientIDs != null && agentConfig.McpClientIDs.Contains(mcpClientID))
                {
                    return true;
                }
            }
            return false;
        }

        private void Save<T>(T t, string filepath)
        {
            string dirpath = Path.GetDirectoryName(filepath);
            if (!Directory.Exists(dirpath))
            {
                Directory.CreateDirectory(dirpath);
            }

            File.WriteAllText(filepath, t.ToString());
        }

        private void Delete(string filepath)
        {
            if (File.Exists(filepath))
            {
                File.Delete(filepath);
                Log.Information($"Deleted file: {filepath}");
            }
        }

        private void SaveConfig(dynamic config, StorageConfig storageConfig)
        {
            string filepath = Path.Combine(storageConfig.StorageFolderPath, $"{config.ID}{storageConfig.FileExtension}");
            DeleteConfig(config.ID, storageConfig);
            Save(config, filepath);
        }

        private void DeleteConfig(string id, StorageConfig storageConfig)
        {
            // string filepath = Path.Combine(dirpath, id + ".json");
            var files = Directory.GetFiles(storageConfig.StorageFolderPath, $"*{storageConfig.FileExtension}", SearchOption.AllDirectories);
            foreach (var filepath in files)
            {
                if (GetIDByJsonFile(filepath) != id)
                {
                    continue;
                }
                Delete(filepath);
            }
        }

        private string GetIDByJsonFile(string jsonFilePath)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(jsonFilePath),
                     new JsonDocumentOptions
                     {
                         CommentHandling = JsonCommentHandling.Skip
                     }))
                {
                    JsonElement root = document.RootElement;
                    string id = root.GetProperty("ID").GetString();

                    return id;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to get ID from JSON file: {ex.Message}");
                return null;
            }
        }



        public StorageConfig AgentsStorageConfig { get; set; }
        public Dictionary<string, AgentConfig> AgentConfigs { get; } = new();

        public bool AddAgentConfig(AgentConfig agentConfig, bool persistFlag = false)
        {
            bool ret = this.AgentConfigs.TryAdd(agentConfig.ID, agentConfig);
            if (ret && persistFlag)
            {
                SaveConfig(agentConfig, AgentsStorageConfig);
            }
            return ret;
        }

        public bool UpdateAgentConfig(AgentConfig agentConfig, bool persistFlag = false)
        {
            if (!this.AgentConfigs.TryGetValue(agentConfig.ID, out var existingConfig))
            {
                return false;
            }

            CopyAgentConfig(existingConfig, agentConfig);
            if (persistFlag)
            {
                SaveConfig(existingConfig, AgentsStorageConfig);
            }
            return true;
        }

        public bool DeleteAgentConfig(string agentID, bool persistFlag = false)
        {
            bool ret = this.AgentConfigs.Remove(agentID);
            if (ret && persistFlag)
            {
                DeleteConfig(agentID, AgentsStorageConfig);
            }
            return ret;
        }

        public StorageConfig McpClientsStorageConfig { get; set; }
        public Dictionary<string, McpClientConfig> McpClientConfigs { get; } = new();
        public Dictionary<string, AutoReconnectMcpClient> McpClients { get; } = new();
        public Dictionary<string, IList<AITool>> McpClientTools { get; } = new();

#if false
        public JsonSerializerOptions? McpToolJsonSerializerOptions { get; set; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
#else
        public JsonSerializerOptions? McpToolJsonSerializerOptions { get; set; } = null;
#endif

        public async Task<bool> AddMcpClientConfigAsync(McpClientConfig mcpClientConfig, bool persistFlag = false)
        {
            bool ret = true;
            try
            {
                if (mcpClientConfig.Enabled)
                {
                    var mcpClient = await CreateMcpClientAsync(mcpClientConfig);

                    if (mcpClient == null)
                    {
                        Log.Error($"Failed to create McpClient: {mcpClientConfig.ToString()}");
                        return false;
                    }

                    ret &= this.McpClients.TryAdd(mcpClientConfig.ID, mcpClient);
                    ret &= this.McpClientTools.TryAdd(mcpClientConfig.ID, [.. await mcpClient.GetChatToolsAsync(cancellationToken: default)]);
                }
                ret &= this.McpClientConfigs.TryAdd(mcpClientConfig.ID, mcpClientConfig);
                if (ret && persistFlag)
                {
                    SaveConfig(mcpClientConfig, McpClientsStorageConfig);
                }
                return ret;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to add McpClient config: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateMcpClientConfigAsync(McpClientConfig mcpClientConfig, bool persistFlag = false)
        {
            if (!this.McpClientConfigs.TryGetValue(mcpClientConfig.ID, out var existingConfig))
            {
                return false;
            }

            CopyMcpClientConfig(existingConfig, mcpClientConfig);

            if (existingConfig.Enabled)
            {
                if (!this.McpClients.TryGetValue(existingConfig.ID, out var existingClient))
                {
                    existingClient = await CreateMcpClientAsync(existingConfig);
                    if (existingClient == null)
                    {
                        return false;
                    }

                    this.McpClients[existingConfig.ID] = existingClient;
                }
                else
                {
                    await existingClient.InitMcpClientAsync();
                }

                this.McpClientTools[existingConfig.ID] = [.. await existingClient.GetChatToolsAsync(forceRefresh: true)];
            }
            else
            {
                if (this.McpClients.TryGetValue(existingConfig.ID, out var existingClient))
                {
                    await existingClient.DisposeAsync();
                }

                this.McpClients.Remove(existingConfig.ID);
                this.McpClientTools.Remove(existingConfig.ID);
            }

            if (persistFlag)
            {
                SaveConfig(existingConfig, McpClientsStorageConfig);
            }

            return true;
        }

        public async Task<AutoReconnectMcpClient?> CreateMcpClientAsync(McpClientConfig mcpClientConfig)
        {
            ChatClientConfig? samplingChatClientConfig = null;
            if (!string.IsNullOrWhiteSpace(mcpClientConfig.SamplingChatClientID))
            {
                this.ChatClientConfigs.TryGetValue(mcpClientConfig.SamplingChatClientID, out samplingChatClientConfig);
            }

            try
            {
                return await AutoReconnectMcpClient.CreateAsync(mcpClientConfig, samplingChatClientConfig, McpToolJsonSerializerOptions);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to create {mcpClientConfig} McpClient: {ex.Message}\n{ex.StackTrace}");
            }

            return null;
        }


        public async Task<bool> DeleteMcpClientConfigAsync(string mcpClientID, bool persistFlag = false, bool forced = false)
        {
            if (!forced && IsMcpClientLocked(mcpClientID))
            {
                Log.Error($"McpClient: {mcpClientID} is locked by agent, please unlock it first.");
                return false;
            }
            this.McpClients.TryGetValue(mcpClientID, out var mcpClient);
            if (mcpClient != null)
            {
                await mcpClient.DisposeAsync();
            }
            this.McpClients.Remove(mcpClientID);
            this.McpClientTools.Remove(mcpClientID);
            bool ret = this.McpClientConfigs.Remove(mcpClientID);
            if (ret && persistFlag)
            {
                DeleteConfig(mcpClientID, McpClientsStorageConfig);
            }
            return ret;
        }

        public async Task<bool> StopMcpClientAsync(string mcpClientID)
        {
            bool ret = true;
            this.McpClients.TryGetValue(mcpClientID, out var mcpClient);
            if (mcpClient != null)
            {
                await mcpClient.DisposeAsync();
            }
            ret &= this.McpClients.Remove(mcpClientID);
            ret &= this.McpClientTools.Remove(mcpClientID);
            if (this.McpClientConfigs.TryGetValue(mcpClientID, out var mcpClientConfig))
            {
                mcpClientConfig.Enabled = false;
                SaveConfig(mcpClientConfig, McpClientsStorageConfig);
                ret &= true;
            }
            else
            {
                ret &= false;
            }
            return ret;
        }

        public async Task<bool> RestartMcpClientAsync(string mcpClientID)
        {
            bool ret = true;
            if (this.McpClientConfigs.TryGetValue(mcpClientID, out var mcpClientConfig))
            {
                if (!this.McpClients.TryGetValue(mcpClientID, out var mcpClient))
                {
                    mcpClient = await CreateMcpClientAsync(mcpClientConfig);
                    if (mcpClient == null)
                    {
                        Log.Error($"Failed to create McpClient: {mcpClientConfig.ToString()}");
                        return false;
                    }

                    ret &= this.McpClients.TryAdd(mcpClientConfig.ID, mcpClient);
                }
                else
                {
                    await mcpClient.InitMcpClientAsync();
                }

                mcpClientConfig.Enabled = true;
                this.McpClientTools[mcpClientConfig.ID] = [.. await mcpClient.GetChatToolsAsync(forceRefresh: true)];
                SaveConfig(mcpClientConfig, McpClientsStorageConfig);
            }
            else
            {
                ret &= false;
            }
            return ret;
        }

        private static void CopyChatClientConfig(ChatClientConfig target, ChatClientConfig source)
        {
            target.Endpoint = source.Endpoint;
            target.ApiKey = source.ApiKey;
            target.ModelId = source.ModelId;
            target.ApiMode = source.ApiMode;
            target.AsSampling = source.AsSampling;
        }

        private static void CopyAgentConfig(AgentConfig target, AgentConfig source)
        {
            target.ChatClientID = source.ChatClientID;
            target.McpClientIDs = source.McpClientIDs?.ToArray();
            target.SystemPrompt = source.SystemPrompt;
            target.ApiKeyExpirations = source.ApiKeyExpirations == null
                ? null
                : new Dictionary<string, string>(source.ApiKeyExpirations);
        }

        private static void CopyMcpClientConfig(McpClientConfig target, McpClientConfig source)
        {
            target.Name = source.Name;
            target.Enabled = source.Enabled;
            target.Description = source.Description;
            target.SamplingChatClientID = source.SamplingChatClientID;
            target.StdioConfig = CloneStdioConfig(source.StdioConfig);
            target.SseConfig = CloneSseConfig(source.SseConfig);
            target.StreamableHttpConfig = CloneStreamableHttpConfig(source.StreamableHttpConfig);
        }

        private static StdioConfig? CloneStdioConfig(StdioConfig? source)
        {
            if (source == null)
            {
                return null;
            }

            return new StdioConfig
            {
                Command = source.Command,
                Arguments = source.Arguments?.ToArray(),
                EnvironmentVariables = source.EnvironmentVariables == null ? null : new Dictionary<string, string>(source.EnvironmentVariables),
                ShutdownTimeout = source.ShutdownTimeout,
                WorkingDirectory = source.WorkingDirectory
            };
        }

        private static SseConfig? CloneSseConfig(SseConfig? source)
        {
            if (source == null)
            {
                return null;
            }

            return new SseConfig
            {
                Endpoint = source.Endpoint,
                AdditionalHeaders = source.AdditionalHeaders == null ? null : new Dictionary<string, string>(source.AdditionalHeaders),
                ConnectionTimeout = source.ConnectionTimeout
            };
        }

        private static StreamableHttpConfig? CloneStreamableHttpConfig(StreamableHttpConfig? source)
        {
            if (source == null)
            {
                return null;
            }

            return new StreamableHttpConfig
            {
                Endpoint = source.Endpoint,
                AdditionalHeaders = source.AdditionalHeaders == null ? null : new Dictionary<string, string>(source.AdditionalHeaders),
                ConnectionTimeout = source.ConnectionTimeout
            };
        }
    }
}

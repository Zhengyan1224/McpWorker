using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Zhengyan.McpHost.Config
{
    /// <summary>
    /// ChatClient配置
    /// </summary>
    public class ChatClientConfig
    {
        /// <summary>
        /// ID
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// Api端点地址
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// Api Key
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// 模型ID
        /// </summary>
        public string ModelId { get; set; }

        public string ApiMode { get; set; } = "chat";

        public bool AsSampling { get; set; } = false;

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            });
        }
    }

    /// <summary>
    /// ChatClient配置集合
    /// </summary>
    public class ChatClientsConfig
    {
        /// <summary>
        /// ChatClient配置列表
        /// </summary>
        public List<ChatClientConfig> ChatClients { get; set; } = new List<ChatClientConfig>();

        /// <summary>
        /// ChatClient配置的存储配置
        /// </summary>
        public StorageConfig? Storage { get; set; } = null;

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            });
        }
    }
}

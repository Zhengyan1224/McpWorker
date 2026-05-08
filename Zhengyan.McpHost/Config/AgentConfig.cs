using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Zhengyan.McpHost.Config
{
    /// <summary>
    /// 智能体配置
    /// </summary>
    public class AgentConfig
    {
        /// <summary>
        /// ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Chat Client ID
        /// </summary>
        public string ChatClientID { get; set; }

        /// <summary>
        /// Mcp Client ID集合
        /// </summary>
        public string[]? McpClientIDs { get; set; }

        /// <summary>
        /// 系统提示词
        /// </summary>
        public string? SystemPrompt { get; set; }

        /// <summary>
        /// 设置Api-Key以及过期时间（Key为Api-Key，Value为过期时间（yyyy-MM-dd HH:mm:ss））
        /// </summary>
        public Dictionary<string, string>? ApiKeyExpirations { get; set; } = new Dictionary<string, string>();

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
    /// 智能体配置集合
    /// </summary>
    public class AgentsConfig
    {
        /// <summary>
        /// 智能体配置列表
        /// </summary>
        public List<AgentConfig> Agents { get; set; } = new List<AgentConfig>();

        /// <summary>
        /// 智能体配置的存储配置
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
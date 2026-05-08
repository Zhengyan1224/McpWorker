using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Zhengyan.McpHost.Config
{
    /// <summary>
    /// MCP 客户端配置
    /// </summary>
    public class McpClientConfig
    {
        /// <summary>
        /// ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 提供给 MCP Server 进行 Sampling 的 ChatClient ID
        /// </summary>
        public string? SamplingChatClientID { get; set; }

        /// <summary>
        /// stdio 配置（stdio/sse/streamable http 三选一）
        /// </summary>
        public StdioConfig? StdioConfig { get; set; }

        /// <summary>
        /// sse 配置（stdio/sse/streamable http 三选一）
        /// </summary>
        public SseConfig? SseConfig { get; set; }

        /// <summary>
        /// streamable http 配置（stdio/sse/streamable http 三选一）
        /// </summary>
        public StreamableHttpConfig? StreamableHttpConfig { get; set; }

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
    /// stdio 配置
    /// </summary>
    public class StdioConfig
    {
        /// <summary>
        /// 执行命令
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// 执行参数
        /// </summary>
        public string[]? Arguments { get; set; }

        /// <summary>
        /// 环境变量
        /// </summary>
        public Dictionary<string, string>? EnvironmentVariables { get; set; }

        /// <summary>
        /// 关闭等待超时时间（秒）
        /// </summary>
        public double ShutdownTimeout { get; set; } = 5.0;

        /// <summary>
        /// 工作目录
        /// </summary>
        public string? WorkingDirectory { get; set; }

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
    /// SSE 配置
    /// </summary>
    public class SseConfig
    {
        /// <summary>
        /// SSE 端点地址
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// 附加请求头
        /// </summary>
        public Dictionary<string, string>? AdditionalHeaders { get; init; }

        /// <summary>
        /// 连接超时时间（秒）
        /// </summary>
        public double ConnectionTimeout { get; set; } = 30.0;

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
    /// Streamable HTTP 配置
    /// </summary>
    public class StreamableHttpConfig
    {
        /// <summary>
        /// Streamable HTTP 端点地址
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// 附加请求头
        /// </summary>
        public Dictionary<string, string>? AdditionalHeaders { get; init; }

        /// <summary>
        /// 连接超时时间（秒）
        /// </summary>
        public double ConnectionTimeout { get; set; } = 30.0;

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
    /// MCP 客户端配置集合
    /// </summary>
    public class McpClientsConfig
    {
        /// <summary>
        /// MCP 客户端配置列表
        /// </summary>
        public List<McpClientConfig> McpClients { get; set; } = new();

        /// <summary>
        /// 持久化存储配置
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

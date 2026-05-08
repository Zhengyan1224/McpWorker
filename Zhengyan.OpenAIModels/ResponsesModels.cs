using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zhengyan.OpenAIModels
{
    /// <summary>
    /// Responses API 请求
    /// https://platform.openai.com/docs/api-reference/responses/create
    /// </summary>
    public class ResponseRequest
    {
        /// <summary>
        /// 模型名称
        /// </summary>
        public string model { get; set; } = string.Empty;

        /// <summary>
        /// 输入内容，可以是字符串、消息对象或消息数组。
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? input { get; set; }

        /// <summary>
        /// 附加系统级指令。
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? instructions { get; set; }

        /// <summary>
        /// 是否启用流式响应。
        /// </summary>
        public bool stream { get; set; } = false;

        /// <summary>
        /// 采样温度。
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? temperature { get; set; }

        /// <summary>
        /// Top-p 采样参数。
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? top_p { get; set; }

        /// <summary>
        /// 最大输出 token 数。
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? max_output_tokens { get; set; }

        /// <summary>
        /// 是否允许并行工具调用。
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? parallel_tool_calls { get; set; }

        /// <summary>
        /// 工具选择策略。
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? tool_choice { get; set; }

        /// <summary>
        /// 工具列表。
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? tools { get; set; }

        /// <summary>
        /// 最终用户标识。
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? user { get; set; }

        /// <summary>
        /// 元数据。
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, JsonElement>? metadata { get; set; }

        /// <summary>
        /// 兼容未显式建模的其他字段。
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? additional_properties { get; set; }
    }

    /// <summary>
    /// Responses API 响应
    /// </summary>
    public class ResponseResponse
    {
        public string id { get; set; } = string.Empty;

        public string @object { get; set; } = "response";

        public long created_at { get; set; }

        public string status { get; set; } = "completed";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? error { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? incomplete_details { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? instructions { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? max_output_tokens { get; set; }

        public string model { get; set; } = string.Empty;

        public ResponseOutputItem[] output { get; set; } = Array.Empty<ResponseOutputItem>();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? parallel_tool_calls { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? previous_response_id { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? store { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? temperature { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ResponseTextConfig? text { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? tool_choice { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? tools { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? top_p { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? truncation { get; set; }

        public ResponseUsage usage { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? user { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, JsonElement>? metadata { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? additional_properties { get; set; }
    }

    /// <summary>
    /// 响应输出项
    /// </summary>
    public class ResponseOutputItem
    {
        public string id { get; set; } = string.Empty;

        public string type { get; set; } = "message";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? status { get; set; } = "completed";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? role { get; set; } = "assistant";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ResponseContentPart[]? content { get; set; }

        /// <summary>
        /// 保留现有 Host 自定义附加信息，例如工具调用结果。
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object?>? additional_properties { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? extension_data { get; set; }
    }

    /// <summary>
    /// 输出内容块
    /// </summary>
    public class ResponseContentPart
    {
        public string type { get; set; } = "output_text";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? text { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object[]? annotations { get; set; } = Array.Empty<object>();

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? extension_data { get; set; }
    }

    /// <summary>
    /// 文本输出配置
    /// </summary>
    public class ResponseTextConfig
    {
        public ResponseTextFormat format { get; set; } = new();
    }

    /// <summary>
    /// 文本输出格式
    /// </summary>
    public class ResponseTextFormat
    {
        public string type { get; set; } = "text";
    }

    /// <summary>
    /// Responses API token 使用统计
    /// </summary>
    public class ResponseUsage
    {
        public long input_tokens { get; set; }

        public long output_tokens { get; set; }

        public long total_tokens { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ResponseTokenDetails? input_tokens_details { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ResponseTokenDetails? output_tokens_details { get; set; }
    }

    /// <summary>
    /// token 细分统计
    /// </summary>
    public class ResponseTokenDetails
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? cached_tokens { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? reasoning_tokens { get; set; }
    }
}

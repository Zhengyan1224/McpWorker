using System.Text.Json.Serialization;

using System.Text.Json;

namespace Zhengyan.OpenAIModels
{
    /// <summary>
    /// 对话完成请求
    /// https://platform.openai.com/docs/api-reference/chat/create
    /// </summary>
    public class ChatCompletionRequest : BaseCompletionRequest
    {

        /// <summary>
        /// 对话历史
        /// </summary>
        public ChatCompletionMessage[] messages { get; set; } = Array.Empty<ChatCompletionMessage>();

        /// <summary>
        /// 控制模型是否调用某个工具，以及如何调用。
        /// 可以是字符串（"none", "auto", "required", "parallel"）或一个指定工具的对象。
        /// {"type": "function", "function": {"name": "my_function"}}
        /// 默认为"none"，表示模型不会调用任何工具，而是生成一条消息。
        /// 如果存在工具，则默认为"auto"。
        /// required，表示必须调用一个或多个工具
        /// parallel，表示并行调用多个工具
        /// </summary>
        /// <example>null</example>
        public object? tool_choice { get; set; }

        /// <summary>
        /// 模型可能调用的工具列表。目前，仅支持函数作为工具。
        /// 使用它可以提供模型可以为其生成JSON输入的函数列表。
        /// 最多支持128个功能。
        /// </summary>
        /// <example>null</example>
        public ToolInfo[]? tools { get; set; }

        /// <summary>
        /// 兼容未显式建模的其他顶层字段。
        /// 例如百炼兼容模式 `chat/completions` 的 `enable_thinking`。
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? additional_properties { get; set; }
    }

    /// <summary>
    /// 对话消息列表
    /// </summary>
    public class ChatCompletionMessage
    {
        /// <summary>
        /// 角色
        /// system, user, assistant, tool
        /// </summary>
        /// <example>user</example>
        public string? role { get; set; } = string.Empty;
        /// <summary>
        /// 对话内容
        /// 兼容以下两种协议：
        /// 1. 纯文本：string
        /// 2. 多模态数组：[{type,text}|{type,image_url}]
        /// </summary>
        /// <example>你好</example>
        public object? content { get; set; }

        /// <summary>
        /// 推理内容
        /// </summary>
        public string? reasoning_content { get; set; }

        /// <summary>
        /// 工具调用信息
        /// </summary>
        /// <example>null</example>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ToolMeaasge[]? tool_calls { get; set; }

        /// <summary>
        /// 调用工具的 ID
        /// role 为 tool 时必填
        /// </summary>
        /// <example>null</example>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? tool_call_id { get; set; }

        /// <summary>
        /// 附加属性
        /// </summary>
        public Dictionary<string, object?>? additional_properties { get; set; } = null;
    }

    /// <summary>
    /// 多模态消息内容块
    /// </summary>
    public class ChatCompletionContentPart
    {
        /// <summary>
        /// 内容块类型
        /// text / image_url
        /// </summary>
        public string? type { get; set; }

        /// <summary>
        /// 文本内容（type=text）
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? text { get; set; }

        /// <summary>
        /// 图像 URL（type=image_url）
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ChatCompletionImageUrl? image_url { get; set; }
    }

    /// <summary>
    /// 图像 URL 对象
    /// </summary>
    public class ChatCompletionImageUrl
    {
        /// <summary>
        /// 图像地址（支持 http(s) 或 data URI）
        /// </summary>
        public string? url { get; set; }

        /// <summary>
        /// 图像细节级别
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? detail { get; set; }
    }

    /// <summary>
    /// 聊天完成响应
    /// </summary>
    public class ChatCompletionResponse : BaseCompletionResponse
    {

        /// <summary>
        /// 对象类型，始终为chat.completion
        /// </summary>
        public string @object = "chat.completion";

        /// <summary>
        /// 聊天完成选择的列表。如果n大于1，则可以有多个
        /// </summary>
        public ChatCompletionResponseChoice[] choices { get; set; } = Array.Empty<ChatCompletionResponseChoice>();

    }

    /// <summary>
    /// 完成的一种选择
    /// </summary>
    public class ChatCompletionResponseChoice : BaseCompletionResponseChoice
    {
        /// <summary>
        /// 模型生成的聊天完成消息
        /// </summary>
        public ChatCompletionMessage message { get; set; } = new();

    }

    /// <summary>
    /// 流式响应的聊天完成响应
    /// https://platform.openai.com/docs/api-reference/chat/streaming
    /// </summary>
    public class ChatCompletionChunkResponse : BaseCompletionResponse
    {

        /// <summary>
        /// 对象类型，始终为chat.completion.chunk
        /// </summary>
        public string @object = "chat.completion.chunk";

        /// <summary>
        /// 聊天完成选择的列表。如果n大于1，则可以有多个
        /// </summary>
        public ChatCompletionChunkResponseChoice[] choices { get; set; } = Array.Empty<ChatCompletionChunkResponseChoice>();
    }

    /// <summary>
    /// 流式响应完成的详情
    /// </summary>
    public class ChatCompletionChunkResponseChoice : BaseCompletionResponseChoice
    {
        /// <summary>
        /// 由流式模型响应生成的聊天完成增量。
        /// </summary>
        public ChatCompletionMessage? delta { get; set; } = new();
    }
}

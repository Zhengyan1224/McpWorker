# Zhengyan.OpenAIModels

`Zhengyan.OpenAIModels` 是 OpenAI 兼容请求/响应模型库。`Zhengyan.McpHost`、ChatUI 和部分 MCP Server 都使用它来序列化和解析 Chat Completions、Responses、Embeddings 和工具调用结构。

## 目标框架

```text
net8.0
net9.0
```

## 主要模型

| 文件 | 内容 |
| --- | --- |
| `ChatCompletionModels.cs` | `/v1/chat/completions` 请求、响应、流式 chunk、消息和图片输入结构。 |
| `ResponsesModels.cs` | `/v1/responses` 请求、响应、output item、content part、usage 结构。 |
| `EmbeddingModels.cs` | embeddings 请求和响应结构。 |
| `ToolModels.cs` | OpenAI function/tool 描述和工具消息结构。 |
| `BaseCompletionModels.cs` | completion/chat 共用字段。 |
| `ChatContentTextExtractor.cs` | 从文本或多模态消息中提取文本内容。 |

## 当前重点

- `ChatCompletionMessage` 支持 `reasoning_content` 字段，用于非流式 Chat Completions 推理过程输出。
- `ResponseOutputItem` 支持 `JsonExtensionData`，可保留 `type: "reasoning"` 的 `summary` 等扩展字段。
- 消息内容支持字符串和多模态数组两类输入。

这个项目不单独运行，由其他项目引用。

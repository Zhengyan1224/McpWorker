# Zhengyan.McpHost

`Zhengyan.McpHost` 是 McpWorker 的核心项目。它运行成一个 ASP.NET Core 服务，对外提供 OpenAI 兼容的对话 API，对内管理 ChatClient、McpClient 和 Agent，并把多个 MCP Server 暴露的工具组合到 Agent 对话里。

## 能力概览

- OpenAI 兼容接口：`/v1/chat/completions` 和 `/v1/responses`。
- ChatClient 管理：上游模型端点、API Key、模型 ID、调用模式。
- McpClient 管理：连接 `stdio`、`sse`、`streamablehttp/http` 类型的 MCP Server。
- Agent 管理：绑定 ChatClient、McpClient 列表、系统提示词和 API Key 过期策略。
- 模型配置列表和切换：供 ChatUI 或外部客户端选择当前 Agent。
- MCP 工具列表查询、工具调用、连接重启和停止。
- 静态管理页和 Swagger。
- 流式和非流式推理过程输出。

## 启动

从仓库根目录运行：

```powershell
dotnet run --project Zhengyan.McpHost\Zhengyan.McpHost.csproj
```

默认配置文件：

```text
Zhengyan.McpHost/profiles/mcphost.json
```

默认监听地址：

```text
http://localhost:9083
```

`mcphost.json` 中配置了统一 API 前缀：

```json
{
  "WebApi": {
    "CentralRoutePrefix": "mcphost/api"
  }
}
```

因此控制器中标注的 `/v1/chat/completions` 实际完整地址是：

```text
http://localhost:9083/mcphost/api/v1/chat/completions
```

## 常用地址

| 地址 | 用途 |
| --- | --- |
| `http://localhost:9083/mcphost/html` | 静态管理页。 |
| `http://localhost:9083/mcphost/swagger` | Swagger 文档。 |
| `http://localhost:9083/mcphost/api/v1/models/config` | 当前可用 Agent/模型列表。 |
| `http://localhost:9083/mcphost/api/v1/models/switch?id=0` | 切换当前 Agent/模型。 |
| `http://localhost:9083/mcphost/api/v1/chat/completions` | OpenAI 兼容 Chat Completions。 |
| `http://localhost:9083/mcphost/api/v1/responses` | OpenAI 兼容 Responses。 |

## 配置存储

`McpHost` 默认把三类配置分开存放：

| 目录 | 内容 |
| --- | --- |
| `profiles/chat` | ChatClient 配置。 |
| `profiles/mcp` | McpClient 配置。 |
| `profiles/agent` | Agent 配置。 |

这些目录由 `mcphost.json` 中的 `ChatClients:Storage`、`McpClients:Storage`、`Agents:Storage` 控制。通过配置接口或管理页新增/更新配置时，配置会持久化到这些目录。

## ChatClient

ChatClient 描述一个上游模型服务。

```json
{
  "ID": "my_model",
  "Endpoint": "https://example.com/v1",
  "ApiKey": "replace-with-your-key",
  "ModelId": "qwen3.6-plus",
  "ApiMode": "responses",
  "AsSampling": false
}
```

字段说明：

| 字段 | 说明 |
| --- | --- |
| `ID` | 本地唯一 ID，会被 Agent 引用。 |
| `Endpoint` | 上游 OpenAI 兼容 API 根地址，例如 `https://.../v1`。 |
| `ApiKey` | 上游模型服务密钥。 |
| `ModelId` | 上游模型名称。 |
| `ApiMode` | `chat` 或 `responses`，决定 Host 调用上游时使用的接口。 |
| `AsSampling` | 是否作为 MCP Server sampling 用的 ChatClient。 |

配置接口：

| 方法 | 路径 |
| --- | --- |
| `POST` | `/config/chatclient/add` |
| `PUT` | `/config/chatclient/update` |
| `DELETE` | `/config/chatclient/delete?id=<id>` |
| `GET` | `/config/chatclient/list` |
| `POST` | `/config/chatclient/chat/completions?id=<id>` |
| `POST` | `/config/chatclient/responses?id=<id>` |

完整路径需要加上统一前缀，例如：

```text
http://localhost:9083/mcphost/api/config/chatclient/list
```

## McpClient

McpClient 描述 `McpHost` 如何连接一个 MCP Server。一个配置中通常只启用一种传输方式。

### stdio

```json
{
  "ID": "skills_stdio",
  "Name": "Skills MCP Server",
  "Enabled": true,
  "Description": "Use local Skills server through stdio.",
  "SamplingChatClientID": null,
  "StdioConfig": {
    "Command": "dotnet",
    "Arguments": [
      "run",
      "--project",
      "../Zhengyan.McpServer.Skills"
    ],
    "EnvironmentVariables": {},
    "WorkingDirectory": "../Zhengyan.McpServer.Skills",
    "ShutdownTimeout": 5
  }
}
```

### Streamable HTTP

```json
{
  "ID": "skills_http",
  "Name": "Skills MCP StreamableHttp Server",
  "Enabled": true,
  "StreamableHttpConfig": {
    "Endpoint": "http://127.0.0.1:5006/skills",
    "AdditionalHeaders": {},
    "ConnectionTimeout": 100
  }
}
```

### SSE

```json
{
  "ID": "websearcher_sse",
  "Name": "WebSearcher MCP SSE Server",
  "Enabled": true,
  "SseConfig": {
    "Endpoint": "http://127.0.0.1:5001/websearcher/sse",
    "AdditionalHeaders": {},
    "ConnectionTimeout": 100
  }
}
```

配置接口：

| 方法 | 路径 |
| --- | --- |
| `POST` | `/config/mcpclient/add` |
| `PUT` | `/config/mcpclient/update` |
| `DELETE` | `/config/mcpclient/delete?id=<id>` |
| `GET` | `/config/mcpclient/list` |
| `GET` | `/config/mcpclient/tools?id=<id>` |
| `POST` | `/config/mcpclient/calltool?id=<id>&toolName=<tool>` |
| `POST` | `/config/mcpclient/restart?id=<id>` |
| `POST` | `/config/mcpclient/stop?id=<id>` |

`McpHost` 会在 MCP Client 启用时拉取工具列表，并把工具包装成 Agent 可调用的函数工具。

## Agent

Agent 把一个 ChatClient 和多个 McpClient 组合成一个可对话的配置。`/v1/models/config` 返回的模型列表本质上就是 Agent 列表。

```json
{
  "ID": "dev_agent",
  "ChatClientID": "my_model",
  "McpClientIDs": [
    "skills_http",
    "websearcher_sse"
  ],
  "SystemPrompt": "You are a tool-using assistant.",
  "ApiKeyExpirations": {
    "dev": "9999-12-31 23:59:59"
  }
}
```

字段说明：

| 字段 | 说明 |
| --- | --- |
| `ID` | Agent ID，也会显示在模型列表里。 |
| `ChatClientID` | 使用哪个 ChatClient 调模型。 |
| `McpClientIDs` | 当前 Agent 可用的 MCP Client 列表。 |
| `SystemPrompt` | 会附加到对话中的系统提示词。 |
| `ApiKeyExpirations` | 调用 Host 时允许的 API Key 及过期时间。 |

配置接口：

| 方法 | 路径 |
| --- | --- |
| `POST` | `/config/agent/add` |
| `PUT` | `/config/agent/update` |
| `DELETE` | `/config/agent/delete?id=<id>` |
| `GET` | `/config/agent/list` |

## 模型列表和切换

查询模型：

```powershell
curl http://localhost:9083/mcphost/api/v1/models/config
```

返回结构：

```json
{
  "current": 0,
  "models": [
    { "name": "dev_agent" }
  ]
}
```

切换当前模型：

```powershell
curl -X PUT "http://localhost:9083/mcphost/api/v1/models/switch?id=0"
```

ChatUI 会使用这两个接口加载模型和切换当前 Agent。

## Chat Completions

非流式请求：

```powershell
curl http://localhost:9083/mcphost/api/v1/chat/completions `
  -H "Content-Type: application/json" `
  -H "Authorization: Bearer dev" `
  -d '{
    "model": "dev_agent",
    "messages": [
      { "role": "user", "content": "你是谁？" }
    ],
    "stream": false
  }'
```

流式请求只需要设置：

```json
{
  "stream": true
}
```

推理过程输出：

- 流式：增量内容会跟随上游 reasoning/thinking delta 输出。
- 非流式：Host 会从上游结果中提取推理过程，并放入 `choices[].message.reasoning_content`。

示例结构：

```json
{
  "choices": [
    {
      "message": {
        "role": "assistant",
        "content": "最终回答",
        "reasoning_content": "推理过程"
      }
    }
  ]
}
```

## Responses

非流式请求：

```powershell
curl http://localhost:9083/mcphost/api/v1/responses `
  -H "Content-Type: application/json" `
  -H "Authorization: Bearer dev" `
  -d '{
    "model": "dev_agent",
    "input": "你知道我是谁吗？",
    "stream": false
  }'
```

推理过程输出：

- 流式：Host 输出 `response.reasoning_text.delta` 一类事件，并在事件内容中保留 `reasoning_content`。
- 非流式：Host 会优先保留上游 `type: "reasoning"` 输出项；如果上游只把推理过程放在附加字段里，Host 会转换成百炼/千问风格的 reasoning 输出项。

非流式 Responses 的目标结构：

```json
{
  "output": [
    {
      "id": "msg_xxx",
      "type": "reasoning",
      "summary": [
        {
          "type": "summary_text",
          "text": "推理过程"
        }
      ]
    },
    {
      "id": "msg_xxx",
      "type": "message",
      "role": "assistant",
      "status": "completed",
      "content": [
        {
          "type": "output_text",
          "text": "最终回答"
        }
      ]
    }
  ]
}
```

## 管理页

访问：

```text
http://localhost:9083/mcphost/html
```

管理页用于维护 ChatClient、McpClient、Agent 配置，也可以查看 MCP 工具列表、直接调用工具、重启或停止 MCP Client，并测试 `chat/completions` 与 `responses`。

## 安全注意

- 不要在公开仓库或生产镜像中保留真实 API Key、SSH 密码或内网地址。
- `ApiKeyExpirations` 控制调用 Host 的外部 API Key，不等同于上游模型 API Key。
- `McpClient` 使用 `stdio` 时会在本机启动进程，命令、工作目录和环境变量都应只配置可信内容。
- `Skills`、`SSH` 这类工具具备文件和命令能力，建议只在可信环境启用。

## 相关文档

- 根目录总览：[../README.MD](../README.MD)
- Chat Completions 示例：[samples/chat-completions/README.md](./samples/chat-completions/README.md)
- ChatUI CLI：[../Zhengyan.ChatUI.CLI/README.md](../Zhengyan.ChatUI.CLI/README.md)
- Skills MCP Server：[../Zhengyan.McpServer.Skills/README.md](../Zhengyan.McpServer.Skills/README.md)

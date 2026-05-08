# Zhengyan.McpServer.Agent

`Zhengyan.McpServer.Agent` 会把一个可调用的 Agent 包装成 MCP Server，对外暴露统一工具 `send_task`。调用方只需要传入自然语言任务，Agent Server 会使用内部 Agent 服务完成任务并返回文本结果。

## 启动模式

默认是 `stdio`：

```powershell
dotnet run --project Zhengyan.McpServer.Agent\Zhengyan.McpServer.Agent.csproj
```

显式使用 `stdio`：

```powershell
dotnet run --project Zhengyan.McpServer.Agent\Zhengyan.McpServer.Agent.csproj -- --mode stdio
```

使用 Streamable HTTP：

```powershell
dotnet run --project Zhengyan.McpServer.Agent\Zhengyan.McpServer.Agent.csproj -- --mode streamablehttp --urls http://0.0.0.0:5005
```

使用 SSE：

```powershell
dotnet run --project Zhengyan.McpServer.Agent\Zhengyan.McpServer.Agent.csproj -- --mode sse --urls http://0.0.0.0:5005
```

HTTP MCP 路径：

```text
http://127.0.0.1:5005/agent
```

SSE 路径通常是：

```text
http://127.0.0.1:5005/agent/sse
```

## 工具

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `send_task` | `instruction` | 接收自然语言任务指令，并返回 Agent 完成后的结果。 |

工具描述和参数描述来自 `profiles/mcp_agent.json`：

```text
ToolDescription
ArgumentDescription
```

## 配置

配置文件：

```text
Zhengyan.McpServer.Agent/profiles/mcp_agent.json
```

主要字段：

| 字段 | 说明 |
| --- | --- |
| `SystemPrompt` | Agent 使用的系统提示词。 |
| `MaxOutputTokens` | 最大输出 token。 |
| `Temperature` | 采样温度。 |
| `TopP` / `TopK` | 采样参数。 |
| `ToolDescription` | `send_task` 暴露给 MCP Client 的工具说明。 |
| `ArgumentDescription` | `instruction` 参数说明。 |

## 接入 McpHost

`McpHost` 可以通过 stdio 或 Streamable HTTP 接入本 Server。示例：

```json
{
  "ID": "agent_mcp_streamablehttp",
  "Name": "Agent MCP StreamableHttp Server",
  "Enabled": true,
  "SamplingChatClientID": "agent_qwen",
  "StreamableHttpConfig": {
    "Endpoint": "http://127.0.0.1:5005/agent",
    "ConnectionTimeout": 100
  }
}
```

如果使用 SSE 模式，旧版 SSE 传输要求有状态连接；当 `--mode sse --stateless true` 同时出现时，程序会强制改为 stateful。

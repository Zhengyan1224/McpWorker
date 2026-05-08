# Zhengyan.McpServer.Divine

`Zhengyan.McpServer.Divine` 是数字起卦和解签 MCP Server。它提供一个数字卦工具，并提供一个基于占卜结果和用户问题继续解读的工具。

## 启动模式

默认是 `stdio`：

```powershell
dotnet run --project Zhengyan.McpServer.Divine\Zhengyan.McpServer.Divine.csproj
```

Streamable HTTP：

```powershell
dotnet run --project Zhengyan.McpServer.Divine\Zhengyan.McpServer.Divine.csproj -- --mode streamablehttp --urls http://0.0.0.0:5006
```

SSE：

```powershell
dotnet run --project Zhengyan.McpServer.Divine\Zhengyan.McpServer.Divine.csproj -- --mode sse --urls http://0.0.0.0:5006
```

HTTP MCP 路径：

```text
http://127.0.0.1:5006/divine
```

## 工具

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `divine_by_3_numbers` | `num1`, `num2`, `num3` | 使用三个大于 100 的整数起卦，返回卦象和爻辞。 |
| `interpret` | `divination`, `question` | 根据卦象/爻辞和用户问题进行解签。 |

`interpret` 会使用 MCP sampling 能力调用模型，因此接入 `McpHost` 时建议给 McpClient 配置 `SamplingChatClientID`。

## 配置

配置文件：

```text
Zhengyan.McpServer.Divine/profiles/mcp_divine.json
```

主要字段：

| 字段 | 说明 |
| --- | --- |
| `ExplanationsFilePath` | 卦象解释资源路径。 |
| `Logger` | 日志配置。 |

资源文件默认位于：

```text
Zhengyan.McpServer.Divine/resources
```

## 接入 McpHost

```json
{
  "ID": "divine_mcp_streamablehttp",
  "Name": "Divine MCP StreamableHttp Server",
  "Enabled": true,
  "SamplingChatClientID": "agent_qwen",
  "StreamableHttpConfig": {
    "Endpoint": "http://127.0.0.1:5006/divine",
    "ConnectionTimeout": 100
  }
}
```

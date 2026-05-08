# Zhengyan.McpServer.EightChar

`Zhengyan.McpServer.EightChar` 是八字测算 MCP Server。它基于阳历年月日时和性别计算农历、八字、纳音、旬空等信息，并可基于结果和用户问题继续做命理解读。

## 启动模式

默认是 `stdio`：

```powershell
dotnet run --project Zhengyan.McpServer.EightChar\Zhengyan.McpServer.EightChar.csproj
```

Streamable HTTP：

```powershell
dotnet run --project Zhengyan.McpServer.EightChar\Zhengyan.McpServer.EightChar.csproj -- --mode streamablehttp --urls http://0.0.0.0:5007
```

SSE：

```powershell
dotnet run --project Zhengyan.McpServer.EightChar\Zhengyan.McpServer.EightChar.csproj -- --mode sse --urls http://0.0.0.0:5007
```

HTTP MCP 路径：

```text
http://127.0.0.1:5007/eightchar
```

## 工具

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `calculation` | `year`, `month`, `day`, `hour`, `gender` | 根据阳历时间和性别计算八字信息。 |
| `interpret` | `eightChar`, `question` | 根据八字详细内容和用户问题进行解读。 |

参数说明：

- `hour` 使用 0 到 23 的 24 小时制。
- `gender` 中 `1` 表示男，`0` 表示女。

`interpret` 会使用 MCP sampling 能力调用模型，因此接入 `McpHost` 时建议给 McpClient 配置 `SamplingChatClientID`。

## 配置

配置文件：

```text
Zhengyan.McpServer.EightChar/profiles/mcp_eightchar.json
```

主要配置目前集中在日志；测算能力依赖 `Zhengyan.Lunar`。

## 接入 McpHost

```json
{
  "ID": "eightchar_mcp_streamablehttp",
  "Name": "EightChar MCP StreamableHttp Server",
  "Enabled": true,
  "SamplingChatClientID": "agent_qwen",
  "StreamableHttpConfig": {
    "Endpoint": "http://127.0.0.1:5007/eightchar",
    "ConnectionTimeout": 100
  }
}
```

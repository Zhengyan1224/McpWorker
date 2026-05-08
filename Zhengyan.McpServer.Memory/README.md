# Zhengyan.McpServer.Memory

`Zhengyan.McpServer.Memory` 是长期记忆 MCP Server。它可以把稳定事实、偏好、承诺、结论等内容写入本地存储，并通过语义检索召回相关记忆，适合给 Agent 提供跨会话记忆能力。

## 启动模式

默认是 `stdio`：

```powershell
dotnet run --project Zhengyan.McpServer.Memory\Zhengyan.McpServer.Memory.csproj
```

Streamable HTTP：

```powershell
dotnet run --project Zhengyan.McpServer.Memory\Zhengyan.McpServer.Memory.csproj -- --mode streamablehttp --urls http://0.0.0.0:5011
```

SSE：

```powershell
dotnet run --project Zhengyan.McpServer.Memory\Zhengyan.McpServer.Memory.csproj -- --mode sse --urls http://0.0.0.0:5012
```

HTTP MCP 路径：

```text
http://127.0.0.1:5011/memory
```

## 工具

| 工具 | 说明 |
| --- | --- |
| `remember_memory` | 写入一条长期记忆。 |
| `recall_memory` | 根据自然语言 query 语义召回记忆。 |
| `list_memories` | 按更新时间列出记忆，可按 scope/tags 过滤。 |
| `forget_memory` | 按 memory ID 删除记忆。 |
| `rebuild_memory_index` | 重建本地向量索引。 |

常用参数：

| 工具 | 参数 |
| --- | --- |
| `remember_memory` | `content`, `scope`, `tags`, `metadataJson`, `summary`, `importance` |
| `recall_memory` | `query`, `topN`, `scope`, `tags`, `minSimilarity` |
| `list_memories` | `scope`, `tags`, `limit` |
| `forget_memory` | `memoryId` |

## 配置

配置文件：

```text
Zhengyan.McpServer.Memory/profiles/mcp_memory.json
```

主要字段：

| 字段 | 说明 |
| --- | --- |
| `StorageDirectoryPath` | 本地记忆存储目录，默认 `./storage`。 |
| `DefaultTopN` / `MaxTopN` | 召回默认和最大条数。 |
| `DefaultListLimit` / `MaxListLimit` | 列表默认和最大条数。 |
| `Embedding.Enabled` | 是否启用 embedding 语义检索。 |
| `Embedding.Endpoint` / `Model` / `ApiKey` | embedding 服务配置。 |
| `MaxMemoriesForEmbedding` | 参与 embedding 的最大记忆数。 |

不要在共享环境中保留真实 embedding API Key。

## 接入 McpHost

```json
{
  "ID": "memory_mcp_streamablehttp",
  "Name": "Memory MCP StreamableHttp Server",
  "Enabled": true,
  "Description": "Provide long-term memory storage and semantic recall for agents.",
  "StreamableHttpConfig": {
    "Endpoint": "http://127.0.0.1:5011/memory",
    "ConnectionTimeout": 100
  }
}
```

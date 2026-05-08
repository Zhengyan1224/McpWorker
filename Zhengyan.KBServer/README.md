# Zhengyan.KBServer

`Zhengyan.KBServer` 是知识库 HTTP + MCP 服务。它基于 `Zhengyan.KnowledgeBase` 和 `Zhengyan.VectorDB` 提供知识写入、搜索、删除，并可以作为 MCP Server 暴露知识库工具。

## 启动

```powershell
dotnet run --project Zhengyan.KBServer\Zhengyan.KBServer.csproj
```

默认配置文件：

```text
Zhengyan.KBServer/profiles/kbserver.json
```

默认监听：

```text
http://localhost:9084
```

## 常用地址

| 地址 | 用途 |
| --- | --- |
| `http://localhost:9084/kbserver/api/knowledge/add` | 写入知识。 |
| `http://localhost:9084/kbserver/api/knowledge/search` | 搜索知识。 |
| `http://localhost:9084/kbserver/api/knowledge/delete` | 删除知识。 |
| `http://localhost:9084/kbserver/api/knowledge/deletedb` | 删除知识库。 |
| `http://localhost:9084/kbserver/mcp` | MCP 路径。 |
| `http://localhost:9084/kbserver/swagger` | Swagger。 |

## MCP 工具

| 工具 | 说明 |
| --- | --- |
| `SearchKnowledge` | 从指定知识库中搜索与问题相关的内容。 |
| `AddKnowledge` | 向指定知识库写入知识内容。 |
| `Summarize` | 根据段落文本生成摘要。 |

## 配置

主要配置项：

| 字段 | 说明 |
| --- | --- |
| `WebApi:CentralRoutePrefix` | HTTP API 前缀，默认 `kbserver/api`。 |
| `McpServer:RoutePrefix` | MCP 路径，默认 `kbserver/mcp`。 |
| `McpServer:Mode` | MCP 传输模式，支持 `sse` / `streamablehttp`。 |
| `TextEmbedding` | 文本向量化服务配置。 |
| `TextProcessor` | 文本处理器配置。 |
| `KnowledgeBase:StorageBaseDir` | 知识库存储目录。 |
| `KnowledgeBase:TextFeaturesMode` | 文本特征模式。 |

请替换配置中的 embedding endpoint 和 API Key，避免提交真实凭据。

## 接入 McpHost

```json
{
  "ID": "kbserver_mcp_streamablehttp",
  "Name": "Knowledge Base MCP StreamableHttp Server",
  "Enabled": true,
  "StreamableHttpConfig": {
    "Endpoint": "http://127.0.0.1:9084/kbserver/mcp",
    "ConnectionTimeout": 100
  }
}
```

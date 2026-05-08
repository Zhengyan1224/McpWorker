# Zhengyan.McpServer.DataQuery

`Zhengyan.McpServer.DataQuery` 是面向本地 CSV 数据的 MCP Server。它会加载 `data` 目录中的 CSV，按自然语言 query 召回相关行，并可使用 embedding 和本地向量缓存提升语义检索效果。

## 启动模式

默认是 `stdio`：

```powershell
dotnet run --project Zhengyan.McpServer.DataQuery\Zhengyan.McpServer.DataQuery.csproj
```

Streamable HTTP：

```powershell
dotnet run --project Zhengyan.McpServer.DataQuery\Zhengyan.McpServer.DataQuery.csproj -- --mode streamablehttp --urls http://0.0.0.0:5008
```

SSE：

```powershell
dotnet run --project Zhengyan.McpServer.DataQuery\Zhengyan.McpServer.DataQuery.csproj -- --mode sse --urls http://0.0.0.0:5008
```

HTTP MCP 路径：

```text
http://127.0.0.1:5008/dataquery
```

## 工具

| 工具 | 说明 |
| --- | --- |
| `list_data_sources` | 列出已加载 CSV 文件和记录数。 |
| `query_data` | 按自然语言 query 召回相似数据行。 |
| `rebuild_data_index` | 重新加载 CSV、重建 embedding 缓存和向量索引。 |

`query_data` 常用参数：

| 参数 | 说明 |
| --- | --- |
| `query` | 自然语言查询。 |
| `topN` | 返回条数，默认 5。 |
| `sourceFiles` | 可选 CSV 文件过滤，例如 `小区.csv` 或逗号分隔列表。 |
| `minSimilarity` | 可选相似度阈值，范围 0 到 1。 |

## 构建缓存

DataQuery 额外支持 `buildcache` 模式：

```powershell
dotnet run --project Zhengyan.McpServer.DataQuery\Zhengyan.McpServer.DataQuery.csproj -- --mode buildcache
```

这个模式会加载数据并预构建缓存/索引，适合在接入 Host 前先初始化数据。

## 配置

配置文件：

```text
Zhengyan.McpServer.DataQuery/profiles/mcp_dataquery.json
```

主要字段：

| 字段 | 说明 |
| --- | --- |
| `DataDirectoryPath` | CSV 数据目录，默认 `./data`。 |
| `CacheDirectoryPath` | 缓存目录，默认 `./cache`。 |
| `DefaultTopN` / `MaxTopN` | 默认和最大返回条数。 |
| `Embedding.Enabled` | 是否启用 embedding。 |
| `Embedding.Endpoint` / `Model` / `ApiKey` | embedding 服务配置。 |
| `MaxRecordsForEmbedding` | 参与 embedding 的最大记录数。 |

不要在共享环境中保留真实 embedding API Key。

## 接入 McpHost

Streamable HTTP 示例：

```json
{
  "ID": "dataquery_mcp_streamablehttp",
  "Name": "DataQuery MCP StreamableHttp Server",
  "Enabled": true,
  "Description": "Recall semantic matches from local CSV data files.",
  "StreamableHttpConfig": {
    "Endpoint": "http://127.0.0.1:5008/dataquery",
    "ConnectionTimeout": 100
  }
}
```

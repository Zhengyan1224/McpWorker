# Zhengyan.McpServer.WebSearcher

`Zhengyan.McpServer.WebSearcher` 是搜索和网页抓取 MCP Server。它封装搜索引擎查询、网页内容抓取和当前时间工具，适合给 Agent 提供联网信息获取能力。

## 启动模式

默认是 `stdio`：

```powershell
dotnet run --project Zhengyan.McpServer.WebSearcher\Zhengyan.McpServer.WebSearcher.csproj
```

Streamable HTTP：

```powershell
dotnet run --project Zhengyan.McpServer.WebSearcher\Zhengyan.McpServer.WebSearcher.csproj -- --mode streamablehttp --urls http://0.0.0.0:5001
```

SSE：

```powershell
dotnet run --project Zhengyan.McpServer.WebSearcher\Zhengyan.McpServer.WebSearcher.csproj -- --mode sse --urls http://0.0.0.0:5001
```

HTTP MCP 路径：

```text
http://127.0.0.1:5001/websearcher
```

## 工具

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `SearchAsync` | `keywords`, `page` | 调用搜索引擎获取搜索结果。 |
| `CrawlUrlsAsync` | `urls` | 并发抓取多个 URL 的正文内容，并返回 JSON。 |
| `GetCurrentTime` | 无 | 返回当前时间。 |

## 配置

配置文件：

```text
Zhengyan.McpServer.WebSearcher/profiles/mcp_websearcher.json
```

主要字段：

| 字段 | 可选值 | 说明 |
| --- | --- | --- |
| `SearchEngine` | `360`, `bing`, `baidu` | 搜索引擎适配器。 |
| `Crawler` | `web`, `document`, `markdown` | 网页抓取和正文转换方式。 |

也可以通过命令行覆盖：

```powershell
dotnet run --project Zhengyan.McpServer.WebSearcher\Zhengyan.McpServer.WebSearcher.csproj -- --mode stdio --SearchEngine=bing --Crawler=markdown
```

## 接入 McpHost

```json
{
  "ID": "websearcher_mcp_streamablehttp",
  "Name": "Web Searcher MCP StreamableHttp Server",
  "Enabled": true,
  "StreamableHttpConfig": {
    "Endpoint": "http://127.0.0.1:5001/websearcher",
    "ConnectionTimeout": 100
  }
}
```

## 注意事项

- 搜索和网页抓取受网络环境、搜索引擎反爬策略和目标网站响应速度影响。
- 若抓取正文过慢，可尝试更换 `Crawler`，或只抓取搜索结果中必要的 URL。

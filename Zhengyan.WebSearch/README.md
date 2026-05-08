# Zhengyan.WebSearch

`Zhengyan.WebSearch` 是搜索和网页抓取基础库，主要被 `Zhengyan.McpServer.WebSearcher` 使用。

## 目标框架

```text
net8.0
net9.0
```

## 主要能力

搜索引擎适配：

| 类型 | 说明 |
| --- | --- |
| `BingSearchEngine` | Bing 搜索适配。 |
| `BaiduSearchEngine` | 百度搜索适配。 |
| `SoSearchEngine` | 360 搜索适配。 |
| `SearchEngineFactory` | 根据配置创建搜索引擎。 |

网页抓取：

| 类型 | 说明 |
| --- | --- |
| `WebCrawler` | 基础网页抓取。 |
| `DocumentCrawler` | 文档/正文抽取。 |
| `MarkdownCrawler` | 将网页正文转换成 Markdown 风格文本。 |
| `CrawlerFactory` | 根据配置创建抓取器。 |

依赖：

```text
HtmlAgilityPack
ReverseMarkdown
SmartReader
```

这个项目不单独运行，由 WebSearcher MCP Server 引用。

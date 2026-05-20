# Web Search Skill (Curl First, Linux Ready)

用于联网检索与网页抓取。  
本技能默认不依赖 `websearcher_mcp_streamablehttp`，可仅通过 `ExecuteCommand + curl` 完成。

## Prerequisites

- 必需工具：`ExecuteCommand`
- 推荐工具：`WriteFile`、`ReadFile`（保存与复核抓取结果）
- 可选：若存在 `Search/CrawlUrls` MCP 工具，可作为补充

## Strategy

1. 先检索新闻源列表（RSS/搜索页）
2. 抽取 3~6 个候选 URL
3. 抓取正文（复杂页面可走 `r.jina.ai`）
4. 汇总结论 + 证据 URL + 关键日期

## Command Templates

### A) Bing News RSS 检索（Windows PowerShell）

```powershell
$q=[uri]::EscapeDataString("OpenAI GPT-5.4 latest news");
$rss=curl.exe -L "https://www.bing.com/news/search?q=$q&format=rss";
[xml]$x=$rss;
$x.rss.channel.item | Select-Object -First 5 | ForEach-Object { "$($_.pubDate)`t$($_.title)`t$($_.link)" }
```

### B) Bing News RSS 检索（Linux Bash）

```bash
q="OpenAI GPT-5.4 latest news"
url="https://www.bing.com/news/search?q=$(python3 - <<'PY'
import urllib.parse
print(urllib.parse.quote("OpenAI GPT-5.4 latest news"))
PY
)&format=rss"
curl -L "$url"
```

> 如果服务器没有 `python3`，可直接先用英文关键词，不做 URL 编码测试连通性。

### C) 抓取单页正文（通用）

```bash
curl -L "https://example.com/news-page"
```

### D) 抓取可读文本版本（通用，页面复杂时优先）

```bash
url="https://example.com/news-page"
plain="${url#http://}"
plain="${plain#https://}"
curl -L "https://r.jina.ai/http://$plain"
```

### E) 批量抓取 URL（Linux Bash）

```bash
for u in \
  "https://example.com/a" \
  "https://example.com/b" \
  "https://example.com/c"
do
  echo "===== URL: $u ====="
  curl -L "$u"
done
```

## ExecuteCommand Usage Example (Linux)

```json
{
  "command": "curl -L \"https://www.bing.com/news/search?q=OpenAI%20GPT-5.4%20latest%20news&format=rss\"",
  "workingDirectory": ".",
  "timeoutSeconds": 60
}
```

## Output Rules

- 输出必须包含：
  - 结论（2~5 句）
  - 证据 URL（至少 2 条）
  - 关键日期（优先绝对日期，如 `2026-04-09`）
- 明确区分：
  - 事件发生日期（event date）
  - 文章发布日期（publish date）
- 若来源冲突，写明冲突点和各自来源。

## Fallback

- RSS 结果不足：更换关键词（中英文/同义词/站点限定）
- 页面抓取失败：先试 `r.jina.ai`，再换来源 URL
- 若 `curl` 被限制：退回 `Search/CrawlUrls`（若已注册）

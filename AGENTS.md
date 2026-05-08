# AGENTS.md

## Quick Commands

```powershell
# Build entire solution
dotnet build McpWorker.sln

# Run McpHost (core service)
dotnet run --project Zhengyan.McpHost/Zhengyan.McpHost.csproj

# Run a single MCP Server (e.g., WebSearcher)
dotnet run --project Zhengyan.McpServer.WebSearcher/Zhengyan.McpServer.WebSearcher.csproj -- --mode streamablehttp --urls http://0.0.0.0:5001

# Run a ChatUI
dotnet run --project Zhengyan.ChatUI.Desktop/Zhengyan.ChatUI.Desktop.csproj
```

## Architecture

- **Core**: `Zhengyan.McpHost` — OpenAI-compatible gateway for MCP. Manages ChatClients, MCP Clients, and Agents.
- **Entry point**: `Zhengyan.McpHost/profiles/mcphost.json`
- **Default port**: `9083`

### Key URLs (after launching McpHost)

| Path | Purpose |
| --- | --- |
| `http://localhost:9083/mcphost/html` | Static management page |
| `http://localhost:9083/mcphost/swagger` | API documentation |
| `http://localhost:9083/mcphost/api/v1/chat/completions` | OpenAI-compatible chat endpoint |
| `http://localhost:9083/mcphost/api/v1/responses` | OpenAI-compatible responses endpoint |
| `http://localhost:9083/mcphost/api/v1/models/config` | List configured agents/models |

## MCP Server Modes

All MCP Servers support these modes:
```
--mode stdio | sse | streamablehttp | http
--stateless true|false
--urls http://0.0.0.0:<port>
```

## Project Structure

| Category | Projects |
| --- | --- |
| Core Host | `Zhengyan.McpHost` |
| Chat UIs | `Zhengyan.ChatUI.{CLI,TUI,Desktop,Web}` |
| MCP Servers | `Zhengyan.McpServer.{Agent,DataQuery,Divine,EightChar,Memory,Skills,Ssh,WebSearcher}` |
| Services | `Zhengyan.KBServer`, `Zhengyan.FSServer` |
| Libraries | `Zhengyan.Commons`, `Zhengyan.Commons.Web`, `Zhengyan.OpenAIModels`, `Zhengyan.WebSearch`, `Zhengyan.KnowledgeBase`, `Zhengyan.VectorDB`, `Zhengyan.HNSW`, `Zhengyan.Lunar` |

## Tech Stack

- **.NET 9.0** (some projects target net10.0)
- **C# 12** with nullable and implicit usings enabled
- **Serilog** for logging
- **Avalonia** for Desktop UI
- **Terminal.Gui** for TUI
- **Gradio.Net** for Web UI

## Important Notes

- MCP Server connection URL format (for McpHost config): `http://127.0.0.1:5001/websearcher`
- Profile configs stored in `Zhengyan.McpHost/profiles/`
- Each ChatUI defaults to connecting `http://localhost:9083/mcphost/api/v1`
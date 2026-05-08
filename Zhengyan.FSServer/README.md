# Zhengyan.FSServer

`Zhengyan.FSServer` 是文件存储 HTTP + MCP 服务。它提供文件上传、静态文件访问，并通过 MCP 工具读取存储中的文本文件内容。

## 启动

```powershell
dotnet run --project Zhengyan.FSServer\Zhengyan.FSServer.csproj
```

默认配置文件：

```text
Zhengyan.FSServer/profiles/fsserver.json
```

默认监听：

```text
http://localhost:9085
```

## 常用地址

| 地址 | 用途 |
| --- | --- |
| `http://localhost:9085/fsserver/api/fs/upload` | 上传文件。 |
| `http://localhost:9085/fsserver/files` | 静态文件访问路径。 |
| `http://localhost:9085/fsserver/mcp` | MCP 路径。 |
| `http://localhost:9085/fsserver/swagger` | Swagger。 |

## MCP 工具

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `ReadFile` | `relativePath`, `length` | 从文件存储服务读取文本内容。 |

## 配置

主要配置项：

| 字段 | 说明 |
| --- | --- |
| `WebApi:CentralRoutePrefix` | HTTP API 前缀，默认 `fsserver/api`。 |
| `McpServer:RoutePrefix` | MCP 路径，默认 `fsserver/mcp`。 |
| `StaticFiles:StaticFilesRoot` | 静态文件根目录，默认 `./datas/files`。 |
| `StaticFiles:RequestPath` | 静态文件访问路径，默认 `/fsserver/files`。 |
| `Storage:StorageBaseDir` | 上传文件存储目录。 |

## 接入 McpHost

```json
{
  "ID": "fsserver_mcp_streamablehttp",
  "Name": "File Storage MCP StreamableHttp Server",
  "Enabled": true,
  "StreamableHttpConfig": {
    "Endpoint": "http://127.0.0.1:9085/fsserver/mcp",
    "ConnectionTimeout": 100
  }
}
```

# Zhengyan.McpServer.Ssh

`Zhengyan.McpServer.Ssh` 是 SSH MCP Server。它连接一台远程主机，对外提供命令执行、文件上传和文件下载工具。

## 启动模式

默认是 `stdio`：

```powershell
dotnet run --project Zhengyan.McpServer.Ssh\Zhengyan.McpServer.Ssh.csproj
```

Streamable HTTP：

```powershell
dotnet run --project Zhengyan.McpServer.Ssh\Zhengyan.McpServer.Ssh.csproj -- --mode streamablehttp --urls http://0.0.0.0:5003
```

SSE：

```powershell
dotnet run --project Zhengyan.McpServer.Ssh\Zhengyan.McpServer.Ssh.csproj -- --mode sse --urls http://0.0.0.0:5003
```

HTTP MCP 路径：

```text
http://127.0.0.1:5003/ssh
```

## 工具

| 工具 | 参数 | 说明 |
| --- | --- | --- |
| `execute-command` | `cmdString` | 在远程主机执行命令并返回输出。 |
| `upload` | `localPath`, `remotePath` | 上传本地文件到远程路径。 |
| `download` | `remotePath`, `localPath` | 从远程路径下载文件。 |

## 配置

配置文件：

```text
Zhengyan.McpServer.Ssh/profiles/mcp_ssh.json
```

主要字段：

| 字段 | 说明 |
| --- | --- |
| `Host` | SSH 主机。 |
| `Port` | SSH 端口。 |
| `UserName` | 用户名。 |
| `Password` | 密码。 |
| `PrivateKey` | 私钥路径，可替代密码。 |
| `Passphrase` | 私钥口令。 |
| `WhiteList` | 可选命令白名单正则。 |
| `BlackList` | 可选命令黑名单正则。 |

请把示例凭据替换成自己的环境值，不要提交真实 SSH 凭据。

## 接入 McpHost

```json
{
  "ID": "ssh_mcp_streamablehttp",
  "Name": "SSH MCP StreamableHttp Server",
  "Enabled": true,
  "StreamableHttpConfig": {
    "Endpoint": "http://127.0.0.1:5003/ssh",
    "ConnectionTimeout": 100
  }
}
```

## 安全注意

- 建议配置命令白名单/黑名单。
- 不要让不可信 Agent 直接访问 SSH 工具。
- 生产环境优先使用最小权限账号和密钥认证。

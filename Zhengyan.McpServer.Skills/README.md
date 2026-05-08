# Zhengyan.McpServer.Skills

`Zhengyan.McpServer.Skills` 是技能和工作区工具 MCP Server。它可以列出、检索、读取本地技能，也提供文件系统、文本搜索、目录操作和命令执行等工具。

## 启动模式

默认是 `stdio`：

```powershell
dotnet run --project Zhengyan.McpServer.Skills\Zhengyan.McpServer.Skills.csproj
```

Streamable HTTP：

```powershell
dotnet run --project Zhengyan.McpServer.Skills\Zhengyan.McpServer.Skills.csproj -- --mode streamablehttp --urls http://0.0.0.0:5006
```

SSE：

```powershell
dotnet run --project Zhengyan.McpServer.Skills\Zhengyan.McpServer.Skills.csproj -- --mode sse --urls http://0.0.0.0:5006
```

HTTP MCP 路径：

```text
http://127.0.0.1:5006/skills
```

## 工具

技能工具：

| 工具 | 说明 |
| --- | --- |
| `ListSkills` | 列出所有可用技能，可按关键词过滤。 |
| `SearchSkills` | 按关键词检索技能。 |
| `ReadSkill` | 读取指定技能的完整 `SKILL.md`。 |
| `ReadSkillFile` | 读取技能目录下的附属文件，例如 `references`、`assets`、`scripts`。 |

工作区工具：

| 工具 | 说明 |
| --- | --- |
| `GetPathInfo` | 获取路径信息。 |
| `ListFiles` | 列出文件或目录。 |
| `FindFiles` | 按通配符查找文件。 |
| `ReadFile` | 读取文本文件。 |
| `ReadFileLines` | 按行读取文件。 |
| `WriteFile` | 写入或追加文本。 |
| `ReplaceInFile` | 搜索并替换文件文本。 |
| `SearchText` | 搜索目录或文件中的文本。 |
| `CreateDirectory` | 创建目录。 |
| `DeletePath` | 删除文件或目录，必须显式传 `force=true`。 |
| `CopyPath` | 复制文件或目录。 |
| `MovePath` | 移动文件或目录。 |
| `ExecuteCommand` | 在指定工作目录执行系统命令。 |

## 配置

配置文件：

```text
Zhengyan.McpServer.Skills/profiles/mcp_skills.json
```

主要字段：

| 字段 | 说明 |
| --- | --- |
| `SkillsRootPath` | 技能根目录。 |
| `WorkspaceRootPath` | 工作区根目录，文件工具只能围绕这个目录工作。 |
| `EntryFileName` | 技能入口文件名，默认 `SKILL.md`。 |
| `MaxContentLength` | 读取技能内容的最大长度。 |
| `MaxFileReadLength` | 文件读取最大长度。 |
| `MaxCommandTimeoutSeconds` | 命令执行最大超时。 |
| `MaxCommandOutputLength` | 命令输出最大长度。 |

默认内置技能目录：

```text
Zhengyan.McpServer.Skills/resources/skills
```

## 接入 McpHost

```json
{
  "ID": "skills_mcp_streamablehttp",
  "Name": "Skills MCP StreamableHttp Server",
  "Enabled": true,
  "Description": "Provide skills discovery and workspace tools.",
  "StreamableHttpConfig": {
    "Endpoint": "http://127.0.0.1:5006/skills",
    "ConnectionTimeout": 100
  }
}
```

## 安全注意

`Skills` 具备文件写入、删除、移动和命令执行能力。只应在可信工作区启用，并谨慎设置 `WorkspaceRootPath`。

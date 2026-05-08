# Zhengyan.ChatUI.CLI

`Zhengyan.ChatUI.CLI` 是面向 `Zhengyan.McpHost` 的命令行对话客户端。它支持交互式聊天，也支持单次请求后退出，适合脚本、管道、终端调试和自动化验证。

## 启动

```powershell
dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj
```

查看内置帮助：

```powershell
dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- --help
```

打印配置文件路径：

```powershell
dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- --config-path
```

默认连接：

```text
http://localhost:9083/mcphost/api/v1
```

## 主要功能

- 连接 `McpHost`，调用 `/chat/completions` 或 `/responses`。
- 加载 `McpHost` 的模型列表并切换当前模型。
- 流式显示 `Thinking`、`Assistant`、`Additional`。
- 支持上游 reasoning/thinking 增量输出。
- 支持图片 URL 和本地图片文件。
- 支持复制最近一次回答、推理过程或完整渲染结果。
- 支持单次请求、stdin 管道输入、输出到文件。

## 交互式命令

常用命令：

```text
/help
/settings
/save
/models
/use <index|name>
/set <server|token|model|max_tokens|temperature|top_p|api> <value>
/image ...
/copy [assistant|thinking|additional|all]
/multiline
/retry
/clear
/history
/exit
```

配置示例：

```text
/set server http://localhost:9083/mcphost/api/v1
/set token dev
/models
/use 0
/set api responses
/save
```

如果普通消息必须以 `/` 开头，用 `//` 作为开头。

## 图片输入

```text
/image add-url <url>
/image add-file <path>
/image list
/image remove <index>
/image clear
```

支持 `png`、`jpg`、`jpeg`、`gif`、`bmp`、`webp`，也支持 `http`、`https` 和 `data:image/...` URL。

## 单次请求

直接传入一条消息：

```powershell
dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- --message "解释一下 MCP 的作用"
```

使用 Responses API：

```powershell
dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- -m "总结这段话" --api responses
```

从 stdin 读取：

```powershell
Get-Content prompt.txt | dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- --stdin -m "请总结："
```

保存结果：

```powershell
Get-Content notes.md | dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- --stdin --output result.md
```

携带图片：

```powershell
dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- -m "描述这张图" --file D:\demo\chart.png --api responses
```

## 配置文件

配置保存在：

```text
%LocalAppData%\Zhengyan.ChatUI.CLI\settings.json
```

保存字段：

```text
ServerEndpoint
ApiKey
Model
MaxTokens
Temperature
TopP
UseResponsesApi
```

完整命令参考见 [HELP.md](./HELP.md)。

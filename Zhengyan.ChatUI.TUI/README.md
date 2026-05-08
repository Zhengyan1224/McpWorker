# Zhengyan.ChatUI.TUI

`Zhengyan.ChatUI.TUI` 是基于 Terminal.Gui 的全屏终端对话界面。它适合在不打开浏览器或桌面窗口的环境中使用 `Zhengyan.McpHost`，同时保留模型切换、图片输入、推理过程和附加 JSON 查看能力。

## 启动

```powershell
dotnet run --project Zhengyan.ChatUI.TUI\Zhengyan.ChatUI.TUI.csproj
```

默认连接：

```text
http://localhost:9083/mcphost/api/v1
```

## 主要功能

- Settings 面板配置 Host 地址、API Key、模型、max tokens、temperature、top_p。
- 加载 `McpHost` 的 `/models/config` 并切换模型。
- 支持 `/v1/chat/completions` 和 `/v1/responses` 两种请求模式。
- Chat 面板进行流式对话。
- Thinking 面板单独显示推理过程。
- Assistant 面板显示最终回答。
- Additional 面板显示工具调用、附加属性等 JSON。
- 支持图片 URL 和本地图片附件。
- 支持发送、重试、清空、保存设置和复制输出。

## 使用流程

1. 先启动 `Zhengyan.McpHost`。
2. 启动 TUI。
3. 在 Settings 中确认 `Server Endpoint`。
4. 输入 API Key，如果 Agent 配置了 `ApiKeyExpirations`，这里填对应 key。
5. 点击或触发加载模型。
6. 选择模型并切换。
7. 在 Chat 中输入消息并发送。
8. 在 Thinking / Assistant / Additional 面板检查返回内容。

## 快捷键

TUI 内置 Shortcuts 面板，常用操作包括：

```text
F11        切换 Chat / Settings / Shortcuts 顶层页签
Ctrl+PgDn  切到 Settings
Ctrl+2     显示 Thinking 面板
Ctrl+4     显示 Additional 面板
Ctrl+W     保存设置
Ctrl+B     选择本地图片
```

不同终端对组合键支持不同；如果快捷键不可用，可以用菜单或按钮完成同样操作。

## 配置文件

配置保存在：

```text
%LocalAppData%\Zhengyan.ChatUI.TUI\settings.json
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

## 注意事项

- Windows Terminal、PowerShell、较新的终端模拟器体验更好。
- 如果模型列表为空，先检查 `McpHost` 是否启动，以及 `/mcphost/api/v1/models/config` 是否可访问。
- 如果 Thinking 为空，说明上游模型或当前响应没有返回 reasoning/thinking 内容。

# Zhengyan.ChatUI.Desktop

`Zhengyan.ChatUI.Desktop` 是基于 Avalonia 的桌面对话客户端。它面向本机长期调试场景，适合用图形界面测试 `Zhengyan.McpHost` 的模型切换、流式输出、多模态输入、推理过程和附加 JSON。

## 启动

```powershell
dotnet run --project Zhengyan.ChatUI.Desktop\Zhengyan.ChatUI.Desktop.csproj
```

默认连接：

```text
http://localhost:9083/mcphost/api/v1
```

## 主要功能

- 配置 Host 地址、API Key、模型、max tokens、temperature、top_p。
- 调用 `/models/config` 加载 Agent/模型列表。
- 调用 `/models/switch?id=<index>` 切换 `McpHost` 当前模型。
- 在 Chat Completions 和 Responses 两种 API 模式之间切换。
- 流式显示助手输出。
- 单独展示 Thinking/Reasoning。
- 展示 Additional Properties，例如工具调用结果、附加 JSON 等。
- 支持图片 URL 和本地图片文件。
- 保存本地设置。

## 使用流程

1. 启动 `Zhengyan.McpHost`。
2. 启动 Desktop UI。
3. 确认 Server Endpoint 为 `http://localhost:9083/mcphost/api/v1`。
4. 设置 API Key。
5. 点击加载模型，选择需要的 Agent。
6. 按需要切换 `Use Responses API`。
7. 输入消息，附加图片后发送。

## 配置文件

配置保存在：

```text
%LocalAppData%\Zhengyan.ChatUI.Desktop\settings.json
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

## 适用场景

- 调试 `McpHost` 的 Agent 配置。
- 验证非流式/流式 reasoning 是否显示正确。
- 验证多模态请求在 Chat Completions 和 Responses 两种格式下是否正常。
- 观察工具调用后的 Additional Properties。

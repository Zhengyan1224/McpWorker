# Zhengyan.ChatUI.Web

`Zhengyan.ChatUI.Web` 是基于 Gradio.Net 的浏览器对话界面。它适合快速本机测试，也适合把对话调试界面暴露到局域网给其他设备访问。

## 启动

```powershell
dotnet run --project Zhengyan.ChatUI.Web\Zhengyan.ChatUI.Web.csproj
```

实际访问端口由 ASP.NET Core 启动参数或环境变量决定。未指定时使用 `dotnet run` 的默认监听地址；如需固定端口，可显式传入：

```powershell
dotnet run --project Zhengyan.ChatUI.Web\Zhengyan.ChatUI.Web.csproj -- --urls http://0.0.0.0:7860
```

默认连接：

```text
http://localhost:9083/mcphost/api/v1
```

## 主要功能

- Settings 页配置 Host 地址、API Key、模型、max tokens、temperature、top_p。
- 配置会保存到浏览器 `localStorage`。
- 加载 `McpHost` 模型列表并切换模型。
- Chat 页进行流式对话。
- 支持 `/v1/chat/completions` 与 `/v1/responses`。
- 支持图片 URL 和本地上传图片。
- 在消息中显示 Additional Properties 折叠区。
- 能从 Responses 输出中识别 reasoning、message、output_text 等结构。

## 使用流程

1. 启动 `Zhengyan.McpHost`。
2. 启动 Web UI。
3. 打开浏览器访问 Web UI 地址。
4. 在 Settings 页确认 `Server Endpoint`。
5. 填写 API Key，加载模型并选择需要的 Agent。
6. 在 Chat 页输入消息并发送。

## 浏览器保存项

Web UI 会在当前浏览器中保存：

```text
ServerEndpoint
ApiKey
SelectedModel
MaxCompletionTokens
Temperature
TopP
UseResponsesApi
```

如果要重置配置，可以清除站点的 localStorage，或直接覆盖 Settings 页中的值。

## 适用场景

- 不安装桌面 UI 时快速验证 Host。
- 在局域网内给其他设备测试模型和 Agent。
- 查看 Responses API 输出和附加 JSON。

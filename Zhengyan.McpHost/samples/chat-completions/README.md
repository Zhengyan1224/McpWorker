# Zhengyan.McpHost Chat Completions Samples

## Windows BAT Quick Start

For teammates who prefer double-click or `cmd.exe`, use these wrappers:

- `text_request.bat`
- `multimodal_url_request.bat`
- `multimodal_local_image_request.bat`
- `multimodal_local_image_stream.bat`

Command line examples:

```bat
cd Zhengyan.McpHost\samples\chat-completions
text_request.bat
multimodal_url_request.bat
multimodal_local_image_request.bat -ImagePath .\demo.jpg
multimodal_local_image_stream.bat -ImagePath .\demo.jpg
```

这个目录提供 `Zhengyan.McpHost` 的 `/v1/chat/completions` 调用示例，覆盖：

- 纯文本输入（兼容旧协议）
- 多模态输入（文本 + 图片 URL）
- 多模态输入（本地图片转 `data:` URI）
- SSE 流式输出

## Windows PowerShell 用法

目录中也提供了 `.ps1` 脚本（适用于 Windows）：

- `text_request.ps1`
- `multimodal_url_request.ps1`
- `multimodal_local_image_request.ps1`
- `multimodal_local_image_stream.ps1`

示例命令：

```powershell
cd Zhengyan.McpHost\samples\chat-completions
powershell -ExecutionPolicy Bypass -File .\text_request.ps1
powershell -ExecutionPolicy Bypass -File .\multimodal_url_request.ps1
powershell -ExecutionPolicy Bypass -File .\multimodal_local_image_request.ps1 -ImagePath .\demo.jpg
powershell -ExecutionPolicy Bypass -File .\multimodal_local_image_stream.ps1 -ImagePath .\demo.jpg
```

## 1. 前置要求

- Linux 环境安装 `curl`
- 本地图片场景需要 `file` 与 `base64`
- `McpHost` 已启动，默认示例地址为 `http://127.0.0.1:5000/v1/chat/completions`

## 2. 快速开始

```bash
cd Zhengyan.McpHost/samples/chat-completions
chmod +x *.sh
```

可选环境变量（不设置会使用脚本默认值）：

- `API_URL`：接口地址，默认 `http://127.0.0.1:5000/v1/chat/completions`
- `API_KEY`：鉴权 key，默认 `your_api_key`
- `MODEL`：文本模型/agent id，默认 `No models available`
- `VISION_MODEL`：视觉模型/agent id，默认 `gpt-4-turbo`

## 3. 运行示例

- 纯文本请求（旧协议）
  - `./text_request.sh`
- 多模态 URL 图片请求
  - `./multimodal_url_request.sh`
- 本地图片（data URI）请求
  - `./multimodal_local_image_request.sh ./demo.jpg`
- 本地图片（data URI）流式请求
  - `./multimodal_local_image_stream.sh ./demo.jpg`

> 也可以直接用 `.http` 文件测试：`Zhengyan.McpHost/Zhengyan.McpHost.http`

## 4. 常见问题排查

### 4.1 返回 401 / 未授权

- 检查请求头是否携带 `Authorization: Bearer <API_KEY>`
- 检查 `McpHost` 中该 key 是否配置且未过期（`ApiKeyExpirations`）

### 4.2 返回空结果或模型不存在

- 检查 `model` 是否是已配置的 agent id
- 服务日志如果出现 `AgentConfigs not found for ID`，说明模型标识不正确

### 4.3 图片请求报错

- `image_url.url` 必须是有效 URL 或 `data:` URI
- 本地图片转码后请求体会变大，过大时可能触发 413（需提升网关/服务 body 限制）
- 确认 MIME 类型正确（脚本默认从 `file --mime-type` 获取）

### 4.4 流式输出没有实时返回

- 使用 `curl -N`（脚本已包含）
- 如果前面有 Nginx/网关，需关闭或调整响应缓冲（例如 `proxy_buffering off;`）

### 4.5 使用 `max_tokens` 无效

- `McpHost` 已兼容 `max_tokens`，中间件会转换为 `max_completion_tokens`

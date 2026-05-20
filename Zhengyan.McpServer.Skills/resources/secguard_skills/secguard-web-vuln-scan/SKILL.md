# SecGuard Web 漏洞扫描（Vuln Scan）

**阶段**：Phase 2  
**目标**：系统化发现 Web 应用的安全缺陷，识别潜在漏洞入口。  
**前置依赖**：Phase 1 已完成，结果在 `secguard-output/recon-*.md` 中。

---

## 可用脚本

| 脚本 | 平台 | 执行方式 |
|------|------|----------|
| `scripts/vuln_scan.py` | Windows / Linux / macOS | `python3 scripts/vuln_scan.py --target <url> --phase all --output <path>` |
| `scripts/vuln_scan.sh` | Linux / macOS | `bash scripts/vuln_scan.sh -t <url> -o <path>` |

---

## 执行流程

### 第一步：读取 Phase 1 结果

使用 `ReadFile` 读取 `secguard-output/` 下最新的 `recon-*.md` 文件，获取目标信息（敏感路径、子域名等）。

### 第二步：执行漏洞扫描

**推荐方式（Python 脚本）**：

```json
{
  "command": "python3 secguard-web-vuln-scan/scripts/vuln_scan.py --target https://example.com --phase all --output secguard-output/vuln-scan-example-com-20260520-143052.md",
  "workingDirectory": ".",
  "timeoutSeconds": 180
}
```

> `--phase all` 包含 6 个阶段：dirs, forms, methods, dirlist, tech, api。

**分批扫描**（路径太多时分多次执行）：

```json
// 第一批：严重级别泄露路径
{"command": "python3 secguard-web-vuln-scan/scripts/vuln_scan.py --target https://example.com --phase dirs --output secguard-output/vuln-scan-example-com-phase1.md", "workingDirectory": ".", "timeoutSeconds": 90}

// 第二批：API 与文档
{"command": "python3 secguard-web-vuln-scan/scripts/vuln_scan.py --target https://example.com --phase forms --output secguard-output/vuln-scan-example-com-phase2.md", "workingDirectory": ".", "timeoutSeconds": 30}

// 第三批：技术识别 + 方法检测
{"command": "python3 secguard-web-vuln-scan/scripts/vuln_scan.py --target https://example.com --phase tech --output secguard-output/vuln-scan-example-com-tech.md", "workingDirectory": ".", "timeoutSeconds": 30}

{"command": "python3 secguard-web-vuln-scan/scripts/vuln_scan.py --target https://example.com --phase methods --output secguard-output/vuln-scan-example-com-methods.md", "workingDirectory": ".", "timeoutSeconds": 30}
```

**Linux/macOS 备选**：

```json
{"command": "bash secguard-web-vuln-scan/scripts/vuln_scan.sh -t https://example.com -o secguard-output/vuln-scan-example-com-20260520-143052.md", "workingDirectory": ".", "timeoutSeconds": 120}
```

### 第三步：检查结果

确认 `secguard-output/vuln-scan-*.md` 已生成，内容不为空。

---

## 扫描内容清单

Python 脚本和 Shell 脚本会自动覆盖以下检查项：

## API 端点 Fuzzing（`--phase api`）

对已发现的 API 端点进行自动化安全检测。支持两种方式提供端点：

- `--api-endpoints`：逗号分隔的路径列表，如 `/api/users,/api/auth`
- `--api-file`：JSON 文件（由 recon `js` 阶段生成的 `*-apis.json`）

```json
{"command": "python3 secguard-web-vuln-scan/scripts/vuln_scan.py --target https://example.com --phase api --api-file secguard-output/recon-example-com-apis.json --output secguard-output/vuln-scan-example-com-api.md", "workingDirectory": ".", "timeoutSeconds": 120}
```

检测内容：

| 检查项 | 说明 |
|--------|------|
| GET/POST 状态码 | 判断端点是否存在、是否需要认证 |
| 敏感信息泄露 | 响应中是否包含 password/secret/token 等关键词 |
| 详细错误信息 | 是否返回 stack trace / exception 等调试信息 |
| CORS 配置 | `Access-Control-Allow-Origin` 是否过于宽松 |
| IDOR 风险 | 返回 JSON 数组的端点可能存在越权访问 |

---

## 扫描内容清单

| 检查项 | 严重性 | 说明 |
|--------|--------|------|
| 目录与文件暴力枚举 | 🔴 | 检测 `.git/config`、`.env`、`phpinfo.php` 等敏感文件 |
| 敏感文件内容分析 | 🔴 | 对 200 响应的文件检查内容是否泄露凭据 |
| API 端点 Fuzzing | 🔴 | 扫描发现的 API 端点是否存在信息泄露 |
| 表单分析 | 🟡 | 发现登录、上传、搜索等输入点 |
| HTTP 方法检测 | 🟡 | PUT/DELETE/TRACE 等危险方法 |
| 目录列表检测 | 🟡 | 检查目录是否开启列表 |
| 技术栈识别 | 🔵 | 识别 Web 服务器、后端技术、前端框架 |

---

## 手动操作指引（脚本不可用）

使用 `webfetch` 逐条探测每个路径，记录响应状态码和内容。

---

## 临时脚本

生成的临时测试脚本**必须**放在 `temp/` 目录下。

---

## 输出示例

```markdown
# Web Vulnerability Scan Report

## Directory & File Bruteforce
| Path | Status | Size | Notes |
|------|--------|------|-------|
| /.env | **200** | 245b | ⚠️ 信息泄露 |
| /admin | **403** | - | 需要认证 |
| /api | **200** | 1.2kb | API 接口 |

## HTTP Methods
- ✅ PUT → 405 (不允许)
- ✅ DELETE → 405 (不允许)

## Technology Detection
- **Server**: nginx/1.24.0
- **Backend**: PHP (基于 Cookie)
```

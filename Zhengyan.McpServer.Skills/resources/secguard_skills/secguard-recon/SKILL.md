# SecGuard 信息收集（Reconnaissance）

**阶段**：Phase 1  
**目标**：收集目标系统的公开信息，绘制攻击面。  
**适用场景**：用户提供了 URL 或域名，需要了解目标的技术架构、开放端口、子域名等信息。

---

## 可用脚本

本 Skill 预置了以下脚本，优先使用它们执行收集：

| 脚本 | 平台 | 执行方式 |
|------|------|----------|
| `scripts/recon.py` | Windows / Linux / macOS | `python3 scripts/recon.py --target <url> --phase all --output <path>` |
| `scripts/recon.sh` | Linux / macOS | `bash scripts/recon.sh -t <url> -o <path>` |

脚本会自动输出 Markdown 格式报告。

---

## 执行流程

### 第一步：创建输出目录

```json
{
  "command": "mkdir -p secguard-output",
  "description": "创建统一输出目录",
  "workingDirectory": ".",
  "timeoutSeconds": 5
}
```

> Windows 上用 `mkdir secguard-output`，Linux/macOS 上用 `mkdir -p secguard-output`。或用智能体的文件工具创建。

### 第二步：执行信息收集

**推荐方式（Python 脚本，跨平台）**：

```json
{
  "command": "python3 secguard-recon/scripts/recon.py --target https://example.com --phase all --output secguard-output/recon-example-com-20260520-143052.md",
  "workingDirectory": ".",
  "timeoutSeconds": 120
}
```

各参数：

| 参数 | 说明 |
|------|------|
| `--target` / `-t` | 目标域名或 URL（必填） |
| `--phase` / `-p` | 阶段：`all`（默认）, `dns`, `ports`, `headers`, `subdomains`, `paths`, `robots`, `js` |
| `--output` / `-o` | **必须设为** `secguard-output/recon-{domain}-{YYYYMMDD-HHmmss}.md` |

**新增 `js` 阶段**：自动下载目标页面的 JS 文件，从中提取 API 端点路径，并保存为 JSON 供后续阶段使用。

**文件名示例**：`secguard-output/recon-example-com-20260520-143052.md`

**分别执行各子阶段**：

```json
// DNS 解析
{"command": "python3 secguard-recon/scripts/recon.py --target example.com --phase dns --output secguard-output/recon-example-com-dns.md", "workingDirectory": ".", "timeoutSeconds": 30}

// 端口扫描
{"command": "python3 secguard-recon/scripts/recon.py --target example.com --phase ports --ports 80,443,8080,8443 --output secguard-output/recon-example-com-ports.md", "workingDirectory": ".", "timeoutSeconds": 60}

// HTTP 头分析
{"command": "python3 secguard-recon/scripts/recon.py --target https://example.com --phase headers --output secguard-output/recon-example-com-headers.md", "workingDirectory": ".", "timeoutSeconds": 30}

// 子域名枚举
{"command": "python3 secguard-recon/scripts/recon.py --target example.com --phase subdomains --output secguard-output/recon-example-com-subdomains.md", "workingDirectory": ".", "timeoutSeconds": 60}

// 敏感路径探测
{"command": "python3 secguard-recon/scripts/recon.py --target https://example.com --phase paths --output secguard-output/recon-example-com-paths.md", "workingDirectory": ".", "timeoutSeconds": 60}
```

**Linux/macOS 备选（Shell 脚本）**：

```json
{"command": "bash secguard-recon/scripts/recon.sh -t example.com -o secguard-output/recon-example-com-20260520-143052.md", "workingDirectory": ".", "timeoutSeconds": 120}
```

### 第三步：确认输出

使用 `ReadFile` 检查 `secguard-output/` 下是否生成了报告文件，确认内容不为空后进入下一阶段。

---

## 手动操作指引（脚本不可用时的兜底）

如果 Python 和 Shell 脚本都不可用，使用 `webfetch` 和 `ExecuteCommand` 手动执行：

| 子任务 | 工具 | 示例 |
|--------|------|------|
| 验证可达性 | `webfetch` | `webfetch("https://example.com/")` |
| 获取响应头 | `webfetch` | 查看返回中的 Header |
| DNS 查询 | `ExecuteCommand` | `nslookup example.com` |
| 端口检测 | `ExecuteCommand` / `webfetch` | `curl -s --connect-timeout 3 https://example.com:8443/` |
| 敏感路径 | `webfetch` | `webfetch("https://example.com/.git/config")` |
| 搜索引擎 | `websearch` | `websearch("site:example.com")` |

---

## 临时脚本

如需生成临时脚本辅助测试，**必须**放在 `temp/` 目录下。示例：
```json
{"command": "python3 temp/my_recon_script.py", "description": "执行临时生成的脚本", "timeoutSeconds": 60}
```

---

## 输出示例

```markdown
# Reconnaissance Report
**Target**: https://example.com
**Timestamp**: 2026-05-20 14:30:52 UTC

## HTTP Headers
- HSTS: ✅ 存在
- CSP: ❌ 缺失，存在 XSS 风险
- Server: nginx/1.24.0

## Open Ports
| Port | Status |
|------|--------|
| 443 | OPEN |
| 80 | OPEN |

## Sensitive Paths
| Path | Status | Notes |
|------|--------|-------|
| /.git/config | **200** | ⚠️ 信息泄露 |
```

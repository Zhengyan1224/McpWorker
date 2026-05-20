# SecGuard 渗透测试报告（Reporting）

**阶段**：Phase 4  
**目标**：将前三个阶段发现的成果汇总为规范的渗透测试报告。  
**前置依赖**：Phase 1-3 已完成，`secguard-output/` 目录下有各阶段的结果文件。

---

## 可用脚本

| 脚本 | 平台 | 执行方式 |
|------|------|----------|
| `scripts/report_gen.py` | Windows / Linux / macOS | `python3 scripts/report_gen.py --target <domain> --recon <file> --vuln <file> --exploit <file> --output <path>` |

> Shell 版脚本不适用——报告需要读取中间结果文件并组装 Markdown，Python 是唯一可行方式。

---

## 执行流程

### 第一步：读取中间结果

从 `secguard-output/` 目录中找到最新的各阶段结果文件：

```yaml
读取 secguard-output/recon-{domain}-{timestamp}.md      # Phase 1 结果
读取 secguard-output/vuln-scan-{domain}-{timestamp}.md   # Phase 2 结果
读取 secguard-output/exploit-{domain}-{timestamp}.md     # Phase 3 结果
```

如果没有找到某阶段结果，在报告中注明"未执行对应阶段测试"。

### 第二步：生成报告

**推荐方式（Python 脚本）**：

```json
{
  "command": "python3 secguard-report/scripts/report_gen.py --target example.com --recon secguard-output/recon-example-com-20260520-143052.md --vuln secguard-output/vuln-scan-example-com-20260520-143052.md --exploit secguard-output/exploit-example-com-20260520-143052.md --output secguard-output/pentest-report-example-com-20260520-143052.md",
  "workingDirectory": ".",
  "timeoutSeconds": 30
}
```

> **文件名自动生成**：如果不传 `--output`，脚本会自动在 `secguard-output/` 下生成带时分秒的唯一文件名 `pentest-report-{domain}-{YYYYMMDD-HHmmss}.md`，防止多次运行互相覆盖。

### 第三步（备选）：手动编写

如果 Python 脚本不可用，使用 `WriteFile` 按下面的模板手动组装报告。用 `ReadFile` 读取中间结果文件后，逐个章节填入模板。

---

## 报告模板

```markdown
# 渗透测试报告

## 1. 概述

| 项目 | 内容 |
|------|------|
| **报告编号** | SEC-{YYYYMMDD}-{XXX} |
| **目标系统** | {目标 URL / IP / 域名} |
| **测试日期** | {开始日期} - {结束日期} |
| **测试方法** | 黑盒 / 灰盒 渗透测试 |
| **测试引擎** | SecGuard Security Agent |
| **报告日期** | {报告生成日期} |

### 1.1 测试范围
- **目标 URL**：{target_url}
- **目标域名**：{domain}

### 1.2 测试方法
1. 信息收集 — HTTP 请求分析、DNS 查询、子域名枚举、路径探测
2. 漏洞扫描 — 目录枚举、敏感文件检测、表单分析、HTTP 方法检测
3. 漏洞利用验证 — SQL 注入、XSS、SSRF、命令注入、认证绕过测试
4. 综合评估 — 风险评级、影响分析、修复建议

---

## 2. 信息收集结果

### 2.1 目标基本信息
| 信息项 | 值 |
|--------|-----|
| 域名 | {domain} |
| Web 服务器 | {Server 头} |
| 后端技术 | {X-Powered-By / Cookie 推断} |

### 2.2 安全头检查
| 头 | 状态 |
|---|------|
| HSTS | ✅ / ❌ |
| CSP | ✅ / ❌ |
| X-Frame-Options | ✅ / ❌ |

### 2.3 开放端口
| 端口 | 协议 | 状态 |
|------|------|------|

### 2.4 敏感路径
| 路径 | 状态 | 说明 |
|------|------|------|

---

## 3. 漏洞清单

### 3.1 风险统计
| 等级 | 数量 |
|------|:----:|
| 🔴 严重 | {n} |
| 🟠 高危 | {n} |
| 🟡 中危 | {n} |
| **合计** | **{total}** |

### 3.2 漏洞详情

#### 🟠 [高危] {漏洞名称}
- **位置**：{URL}
- **检测方式**：{使用的 Payload 或请求}

**漏洞描述**：{详细说明}
**危害影响**：{实际危害}

**复现步骤**：
1. {请求示例}
2. {响应特征}
3. {分析结论}

**修复建议**：{具体修复措施}

---

## 4. 总结与安全建议

### 4.1 整体评估
{对整个系统安全性的总结}

### 4.2 优先修复
1. **【紧急】** {最高优先级} — 24 小时内
2. **【重要】** {次高优先级} — 1 周内

### 4.3 安全加固
1. 部署 WAF
2. 安全开发生命周期
3. 定期安全评估
4. 最小权限原则

---

## 5. 附录

### 5.1 风险等级定义
| 等级 | CVSS | 定义 |
|------|:----:|------|
| 🔴 严重 | 9.0-10.0 | 远程获取系统权限或核心数据 |
| 🟠 高危 | 7.0-8.9 | 敏感数据泄露或局部控制 |
| 🟡 中危 | 4.0-6.9 | 有限信息泄露或越权 |
| 🔵 低危 | 0.1-3.9 | 安全加固参考 |

---

*本报告由 SecGuard Security Agent 自动生成。*
```

---

## 报告规则

1. **真实准确**：所有数据源自 Phase 1-3 的实际结果，不得虚构。
2. **结构完整**：某章节无内容时注明"无"，不删除章节。
3. **脱敏处理**：密码、Token、个人数据用 `****` 替代。
4. **独立可读**：每条漏洞描述独立完整。

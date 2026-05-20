# 🛡️ SecGuard 网络安全渗透测试技能组

**SecGuard** 是一套面向网络安全专家的 MCP 技能集合，为智能体提供 Web 系统渗透测试的完整能力。
本技能组遵循 PTES 方法论，覆盖从信息收集到报告输出的全流程。

---

## ⚠️ 法律与道德声明

1. **已获得目标系统所有者的书面授权**，否则立即停止。
2. **测试范围明确**：仅测试授权范围内的 URL、IP 段和系统。
3. **数据不得泄露**：发现的用户数据、凭据、敏感文件在报告中脱敏处理。
4. **遵守所在地法律法规**（如《中华人民共和国网络安全法》）。

---

## 渗透测试方法论（四阶段）

```
Phase 1: 信息收集 (Reconnaissance)     → secguard-recon
Phase 2: Web 漏洞扫描 (Vuln Scan)      → secguard-web-vuln-scan
Phase 3: 漏洞利用 (Exploitation)        → secguard-exploit
Phase 4: 报告输出 (Reporting)           → secguard-report
```

---

## 统一输出目录

所有文件**统一保存到 `secguard-output/` 目录**（相对于 Workspace 根目录）。

| 阶段 | 文件路径 | 说明 |
|------|---------|------|
| Phase 1 | `secguard-output/recon-{domain}-{YYYYMMDD-HHmmss}.md` | 信息收集结果（含 `js` 阶段 → API 端点发现） |
| Phase 2 | `secguard-output/vuln-scan-{domain}-{YYYYMMDD-HHmmss}.md` | 漏洞扫描结果（含 `api` 阶段 → API Fuzzing） |
| Phase 3 | `secguard-output/exploit-{domain}-{YYYYMMDD-HHmmss}.md` | 漏洞验证结果（支持 `--register` 自动认证） |
| Phase 4 | `secguard-output/pentest-report-{domain}-{YYYYMMDD-HHmmss}.md` | 最终渗透测试报告 |

> **{domain}**：域名中的点号替换为连字符（如 `example-com`）。  
> **{YYYYMMDD-HHmmss}**：当前本地时间（如 `20260520-143052`），确保每次运行生成唯一文件。

---

## 临时目录规则

执行过程中智能体如果需要生成临时脚本或资源文件，**必须全部放在 `temp/` 目录**下（相对于 Workspace 根目录）。  
如果 `temp/` 不存在，使用 `ExecuteCommand` 创建。测试完成后**不需要清理**，但临时文件不要混入技能资源目录。

正确示例：
```
temp/check_login.py          ✅ 临时脚本放 temp/
temp/payloads.txt            ✅ 临时数据放 temp/
secguard-output/report.md    ✅ 正式输出放 secguard-output/
```

错误示例：
```
secguard_skills/my_script.py ❌ 不要直接写在技能资源目录下
```

---

## 工作流程

1. **前置准备**：确保 `secguard-output/` 和 `temp/` 目录存在（不存在则创建）。
2. **Phase 1**：调用 `ReadSkill` 读取 `secguard-recon`，按 Skill 指令执行信息收集。完成后文件保存到 `secguard-output/recon-*.md`。
3. **Phase 2**：调用 `ReadSkill` 读取 `secguard-web-vuln-scan`，执行漏洞扫描。完成后文件保存到 `secguard-output/vuln-scan-*.md`。
4. **Phase 3**：调用 `ReadSkill` 读取 `secguard-exploit`，验证漏洞。完成后文件保存到 `secguard-output/exploit-*.md`。
5. **Phase 4**：调用 `ReadSkill` 读取 `secguard-report`，汇总生成最终报告，文件保存到 `secguard-output/pentest-report-*.md`。

**禁止行为**：
- ❌ 禁止在没有读取对应 Skill 的情况下自行动作
- ❌ 禁止虚构漏洞或数据
- ❌ 禁止在未经授权的情况下修改目标系统数据
- ❌ 禁止在测试中途停下来问用户"是否继续"——这是一个测试环境，应当全力以赴完成所有测试

> 详见 `systemprompt.txt` 内的完整自治指令。

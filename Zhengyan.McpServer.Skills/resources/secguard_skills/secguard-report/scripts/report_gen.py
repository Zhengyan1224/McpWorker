#!/usr/bin/env python3
"""
SecGuard Report Generator — Cross-platform penetration test report generation.
Assembles findings from recon, vuln scan, and exploit phases into a
professional penetration test report in Markdown format.

Usage:
    python report_gen.py --target example.com --recon recon-results.md \
                         --vuln vuln-scan-results.md --exploit exploit-results.md \
                         --output pentest-report.md
    python report_gen.py --template-only  # Print the empty template

Dependencies: standard library only (no pip install required).
Works on Windows, Linux, and macOS.
"""

import argparse
import os
import re
import sys
from datetime import datetime, timezone


# =============================================================================
# Constants
# =============================================================================

REPORT_TEMPLATE = """# 渗透测试报告

## 1. 概述

| 项目 | 内容 |
|------|------|
| **报告编号** | SEC-{date}-{serial} |
| **目标系统** | {target} |
| **测试日期** | {start_date} - {end_date} |
| **测试方法** | 黑盒 / 灰盒 渗透测试 |
| **测试引擎** | SecGuard Security Agent (MCP Skills) |
| **报告日期** | {report_date} |

### 1.1 测试范围

- **目标 URL**：{target_url}
- **目标域名**：{domain}
- **测试 IP**：{target_ip}
- **排除范围**：无

### 1.2 测试目标

评估目标 Web 系统的安全性，识别安全漏洞和配置缺陷，提供修复建议以提升整体安全防护能力。

### 1.3 测试方法

本次测试采用黑盒/灰盒渗透测试方法，遵循 PTES 标准流程：
1. **信息收集** — DNS 解析、端口扫描、技术栈识别、子域名枚举、敏感路径探测
2. **漏洞扫描** — 目录枚举、表单分析、HTTP 方法检测、目录遍历检测
3. **漏洞利用验证** — SQL 注入、XSS、SSRF、命令注入、认证绕过测试
4. **综合评估** — 风险评级、影响分析、修复建议

---

## 2. 信息收集结果

{recon_summary}

### 2.1 目标基本信息

| 信息项 | 值 |
|--------|-----|
| 目标域名 | {domain} |
| 解析 IP | {target_ip} |
| Web 服务器 | {web_server} |
| 后端技术 | {backend_tech} |
| 前端技术 | {frontend_tech} |

### 2.2 DNS 记录

{dns_records}

### 2.3 开放端口

{open_ports}

### 2.4 发现的子域名

{subdomains}

### 2.5 敏感路径与端点

{sensitive_paths}

---

## 3. 漏洞清单

### 3.1 风险统计

| 风险等级 | 数量 |
|----------|:----:|
| 🔴 严重 (Critical, 9.0-10.0) | {critical_count} |
| 🟠 高危 (High, 7.0-8.9) | {high_count} |
| 🟡 中危 (Medium, 4.0-6.9) | {medium_count} |
| 🔵 低危 (Low, 0.1-3.9) | {low_count} |
| ⚪ 信息 (Info, 0.0) | {info_count} |
| **合计** | **{total_count}** |

### 3.2 漏洞详情

{vulnerability_details}

---

## 4. 总结与安全建议

### 4.1 整体安全评估

{overall_assessment}

### 4.2 优先修复建议

{priority_fixes}

### 4.3 安全加固建议

{security_recommendations}

---

## 5. 附录

### 5.1 风险等级定义

| 等级 | CVSS 评分 | 定义 |
|------|:---------:|------|
| 🔴 严重 | 9.0-10.0 | 可导致远程未授权获取系统权限或敏感数据，对系统造成严重影响 |
| 🟠 高危 | 7.0-8.9 | 可导致敏感数据泄露或局部控制，需要立即修复 |
| 🟡 中危 | 4.0-6.9 | 在特定条件下可能导致有限的信息泄露或越权 |
| 🔵 低危 | 0.1-3.9 | 影响较小，一般作为安全加固参考 |
| ⚪ 信息 | 0.0 | 仅提供参考信息，不构成直接风险 |

### 5.2 测试环境

| 项目 | 内容 |
|------|------|
| 测试工具 | Python 3 (urllib, socket), curl, nslookup |
| 操作系统 | {platform} |
| 测试时间 | {start_date} - {end_date} |
| 输出目录 | `secguard-output/` |

---

*本报告由 SecGuard Security Agent 自动生成。建议开发团队在修复所有高危及以上漏洞后重新进行安全评估。*
"""


# =============================================================================
# Utility
# =============================================================================

def timestamp():
    return datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")


def today_str():
    return datetime.now(timezone.utc).strftime("%Y%m%d")


def today_display():
    return datetime.now(timezone.utc).strftime("%Y-%m-%d")


def now_timestamp():
    """返回时分秒精度的时间戳，用于唯一文件名。格式: YYYYMMDD-HHmmss"""
    return datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")


def extract_domain(target):
    """Extract domain from URL."""
    target = target.strip()
    if "://" in target:
        from urllib.parse import urlparse
        target = urlparse(target).hostname or target
    target = target.split("/")[0]
    target = target.split(":")[0]
    return target.lower()


def parse_results_file(filepath):
    """Read a results file and return its content, or a not-found message."""
    if not filepath or not os.path.exists(filepath):
        return "*[未提供此阶段数据]*\n"
    with open(filepath, "r", encoding="utf-8") as f:
        return f.read()


def extract_section(content, section_title):
    """Extract a section from markdown content between headings."""
    pattern = rf"##\s+{re.escape(section_title)}.*?(?=\n##|\Z)"
    match = re.search(pattern, content, re.DOTALL | re.IGNORECASE)
    if match:
        return match.group(0)
    return ""


def extract_table_from_section(content):
    """Extract table rows from a section of markdown."""
    lines = content.split("\n")
    in_table = False
    rows = []
    for line in lines:
        if "|" in line and "---" not in line:
            in_table = True
            cells = [c.strip() for c in line.split("|") if c.strip()]
            if cells:
                rows.append(cells)
        elif in_table and not line.strip():
            break
    return rows


# =============================================================================
# Recon Summary Extraction
# =============================================================================

def extract_recon_summary(recon_content):
    """Extract key findings from recon report."""
    if not recon_content or recon_content.startswith("*["):
        return {}, "", "", "", "", "", ""

    info = {}

    # Extract tech fingerprinting from HTTP headers
    server = ""
    backend = ""
    frontend = ""
    for line in recon_content.split("\n"):
        if "**Server**:" in line:
            server = line.split(":")[-1].strip().strip("**")
        if "**X-Powered-By**:" in line:
            backend = line.split(":", 1)[-1].strip()

    # Extract DNS records
    dns_section = extract_section(recon_content, "DNS Records")
    dns_content = dns_section if dns_section else "*DNS 信息详见完整报告*\n"

    # Extract port scan results
    port_section = extract_section(recon_content, "Port Scan")
    port_content = port_section if port_section else "*端口信息详见完整报告*\n"

    # Extract subdomains
    sub_section = extract_section(recon_content, "Subdomain")
    sub_content = sub_section if sub_section else "*未发现子域名*\n"

    # Extract sensitive paths
    path_section = extract_section(recon_content, "Sensitive Path")
    # Also try "Path Probe"
    if not path_section:
        path_section = extract_section(recon_content, "Path Probe")
    path_content = path_section if path_section else "*详见漏洞扫描报告*\n"

    return info, dns_content, port_content, sub_content, path_content, server, backend


# =============================================================================
# Vulnerability Details Extraction
# =============================================================================

def extract_vulnerabilities(exploit_content, vuln_content):
    """Extract and categorize vulnerabilities from exploit and vuln scan reports."""
    vulns = {"critical": [], "high": [], "medium": [], "low": [], "info": []}

    # Parse exploit report for detected vulnerabilities
    if exploit_content and not exploit_content.startswith("*["):
        lines = exploit_content.split("\n")
        for i, line in enumerate(lines):
            line_lower = line.lower()
            # Detect "✅ [xxx]" markers from exploit.py output
            if "✅ **[" in line or "✅ [" in line:
                vuln_type = ""
                severity = "info"
                detail = line.strip()

                if "sqli" in line_lower or "sql injection" in line_lower:
                    vuln_type = "SQL Injection"
                    severity = "critical"
                elif "xss" in line_lower:
                    vuln_type = "XSS"
                    severity = "high"
                elif "ssrf" in line_lower:
                    vuln_type = "SSRF"
                    severity = "high"
                elif "cmd injection" in line_lower or "command injection" in line_lower:
                    vuln_type = "Command Injection"
                    severity = "critical"
                elif "idor" in line_lower:
                    vuln_type = "IDOR"
                    severity = "high"
                elif "auth bypass" in line_lower:
                    vuln_type = "Authentication Bypass"
                    severity = "critical"
                elif "upload" in line_lower:
                    vuln_type = "File Upload Bypass"
                    severity = "high"
                elif "boolean" in line_lower:
                    vuln_type = "Boolean-based SQLi"
                    severity = "high"
                elif "time" in line_lower and ("sqli" in line_lower or "sql" in line_lower):
                    vuln_type = "Time-based SQLi"
                    severity = "critical"
                elif "directory listing" in line_lower:
                    vuln_type = "Directory Listing"
                    severity = "medium"
                elif "internal access" in line_lower or "ssrf" in line_lower:
                    vuln_type = "SSRF"
                    severity = "high"

                vulns[severity].append({
                    "type": vuln_type,
                    "detail": detail,
                    "description": "",
                    "remediation": "",
                })

    # Parse vuln scan report for info disclosure findings
    if vuln_content and not vuln_content.startswith("*["):
        for line in vuln_content.split("\n"):
            if "INFO LEAK" in line or "info leak" in line.lower():
                vulns["high"].append({
                    "type": "Information Disclosure",
                    "detail": line.strip(),
                    "description": "",
                    "remediation": "",
                })
            if "Directory listing ENABLED" in line:
                vulns["medium"].append({
                    "type": "Directory Listing",
                    "detail": line.strip(),
                    "description": "",
                    "remediation": "",
                })

    return vulns


def format_vulnerabilities(vulns):
    """Format vulnerabilities into markdown."""
    if not any(vulns.values()):
        return "*本次测试未发现安全漏洞。*\n"

    parts = []
    severity_order = [
        ("critical", "🔴 严重"),
        ("high", "🟠 高危"),
        ("medium", "🟡 中危"),
        ("low", "🔵 低危"),
        ("info", "⚪ 信息"),
    ]

    for sev_key, sev_label in severity_order:
        items = vulns[sev_key]
        if not items:
            continue
        for idx, vuln in enumerate(items, 1):
            parts.append(f"---\n\n#### {sev_label} #{idx}: {vuln['type']}\n\n")
            parts.append(f"**发现详情**：\n{vuln['detail']}\n\n")

            # Provide default remediation based on vulnerability type
            remediation = vuln.get("remediation", "")
            if not remediation:
                remediation = DEFAULT_REMEDIATIONS.get(vuln["type"], "建议联系安全团队进行详细评估。")
            parts.append(f"**修复建议**：\n{remediation}\n\n")

    return "".join(parts)


DEFAULT_REMEDIATIONS = {
    "SQL Injection": (
        "1. 使用参数化查询（Prepared Statements）替代字符串拼接 SQL。\n"
        "2. 对用户输入进行严格的类型校验和白名单过滤。\n"
        "3. 最小化数据库账户权限，避免使用 DBA 账户连接。\n"
        "4. 部署 WAF 作为额外防护层。"
    ),
    "XSS": (
        "1. 对用户输出进行上下文相关的编码（HTML Entity/URL/JavaScript 编码）。\n"
        "2. 实施 Content-Security-Policy（CSP）响应头。\n"
        "3. 对用户输入进行白名单过滤。\n"
        "4. 设置 HttpOnly 和 Secure 标记的 Cookie。"
    ),
    "SSRF": (
        "1. 对内网 IP 范围进行黑名单/白名单过滤。\n"
        "2. 禁用不必要的 URL 重定向功能。\n"
        "3. 对用户输入的 URL 进行严格验证。\n"
        "4. 使用网络策略限制服务器对外连接。"
    ),
    "Command Injection": (
        "1. 避免直接调用系统命令。\n"
        "2. 如果必须使用，使用白名单验证输入参数。\n"
        "3. 对用户输入进行逃逸字符过滤。\n"
        "4. 以最小权限运行应用服务。"
    ),
    "IDOR": (
        "1. 实施访问控制检查，验证用户对资源的访问权限。\n"
        "2. 使用不可预测的资源 ID（如 UUID）。\n"
        "3. 不要在 URL 中暴露内部 ID。"
    ),
    "Authentication Bypass": (
        "1. 使用强密码策略和多因素认证。\n"
        "2. 对登录接口实施速率限制。\n"
        "3. 使用参数化查询防止 SQL 注入绕过。"
    ),
    "File Upload Bypass": (
        "1. 验证文件 MIME 类型和文件头内容。\n"
        "2. 限制可上传的文件类型为白名单。\n"
        "3. 上传文件存储于 Web 根目录之外。\n"
        "4. 对上传文件名进行随机化重命名。"
    ),
    "Information Disclosure": (
        "1. 移除或限制访问敏感文件和目录。\n"
        "2. 配置 Web 服务器禁止目录列表。\n"
        "3. 不要在 Web 根目录存放配置文件。\n"
        "4. 使用 .gitignore 正确处理版本控制文件。"
    ),
    "Directory Listing": (
        "1. 在 Web 服务器配置中禁用目录列表。\n"
        "   - Nginx: `autoindex off;`\n"
        "   - Apache: `Options -Indexes`\n"
        "   - IIS: 在目录浏览功能中禁用。"
    ),
}


# =============================================================================
# Report Generation
# =============================================================================

def generate_report(target, recon_file, vuln_file, exploit_file, output_file=None):
    """Generate a full penetration test report."""
    domain = extract_domain(target)
    target_url = target if target.startswith(("http://", "https://")) else f"https://{domain}/"

    # Read intermediate results
    recon_content = parse_results_file(recon_file)
    vuln_content = parse_results_file(vuln_file)
    exploit_content = parse_results_file(exploit_file)

    # Extract recon summary
    recon_info, dns_records, open_ports, subdomains, sensitive_paths, server, backend = \
        extract_recon_summary(recon_content)

    # Extract vulnerabilities
    vulns = extract_vulnerabilities(exploit_content, vuln_content)

    # Format vulnerability details
    vulnerability_details = format_vulnerabilities(vulns)

    # Count by severity
    critical_count = len(vulns["critical"])
    high_count = len(vulns["high"])
    medium_count = len(vulns["medium"])
    low_count = len(vulns["low"])
    info_count = len(vulns["info"])
    total_count = critical_count + high_count + medium_count + low_count + info_count

    # Recon summary
    recon_summary = f"对目标 `{domain}` 执行了完整的信息收集。'dns_records', 'open_ports', 'subdomains' 和 'sensitive_paths' 详见对应章节。"

    # Overall assessment
    if total_count == 0:
        overall_assessment = f"经过全面测试，目标系统 `{domain}` 未发现安全漏洞，安全状况良好。建议继续保持并定期进行安全检查。"
    elif critical_count > 0:
        overall_assessment = (f"目标系统 `{domain}` 存在 {critical_count} 个严重级别安全漏洞，"
                              f"整体安全风险**极高**。建议立即修复严重和高危漏洞，"
                              f"并在修复完成后重新进行渗透测试。")
    elif high_count > 0:
        overall_assessment = (f"目标系统 `{domain}` 存在 {high_count} 个高危安全漏洞，"
                              f"整体安全风险**较高**。建议优先修复高危漏洞，"
                              f"并在修复完成后重新评估。")
    else:
        overall_assessment = (f"目标系统 `{domain}` 存在 {total_count} 个安全漏洞/配置问题，"
                              f"整体安全风险**中等**。建议按优先级逐步修复。")

    # Priority fixes
    if critical_count > 0:
        priority_fixes = (
            "1. **【紧急】** 修复严重级别漏洞\n"
            "   - 影响范围：可能导致服务器被完全控制或核心数据泄露\n"
            "   - 建议完成时间：**24 小时内**\n\n"
            "2. **【重要】** 修复高危级别漏洞\n"
            "   - 影响范围：可能导致敏感数据泄露或权限提升\n"
            "   - 建议完成时间：**1 周内**\n\n"
            "3. **【建议】** 修复中低危级别问题\n"
            "   - 建议完成时间：**1 个月内**"
        )
    elif high_count > 0:
        priority_fixes = (
            "1. **【重要】** 修复高危级别漏洞\n"
            "   - 建议完成时间：**1 周内**\n\n"
            "2. **【建议】** 修复中低危级别问题\n"
            "   - 建议完成时间：**1 个月内**"
        )
    else:
        priority_fixes = (
            "1. **【建议】** 按优先级修复中低危级别问题\n"
            "   - 建议完成时间：**1 个月内**\n\n"
            "2. **【加固】** 实施安全加固建议以提升整体安全性"
        )

    # Security recommendations
    security_recommendations = (
        "1. **部署 Web 应用防火墙（WAF）**\n"
        "   - 建议在应用前端部署 WAF 以提供实时攻击防护。\n\n"
        "2. **实施安全开发生命周期（SDL）**\n"
        "   - 在开发阶段进行安全评审和安全测试。\n"
        "   - 建立漏洞修复 SLA 制度。\n\n"
        "3. **定期安全评估**\n"
        "   - 建议每季度进行一次全面安全评估。\n"
        "   - 重大版本更新后应进行回归测试。\n\n"
        "4. **日志审计与监控**\n"
        "   - 完善日志记录，确保关键操作留有审计痕迹。\n"
        "   - 部署入侵检测系统（IDS）实时告警。\n\n"
        "5. **权限最小化**\n"
        "   - 严格遵循最小权限原则。\n"
        "   - 定期审计账户权限和系统配置。"
    )

    # Build report
    report_date = timestamp()
    date = today_str()
    serial = f"{date[-4:]}{date[-2:]}{now_timestamp()[-6:]}"

    report = REPORT_TEMPLATE.format(
        target=domain,
        target_url=target_url,
        domain=domain,
        target_ip="(详见 DNS 记录)",
        date=date,
        serial=serial,
        start_date=report_date,
        end_date=report_date,
        report_date=report_date,
        platform=sys.platform,
        recon_summary=recon_summary,
        dns_records=dns_records,
        open_ports=open_ports,
        subdomains=subdomains,
        sensitive_paths=sensitive_paths,
        web_server=server or "未识别",
        backend_tech=backend or "未识别",
        frontend_tech="未识别",
        critical_count=critical_count,
        high_count=high_count,
        medium_count=medium_count,
        low_count=low_count,
        info_count=info_count,
        total_count=total_count,
        vulnerability_details=vulnerability_details,
        overall_assessment=overall_assessment,
        priority_fixes=priority_fixes,
        security_recommendations=security_recommendations,
    )

    if output_file:
        with open(output_file, "w", encoding="utf-8") as f:
            f.write(report)
        print(f"[+] Penetration test report saved to: {output_file}")
    else:
        # 自动生成带时分秒的唯一文件名
        auto_name = f"secguard-output/pentest-report-{domain}-{now_timestamp()}.md"
        try:
            os.makedirs("secguard-output", exist_ok=True)
            with open(auto_name, "w", encoding="utf-8") as f:
                f.write(report)
            print(f"[+] Penetration test report saved to: {auto_name}")
        except Exception as e:
            print(f"[-] Could not save auto-named report ({e}), printing to stdout:")
            print(report)

    return report


# =============================================================================
# CLI
# =============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="SecGuard Report Generator — cross-platform pentest report generation",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python report_gen.py --target example.com --recon recon-results.md \\
      --vuln vuln-scan-results.md --exploit exploit-results.md --output report.md
  python report_gen.py --target example.com  # Generate from existing results files
  python report_gen.py --template-only       # Print empty template
        """,
    )
    parser.add_argument("--target", "-t", help="Target domain or URL")
    parser.add_argument("--recon", "-r", help="Path to recon results file")
    parser.add_argument("--vuln", "-v", help="Path to vulnerability scan results file")
    parser.add_argument("--exploit", "-e", help="Path to exploit results file")
    parser.add_argument("--output", "-o", help="Output file path (Markdown)")
    parser.add_argument("--template-only", action="store_true",
                        help="Print the report template and exit")

    args = parser.parse_args()

    if args.template_only:
        print(REPORT_TEMPLATE)
        return

    if not args.target:
        parser.print_help()
        return

    generate_report(
        target=args.target,
        recon_file=args.recon,
        vuln_file=args.vuln,
        exploit_file=args.exploit,
        output_file=args.output,
    )


if __name__ == "__main__":
    main()

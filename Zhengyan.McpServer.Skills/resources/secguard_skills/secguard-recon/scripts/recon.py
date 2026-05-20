#!/usr/bin/env python3
"""
SecGuard Reconnaissance Tool — Cross-platform information gathering.
Performs DNS resolution, port scanning, HTTP header analysis,
subdomain enumeration, and sensitive path probing.

Usage:
    python recon.py --target example.com --output results.md
    python recon.py --target example.com --phase dns
    python recon.py --target example.com --phase ports --ports 80,443,8080
    python recon.py --target https://example.com --phase js
    python recon.py --target https://example.com --phase all

Dependencies: standard library only (no pip install required).
Works on Windows, Linux, and macOS.
"""

import argparse
import json
import os
import re
import socket
import ssl
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone


# =============================================================================
# Utility
# =============================================================================

def timestamp():
    return datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")


def extract_domain(target):
    """Extract domain from URL or return as-is if already a domain."""
    target = target.strip()
    # Remove protocol
    if "://" in target:
        target = urllib.parse.urlparse(target).hostname or target
    # Remove path/port if present
    target = target.split("/")[0]
    target = target.split(":")[0]
    return target.lower()


def resolve_host(domain):
    """Resolve domain to IPv4 addresses."""
    results = []
    try:
        infos = socket.getaddrinfo(domain, None)
        seen = set()
        for info in infos:
            ip = info[4][0]
            if ip not in seen:
                seen.add(ip)
                results.append(ip)
    except socket.gaierror as e:
        results.append(f"[ERROR] DNS resolution failed: {e}")
    return results


# =============================================================================
# Phase 1: DNS Records
# =============================================================================

def dns_lookup(domain):
    """Basic DNS record lookup."""
    results = []
    results.append(f"## DNS Records\n")
    results.append(f"**Target domain**: {domain}\n")

    # A / AAAA records via socket
    try:
        ips = resolve_host(domain)
        for ip in ips:
            results.append(f"- **A** → {ip}")
    except Exception as e:
        results.append(f"- A record lookup failed: {e}")

    # Attempt nslookup/host/dig for richer results (cross-platform)
    # Try multiple tools in order of preference
    ns_tools = [
        ("nslookup", ["nslookup", "-type=any", domain] if sys.platform == "win32"
                     else ["nslookup", "-type=any", domain]),
    ]

    if sys.platform != "win32":
        ns_tools.insert(0, ("host", ["host", "-a", domain]))
        ns_tools.insert(0, ("dig", ["dig", domain, "ANY", "+short"]))

    for tool_name, cmd in ns_tools:
        try:
            r = subprocess.run(cmd, capture_output=True, text=True, timeout=15)
            if r.returncode == 0 and r.stdout.strip():
                results.append(f"\n**{tool_name} output**:\n```\n{r.stdout.strip()}\n```\n")
                break
        except (FileNotFoundError, subprocess.TimeoutExpired):
            continue

    return "\n".join(results)


# =============================================================================
# Phase 2: Port Scanning
# =============================================================================

def port_scan(target, ports=None):
    """Check if common ports are open on the target."""
    if ports is None:
        ports = [80, 443, 8080, 8443, 22, 21, 3306, 5432, 6379, 27017,
                 3000, 5000, 8000, 8888, 9000, 9090, 9443, 1433, 1521]

    domain = extract_domain(target)
    results = [f"## Port Scan\n\n**Target**: {domain}\n\n| Port | Protocol | Status |\n|------|----------|--------|\n"]

    for port in ports:
        proto = "HTTPS" if port in (443, 8443, 9443) else "HTTP" if port in (80, 8080, 3000, 5000, 8000, 8888, 9000, 9090) else "TCP"
        try:
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(2.0)
            result = sock.connect_ex((domain, port))
            sock.close()
            if result == 0:
                # Try HTTP GET to see if it's a web server
                try:
                    url = f"https://{domain}:{port}/" if port in (443, 8443, 9443) else f"http://{domain}:{port}/"
                    req = urllib.request.Request(url, method="HEAD")
                    with urllib.request.urlopen(req, timeout=3) as resp:
                        results.append(f"| {port} | {proto} | **OPEN** (HTTP {resp.status}) |\n")
                except Exception:
                    results.append(f"| {port} | {proto} | **OPEN** (non-HTTP) |\n")
            else:
                results.append(f"| {port} | {proto} | Closed |\n")
        except Exception as e:
            results.append(f"| {port} | {proto} | Error: {e} |\n")

    return "".join(results)


# =============================================================================
# Phase 3: HTTP Header Analysis
# =============================================================================

def http_headers(url):
    """Fetch and analyze HTTP response headers."""
    if not url.startswith(("http://", "https://")):
        url = f"https://{url}/"

    results = [f"## HTTP Headers\n\n**URL**: {url}\n\n"]

    try:
        req = urllib.request.Request(url, method="GET")
        # Set a common User-Agent
        req.add_header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
        with urllib.request.urlopen(req, timeout=10) as resp:
            headers = dict(resp.headers)
            results.append("### Response Headers\n\n```\n")
            for k, v in sorted(headers.items()):
                results.append(f"{k}: {v}\n")
            results.append("```\n\n")

            # Analysis
            results.append("### Security Header Analysis\n\n")
            security_headers = {
                "Strict-Transport-Security": "HTTP Strict Transport Security (HSTS) — 缺失，建议启用",
                "Content-Security-Policy": "Content Security Policy (CSP) — 缺失，存在 XSS 风险",
                "X-Content-Type-Options": "X-Content-Type-Options — 缺失，存在 MIME 嗅探风险",
                "X-Frame-Options": "X-Frame-Options — 缺失，存在点击劫持风险",
                "X-XSS-Protection": "X-XSS-Protection — 缺失",
                "Referrer-Policy": "Referrer-Policy — 缺失",
                "Permissions-Policy": "Permissions-Policy — 缺失",
                "Access-Control-Allow-Origin": "CORS 配置 — 需检查是否过于宽松",
                "Set-Cookie": "Cookie 配置 — 需检查 HttpOnly/Secure/SameSite 属性",
            }
            has_issues = False
            for hdr, desc in security_headers.items():
                if hdr in headers:
                    results.append(f"- ✅ **{hdr}**: {headers[hdr][:80]}\n")
                else:
                    results.append(f"- ❌ **{hdr}**: {desc}\n")
                    has_issues = True

            # Technology fingerprinting
            results.append("\n### Technology Fingerprinting\n\n")
            tech_indicators = {
                "Server": headers.get("Server", ""),
                "X-Powered-By": headers.get("X-Powered-By", ""),
                "X-AspNet-Version": headers.get("X-AspNet-Version", ""),
                "X-Runtime": headers.get("X-Runtime", ""),
                "X-Generator": headers.get("X-Generator", ""),
            }
            for k, v in tech_indicators.items():
                if v:
                    results.append(f"- **{k}**: {v}\n")

            # Cookie analysis
            if "Set-Cookie" in headers:
                cookie = headers["Set-Cookie"]
                results.append("\n### Cookie Security Check\n\n")
                if "HttpOnly" not in cookie:
                    results.append("- ⚠️ Cookie 缺少 **HttpOnly** 标记，可能被 XSS 窃取\n")
                if "Secure" not in cookie and url.startswith("https"):
                    results.append("- ⚠️ Cookie 缺少 **Secure** 标记，可能通过 HTTP 泄露\n")
                if "SameSite" not in cookie:
                    results.append("- ℹ️ Cookie 缺少 **SameSite** 标记（CSRF 防护建议启用）\n")

            # Status code
            results.append(f"\n**HTTP Status**: {resp.status} {resp.reason}\n")

    except urllib.error.HTTPError as e:
        results.append(f"HTTP Error: {e.code} {e.reason}\n")
    except urllib.error.URLError as e:
        results.append(f"Connection Error: {e.reason}\n")
    except Exception as e:
        results.append(f"Error: {e}\n")

    return "".join(results)


# =============================================================================
# Phase 4: Subdomain Enumeration
# =============================================================================

def subdomain_enum(domain, wordlist=None):
    """Enumerate common subdomains via DNS resolution and HTTP probe."""
    if wordlist is None:
        wordlist = [
            "www", "mail", "admin", "api", "dev", "test", "staging",
            "blog", "portal", "login", "cdn", "static", "m", "app",
            "vpn", "git", "jenkins", "wiki", "forum", "docs", "backup",
            "demo", "beta", "alpha", "stage", "prod", "production",
            "console", "dashboard", "manager", "management", "monitor",
            "status", "help", "support", "shop", "store", "bbs",
            "oa", "hr", "sso", "auth", "pay", "payment", "webmail",
            "exchange", "remote", "download", "upload", "video",
            "news", "media", "img", "static", "res", "resource",
            "track", "tracking", "report", "analytics", "log",
            "ws", "wss", "socket", "chat", "im", "push",
            "cdn", "static", "img", "css", "js", "assets",
        ]

    domain = extract_domain(domain)
    results = [f"## Subdomain Enumeration\n\n**Domain**: {domain}\n\n"]
    results.append("| Subdomain | IP | HTTP Status |\n|-----------|-----|-------------|\n")

    found = 0
    for sub in wordlist:
        fqdn = f"{sub}.{domain}"
        ips = set()
        try:
            infos = socket.getaddrinfo(fqdn, 80, socket.AF_INET)
            for info in infos:
                ips.add(info[4][0])
        except socket.gaierror:
            continue

        # If resolved, try HTTP probe
        http_status = "-"
        for ip in ips:
            try:
                req = urllib.request.Request(f"http://{fqdn}/", method="HEAD")
                req.add_header("User-Agent", "Mozilla/5.0")
                req.add_header("Host", fqdn)
                with urllib.request.urlopen(req, timeout=3) as resp:
                    http_status = str(resp.status)
                    break
            except urllib.error.HTTPError as e:
                http_status = str(e.code)
                break
            except Exception:
                http_status = "no HTTP"

        results.append(f"| {fqdn} | {', '.join(ips)} | {http_status} |\n")
        found += 1
        if found >= 50:  # Limit results
            results.append(f"\n*... showing first 50 results (more subdomains not tested) *\n")
            break

    if found == 0:
        results.append("| *(no subdomains found)* | - | - |\n")

    return "".join(results)


# =============================================================================
# Phase 5: Sensitive Path Probing
# =============================================================================

def check_paths(url, wordlist=None):
    """Probe common sensitive paths on the target."""
    if wordlist is None:
        wordlist = [
            "/robots.txt", "/sitemap.xml", "/sitemap_index.xml",
            "/.git/config", "/.git/HEAD", "/.env", "/.env.production",
            "/.htaccess", "/config.json", "/config.php",
            "/web.config", "/nginx.conf", "/appsettings.json",
            "/package.json", "/composer.json", "/composer.lock",
            "/phpinfo.php", "/info.php", "/test.php",
            "/crossdomain.xml", "/clientaccesspolicy.xml",
            "/backup.zip", "/backup.tar.gz", "/db_backup.sql",
            "/README.md", "/CHANGELOG.md",
            "/admin", "/administrator", "/manager", "/management", "/dashboard",
            "/login", "/signin", "/auth", "/register", "/signup",
            "/api", "/api/v1", "/api/v2", "/graphql", "/swagger", "/docs",
            "/uploads", "/download", "/files",
            "/wp-admin", "/wp-content", "/wp-includes",
            "/wp-json/wp/v2/",
        ]

    if not url.startswith(("http://", "https://")):
        url = f"https://{url}/"

    # Normalize URL
    if not url.endswith("/"):
        url += "/"

    results = [f"## Sensitive Path Probe\n\n**Target**: {url}\n\n"]
    results.append("| Path | Status | Size |\n|------|--------|------|\n")

    for path in wordlist:
        target = url.rstrip("/") + path
        try:
            req = urllib.request.Request(target, method="GET")
            req.add_header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
            with urllib.request.urlopen(req, timeout=5) as resp:
                content = resp.read()
                size = len(content)
                status = resp.status
                results.append(f"| {path} | **{status}** | {size} bytes |\n")
        except urllib.error.HTTPError as e:
            if e.code in (301, 302, 303, 307, 308):
                loc = e.headers.get("Location", "")
                results.append(f"| {path} | {e.code} → {loc} | - |\n")
            elif e.code in (401, 403):
                results.append(f"| {path} | **{e.code}** | (auth required/forbidden) |\n")
            elif e.code == 405:
                results.append(f"| {path} | {e.code} | (method not allowed) |\n")
            else:
                results.append(f"| {path} | {e.code} | - |\n")
        except urllib.error.URLError:
            pass
        except Exception as e:
            results.append(f"| {path} | Error | {e} |\n")

    return "".join(results)


# =============================================================================
# Phase 6: Robots.txt & Sitemap Analysis
# =============================================================================

def fetch_robots(url):
    """Fetch and parse robots.txt."""
    if not url.startswith(("http://", "https://")):
        url = f"https://{url}/"

    parsed = urllib.parse.urlparse(url)
    robots_url = f"{parsed.scheme}://{parsed.netloc}/robots.txt"
    sitemap_url = f"{parsed.scheme}://{parsed.netloc}/sitemap.xml"

    results = [f"## Robots.txt & Sitemap\n\n"]

    # robots.txt
    try:
        req = urllib.request.Request(robots_url)
        req.add_header("User-Agent", "Mozilla/5.0")
        with urllib.request.urlopen(req, timeout=5) as resp:
            content = resp.read().decode("utf-8", errors="replace")
            results.append(f"**robots.txt** ({robots_url})\n\n```\n{content}\n```\n\n")
            # Extract disallowed paths
            disallowed = []
            for line in content.splitlines():
                if line.lower().startswith("disallow"):
                    parts = line.split(":", 1)
                    if len(parts) > 1:
                        path = parts[1].strip()
                        if path:
                            disallowed.append(path)
            if disallowed:
                results.append("**Disallowed paths (may contain sensitive areas)**:\n")
                for p in disallowed:
                    results.append(f"- {p}\n")
                results.append("\n")
    except urllib.error.HTTPError as e:
        if e.code == 404:
            results.append(f"- robots.txt: Not found (404)\n")
        else:
            results.append(f"- robots.txt: HTTP {e.code}\n")
    except Exception as e:
        results.append(f"- robots.txt: Error - {e}\n")

    # sitemap.xml
    try:
        req = urllib.request.Request(sitemap_url)
        req.add_header("User-Agent", "Mozilla/5.0")
        with urllib.request.urlopen(req, timeout=5) as resp:
            content = resp.read().decode("utf-8", errors="replace")
            if content.strip():
                results.append(f"\n**sitemap.xml** ({sitemap_url}) - {len(content)} bytes\n")
    except urllib.error.HTTPError as e:
        if e.code == 404:
            results.append(f"- sitemap.xml: Not found (404)\n")
    except Exception:
        pass

    return "".join(results)


# =============================================================================
# Phase 7: JS API Discovery
# =============================================================================

def js_api_discovery(url, output_file=None):
    """Scrape target homepage for JS files, download them, and extract API endpoints.
    Uses only standard library — no pip install required.
    """
    if not url.startswith(("http://", "https://")):
        url = f"https://{url}/"
    if not url.endswith("/"):
        url += "/"

    results = [f"## JS API Discovery\n\n**Target**: {url}\n\n"]

    # Step 1: Fetch homepage
    print("[*] Fetching homepage...")
    html = ""
    try:
        req = urllib.request.Request(url)
        req.add_header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
        with urllib.request.urlopen(req, timeout=15) as resp:
            html = resp.read().decode("utf-8", errors="replace")
        results.append(f"Homepage fetched: {len(html)} bytes\n\n")
    except Exception as e:
        results.append(f"Failed to fetch homepage: {e}\n")
        return "".join(results)

    # Step 2: Extract JS file references
    print("[*] Extracting JS file references...")
    js_files = re.findall(r'<script[^>]*src=["\']([^"\']+\.js[^"\']*)["\']', html, re.IGNORECASE)
    # Also find non-.js script src (e.g. dynamic module paths)
    js_files += re.findall(r'<script[^>]*src=["\']([^"\']+)["\'][^>]*>', html, re.IGNORECASE)
    # Deduplicate
    js_files = list(dict.fromkeys(js_files))
    results.append(f"Found {len(js_files)} script file(s):\n")
    for jf in js_files:
        results.append(f"- `{jf}`\n")
    results.append("\n")

    # Step 3: Extract inline script content
    print("[*] Analyzing inline scripts...")
    inline_scripts = re.findall(r'<script[^>]*>(.*?)</script>', html, re.DOTALL)
    inline_scripts = [s.strip() for s in inline_scripts if len(s.strip()) > 20]

    all_inline_apis = set()
    for script in inline_scripts:
        apis = re.findall(r'["\'](/api/[^"\']+)["\']', script)
        for a in apis:
            all_inline_apis.add(a)
        fetches = re.findall(r'(?:fetch|axios\.\w+|get|post|put|delete)\s*\(\s*["\']([^"\']+)["\']', script)
        for f in fetches:
            if '/api/' in f:
                all_inline_apis.add(f)

    if inline_scripts:
        results.append(f"Analyzed {len(inline_scripts)} inline script(s)\n")

    # Step 4: Download each JS file and extract API endpoints
    print("[*] Downloading JS files and extracting API endpoints...")
    all_apis = set()
    js_details = []

    for js in js_files:
        js_url = urllib.parse.urljoin(url, js)
        try:
            req = urllib.request.Request(js_url)
            req.add_header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
            with urllib.request.urlopen(req, timeout=10) as resp:
                js_content = resp.read().decode("utf-8", errors="replace")
        except Exception as e:
            js_details.append((js, 0, f"Error: {e}"))
            continue

        js_details.append((js, len(js_content), "OK"))

        # Pattern 1: "/api/xxx" strings
        apis = re.findall(r'["\'](/api/[^"\']+)["\']', js_content)
        for a in apis:
            all_apis.add(a)

        # Pattern 2: fetch/axios calls
        fetches = re.findall(r'(?:fetch|axios\.\w+)\s*\(\s*["\']([^"\']+)["\']', js_content)
        for f in fetches:
            if '/api/' in f:
                all_apis.add(f)

        # Pattern 3: path/route/endpoint assignments
        routes = re.findall(r'(?:path|url|endpoint|href)\s*[:=]\s*["\']([^"\']+)["\']', js_content)
        for r in routes:
            if '/api/' in r:
                all_apis.add(r)

        # Pattern 4: variable assignments containing API paths
        var_apis = re.findall(r'["\'](/[^"\']{5,})["\']', js_content)
        for v in var_apis:
            if v.startswith('/api/') and v not in all_apis:
                # De-duplicate with common suffix trimming
                if not any(existing.endswith(v) or v.endswith(existing) for existing in all_apis):
                    all_apis.add(v)

    # Output JS file details
    results.append("### JS Files Analyzed\n\n| File | Size | Status |\n|------|------|--------|\n")
    for name, size, status in js_details:
        results.append(f"| `{name}` | {size} bytes | {status} |\n")
    results.append("\n")

    # Merge inline APIs
    all_apis.update(all_inline_apis)

    # Step 5: Output discovered API endpoints
    results.append("### Discovered API Endpoints\n\n")
    if all_apis:
        results.append(f"**Total**: {len(all_apis)} endpoint(s)\n\n")
        for api in sorted(all_apis):
            results.append(f"- `{api}`\n")

        # Optional: save to JSON file for consumption by other phases
        if output_file:
            json_path = output_file.replace(".md", "-apis.json")
            try:
                with open(json_path, "w", encoding="utf-8") as f:
                    json.dump(sorted(all_apis), f, indent=2, ensure_ascii=False)
                results.append(f"\n\nAPI list saved to: `{json_path}`\n")
            except Exception:
                pass
    else:
        results.append("No API endpoints discovered from JS analysis.\n")
        results.append("\nSuggestions:\n")
        results.append("- Try `--phase paths` for directory brute-force discovery\n")

    return "".join(results)


# =============================================================================
# Full Reconnaissance Pipeline
# =============================================================================

def full_recon(target, output_file=None):
    """Run all reconnaissance phases and optionally save to file."""
    domain = extract_domain(target)
    url = target if target.startswith(("http://", "https://")) else f"https://{domain}/"

    lines = [
        f"# Reconnaissance Report\n",
        f"\n**Target**: {target}\n",
        f"**Domain**: {domain}\n",
        f"**Timestamp**: {timestamp()}\n",
        f"**Platform**: {sys.platform}\n",
        "---\n\n",
    ]

    print("[*] Phase 1: DNS Lookup...")
    lines.append(dns_lookup(domain))
    lines.append("\n---\n")

    print("[*] Phase 2: Port Scan...")
    lines.append(port_scan(domain))
    lines.append("\n---\n")

    print("[*] Phase 3: HTTP Header Analysis...")
    lines.append(http_headers(url))
    lines.append("\n---\n")

    print("[*] Phase 4: Subdomain Enumeration...")
    lines.append(subdomain_enum(domain))
    lines.append("\n---\n")

    print("[*] Phase 5: Sensitive Path Probe...")
    lines.append(check_paths(url))
    lines.append("\n---\n")

    print("[*] Phase 6: Robots.txt & Sitemap...")
    lines.append(fetch_robots(url))
    lines.append("\n---\n")

    print("[*] Phase 7: JS API Discovery...")
    lines.append(js_api_discovery(url, output_file))

    report = "".join(lines)

    if output_file:
        with open(output_file, "w", encoding="utf-8") as f:
            f.write(report)
        print(f"[+] Report saved to: {output_file}")

    return report


# =============================================================================
# CLI Entry Point
# =============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="SecGuard Reconnaissance Tool — cross-platform information gathering",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python recon.py --target example.com --output recon-report.md
  python recon.py --target https://example.com --phase dns
  python recon.py --target example.com --phase ports --ports 80,443,8080
  python recon.py --target https://example.com --phase js
  python recon.py --target example.com --phase all
        """,
    )
    parser.add_argument("--target", "-t", required=True, help="Target domain or URL")
    parser.add_argument("--phase", "-p", default="all",
                        choices=["all", "dns", "ports", "headers", "subdomains", "paths", "robots", "js"],
                        help="Reconnaissance phase to run (default: all)")
    parser.add_argument("--output", "-o", help="Output file path (Markdown)")
    parser.add_argument("--ports", help="Comma-separated port list (default: common web ports)")

    args = parser.parse_args()
    target = args.target
    output_file = args.output

    if args.phase == "all":
        report = full_recon(target, output_file)
        if not output_file:
            print(report)
    else:
        domain = extract_domain(target)
        url = target if target.startswith(("http://", "https://")) else f"https://{domain}/"

        phase_map = {
            "dns": ("DNS Lookup", dns_lookup(domain)),
            "ports": ("Port Scan", port_scan(domain, [int(p) for p in args.ports.split(",")]) if args.ports else port_scan(domain)),
            "headers": ("HTTP Headers", http_headers(url)),
            "subdomains": ("Subdomains", subdomain_enum(domain)),
            "paths": ("Paths", check_paths(url)),
            "robots": ("Robots", fetch_robots(url)),
            "js": ("JS API Discovery", js_api_discovery(url, output_file)),
        }

        title, content = phase_map[args.phase]
        report = f"# Reconnaissance — {title}\n\n**Target**: {target}\n**Timestamp**: {timestamp()}\n\n{content}"

        if output_file:
            with open(output_file, "w", encoding="utf-8") as f:
                f.write(report)
            print(f"[+] Report saved to: {output_file}")
        else:
            print(report)


if __name__ == "__main__":
    main()

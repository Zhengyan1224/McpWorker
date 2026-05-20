#!/usr/bin/env python3
"""
SecGuard Web Vulnerability Scanner — Cross-platform web vulnerability detection.
Performs directory enumeration, sensitive file detection, form analysis,
HTTP method testing, and initial vulnerability pre-checks.

Usage:
    python vuln_scan.py --target https://example.com --output scan-results.md
    python vuln_scan.py --target https://example.com/ --phase dirs
    python vuln_scan.py --target https://example.com/ --phase forms
    python vuln_scan.py --target https://example.com/ --phase api
    python vuln_scan.py --target https://example.com/ --phase api --api-file apis.json

Dependencies: standard library only (no pip install required).
Works on Windows, Linux, and macOS.
"""

import argparse
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from html.parser import HTMLParser


# =============================================================================
# Utility
# =============================================================================

def timestamp():
    return datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")


def normalize_url(url):
    """Ensure URL has scheme and no trailing path issues."""
    if not url.startswith(("http://", "https://")):
        url = f"https://{url}"
    return url.rstrip("/") + "/"


def fetch_url(url, method="GET", data=None, headers_extra=None, timeout=10):
    """Fetch a URL and return (status, headers, body)."""
    req = urllib.request.Request(url, method=method, data=data)
    req.add_header("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36")
    req.add_header("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8")
    if headers_extra:
        for k, v in headers_extra.items():
            req.add_header(k, v)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            body = resp.read()
            return resp.status, dict(resp.headers), body
    except urllib.error.HTTPError as e:
        body = e.read() if e.fp else b""
        return e.code, dict(e.headers), body
    except urllib.error.URLError as e:
        return 0, {}, f"Connection error: {e.reason}".encode()


# =============================================================================
# Phase 1: Directory & File Bruteforce
# =============================================================================

def dir_bruteforce(url, wordlist=None):
    """Bruteforce common web paths."""
    if wordlist is None:
        wordlist = [
            # Admin & Management
            "/admin", "/administrator", "/manager", "/management", "/dashboard",
            "/console", "/panel", "/cpanel", "/controlpanel", "/backend",
            "/admin/", "/administrator/", "/manager/", "/dashboard/",
            # Login & Auth
            "/login", "/signin", "/auth", "/register", "/signup",
            "/login/", "/signin/", "/auth/", "/register/",
            # API & Docs
            "/api", "/api/v1", "/api/v2", "/api/v3",
            "/api/", "/api/v1/", "/api/v2/",
            "/graphql", "/swagger", "/swagger-ui", "/swagger-ui.html",
            "/swagger/v1/swagger.json", "/api/swagger",
            "/docs", "/api/docs", "/openapi.json",
            "/api/v1/openapi.json",
            # Sensitive files
            "/robots.txt", "/sitemap.xml", "/sitemap_index.xml",
            "/.git/config", "/.git/HEAD", "/.env", "/.env.production",
            "/.env.local", "/.env.development",
            "/.htaccess", "/.htpasswd",
            "/config.json", "/config.php", "/config.js",
            "/web.config", "/nginx.conf", "/appsettings.json",
            "/package.json", "/composer.json", "/composer.lock",
            "/yarn.lock", "/package-lock.json",
            "/phpinfo.php", "/info.php", "/test.php",
            "/crossdomain.xml", "/clientaccesspolicy.xml",
            "/backup.zip", "/backup.tar.gz", "/backup.sql",
            "/db_backup.sql", "/database.sql",
            "/README.md", "/CHANGELOG.md", "/CHANGELOG",
            # WordPress
            "/wp-admin", "/wp-content", "/wp-includes",
            "/wp-config.php", "/wp-config.bak",
            "/wp-json/wp/v2/",
            "/wp-content/plugins/", "/wp-content/themes/",
            "/wp-content/uploads/",
            # Joomla
            "/administrator", "/components", "/modules", "/plugins",
            # Upload & Files
            "/uploads", "/upload", "/download", "/downloads",
            "/files", "/file", "/assets", "/static", "/public",
            "/backup", "/backups", "/bak", "/old", "/tmp",
            "/test", "/dev", "/debug", "/staging",
            # Common backends
            "/phpMyAdmin", "/phpmyadmin", "/pma",
            "/mysql", "/adminer.php",
            "/jmx-console", "/web-console",
            "/actuator", "/actuator/health", "/actuator/info",
            "/actuator/env", "/actuator/beans",
            "/actuator/httptrace", "/actuator/metrics",
            # Unvalidated redirect
            "/redirect", "/out", "/link", "/goto",
        ]

    url = normalize_url(url)
    results = [f"## Directory & File Bruteforce\n\n**Target**: {url}\n\n"]
    results.append("| Path | Status | Size | Notes |\n|------|--------|------|-------|\n")

    found_any = False
    for path in wordlist:
        target_url = url.rstrip("/") + path
        status, headers, body = fetch_url(target_url, timeout=5)

        if status in (200, 201, 204):
            found_any = True
            size = len(body)
            notes = ""
            if path in ("/.git/config", "/.env", "/.env.production",
                        "/.env.local", "/phpinfo.php", "/web.config",
                        "/appsettings.json", "/db_backup.sql"):
                notes = "⚠️ POTENTIAL INFO LEAK"
            results.append(f"| {path} | **{status}** | {size} bytes | {notes} |\n")
        elif status in (301, 302, 303, 307, 308):
            found_any = True
            loc = headers.get("Location", "")
            results.append(f"| {path} | **{status}** | → {loc} | Redirect |\n")
        elif status in (401, 403):
            found_any = True
            results.append(f"| {path} | **{status}** | - | Auth required/Forbidden |\n")
        elif status == 405:
            results.append(f"| {path} | {status} | - | Method not allowed |\n")
        elif status == 500:
            results.append(f"| {path} | **{status}** | - | Server error |\n")

    if not found_any:
        results.append("| *(no accessible paths found)* | - | - | - |\n")

    return "".join(results)


# =============================================================================
# Phase 2: Form Extraction & Analysis
# =============================================================================

class FormParser(HTMLParser):
    """Extract forms from HTML."""

    def __init__(self):
        super().__init__()
        self.forms = []
        self._current_form = None
        self._in_form = False

    def handle_starttag(self, tag, attrs):
        attrs_dict = dict(attrs)
        if tag == "form":
            self._in_form = True
            self._current_form = {
                "action": attrs_dict.get("action", ""),
                "method": attrs_dict.get("method", "GET").upper(),
                "inputs": [],
                "enctype": attrs_dict.get("enctype", ""),
            }
        elif self._in_form and tag == "input":
            self._current_form["inputs"].append({
                "name": attrs_dict.get("name", ""),
                "type": attrs_dict.get("type", "text"),
                "value": attrs_dict.get("value", ""),
            })
        elif self._in_form and tag == "textarea":
            self._current_form["inputs"].append({
                "name": attrs_dict.get("name", ""),
                "type": "textarea",
                "value": "",
            })

    def handle_endtag(self, tag):
        if tag == "form" and self._in_form:
            self.forms.append(self._current_form)
            self._current_form = None
            self._in_form = False


def extract_forms(url):
    """Find and analyze forms on the page."""
    url = normalize_url(url)
    results = [f"## Form Extraction & Analysis\n\n**Target**: {url}\n\n"]

    status, headers, body = fetch_url(url)
    if status != 200:
        results.append(f"Could not fetch page (HTTP {status}).\n")
        return "".join(results)

    html = body.decode("utf-8", errors="replace")
    parser = FormParser()
    parser.feed(html)

    if not parser.forms:
        results.append("No forms detected on this page.\n")
        return "".join(results)

    for i, form in enumerate(parser.forms, 1):
        results.append(f"### Form #{i}\n")
        results.append(f"- **Action**: {form['action'] or '(same page)'}\n")
        results.append(f"- **Method**: {form['method']}\n")
        if form["enctype"]:
            results.append(f"- **Enctype**: {form['enctype']}\n")
        if form["inputs"]:
            results.append("- **Inputs**:\n\n")
            results.append("| Field Name | Type |\n|------------|------|\n")
            for inp in form["inputs"]:
                results.append(f"| {inp['name']} | {inp['type']} |\n")
        results.append("\n")

        # Security notes for forms
        results.append("**Security Notes**:\n")
        action_url = form["action"] if form["action"].startswith("http") else urllib.parse.urljoin(url, form["action"])
        if form["method"] == "GET" and any(inp["type"] in ("password", "hidden") for inp in form["inputs"]):
            results.append("- ⚠️ Form with sensitive fields uses GET method (data exposed in URL)\n")
        if form["enctype"] and "multipart" in form["enctype"]:
            results.append("- ℹ️ File upload form detected — potential upload vulnerability\n")
        if any(inp["type"] == "password" for inp in form["inputs"]):
            results.append("- ℹ️ Login form — test for default credentials, SQL injection, brute-force\n")
        if any(inp["type"] == "text" for inp in form["inputs"]):
            results.append("- ℹ️ Input fields present — test for XSS, SQL injection\n")
        results.append(f"- 🔗 Full action URL: {action_url}\n\n")

    # Extract links
    link_pattern = re.compile(r'<a[^>]+href=["\']([^"\']+)["\']', re.IGNORECASE)
    links = link_pattern.findall(html)
    if links:
        results.append("### Discovered Links\n\n")
        internal_links = []
        external_links = []
        for link in links:
            if link.startswith("http"):
                external_links.append(link)
            elif link.startswith("/") or link.startswith("#") or link.startswith("?"):
                internal_links.append(link)
        if internal_links:
            results.append(f"**Internal paths** ({len(internal_links)}):\n")
            for l in internal_links[:30]:
                results.append(f"- {l}\n")
            if len(internal_links) > 30:
                results.append(f"- ... and {len(internal_links) - 30} more\n")
        if external_links:
            results.append(f"\n**External links** ({len(external_links)}):\n")
            for l in external_links[:20]:
                results.append(f"- {l}\n")

    return "".join(results)


# =============================================================================
# Phase 3: HTTP Methods
# =============================================================================

def check_http_methods(url):
    """Test allowed HTTP methods."""
    url = normalize_url(url)
    results = [f"## HTTP Methods\n\n**Target**: {url}\n\n"]
    methods = ["OPTIONS", "PUT", "DELETE", "PATCH", "TRACE", "CONNECT", "HEAD"]

    allowed = []
    for method in methods:
        status, headers, body = fetch_url(url, method=method, timeout=5)
        if status in (200, 204, 205):
            note = ""
            if method == "PUT":
                note = " ⚠️ Potential file upload via PUT"
            elif method == "DELETE":
                note = " ⚠️ Potential resource deletion"
            elif method == "TRACE":
                note = " ⚠️ Potential XST attack"
            results.append(f"- ✅ **{method}** → HTTP {status}{note}\n")
            allowed.append(method)

    if not allowed:
        results.append("No non-standard methods allowed (only standard GET/POST expected).\n")

    # Check OPTIONS for Allow header
    status, headers, body = fetch_url(url, method="OPTIONS", timeout=5)
    if "Allow" in headers:
        results.append(f"\n**Allow header**: {headers['Allow']}\n")

    return "".join(results)


# =============================================================================
# Phase 4: Directory Listing Detection
# =============================================================================

def check_directory_listing(url):
    """Check if common directories have listing enabled."""
    url = normalize_url(url)
    results = [f"## Directory Listing Check\n\n**Target**: {url}\n\n"]

    dirs = [
        "/uploads", "/assets", "/backup", "/files", "/images",
        "/img", "/css", "/js", "/static", "/public", "/download",
        "/media", "/resources", "/data", "/temp", "/tmp",
        "/logs", "/error_log", "/cache",
    ]

    found = False
    listing_patterns = [
        "Index of ", "Parent Directory", "Directory Listing",
        "[To Parent Directory]", "Apache/", "<title>Directory:",
    ]

    for d in dirs:
        target = url.rstrip("/") + d + "/"
        status, headers, body = fetch_url(target, timeout=5)
        if status == 200:
            html = body.decode("utf-8", errors="replace")
            if any(p in html for p in listing_patterns):
                results.append(f"- ❌ **{d}/** → Directory listing ENABLED\n")
                found = True
            else:
                # Check if it returns something but not a listing
                content_type = headers.get("Content-Type", "")
                if "text/html" in content_type and len(body) > 500:
                    pass  # Probably a 404 page or redirect

    if not found:
        results.append("No directory listing detected on common paths.\n")

    return "".join(results)


# =============================================================================
# Phase 5: Technology & Framework Detection
# =============================================================================

def detect_technology(url):
    """Detect web technology stack from page content."""
    url = normalize_url(url)
    results = [f"## Technology Detection\n\n**Target**: {url}\n\n"]

    status, headers, body = fetch_url(url)
    if status != 200:
        results.append(f"Could not reach target (HTTP {status}).\n")
        return "".join(results)

    html = body.decode("utf-8", errors="replace").lower()

    tech_signatures = {
        "WordPress": [
            "/wp-content/", "/wp-includes/", "/wp-json/",
            "wordpress", "wp-embed",
        ],
        "Drupal": ["drupal", "drupal.js", "drupal.settings"],
        "Joomla": ["joomla", "com_content", "com_users"],
        "Discuz": ["discuz", "discuzx", "nhc_uid"],
        "DedeCMS": ["dedecms", "dede"],
        "ThinkPHP": ["thinkphp", "think_template"],
        "Laravel": ["laravel", "livewire"],
        "Django": ["csrftoken", "django", "wsgi"],
        "Ruby on Rails": ["rails", "ruby on rails", "csrf-param"],
        "ASP.NET": ["__viewstate", "__eventvalidation", "asp.net", "aspnet"],
        "Express/Node.js": ["express", "node.js", "x-powered-by: express"],
        "Spring Boot": ["spring", "actuator", "whitelabel error page"],
        "jQuery": ["jquery"],
        "React": ["react", "react-dom", "create-react-app", "__next"],
        "Vue.js": ["vue.js", "vuejs", "vue-router"],
        "Angular": ["angular", "ng-app", "ng-version"],
        "Bootstrap": ["bootstrap", "bootstrap.min"],
        "Nginx": ["nginx"],
        "Apache": ["apache"],
        "IIS": ["iis", "iis/", "asp.net"],
        "Cloudflare": ["cloudflare", "__cfduid"],
    }

    results.append("### Detected Technologies\n\n")
    detected = []
    for tech, signatures in sorted(tech_signatures.items()):
        for sig in signatures:
            if sig in html or sig in str(headers).lower():
                detected.append(tech)
                break

    if detected:
        for tech in sorted(set(detected)):
            results.append(f"- **{tech}**\n")
    else:
        results.append("No specific technology signatures detected.\n")

    # Version from headers
    server = headers.get("Server", "")
    x_powered = headers.get("X-Powered-By", "")
    if server:
        results.append(f"\n- **Server**: {server}\n")
    if x_powered:
        results.append(f"- **X-Powered-By**: {x_powered}\n")

    return "".join(results)


# =============================================================================
# Phase 6: API Endpoint Fuzzing
# =============================================================================

def fuzz_api_endpoints(url, api_list=None, api_file=None):
    """Fuzz discovered API endpoints for information disclosure and basic security checks.
    
    Args:
        url: Base target URL (for resolving relative paths)
        api_list: Comma-separated list of API paths (e.g. /api/users,/api/auth)
        api_file: JSON file containing a list of API paths
    """
    url = normalize_url(url)
    results = [f"## API Endpoint Fuzzing\n\n**Target**: {url}\n\n"]

    # Collect API endpoints
    endpoints = []
    if api_file:
        try:
            with open(api_file, "r", encoding="utf-8") as f:
                data = json.load(f)
            if isinstance(data, list):
                endpoints = data
                results.append(f"Loaded {len(endpoints)} endpoints from: `{api_file}`\n\n")
        except Exception as e:
            results.append(f"Failed to load API file `{api_file}`: {e}\n\n")

    if api_list:
        custom = [e.strip() for e in api_list.split(",") if e.strip()]
        endpoints.extend(custom)
        results.append(f"Added {len(custom)} endpoint(s) from --api-endpoints\n\n")

    # If no endpoints provided, use a common API path wordlist
    if not endpoints:
        results.append("No API endpoints provided. Probing common API paths...\n\n")
        endpoints = [
            "/api", "/api/v1", "/api/v2", "/api/v3",
            "/api/users", "/api/user", "/api/auth", "/api/login",
            "/api/admin", "/api/config", "/api/status", "/api/health",
            "/api/metrics", "/api/info", "/api/version",
            "/api/data", "/api/items", "/api/list", "/api/search",
            "/api/apps", "/api/plaza", "/api/create",
            "/graphql", "/swagger", "/docs", "/openapi.json",
        ]

    # Deduplicate while preserving order
    seen = set()
    unique_endpoints = []
    for e in endpoints:
        e = e if e.startswith("/") else "/" + e
        if e not in seen:
            seen.add(e)
            unique_endpoints.append(e)
    endpoints = unique_endpoints

    results.append(f"Testing {len(endpoints)} endpoint(s)...\n\n")
    results.append("| Endpoint | GET | POST | Auth Required | Content-Type | Notes |\n")
    results.append("|----------|-----|------|---------------|--------------|-------|\n")

    findings_summary = []

    for ep in endpoints:
        target_url = url.rstrip("/") + ep

        # GET request
        get_status, get_headers, get_body = fetch_url(target_url, timeout=8)
        get_size = len(get_body) if get_body else 0
        get_ctype = get_headers.get("Content-Type", "")[:40]

        # POST request (empty body)
        post_status, post_headers, post_body = fetch_url(
            target_url, method="POST", data=b"", timeout=8
        )
        post_size = len(post_body) if post_body else 0
        post_ctype = post_headers.get("Content-Type", "")[:40]

        # Determine if auth required
        auth_required = ""
        if get_status == 401 or get_status == 403:
            auth_required = "Yes"
        elif post_status == 401 or post_status == 403:
            auth_required = "Yes"

        # Check for info disclosure
        notes = []
        if get_status == 200 and get_size > 0:
            body_text = (get_body or b"").decode("utf-8", errors="replace")
            # Check for sensitive keywords
            sensitive_keywords = ["password", "secret", "token", "api_key", "apikey",
                                  "private", "jwt", "authorization", "admin"]
            found_sensitive = [kw for kw in sensitive_keywords if kw in body_text.lower()]
            if found_sensitive:
                notes.append(f"INFO LEAK: {', '.join(found_sensitive)}")

            # Check for verbose errors
            error_patterns = ["exception", "stack trace", "traceback", "syntax error",
                              "unexpected token", "cannot read property"]
            if any(p in body_text.lower() for p in error_patterns):
                notes.append("VERBOSE ERROR")

            # Check if returns JSON array (potential IDOR)
            if body_text.strip().startswith("[") and body_text.strip().endswith("]"):
                notes.append("JSON array → IDOR risk")

        if get_status == 200 and get_status != post_status:
            notes.append(f"POST→{post_status} differs")

        # Check CORS
        cors_origin = get_headers.get("Access-Control-Allow-Origin", "")
        if cors_origin == "*":
            notes.append("CORS: wildcard origin")
        elif cors_origin and cors_origin != url.rstrip("/"):
            notes.append(f"CORS: {cors_origin}")

        get_label = f"**{get_status}** ({get_size}b)" if get_status in (200, 201, 204) else str(get_status)
        post_label = f"**{post_status}** ({post_size}b)" if post_status in (200, 201, 204) else str(post_status)
        notes_label = "; ".join(notes) if notes else "-"

        results.append(f"| `{ep}` | {get_label} | {post_label} | {auth_required or '-'} | {get_ctype} | {notes_label} |\n")

        if notes:
            findings_summary.append(f"- `{ep}`: {notes_label}")

    results.append("\n")
    if findings_summary:
        results.append("### Findings Summary\n\n")
        for f in findings_summary:
            results.append(f + "\n")
    else:
        results.append("No significant findings from API endpoint fuzzing.\n")

    return "".join(results)


# =============================================================================
# Pipeline Runner
# =============================================================================

def run_all(url, output_file=None):
    """Run all vulnerability scan phases."""
    url = normalize_url(url)

    lines = [
        f"# Web Vulnerability Scan Report\n",
        f"\n**Target**: {url}\n",
        f"**Timestamp**: {timestamp()}\n",
        f"**Platform**: {sys.platform}\n",
        "---\n\n",
    ]

    print("[*] Phase 1: Directory & File Bruteforce...")
    lines.append(dir_bruteforce(url))
    lines.append("\n---\n")

    print("[*] Phase 2: Form Extraction & Analysis...")
    lines.append(extract_forms(url))
    lines.append("\n---\n")

    print("[*] Phase 3: HTTP Methods Check...")
    lines.append(check_http_methods(url))
    lines.append("\n---\n")

    print("[*] Phase 4: Directory Listing Check...")
    lines.append(check_directory_listing(url))
    lines.append("\n---\n")

    print("[*] Phase 5: Technology Detection...")
    lines.append(detect_technology(url))
    lines.append("\n---\n")

    print("[*] Phase 6: API Endpoint Fuzzing...")
    lines.append(fuzz_api_endpoints(url))

    report = "".join(lines)

    if output_file:
        with open(output_file, "w", encoding="utf-8") as f:
            f.write(report)
        print(f"[+] Report saved to: {output_file}")

    return report


# =============================================================================
# CLI
# =============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="SecGuard Web Vulnerability Scanner — cross-platform web vulnerability detection",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python vuln_scan.py --target https://example.com --output scan-results.md
  python vuln_scan.py --target https://example.com/ --phase dirs
  python vuln_scan.py --target https://example.com/ --phase forms
  python vuln_scan.py --target https://example.com/ --phase api
  python vuln_scan.py --target https://example.com/ --phase api --api-file apis.json
        """,
    )
    parser.add_argument("--target", "-t", required=True, help="Target URL")
    parser.add_argument("--phase", "-p", default="all",
                        choices=["all", "dirs", "forms", "methods", "dirlist", "tech", "api"],
                        help="Scan phase (default: all)")
    parser.add_argument("--api-endpoints", help="Comma-separated API paths to fuzz (e.g. /api/users,/api/auth)")
    parser.add_argument("--api-file", help="JSON file containing a list of API paths to fuzz")
    parser.add_argument("--output", "-o", help="Output file path (Markdown)")

    args = parser.parse_args()
    url = args.target

    phase_map = {
        "dirs": ("Directory Bruteforce", dir_bruteforce(url)),
        "forms": ("Form Extraction", extract_forms(url)),
        "methods": ("HTTP Methods", check_http_methods(url)),
        "dirlist": ("Directory Listing", check_directory_listing(url)),
        "tech": ("Technology Detection", detect_technology(url)),
        "api": ("API Endpoint Fuzzing", fuzz_api_endpoints(url, api_list=args.api_endpoints, api_file=args.api_file)),
    }

    if args.phase == "all":
        report = run_all(url, args.output)
        if not args.output:
            print(report)
    else:
        title, content = phase_map[args.phase]
        report = f"# Vulnerability Scan — {title}\n\n**Target**: {url}\n**Timestamp**: {timestamp()}\n\n{content}"
        if args.output:
            with open(args.output, "w", encoding="utf-8") as f:
                f.write(report)
            print(f"[+] Saved to: {args.output}")
        else:
            print(report)


if __name__ == "__main__":
    main()

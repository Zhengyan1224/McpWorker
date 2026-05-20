#!/bin/bash
# SecGuard Reconnaissance Tool — Linux/macOS shell version
# Cross-platform recon using curl, dig/host, and standard Unix tools.
#
# Usage:
#   chmod +x recon.sh
#   ./recon.sh -t example.com -o secguard-output/recon-example-com-20260520-143052.md
#   ./recon.sh -t https://example.com --phase headers
#
# Dependencies: curl, dig (or host/nslookup), date

set -e

# ============================================================
# Parse arguments
# ============================================================
TARGET=""
OUTPUT=""
PHASE="all"
PORTS="80,443,8080,8443,3000,5000,8000,8888,9000,9090,9443"

usage() {
    echo "Usage: $0 -t <target> [-o <output>] [-p <phase>] [--ports <ports>]"
    echo "  -t, --target    Target domain or URL (required)"
    echo "  -o, --output    Output file path"
    echo "  -p, --phase     Phase: all, dns, ports, headers, subdomains, paths, robots"
    echo "  --ports         Comma-separated port list"
    exit 1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -t|--target) TARGET="$2"; shift 2 ;;
        -o|--output) OUTPUT="$2"; shift 2 ;;
        -p|--phase) PHASE="$2"; shift 2 ;;
        --ports) PORTS="$2"; shift 2 ;;
        *) echo "Unknown option: $1"; usage ;;
    esac
done

[ -z "$TARGET" ] && usage

# ============================================================
# Utility functions
# ============================================================
TIMESTAMP=$(date +"%Y-%m-%d %H:%M:%S UTC")
FILENAME_TIMESTAMP=$(date +"%Y%m%d-%H%M%S")

# Extract domain from URL
get_domain() {
    local t="$1"
    # Remove protocol
    t="${t#http://}"
    t="${t#https://}"
    # Remove path and port
    t="${t%%/*}"
    t="${t%%:*}"
    echo "$t"
}

DOMAIN=$(get_domain "$TARGET")
URL="https://${DOMAIN}/"

# Output accumulator
OUT=""
out() { OUT="${OUT}$1\n"; }

# ============================================================
# Phase 1: DNS Lookup
# ============================================================
dns_lookup() {
    out "## DNS Records"
    out ""
    out "**Target domain**: $DOMAIN"
    out ""
    out "| Type | Value |"
    out "|------|-------|"

    # A record
    local ips=$(dig +short "$DOMAIN" A 2>/dev/null || host -t A "$DOMAIN" 2>/dev/null | grep "has address" | awk '{print $NF}' || nslookup "$DOMAIN" 2>/dev/null | grep "Address:" | tail -n +2 | awk '{print $2}')
    if [ -n "$ips" ]; then
        while IFS= read -r ip; do
            [ -n "$ip" ] && out "| A | $ip |"
        done <<< "$ips"
    fi

    # CNAME
    local cname=$(dig +short "$DOMAIN" CNAME 2>/dev/null | head -1)
    [ -n "$cname" ] && out "| CNAME | $cname |"

    # NS records
    local ns=$(dig +short "$DOMAIN" NS 2>/dev/null | head -5)
    if [ -n "$ns" ]; then
        while IFS= read -r n; do
            [ -n "$n" ] && out "| NS | $n |"
        done <<< "$ns"
    fi

    # MX records
    local mx=$(dig +short "$DOMAIN" MX 2>/dev/null | head -5)
    if [ -n "$mx" ]; then
        while IFS= read -r m; do
            [ -n "$m" ] && out "| MX | $m |"
        done <<< "$mx"
    fi
    out ""
}

# ============================================================
# Phase 2: Port Scan
# ============================================================
port_scan() {
    out "## Port Scan"
    out ""
    out "**Target**: $DOMAIN"
    out ""
    out "| Port | Protocol | Status |"
    out "|------|----------|--------|"

    IFS=',' read -ra PORT_ARRAY <<< "$PORTS"
    for port in "${PORT_ARRAY[@]}"; do
        local proto="TCP"
        if [ "$port" = "443" ] || [ "$port" = "8443" ] || [ "$port" = "9443" ]; then
            proto="HTTPS"
        elif [ "$port" = "80" ]; then
            proto="HTTP"
        fi

        # Use /dev/tcp if available (bash built-in)
        if timeout 2 bash -c "echo >/dev/tcp/$DOMAIN/$port" 2>/dev/null; then
            # Check if HTTP service
            local scheme="http"
            [ "$port" = "443" ] || [ "$port" = "8443" ] || [ "$port" = "9443" ] && scheme="https"
            local code=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 3 "${scheme}://${DOMAIN}:${port}/" 2>/dev/null)
            if [ -n "$code" ] && [ "$code" != "000" ]; then
                out "| $port | $proto | **OPEN** (HTTP $code) |"
            else
                out "| $port | $proto | **OPEN** (non-HTTP) |"
            fi
        else
            out "| $port | $proto | Closed |"
        fi
    done
    out ""
}

# ============================================================
# Phase 3: HTTP Headers
# ============================================================
http_headers() {
    out "## HTTP Headers"
    out ""
    out "**URL**: $URL"
    out ""

    local headers=$(curl -s -I -L --connect-timeout 10 "$URL" 2>/dev/null)
    out '```'
    out "$headers"
    out '```'
    out ""

    out "### Security Header Analysis"
    out ""

    # Check specific security headers
    local hsts=$(echo "$headers" | grep -i "Strict-Transport-Security" | head -1)
    local csp=$(echo "$headers" | grep -i "Content-Security-Policy" | head -1)
    local xcto=$(echo "$headers" | grep -i "X-Content-Type-Options" | head -1)
    local xfo=$(echo "$headers" | grep -i "X-Frame-Options" | head -1)
    local rp=$(echo "$headers" | grep -i "Referrer-Policy" | head -1)

    [ -n "$hsts" ] && out "- ✅ **HSTS**: $hsts" || out "- ❌ **HSTS**: 缺失，存在 HTTP 降级风险"
    [ -n "$csp" ] && out "- ✅ **CSP**: $csp" || out "- ❌ **CSP**: 缺失，存在 XSS 风险"
    [ -n "$xcto" ] && out "- ✅ **X-Content-Type-Options**: $xcto" || out "- ❌ **X-Content-Type-Options**: 缺失，存在 MIME 嗅探风险"
    [ -n "$xfo" ] && out "- ✅ **X-Frame-Options**: $xfo" || out "- ❌ **X-Frame-Options**: 缺失，存在点击劫持风险"
    [ -n "$rp" ] && out "- ✅ **Referrer-Policy**: $rp" || out "- ❌ **Referrer-Policy**: 缺失"

    out ""
    out "### Technology Fingerprinting"
    out ""
    local server=$(echo "$headers" | grep -i "^Server:" | head -1)
    local xpb=$(echo "$headers" | grep -i "^X-Powered-By:" | head -1)
    [ -n "$server" ] && out "- **Server**: $server"
    [ -n "$xpb" ] && out "- **X-Powered-By**: $xpb"

    # Cookie analysis
    local cookie=$(echo "$headers" | grep -i "^Set-Cookie:" | head -1)
    if [ -n "$cookie" ]; then
        out ""
        out "### Cookie Security"
        out ""
        echo "$cookie" | grep -qi "httponly" || out "- ⚠️ Cookie 缺少 HttpOnly 标记"
        echo "$cookie" | grep -qi "secure" || out "- ⚠️ Cookie 缺少 Secure 标记"
        echo "$cookie" | grep -qi "samesite" || out "- ℹ️ Cookie 缺少 SameSite 标记"
    fi
    out ""
}

# ============================================================
# Phase 4: Subdomain Enumeration
# ============================================================
subdomain_enum() {
    out "## Subdomain Enumeration"
    out ""
    out "**Domain**: $DOMAIN"
    out ""
    out "| Subdomain | HTTP Status |"
    out "|-----------|-------------|"

    local subs="www mail admin api dev test staging blog portal login cdn m app vpn git jenkins wiki forum docs backup demo"
    for sub in $subs; do
        local fqdn="${sub}.${DOMAIN}"
        local ip=$(dig +short "$fqdn" A 2>/dev/null | head -1)
        if [ -n "$ip" ]; then
            local code=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 3 "http://${fqdn}/" 2>/dev/null)
            [ -z "$code" ] && code="no HTTP"
            out "| $fqdn | $code |"
        fi
    done
    out ""
}

# ============================================================
# Phase 5: Sensitive Path Probing
# ============================================================
check_paths() {
    out "## Sensitive Path Probe"
    out ""
    out "**Target**: $URL"
    out ""
    out "| Path | Status | Size |"
    out "|------|--------|------|"

    local paths="/robots.txt /sitemap.xml /.git/config /.git/HEAD /.env /.env.production /.htaccess /config.json /web.config /appsettings.json /package.json /phpinfo.php /info.php /crossdomain.xml /backup.zip /db_backup.sql /admin /api /swagger /docs /login /uploads /wp-admin"

    for path in $paths; do
        local target_url="${URL%/}${path}"
        local code=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 "$target_url" 2>/dev/null)
        case "$code" in
            200|201|301|302|401|403)
                local size=$(curl -s -o /dev/null -w "%{size_download}" --connect-timeout 5 "$target_url" 2>/dev/null)
                out "| $path | **$code** | ${size}b |"
                ;;
        esac
    done
    out ""
}

# ============================================================
# Phase 6: Robots.txt
# ============================================================
fetch_robots() {
    out "## Robots.txt & Sitemap"
    out ""
    local robots_text=$(curl -s --connect-timeout 10 "${URL}robots.txt" 2>/dev/null)
    if [ -n "$robots_text" ] && ! echo "$robots_text" | grep -qi "not found\|404\|could not"; then
        out "**robots.txt** found:"
        out ""
        out '```'
        out "$robots_text"
        out '```'
        out ""
        # Extract Disallow paths
        local disallows=$(echo "$robots_text" | grep -i "^Disallow:" | sed 's/Disallow: //i')
        if [ -n "$disallows" ]; then
            out "**Disallowed paths**:"
            while IFS= read -r d; do
                [ -n "$d" ] && out "- $d"
            done <<< "$disallows"
            out ""
        fi
    else
        out "- robots.txt: Not found (404)"
        out ""
    fi
}

# ============================================================
# Full Report
# ============================================================
full_recon() {
    out "# Reconnaissance Report"
    out ""
    out "**Target**: $TARGET"
    out "**Domain**: $DOMAIN"
    out "**Timestamp**: $TIMESTAMP"
    out "**Platform**: Linux/macOS"
    out ""
    out "---"
    out ""

    dns_lookup
    out "---"
    out ""
    port_scan
    out "---"
    out ""
    http_headers
    out "---"
    out ""
    subdomain_enum
    out "---"
    out ""
    check_paths
    out "---"
    out ""
    fetch_robots
}

# ============================================================
# Main
# ============================================================
case "$PHASE" in
    all)
        full_recon
        ;;
    dns)
        out "# Reconnaissance — DNS Lookup"
        dns_lookup
        ;;
    ports)
        out "# Reconnaissance — Port Scan"
        port_scan
        ;;
    headers)
        out "# Reconnaissance — HTTP Headers"
        http_headers
        ;;
    subdomains)
        out "# Reconnaissance — Subdomains"
        subdomain_enum
        ;;
    paths)
        out "# Reconnaissance — Path Probe"
        check_paths
        ;;
    robots)
        out "# Reconnaissance — Robots.txt"
        fetch_robots
        ;;
esac

# Output
if [ -n "$OUTPUT" ]; then
    mkdir -p "$(dirname "$OUTPUT")"
    echo -e "$OUT" > "$OUTPUT"
    echo "[+] Report saved to: $OUTPUT"
else
    echo -e "$OUT"
fi

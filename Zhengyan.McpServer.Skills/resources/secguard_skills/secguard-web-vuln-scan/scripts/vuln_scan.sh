#!/bin/bash
# SecGuard Web Vulnerability Scanner — Linux/macOS shell version
# Cross-platform web vulnerability scanning using curl.
#
# Usage:
#   chmod +x vuln_scan.sh
#   ./vuln_scan.sh -t https://example.com -o secguard-output/vuln-scan-example-com-20260520-143052.md
#   ./vuln_scan.sh -t https://example.com --phase dirs
#
# Dependencies: curl, grep, date

set -e

# ============================================================
# Parse arguments
# ============================================================
TARGET=""
OUTPUT=""
PHASE="all"

usage() {
    echo "Usage: $0 -t <target> [-o <output>] [-p <phase>]"
    echo "  -t, --target    Target URL (required)"
    echo "  -o, --output    Output file path"
    echo "  -p, --phase     Phase: all, dirs, forms, methods, dirlist, tech"
    exit 1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -t|--target) TARGET="$2"; shift 2 ;;
        -o|--output) OUTPUT="$2"; shift 2 ;;
        -p|--phase) PHASE="$2"; shift 2 ;;
        *) echo "Unknown option: $1"; usage ;;
    esac
done

[ -z "$TARGET" ] && usage

# ============================================================
# Utility
# ============================================================
TIMESTAMP=$(date +"%Y-%m-%d %H:%M:%S UTC")
OUT=""
out() { OUT="${OUT}$1\n"; }

# Normalize URL
BASE="${TARGET%/}/"

# ============================================================
# Phase 1: Directory & File Bruteforce
# ============================================================
dir_bruteforce() {
    out "## Directory & File Bruteforce"
    out ""
    out "**Target**: $BASE"
    out ""
    out "| Path | Status | Size | Notes |"
    out "|------|--------|------|-------|"

    local paths=(
        "/admin" "/administrator" "/manager" "/dashboard" "/login"
        "/api" "/api/v1" "/graphql" "/swagger" "/docs"
        "/robots.txt" "/sitemap.xml"
        "/.git/config" "/.git/HEAD" "/.env" "/.env.production"
        "/config.json" "/web.config" "/appsettings.json"
        "/package.json" "/composer.json"
        "/phpinfo.php" "/info.php" "/test.php"
        "/crossdomain.xml"
        "/backup.zip" "/backup.tar.gz" "/db_backup.sql"
        "/wp-admin" "/wp-content" "/wp-includes"
        "/actuator" "/actuator/health" "/actuator/env"
        "/uploads" "/download" "/files" "/backup" "/assets"
        "/test" "/dev" "/debug" "/tmp"
    )

    for path in "${paths[@]}"; do
        local url="${BASE%/}${path}"
        local code=$(curl -s -o /dev/null -w "%{http_code}" --connect-timeout 5 "$url" 2>/dev/null)
        case "$code" in
            200|201)
                local size=$(curl -s -o /dev/null -w "%{size_download}" --connect-timeout 5 "$url" 2>/dev/null)
                local notes=""
                case "$path" in
                    "/.git/config"|"/.git/HEAD"|"/.env"|"/.env.production"|"/phpinfo.php"|"/web.config"|"/appsettings.json"|"/db_backup.sql")
                        notes="⚠️ POTENTIAL INFO LEAK"
                        ;;
                esac
                out "| $path | **$code** | ${size}b | $notes |"
                ;;
            301|302|303|307|308)
                local loc=$(curl -s -o /dev/null -w "%{redirect_url}" --connect-timeout 5 "$url" 2>/dev/null)
                out "| $path | **$code** | → ${loc:0:60} | Redirect |"
                ;;
            401|403)
                out "| $path | **$code** | - | Auth required |"
                ;;
        esac
    done
    out ""
}

# ============================================================
# Phase 2: HTTP Methods
# ============================================================
check_http_methods() {
    out "## HTTP Methods"
    out ""
    out "**Target**: $BASE"
    out ""

    for method in OPTIONS PUT DELETE PATCH TRACE CONNECT HEAD; do
        local code=$(curl -s -o /dev/null -w "%{http_code}" -X "$method" --connect-timeout 5 "$BASE" 2>/dev/null)
        if [ "$code" = "200" ] || [ "$code" = "204" ] || [ "$code" = "205" ]; then
            local note=""
            [ "$method" = "PUT" ] && note=" ⚠️ Potential file upload"
            [ "$method" = "DELETE" ] && note=" ⚠️ Potential resource deletion"
            [ "$method" = "TRACE" ] && note=" ⚠️ Potential XST attack"
            out "- ✅ **${method}** → HTTP ${code}${note}"
        fi
    done
    out ""
}

# ============================================================
# Phase 3: Directory Listing
# ============================================================
check_directory_listing() {
    out "## Directory Listing Check"
    out ""
    out "**Target**: $BASE"
    out ""

    local dirs=("/uploads" "/backup" "/files" "/assets" "/images" "/static" "/download" "/logs")
    local found=false

    for dir in "${dirs[@]}"; do
        local body=$(curl -s --connect-timeout 5 "${BASE%/}${dir}/" 2>/dev/null)
        if echo "$body" | grep -qiE "Index of|Parent Directory|Directory Listing|\[To Parent"; then
            out "- ❌ **${dir}/** → Directory listing ENABLED"
            found=true
        fi
    done

    $found || out "- No directory listing detected"
    out ""
}

# ============================================================
# Phase 4: Technology Detection
# ============================================================
detect_technology() {
    out "## Technology Detection"
    out ""
    out "**Target**: $BASE"
    out ""

    local headers=$(curl -s -I -L --connect-timeout 10 "$BASE" 2>/dev/null)
    local server=$(echo "$headers" | grep -i "^Server:" | sed 's/^[Ss]erver: //')
    local xpb=$(echo "$headers" | grep -i "^X-Powered-By:" | sed 's/^[Xx]-[Pp]owered-[Bb]y: //')
    local cookie=$(echo "$headers" | grep -i "^Set-Cookie:" | head -1)

    [ -n "$server" ] && out "- **Server**: $server"
    [ -n "$xpb" ] && out "- **X-Powered-By**: $xpb"

    # Cookie-based detection
    if echo "$cookie" | grep -qi "PHPSESSID"; then out "- **Backend**: PHP"; fi
    if echo "$cookie" | grep -qi "JSESSIONID"; then out "- **Backend**: Java/JSP"; fi
    if echo "$cookie" | grep -qi "\.ASPXAUTH"; then out "- **Backend**: ASP.NET"; fi

    out ""
}

# ============================================================
# Form Extraction (HTML parsing via grep)
# ============================================================
extract_forms() {
    out "## Form Extraction"
    out ""
    out "**Target**: $BASE"
    out ""

    local html=$(curl -s -L --connect-timeout 10 "$BASE" 2>/dev/null)

    # Extract form actions
    local forms=$(echo "$html" | grep -oiP '<form[^>]*action=["'"'"'][^"'"'"']*["'"'"'][^>]*>' 2>/dev/null || echo "$html" | grep -oi '<form[^>]*>' 2>/dev/null)
    if [ -n "$forms" ]; then
        out "### Forms Found"
        out ""
        while IFS= read -r form; do
            local action=$(echo "$form" | grep -oiP 'action=["'"'"']([^"'"'"']*)' 2>/dev/null | sed 's/action=//i' | tr -d '"'"'")
            local method=$(echo "$form" | grep -oiP 'method=["'"'"']([^"'"'"']*)' 2>/dev/null | sed 's/method=//i' | tr -d '"'"'')
            [ -z "$method" ] && method="GET"
            [ -z "$action" ] && action="(same page)"
            out "- Form: method=$method action=$action"
        done <<< "$forms"
    else
        out "- No forms detected"
    fi
    out ""
}

# ============================================================
# Main
# ============================================================
full_scan() {
    out "# Web Vulnerability Scan Report"
    out ""
    out "**Target**: $BASE"
    out "**Timestamp**: $TIMESTAMP"
    out ""
    out "---"
    out ""

    dir_bruteforce
    out "---"
    out ""
    check_http_methods
    out "---"
    out ""
    check_directory_listing
    out "---"
    out ""
    detect_technology
    out "---"
    out ""
    extract_forms
}

case "$PHASE" in
    all) full_scan ;;
    dirs) dir_bruteforce ;;
    methods) check_http_methods ;;
    dirlist) check_directory_listing ;;
    tech) detect_technology ;;
    forms) extract_forms ;;
esac

# Output
if [ -n "$OUTPUT" ]; then
    mkdir -p "$(dirname "$OUTPUT")"
    echo -e "$OUT" > "$OUTPUT"
    echo "[+] Report saved to: $OUTPUT"
else
    echo -e "$OUT"
fi

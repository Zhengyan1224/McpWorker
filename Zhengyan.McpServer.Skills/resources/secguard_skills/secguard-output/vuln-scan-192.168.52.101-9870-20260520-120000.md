# Web Vulnerability Scan Report

**Target**: http://192.168.52.101:9870/
**Timestamp**: 2026-05-20 11:20:38 UTC
**Platform**: win32
---

## Directory & File Bruteforce

**Target**: http://192.168.52.101:9870/

| Path | Status | Size | Notes |
|------|--------|------|-------|
| /admin | **200** | 16179 bytes |  |
| /administrator | **200** | 16179 bytes |  |
| /manager | **200** | 16179 bytes |  |
| /management | **200** | 16179 bytes |  |
| /dashboard | **200** | 16179 bytes |  |
| /console | **200** | 16179 bytes |  |
| /panel | **200** | 16179 bytes |  |
| /cpanel | **200** | 16179 bytes |  |
| /controlpanel | **200** | 16179 bytes |  |
| /backend | **200** | 16179 bytes |  |
| /admin/ | **200** | 16179 bytes |  |
| /administrator/ | **200** | 16179 bytes |  |
| /manager/ | **200** | 16179 bytes |  |
| /dashboard/ | **200** | 16179 bytes |  |
| /login | **200** | 16179 bytes |  |
| /signin | **200** | 16179 bytes |  |
| /auth | **200** | 16179 bytes |  |
| /register | **200** | 16179 bytes |  |
| /signup | **200** | 16179 bytes |  |
| /login/ | **200** | 16179 bytes |  |
| /signin/ | **200** | 16179 bytes |  |
| /auth/ | **200** | 16179 bytes |  |
| /register/ | **200** | 16179 bytes |  |
| /api | **200** | 16179 bytes |  |
| /api/v1 | **200** | 16179 bytes |  |
| /api/v2 | **200** | 16179 bytes |  |
| /api/v3 | **200** | 16179 bytes |  |
| /api/ | **200** | 16179 bytes |  |
| /api/v1/ | **200** | 16179 bytes |  |
| /api/v2/ | **200** | 16179 bytes |  |
| /graphql | **200** | 16179 bytes |  |
| /swagger | **200** | 16179 bytes |  |
| /swagger-ui | **200** | 16179 bytes |  |
| /swagger-ui.html | **200** | 16179 bytes |  |
| /swagger/v1/swagger.json | **200** | 16179 bytes |  |
| /api/swagger | **200** | 16179 bytes |  |
| /docs | **200** | 16179 bytes |  |
| /api/docs | **200** | 16179 bytes |  |
| /openapi.json | **200** | 16179 bytes |  |
| /api/v1/openapi.json | **200** | 16179 bytes |  |
| /robots.txt | **200** | 16179 bytes |  |
| /sitemap.xml | **200** | 16179 bytes |  |
| /sitemap_index.xml | **200** | 16179 bytes |  |
| /.git/config | **200** | 16179 bytes | ⚠️ POTENTIAL INFO LEAK |
| /.git/HEAD | **200** | 16179 bytes |  |
| /.env | **200** | 16179 bytes | ⚠️ POTENTIAL INFO LEAK |
| /.env.production | **200** | 16179 bytes | ⚠️ POTENTIAL INFO LEAK |
| /.env.local | **200** | 16179 bytes | ⚠️ POTENTIAL INFO LEAK |
| /.env.development | **200** | 16179 bytes |  |
| /.htaccess | **200** | 16179 bytes |  |
| /.htpasswd | **200** | 16179 bytes |  |
| /config.json | **200** | 16179 bytes |  |
| /config.php | **200** | 16179 bytes |  |
| /config.js | **200** | 16179 bytes |  |
| /web.config | **200** | 16179 bytes | ⚠️ POTENTIAL INFO LEAK |
| /nginx.conf | **200** | 16179 bytes |  |
| /appsettings.json | **200** | 16179 bytes | ⚠️ POTENTIAL INFO LEAK |
| /package.json | **200** | 16179 bytes |  |
| /composer.json | **200** | 16179 bytes |  |
| /composer.lock | **200** | 16179 bytes |  |
| /yarn.lock | **200** | 16179 bytes |  |
| /package-lock.json | **200** | 16179 bytes |  |
| /phpinfo.php | **200** | 16179 bytes | ⚠️ POTENTIAL INFO LEAK |
| /info.php | **200** | 16179 bytes |  |
| /test.php | **200** | 16179 bytes |  |
| /crossdomain.xml | **200** | 16179 bytes |  |
| /clientaccesspolicy.xml | **200** | 16179 bytes |  |
| /backup.zip | **200** | 16179 bytes |  |
| /backup.tar.gz | **200** | 16179 bytes |  |
| /backup.sql | **200** | 16179 bytes |  |
| /db_backup.sql | **200** | 16179 bytes | ⚠️ POTENTIAL INFO LEAK |
| /database.sql | **200** | 16179 bytes |  |
| /README.md | **200** | 16179 bytes |  |
| /CHANGELOG.md | **200** | 16179 bytes |  |
| /CHANGELOG | **200** | 16179 bytes |  |
| /wp-admin | **200** | 16179 bytes |  |
| /wp-content | **200** | 16179 bytes |  |
| /wp-includes | **200** | 16179 bytes |  |
| /wp-config.php | **200** | 16179 bytes |  |
| /wp-config.bak | **200** | 16179 bytes |  |
| /wp-json/wp/v2/ | **200** | 16179 bytes |  |
| /wp-content/plugins/ | **200** | 16179 bytes |  |
| /wp-content/themes/ | **200** | 16179 bytes |  |
| /wp-content/uploads/ | **200** | 16179 bytes |  |
| /administrator | **200** | 16179 bytes |  |
| /components | **200** | 16179 bytes |  |
| /modules | **200** | 16179 bytes |  |
| /plugins | **200** | 16179 bytes |  |
| /uploads | **200** | 16179 bytes |  |
| /download | **200** | 16179 bytes |  |
| /downloads | **200** | 16179 bytes |  |
| /files | **200** | 16179 bytes |  |
| /file | **200** | 16179 bytes |  |
| /assets | **200** | 16179 bytes |  |
| /static | **200** | 16179 bytes |  |
| /public | **200** | 16179 bytes |  |
| /backup | **200** | 16179 bytes |  |
| /backups | **200** | 16179 bytes |  |
| /bak | **200** | 16179 bytes |  |
| /old | **200** | 16179 bytes |  |
| /tmp | **200** | 16179 bytes |  |
| /test | **200** | 16179 bytes |  |
| /dev | **200** | 16179 bytes |  |
| /debug | **200** | 16179 bytes |  |
| /staging | **200** | 16179 bytes |  |
| /phpMyAdmin | **200** | 16179 bytes |  |
| /phpmyadmin | **200** | 16179 bytes |  |
| /pma | **200** | 16179 bytes |  |
| /mysql | **200** | 16179 bytes |  |
| /adminer.php | **200** | 16179 bytes |  |
| /jmx-console | **200** | 16179 bytes |  |
| /web-console | **200** | 16179 bytes |  |
| /actuator | **200** | 16179 bytes |  |
| /actuator/health | **200** | 16179 bytes |  |
| /actuator/info | **200** | 16179 bytes |  |
| /actuator/env | **200** | 16179 bytes |  |
| /actuator/beans | **200** | 16179 bytes |  |
| /actuator/httptrace | **200** | 16179 bytes |  |
| /actuator/metrics | **200** | 16179 bytes |  |
| /redirect | **200** | 16179 bytes |  |
| /out | **200** | 16179 bytes |  |
| /link | **200** | 16179 bytes |  |
| /goto | **200** | 16179 bytes |  |

---
## Form Extraction & Analysis

**Target**: http://192.168.52.101:9870/

No forms detected on this page.

---
## HTTP Methods

**Target**: http://192.168.52.101:9870/

- ✅ **HEAD** → HTTP 200

---
## Directory Listing Check

**Target**: http://192.168.52.101:9870/

No directory listing detected on common paths.

---
## Technology Detection

**Target**: http://192.168.52.101:9870/

### Detected Technologies

- **jQuery**

---
## API Endpoint Fuzzing

**Target**: http://192.168.52.101:9870/

No API endpoints provided. Probing common API paths...

Testing 26 endpoint(s)...

| Endpoint | GET | POST | Auth Required | Content-Type | Notes |
|----------|-----|------|---------------|--------------|-------|
| `/api` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/v1` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/v2` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/v3` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/users` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/user` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/auth` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/login` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/admin` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/config` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/status` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/health` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/metrics` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/info` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/version` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/data` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/items` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/list` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/search` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/apps` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/plaza` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/api/create` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/graphql` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/swagger` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/docs` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |
| `/openapi.json` | **200** (16179b) | **200** (16179b) | - | text/html;charset=utf-8 | INFO LEAK: password |

### Findings Summary

- `/api`: INFO LEAK: password
- `/api/v1`: INFO LEAK: password
- `/api/v2`: INFO LEAK: password
- `/api/v3`: INFO LEAK: password
- `/api/users`: INFO LEAK: password
- `/api/user`: INFO LEAK: password
- `/api/auth`: INFO LEAK: password
- `/api/login`: INFO LEAK: password
- `/api/admin`: INFO LEAK: password
- `/api/config`: INFO LEAK: password
- `/api/status`: INFO LEAK: password
- `/api/health`: INFO LEAK: password
- `/api/metrics`: INFO LEAK: password
- `/api/info`: INFO LEAK: password
- `/api/version`: INFO LEAK: password
- `/api/data`: INFO LEAK: password
- `/api/items`: INFO LEAK: password
- `/api/list`: INFO LEAK: password
- `/api/search`: INFO LEAK: password
- `/api/apps`: INFO LEAK: password
- `/api/plaza`: INFO LEAK: password
- `/api/create`: INFO LEAK: password
- `/graphql`: INFO LEAK: password
- `/swagger`: INFO LEAK: password
- `/docs`: INFO LEAK: password
- `/openapi.json`: INFO LEAK: password

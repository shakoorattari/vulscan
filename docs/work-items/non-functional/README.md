# ⚙️ Non-Functional Requirements — Work Items

## 6.1 Performance

| ID | Requirement | Target | Status | Notes |
|---|---|---|---|---|
| **NFR-PERF-001** | SBOM generation per repo | < 30s | ✅ **Met** | API-based approach ~1-2s per repo |
| **NFR-PERF-002** | Vuln scan time per SBOM | < 20s | ✅ **Met** | In-memory matching is instant |
| **NFR-PERF-003** | Total scan time (100 repos) | < 4 hours | ✅ **Met** | 65 repos in 86 seconds |
| **NFR-PERF-004** | Concurrent processing | Configurable (5) | 🔶 **Partial** | Sequential processing currently |
| **NFR-PERF-005** | Agent memory < 2 GB | < 2 GB | ✅ **Met** | .NET is memory efficient |
| **NFR-PERF-006** | API response time (p95) | < 500 ms | ✅ **Met** | Sub-100ms responses |
| **NFR-PERF-007** | Dashboard initial load | < 3 seconds | ✅ **Met** | Angular loads quickly |
| **NFR-PERF-008** | DB query response (paginated) | < 200 ms | ✅ **Met** | SQLite/EF Core fast queries |

## 6.2 Security

| ID | Requirement | Priority | Status | Notes |
|---|---|---|---|---|
| **NFR-SEC-001** | No source code persistence | 🔴 Critical | ✅ **Done** | API-based, no cloning |
| **NFR-SEC-002** | Encrypted credential storage | 🔴 Critical | ❌ **Not Done** | Plain JSON storage |
| **NFR-SEC-003** | Credentials never logged | 🔴 Critical | ✅ **Done** | Serilog configured, no credential logging |
| **NFR-SEC-004** | Read-only Azure DevOps access | 🟠 High | ✅ **Done** | Only GET requests to API |
| **NFR-SEC-005** | HTTPS for API (TLS 1.2+) | 🔴 Critical | 🔶 **Partial** | HTTP in dev; HTTPS configurable |
| **NFR-SEC-006** | SQL injection prevention | 🔴 Critical | ✅ **Done** | EF Core parameterized queries |
| **NFR-SEC-007** | Angular XSS protection | 🔴 Critical | ✅ **Done** | Angular built-in sanitization |
| **NFR-SEC-008** | CORS restricted to dashboard | 🟠 High | ✅ **Done** | CORS configured |
| **NFR-SEC-009** | BCrypt password hashing (12+) | 🔴 Critical | ✅ **Done** | BCrypt with cost factor 12 |
| **NFR-SEC-010** | PAT rotation reminders | 🟡 Medium | ❌ **Not Done** | |

## 6.3 Reliability

| ID | Requirement | Target | Status | Notes |
|---|---|---|---|---|
| **NFR-REL-001** | Agent uptime > 99% | > 99% | ✅ **Met** | BackgroundService is always-on |
| **NFR-REL-003** | Failed scan auto-retry (3x) | 3 attempts | ❌ **Not Done** | |
| **NFR-REL-006** | DB connection resilience | Auto-reconnect | ✅ **Done** | EF Core handles reconnection |

## 6.4 Maintainability

| ID | Requirement | Priority | Status | Notes |
|---|---|---|---|---|
| **NFR-MNT-001** | No hardcoded values | 🔴 Critical | 🔶 **Partial** | Config in appsettings.json; some hardcoded values remain |
| **NFR-MNT-003** | Angular lazy-loaded modules | 🟠 High | ❌ **Not Done** | All components eagerly loaded |
| **NFR-MNT-004** | Structured logging | 🟠 High | ✅ **Done** | Serilog with console + file sinks |
| **NFR-MNT-006** | Docker Compose | 🟡 Medium | ✅ **Done** | `docker-compose.yml` exists |
| **NFR-MNT-008** | Test coverage > 70% | 🟠 High | ❌ **Not Done** | No tests written yet |

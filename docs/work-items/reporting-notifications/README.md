# 📧 Reporting & Notifications — Work Items

| ID | Requirement | Priority | Status | Notes |
|---|---|---|---|---|
| **FR-REP-001** | HTML executive summary reports | 🟠 High | 🔶 **Partial** | JSON executive summary API ✅; Angular reports page ✅; HTML render ❌ |
| **FR-REP-002** | CSV/JSON detailed reports | 🟠 High | ✅ **Done** | CSV export (packages, vulns) ✅; JSON per-project ✅; JSON per-CVE ✅ |
| **FR-REP-003** | Attach SBOMs to reports | 🟡 Medium | ❌ **Not Done** | |
| **FR-REP-004** | SMTP email notifications | 🔴 Critical | ❌ **Not Done** | |
| **FR-REP-005** | Teams webhook notifications | 🟠 High | ❌ **Not Done** | |
| **FR-REP-006** | In-dashboard alerts | 🟠 High | ❌ **Not Done** | |
| **FR-REP-007** | Vulnerability age tracking | 🟡 Medium | ✅ **Done** | `FirstDetectedAt` stored; age displayed in project detail reports |
| **FR-REP-008** | SLA breach alerts | 🟡 Medium | ❌ **Not Done** | |
| **FR-REP-009** | Notification history in DB | 🟡 Medium | ❌ **Not Done** | |
| **FR-REP-010** | Report generation from dashboard | 🟠 High | ✅ **Done** | Reports page with Projects/Vulnerabilities/Trends tabs; drill-down views |

## Completed: Project & Vulnerability Reports

Report capabilities built:

1. **Per-Project Report** — All repos, packages and vulnerabilities for a specific project ✅
2. **Per-Vulnerability Report** — All repos/packages affected by a specific CVE ✅
3. **Executive Summary Report** — Overall scan statistics with severity/ecosystem breakdown ✅
4. **CSV Export** — Packages CSV + vulnerabilities CSV for projects and global ✅
5. **Severity Trends** — Historical trend data across scans with bar chart ✅
6. **Angular Reports Page** — 3 tabs (Projects, Vulnerabilities, Trends) + detail pages ✅

# 📊 Angular Dashboard — Work Items

| ID | Requirement | Priority | Status | Notes |
|---|---|---|---|---|
| **FR-UI-001** | Angular 18+ SPA with standalone components | 🔴 Critical | ✅ **Done** | Angular 19+ with standalone components |
| **FR-UI-002** | Angular Material component library | 🔴 Critical | ✅ **Done** | Material Design throughout |
| **FR-UI-003** | Responsive layout (desktop + tablet) | 🟠 High | 🔶 **Partial** | Desktop layout works; tablet could be improved |
| **FR-UI-004** | JWT login page + HttpInterceptor | 🔴 Critical | ✅ **Done** | `LoginComponent` + `authInterceptor` |
| **FR-UI-005** | Route guards (canActivate) | 🔴 Critical | ✅ **Done** | `authGuard` + `adminGuard` |
| **FR-UI-006** | Executive Dashboard (KPI, charts) | 🔴 Critical | 🔶 **Partial** | KPI cards ✅; tables ✅; pie chart ❌; trend chart in Reports ✅ |
| **FR-UI-007** | Vulnerability Explorer (paginated) | 🔴 Critical | ✅ **Done** | Reports → Vulnerabilities tab with severity filter |
| **FR-UI-008** | Vulnerability Detail view | 🟠 High | ✅ **Done** | `VulnerabilityDetailComponent` — affected repos, packages |
| **FR-UI-009** | Scan History page | 🟠 High | ✅ **Done** | `ScansComponent` with history table |
| **FR-UI-010** | SBOM Browser | 🟡 Medium | ❌ **Not Done** | |
| **FR-UI-011** | Repository Management | 🟠 High | ❌ **Not Done** | |
| **FR-UI-012** | Configuration Manager (admin) | 🟡 Medium | ❌ **Not Done** | |
| **FR-UI-013** | User Management (admin) | 🟡 Medium | ❌ **Not Done** | |
| **FR-UI-014** | Notification Log | 🟢 Low | ❌ **Not Done** | |
| **FR-UI-015** | Report Generator (HTML/CSV/JSON) | 🟠 High | ✅ **Done** | Reports page with 3 tabs, drill-down, CSV export |
| **FR-UI-016** | Dark/light theme toggle | 🟢 Low | ❌ **Not Done** | |
| **FR-UI-017** | Real-time scan progress (polling/SSE) | 🟡 Medium | ❌ **Not Done** | |

## Current Angular Routes

| Path | Component | Status |
|---|---|---|
| `/login` | `LoginComponent` | ✅ Done |
| `/dashboard` | `DashboardComponent` | ✅ Done |
| `/scans` | `ScansComponent` | ✅ Done |
| `/reports` | `ReportsComponent` | ✅ Done |
| `/reports/projects/:id` | `ProjectDetailComponent` | ✅ Done |
| `/reports/vulnerabilities/:cveId` | `VulnerabilityDetailComponent` | ✅ Done |
| `/repositories` | — | ❌ Not Done |

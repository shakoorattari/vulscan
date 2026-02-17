# **Business Requirements Document (BRD) v3.0: Vulscan Agent**

## **Azure DevOps On-Premises Vulnerability Scanning Platform — SBOM-First Architecture with SQL Backend & Angular Admin Dashboard**

---

## **1. Executive Summary**

**Vulscan Agent** is a comprehensive, scheduled vulnerability scanning platform purpose-built for on-premises Azure DevOps Server environments. The platform adopts an industry-standard **SBOM-first architecture** using Syft + Grype to scan npm and C#/.NET repositories, persists all scan telemetry and vulnerability findings into a **SQL Server database**, and exposes an **Angular-based admin dashboard** for real-time reporting, trend analysis, and operational management.

:::mermaid
graph LR
    subgraph "🔍 Scan Pipeline"
        A["📡 Azure DevOps<br/>REST API"] --> B["📂 Repository<br/>Discovery & Clone"]
        B --> C["📜 SBOM Generation<br/>(Syft / Trivy)"]
        C --> D["🛡️ Vulnerability Scan<br/>(Grype)"]
    end

    subgraph "💾 Data Layer"
        D --> E["🗄️ SQL Server<br/>Database"]
    end

    subgraph "🖥️ Presentation Layer"
        E --> F["⚡ FastAPI<br/>REST API"]
        F --> G["📊 Angular Admin<br/>Dashboard"]
    end

    %% Styling
    classDef scan fill:#e3f2fd,stroke:#1565c0,stroke-width:2px,color:#0d47a1
    classDef data fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px,color:#1b5e20
    classDef ui fill:#fff3e0,stroke:#e65100,stroke-width:2px,color:#bf360c

    class A,B,C,D scan
    class E data
    class F,G ui
:::

### **Key Differentiators from v1.0 & v2.0**

| Capability | v1.0 | v2.0 | **v3.0** |
|---|---|---|---|
| Dependency Analysis | Custom parsers | SBOM (Syft/Grype) | **SBOM-first (Syft + Grype)** |
| Database Backend | SQLite | SQLite (optional SQL Server) | **SQL Server (primary)** |
| Admin Dashboard | Static HTML reports | Optional dashboard | **Angular SPA with full CRUD** |
| REST API | — | — | **FastAPI backend** |
| Authentication | Basic Auth only | PAT + Basic Auth | **PAT + Basic Auth + JWT (API)** |
| SBOM Compliance | — | CycloneDX/SPDX | **CycloneDX/SPDX + archival** |
| Notifications | Email only | Email + Teams | **Email + Teams + Dashboard alerts** |
| Scheduling | Windows Task Scheduler | Windows Task Scheduler | **APScheduler + Task Scheduler** |

---

## **2. Project Overview**

### **2.1. Problem Statement**

- No automated vulnerability detection for projects in on-premises Azure DevOps Server
- Manual dependency checking is time-consuming and error-prone
- Lack of visibility into security risks in dependent libraries
- No standardized SBOM generation for compliance/audit purposes
- No centralized reporting, historical tracking, or executive-level dashboards for vulnerabilities
- Scan results scattered across logs with no queryable data store
- No role-based access to vulnerability data for different stakeholders

### **2.2. Strategic Solution: Three-Tier Architecture**

:::mermaid
graph TD
    subgraph "🎯 Tier 1 — Scanning Agent (Python)"
        S1["📡 Azure DevOps API Client<br/>(PAT / Basic Auth)"]
        S2["📂 Repository Discovery & Cloning"]
        S3["📜 SBOM Generation (Syft CLI)"]
        S4["🛡️ Vulnerability Analysis (Grype CLI)"]
        S5["⏰ Scheduler (APScheduler + Task Scheduler)"]
        S1 --> S2 --> S3 --> S4
        S5 -.->|"triggers"| S1
    end

    subgraph "🎯 Tier 2 — API & Data Layer"
        A1["⚡ FastAPI REST API"]
        A2["🔐 JWT Authentication"]
        A3["🗄️ SQL Server via SQLAlchemy ORM"]
        A4["📤 Notification Engine<br/>(SMTP + Teams Webhook)"]
        A1 --- A2
        A1 --- A3
        A1 --- A4
    end

    subgraph "🎯 Tier 3 — Admin Dashboard (Angular)"
        U1["📊 Executive Dashboard"]
        U2["📋 Vulnerability Explorer"]
        U3["📜 SBOM Viewer"]
        U4["⚙️ Configuration Manager"]
        U5["👤 User & Role Management"]
    end

    S4 -->|"writes results"| A3
    A1 -->|"serves data"| U1
    A1 -->|"serves data"| U2
    A1 -->|"serves data"| U3
    A1 -->|"serves data"| U4
    A1 -->|"serves data"| U5

    %% Styling
    classDef agent fill:#e8eaf6,stroke:#283593,stroke-width:2px,color:#1a237e
    classDef api fill:#e0f2f1,stroke:#00695c,stroke-width:2px,color:#004d40
    classDef ui fill:#fce4ec,stroke:#c62828,stroke-width:2px,color:#b71c1c

    class S1,S2,S3,S4,S5 agent
    class A1,A2,A3,A4 api
    class U1,U2,U3,U4,U5 ui
:::

### **2.3. Objectives**

1. **Automate** SBOM generation and vulnerability scanning for npm and NuGet projects across Azure DevOps collections
2. **Persist** all scan metadata, SBOM artifacts, vulnerability findings, and agent telemetry into SQL Server
3. **Deliver** an Angular admin dashboard with executive summaries, drill-down reports, trend analysis, and configuration management
4. **Expose** a FastAPI REST API for programmatic access, dashboard data, and third-party integrations
5. **Comply** with CISA, NTIA, and Executive Order 14028 requirements for software supply chain security
6. **Support** multiple authentication methods (PAT, Basic Auth) per Azure DevOps collection
7. **Notify** stakeholders via email, Teams, and in-dashboard alerts based on configurable severity thresholds

---

## **3. Scope**

### **3.1. In Scope**

| Area | Details |
|---|---|
| **SBOM Generation** | npm (package.json, package-lock.json) and .NET (.csproj, packages.config, .sln) via **Syft** (primary) / **Trivy** (fallback) → CycloneDX JSON |
| **Vulnerability Scanning** | SBOM analysis via **Grype** against NVD, GitHub Advisory DB, **Microsoft MSRC** |
| **Azure DevOps Integration** | Collections: `https://devops.ishj.ae/SDD`, `https://devops.ishj.ae/sih` — PAT + Basic Auth |
| **SQL Server Database** | All scan results, SBOMs, vulnerability findings, agent run logs, configuration, user data |
| **FastAPI REST API** | Authenticated endpoints for dashboard, reports, SBOM retrieval, configuration, agent control |
| **Angular Admin Dashboard** | Executive dashboard, vulnerability explorer, SBOM viewer, trend charts, config management, RBAC |
| **Scheduling** | APScheduler (in-process) + Windows Task Scheduler (OS-level) |
| **Notifications** | SMTP email + Microsoft Teams webhook + in-dashboard alerts |
| **Reporting** | HTML, CSV, JSON exports from dashboard; automated email reports |

### **3.2. Out of Scope**

- Runtime/DAST scanning
- Secret detection
- Container image scanning
- Real-time scanning on commit (webhooks)
- Mobile application scanning
- Custom static code analysis (SAST)
- Cloud-hosted Azure DevOps Services (only on-premises)

---

## **4. Stakeholders**

| Role | Responsibility | Contact |
|---|---|---|
| 🛡️ Security Team | Define vulnerability thresholds, review SBOM reports, triage findings | TBD |
| ⚙️ DevOps Team | Agent deployment, authentication config, infrastructure | TBD |
| 👩‍💻 Development Teams | Remediation actions, SBOM review, dependency updates | TBD |
| 🏗️ IT Infrastructure | Windows Server management, SQL Server, network access | TBD |
| 📋 Compliance Team | SBOM retention, audit requirements, policy enforcement | TBD |
| 💼 Project Sponsor | Budget approval, strategic oversight | TBD |

---

## **5. Functional Requirements**

### **5.1. Authentication & Authorization**

#### **5.1.1. Azure DevOps Authentication**

| ID | Requirement | Priority |
|---|---|---|
| **FR-AUTH-001** | Support **Personal Access Tokens (PAT)** with read-only scopes | 🔴 Critical |
| **FR-AUTH-002** | Support **Basic Authentication** (username/password) for on-prem Azure DevOps | 🔴 Critical |
| **FR-AUTH-003** | Per-instance authentication configuration (different methods per collection) | 🟠 High |
| **FR-AUTH-004** | Secure credential storage via Windows Credential Manager (`keyring` package) | 🔴 Critical |
| **FR-AUTH-005** | Service account support for unattended scheduled execution | 🟠 High |
| **FR-AUTH-006** | Credential validation before scan execution with clear error messages | 🟡 Medium |
| **FR-AUTH-007** | PAT expiry monitoring with proactive alerts (30/60/90 day warnings) | 🟡 Medium |
| **FR-AUTH-008** | Audit logging of all authentication attempts (success/failure) to SQL Server | 🟡 Medium |

#### **5.1.2. Dashboard & API Authentication**

| ID | Requirement | Priority |
|---|---|---|
| **FR-AUTH-010** | **JWT-based authentication** for FastAPI REST API | 🔴 Critical |
| **FR-AUTH-011** | Role-Based Access Control (RBAC): `admin`, `security_analyst`, `developer`, `viewer` | 🟠 High |
| **FR-AUTH-012** | Login page with username/password stored as hashed credentials in SQL Server | 🔴 Critical |
| **FR-AUTH-013** | JWT token refresh mechanism with configurable TTL | 🟠 High |
| **FR-AUTH-014** | API key support for service-to-service integrations | 🟡 Medium |

:::mermaid
graph TD
    subgraph "🔐 Authentication Flow"
        A["📥 Login Request<br/>(username + password)"] --> B{"Validate Credentials<br/>against SQL Server"}
        B -->|"✅ Valid"| C["🔑 Generate JWT Token<br/>(access + refresh)"]
        B -->|"❌ Invalid"| D["🚫 401 Unauthorized"]
        C --> E["📤 Return JWT<br/>to Angular Client"]
        E --> F["🔒 Angular Stores Token<br/>in HttpOnly Cookie"]
        F --> G["📡 Subsequent API Calls<br/>Include Bearer Token"]
        G --> H{"🛡️ Validate JWT<br/>+ Check RBAC Role"}
        H -->|"✅ Authorized"| I["✅ Process Request"]
        H -->|"❌ Forbidden"| J["🚫 403 Forbidden"]
    end

    %% Styling
    classDef process fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px
    classDef decision fill:#fff3e0,stroke:#ef6c00,stroke-width:2px
    classDef error fill:#ffebee,stroke:#c62828,stroke-width:2px
    classDef success fill:#e0f2f1,stroke:#00695c,stroke-width:2px

    class A,C,E,F,G process
    class B,H decision
    class D,J error
    class I success
:::

---

### **5.2. Repository Discovery & Management**

| ID | Requirement | Priority |
|---|---|---|
| **FR-REPO-001** | Enumerate all projects within specified Azure DevOps collections via REST API | 🔴 Critical |
| **FR-REPO-002** | Support PAT and Basic Auth for Azure DevOps REST API v6.0+ | 🔴 Critical |
| **FR-REPO-003** | Include/exclude filter by project name, repo name, or regex patterns | 🟠 High |
| **FR-REPO-004** | Clone repositories to temporary local filesystem path | 🔴 Critical |
| **FR-REPO-005** | **Automatic cleanup** of cloned repositories immediately after scan | 🔴 Critical |
| **FR-REPO-006** | Sparse checkout for large repos (only package manifest files) | 🟡 Medium |
| **FR-REPO-007** | Git credential helper integration for seamless authenticated cloning | 🟡 Medium |
| **FR-REPO-008** | Track last scanned commit hash per repo in SQL Server to enable delta detection | 🟡 Medium |

---

### **5.3. SBOM Generation & Management**

| ID | Requirement | Priority |
|---|---|---|
| **FR-SBOM-001** | Integrate **Syft** as primary SBOM generation engine | 🔴 Critical |
| **FR-SBOM-002** | Support **Trivy** as fallback SBOM generator if Syft fails | 🟡 Medium |
| **FR-SBOM-003** | Generate SBOMs in **CycloneDX JSON** format (primary) | 🔴 Critical |
| **FR-SBOM-004** | Generate SBOMs in **SPDX JSON** format (optional for compliance) | 🟢 Low |
| **FR-SBOM-005** | Cache SBOMs in SQL Server — skip regeneration if repo commit hash unchanged | 🟡 Medium |
| **FR-SBOM-006** | Store **historical SBOMs** in SQL Server with retention policy for audit trails | 🟠 High |
| **FR-SBOM-007** | Validate generated SBOM against CycloneDX JSON schema before persisting | 🟡 Medium |
| **FR-SBOM-008** | Log SBOM generation duration, component count, and status to SQL Server | 🟠 High |

**SBOM Generation Commands:**

```bash
# Syft — Primary SBOM generator
syft dir:/path/to/cloned/repo -o cyclonedx-json > sbom.json

# Trivy — Fallback SBOM generator
trivy filesystem --format cyclonedx /path/to/cloned/repo > sbom.json
```

---

### **5.4. Vulnerability Scanning (SBOM Analysis)**

| ID | Requirement | Priority |
|---|---|---|
| **FR-SCAN-001** | Integrate **Grype** as primary SBOM vulnerability scanner | 🔴 Critical |
| **FR-SCAN-002** | Support **Trivy** as alternative SBOM scanner (fallback) | 🟡 Medium |
| **FR-SCAN-003** | Match vulnerabilities against **NVD** (National Vulnerability Database) | 🔴 Critical |
| **FR-SCAN-004** | Match vulnerabilities against **GitHub Advisory Database** | 🔴 Critical |
| **FR-SCAN-005** | Match vulnerabilities against **Microsoft MSRC** (critical for .NET) | 🟠 High |
| **FR-SCAN-006** | CVSS v2/v3.1 score calculation with severity mapping (Critical/High/Medium/Low/Negligible) | 🔴 Critical |
| **FR-SCAN-007** | Fixed version detection and remediation suggestions | 🟠 High |
| **FR-SCAN-008** | False positive suppression/whitelisting via ignore rules (persisted in SQL) | 🟡 Medium |
| **FR-SCAN-009** | **Persist all vulnerability findings to SQL Server** with foreign keys to scan/repo/SBOM | 🔴 Critical |
| **FR-SCAN-010** | Track vulnerability lifecycle: `new` → `acknowledged` → `in-progress` → `resolved` → `suppressed` | 🟠 High |

**Grype Configuration (`.grype.yaml`):**

```yaml
output: json
fail-on-severity: critical
sort-by: severity
check-for-app-update: false
db:
  auto-update: true
  cache-dir: C:\ProgramData\VulscanAgent\grype-db
ignore: []
```

**Vulnerability Scan Command:**

```bash
grype sbom:sbom.json --output json > vulnerabilities.json
```

:::mermaid
graph TD
    subgraph "🛡️ Vulnerability Scanning Pipeline"
        A["📂 Cloned Repository"] --> B["📜 Syft SBOM Generation<br/>(CycloneDX JSON)"]
        B --> C{"SBOM Valid?"}
        C -->|"✅ Yes"| D["🛡️ Grype Vulnerability Scan"]
        C -->|"❌ No"| E["⚠️ Trivy Fallback"]
        E --> D
        D --> F["📊 Parse JSON Results"]
        F --> G["🗄️ Persist to SQL Server"]
        G --> H{"Severity ≥ Threshold?"}
        H -->|"✅ Yes"| I["📧 Send Notifications<br/>(Email + Teams)"]
        H -->|"❌ No"| J["✅ Scan Complete"]
        I --> J
    end

    %% Styling
    classDef process fill:#e3f2fd,stroke:#1565c0,stroke-width:2px
    classDef decision fill:#fff3e0,stroke:#ef6c00,stroke-width:2px
    classDef alert fill:#ffebee,stroke:#c62828,stroke-width:2px
    classDef success fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px

    class A,B,D,F,G process
    class C,H decision
    class E,I alert
    class J success
:::

---

### **5.5. SQL Server Database (Central Data Store)**

| ID | Requirement | Priority |
|---|---|---|
| **FR-DB-001** | Use **SQL Server** as the primary relational database | 🔴 Critical |
| **FR-DB-002** | **SQLAlchemy ORM** for all database interactions (models, queries, migrations) | 🔴 Critical |
| **FR-DB-003** | **Alembic** for database schema migrations and version control | 🟠 High |
| **FR-DB-004** | Connection via `mssql+pyodbc` driver string with connection pooling | 🔴 Critical |
| **FR-DB-005** | Store scan run metadata (start/end time, duration, repo count, status, errors) | 🔴 Critical |
| **FR-DB-006** | Store vulnerability findings with full CVE detail, CVSS scores, fix versions | 🔴 Critical |
| **FR-DB-007** | Store SBOM artifacts (JSON blob or file reference) with retention policy | 🟠 High |
| **FR-DB-008** | Store repository metadata (name, project, collection, last scanned commit) | 🔴 Critical |
| **FR-DB-009** | Store agent configuration (editable from dashboard) | 🟠 High |
| **FR-DB-010** | Store notification history (sent/failed, recipients, timestamp) | 🟡 Medium |
| **FR-DB-011** | Store user accounts, roles, and audit logs | 🔴 Critical |
| **FR-DB-012** | Data retention policies: scan data 1 year, SBOMs 90 days (configurable) | 🟡 Medium |
| **FR-DB-013** | Database backup strategy documentation and stored procedure | 🟡 Medium |

#### **5.5.1. Database Schema (Entity-Relationship Overview)**

:::mermaid
erDiagram
    USERS {
        int id PK
        string username UK
        string email
        string password_hash
        string role
        datetime created_at
        datetime last_login
        boolean is_active
    }

    AZURE_DEVOPS_INSTANCES {
        int id PK
        string name UK
        string url
        string auth_method
        string collection
        string credential_ref
        boolean enabled
        datetime created_at
    }

    PROJECTS {
        int id PK
        int instance_id FK
        string name
        string azure_project_id
        datetime discovered_at
    }

    REPOSITORIES {
        int id PK
        int project_id FK
        string name
        string clone_url
        string default_branch
        string last_scanned_commit
        datetime last_scanned_at
        boolean enabled
    }

    SCAN_RUNS {
        int id PK
        int instance_id FK
        datetime started_at
        datetime completed_at
        int duration_seconds
        string status
        int repos_scanned
        int repos_failed
        int total_vulnerabilities
        int critical_count
        int high_count
        int medium_count
        int low_count
        string triggered_by
        text error_log
    }

    SBOMS {
        int id PK
        int repository_id FK
        int scan_run_id FK
        string format
        string generator
        int component_count
        text sbom_json
        string commit_hash
        datetime generated_at
        int generation_duration_ms
    }

    VULNERABILITIES {
        int id PK
        int sbom_id FK
        int scan_run_id FK
        int repository_id FK
        string cve_id
        string package_name
        string installed_version
        string fixed_version
        string severity
        float cvss_score
        string cvss_vector
        text description
        string source_db
        string status
        datetime first_detected_at
        datetime resolved_at
        int age_days
    }

    SUPPRESSED_VULNERABILITIES {
        int id PK
        string cve_id
        string package_name
        string reason
        int suppressed_by FK
        datetime suppressed_at
        datetime expires_at
    }

    NOTIFICATION_LOG {
        int id PK
        int scan_run_id FK
        string channel
        string recipients
        string status
        text message_summary
        datetime sent_at
    }

    AUDIT_LOG {
        int id PK
        int user_id FK
        string action
        string entity_type
        int entity_id
        text details
        string ip_address
        datetime timestamp
    }

    AZURE_DEVOPS_INSTANCES ||--o{ PROJECTS : "contains"
    PROJECTS ||--o{ REPOSITORIES : "contains"
    AZURE_DEVOPS_INSTANCES ||--o{ SCAN_RUNS : "scanned by"
    SCAN_RUNS ||--o{ SBOMS : "produces"
    SCAN_RUNS ||--o{ VULNERABILITIES : "detects"
    REPOSITORIES ||--o{ SBOMS : "has"
    REPOSITORIES ||--o{ VULNERABILITIES : "affected"
    SBOMS ||--o{ VULNERABILITIES : "contains"
    SCAN_RUNS ||--o{ NOTIFICATION_LOG : "triggers"
    USERS ||--o{ AUDIT_LOG : "performs"
    USERS ||--o{ SUPPRESSED_VULNERABILITIES : "suppresses"
:::

#### **5.5.2. SQLAlchemy Connection Configuration**

```python
from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker

# SQL Server via pyodbc
DATABASE_URL = (
    "mssql+pyodbc://sa:YourPassword@localhost:1433/VulscanDB"
    "?driver=ODBC+Driver+18+for+SQL+Server"
    "&TrustServerCertificate=yes"
)

engine = create_engine(
    DATABASE_URL,
    pool_size=10,
    max_overflow=20,
    pool_recycle=3600,
    echo=False,
)

SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
```

---

### **5.6. FastAPI REST API (Backend for Dashboard)**

| ID | Requirement | Priority |
|---|---|---|
| **FR-API-001** | FastAPI application with automatic OpenAPI/Swagger documentation | 🔴 Critical |
| **FR-API-002** | JWT authentication middleware with role-based endpoint protection | 🔴 Critical |
| **FR-API-003** | CORS configuration for Angular dashboard origin | 🔴 Critical |
| **FR-API-004** | **Dashboard endpoints**: summary stats, severity breakdown, trend data | 🔴 Critical |
| **FR-API-005** | **Vulnerability endpoints**: list, filter, search, detail, status update, suppress | 🔴 Critical |
| **FR-API-006** | **SBOM endpoints**: list, download, compare across scans | 🟠 High |
| **FR-API-007** | **Scan endpoints**: trigger manual scan, view history, view run detail | 🟠 High |
| **FR-API-008** | **Configuration endpoints**: view/update agent config (admin only) | 🟠 High |
| **FR-API-009** | **User management endpoints**: CRUD users, assign roles (admin only) | 🟠 High |
| **FR-API-010** | **Report endpoints**: generate/download HTML, CSV, JSON reports | 🟠 High |
| **FR-API-011** | **Notification endpoints**: view log, test notification channel | 🟡 Medium |
| **FR-API-012** | Pagination, sorting, and filtering on all list endpoints | 🔴 Critical |
| **FR-API-013** | Background task support (FastAPI `BackgroundTasks`) for async scan triggering | 🟠 High |
| **FR-API-014** | Health check endpoint (`/health`) for monitoring | 🟠 High |
| **FR-API-015** | Rate limiting on public endpoints | 🟡 Medium |

#### **5.6.1. API Endpoint Structure**

```
/api/v1/
├── auth/
│   ├── POST   /login                    → JWT token
│   ├── POST   /refresh                  → Refresh JWT
│   └── POST   /logout                   → Invalidate token
├── dashboard/
│   ├── GET    /summary                  → Severity counts, totals
│   ├── GET    /trends                   → Time-series vulnerability data
│   └── GET    /top-vulnerable-repos     → Ranked list
├── vulnerabilities/
│   ├── GET    /                         → Paginated list (filters: severity, repo, status, CVE)
│   ├── GET    /{id}                     → Detail with SBOM context
│   ├── PATCH  /{id}/status              → Update status (acknowledge, resolve, suppress)
│   └── GET    /export                   → CSV/JSON download
├── scans/
│   ├── GET    /                         → Scan run history
│   ├── GET    /{id}                     → Scan run detail
│   ├── POST   /trigger                  → Trigger manual scan (admin)
│   └── GET    /{id}/logs                → Agent logs for run
├── sboms/
│   ├── GET    /                         → Paginated list
│   ├── GET    /{id}                     → SBOM detail + components
│   ├── GET    /{id}/download            → Raw CycloneDX JSON
│   └── GET    /compare                  → Diff two SBOMs
├── repositories/
│   ├── GET    /                         → All discovered repos
│   ├── PATCH  /{id}                     → Enable/disable scanning
│   └── GET    /{id}/history             → Scan history for repo
├── config/
│   ├── GET    /                         → Current agent config
│   └── PUT    /                         → Update config (admin)
├── users/
│   ├── GET    /                         → List users (admin)
│   ├── POST   /                         → Create user (admin)
│   ├── PATCH  /{id}                     → Update user (admin)
│   └── DELETE /{id}                     → Deactivate user (admin)
├── notifications/
│   ├── GET    /log                      → Notification history
│   └── POST   /test                     → Test notification channel
└── health                               → Service health check
```

---

### **5.7. Angular Admin Dashboard**

| ID | Requirement | Priority |
|---|---|---|
| **FR-UI-001** | **Angular 18+** Single Page Application with standalone components | 🔴 Critical |
| **FR-UI-002** | **Angular Material** component library for consistent UI | 🔴 Critical |
| **FR-UI-003** | Responsive layout supporting desktop and tablet viewports | 🟠 High |
| **FR-UI-004** | JWT-based login page with token storage and `HttpInterceptor` for Bearer header | 🔴 Critical |
| **FR-UI-005** | Route guards (`canActivate`) for role-based page access | 🔴 Critical |
| **FR-UI-006** | **Executive Dashboard** page with KPI cards, severity pie chart, trend line chart | 🔴 Critical |
| **FR-UI-007** | **Vulnerability Explorer** page with server-side paginated table, filters, search | 🔴 Critical |
| **FR-UI-008** | **Vulnerability Detail** view with CVE info, affected repos, SBOM context, status actions | 🟠 High |
| **FR-UI-009** | **Scan History** page with run list, status badges, drill-down to findings | 🟠 High |
| **FR-UI-010** | **SBOM Browser** page listing SBOMs with component count, download action | 🟡 Medium |
| **FR-UI-011** | **Repository Management** page to enable/disable repos, view last scan date | 🟠 High |
| **FR-UI-012** | **Configuration Manager** page to edit agent settings (admin only) | 🟡 Medium |
| **FR-UI-013** | **User Management** page with CRUD (admin only) | 🟡 Medium |
| **FR-UI-014** | **Notification Log** page showing sent/failed notifications | 🟢 Low |
| **FR-UI-015** | **Report Generator** page to export HTML/CSV/JSON with date range and filters | 🟠 High |
| **FR-UI-016** | Dark/light theme toggle | 🟢 Low |
| **FR-UI-017** | Real-time scan progress indicator (polling or SSE) | 🟡 Medium |

#### **5.7.1. Dashboard Wireframe Layout**

:::mermaid
graph TD
    subgraph "🖥️ Angular Admin Dashboard"
        NAV["📌 Side Navigation<br/>• Dashboard<br/>• Vulnerabilities<br/>• Scan History<br/>• SBOMs<br/>• Repositories<br/>• Config<br/>• Users<br/>• Reports"]

        subgraph "📊 Executive Dashboard Page"
            KPI1["🔴 Critical<br/>12"]
            KPI2["🟠 High<br/>45"]
            KPI3["🟡 Medium<br/>123"]
            KPI4["🟢 Low<br/>67"]
            CHART1["📈 Trend Line Chart<br/>(30/60/90 days)"]
            CHART2["🍩 Severity Breakdown<br/>(Pie Chart)"]
            TABLE1["📋 Top 10 Vulnerable<br/>Repositories"]
        end

        subgraph "🔍 Vulnerability Explorer Page"
            FILTERS["🔎 Filters: Severity | Status | Repo | CVE | Date Range"]
            VTABLE["📊 Paginated Data Table<br/>CVE | Package | Version | Severity | Status | Repo | Age"]
            ACTIONS["⚡ Actions: Acknowledge | Suppress | Export"]
        end
    end

    NAV --> KPI1
    NAV --> FILTERS

    %% Styling
    classDef nav fill:#263238,stroke:#37474f,color:#eceff1,stroke-width:2px
    classDef kpi fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px
    classDef chart fill:#e3f2fd,stroke:#1565c0,stroke-width:2px
    classDef table fill:#fff3e0,stroke:#ef6c00,stroke-width:2px
    classDef filter fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px

    class NAV nav
    class KPI1,KPI2,KPI3,KPI4 kpi
    class CHART1,CHART2 chart
    class TABLE1,VTABLE table
    class FILTERS,ACTIONS filter
:::

#### **5.7.2. Angular Technology Stack**

| Technology | Purpose |
|---|---|
| **Angular 18+** | SPA framework with standalone components, signals |
| **Angular Material** | UI component library (tables, cards, dialogs, forms) |
| **Angular Router** | Client-side routing with lazy loading |
| **Angular HttpClient** | API communication with interceptors |
| **RxJS** | Reactive data streams, polling |
| **Chart.js / ng2-charts** | Trend line charts, pie charts, bar charts |
| **Angular CDK** | Virtual scrolling, drag-drop, accessibility |
| **Tailwind CSS** (optional) | Utility-first styling alongside Angular Material |

---

### **5.8. Scheduling & Automation**

| ID | Requirement | Priority |
|---|---|---|
| **FR-SCHED-001** | **APScheduler** integration for in-process scheduling within the Python agent | 🟠 High |
| **FR-SCHED-002** | **Windows Task Scheduler** as OS-level trigger for reliability | 🟠 High |
| **FR-SCHED-003** | Configurable scan frequency: hourly, daily, weekly, cron expression | 🔴 Critical |
| **FR-SCHED-004** | Different schedules per Azure DevOps collection | 🟡 Medium |
| **FR-SCHED-005** | Skip scan if no new commits detected (delta detection via commit hash) | 🟡 Medium |
| **FR-SCHED-006** | Retry mechanism with exponential backoff (max 3 attempts) | 🟡 Medium |
| **FR-SCHED-007** | Scan locking to prevent concurrent runs on same collection | 🟠 High |
| **FR-SCHED-008** | Log all schedule triggers and outcomes to SQL Server | 🟠 High |

---

### **5.9. Reporting & Notification**

| ID | Requirement | Priority |
|---|---|---|
| **FR-REP-001** | Generate **HTML executive summary** reports with severity breakdown charts | 🟠 High |
| **FR-REP-002** | Generate **CSV/JSON detailed reports** for tooling integration | 🟠 High |
| **FR-REP-003** | Attach SBOMs to reports for audit compliance | 🟡 Medium |
| **FR-REP-004** | **SMTP email notifications** with TLS, authentication, configurable recipients | 🔴 Critical |
| **FR-REP-005** | **Microsoft Teams webhook** notifications with adaptive card formatting | 🟠 High |
| **FR-REP-006** | **In-dashboard alerts** visible on login (unacknowledged critical/high vulns) | 🟠 High |
| **FR-REP-007** | Vulnerability age tracking (days since first detection) | 🟡 Medium |
| **FR-REP-008** | SLA breach alerts (e.g., critical vuln open > 7 days, high > 30 days) | 🟡 Medium |
| **FR-REP-009** | Notification history stored in SQL Server with delivery status | 🟡 Medium |
| **FR-REP-010** | Report generation from Angular dashboard with date range and filter selection | 🟠 High |

---

## **6. Non-Functional Requirements**

### **6.1. Performance**

| ID | Requirement | Target |
|---|---|---|
| **NFR-PERF-001** | SBOM generation time per repository | < 30 seconds |
| **NFR-PERF-002** | Vulnerability scan time per SBOM | < 20 seconds |
| **NFR-PERF-003** | Total scan time for 100 repositories | < 4 hours |
| **NFR-PERF-004** | Concurrent repository processing | Configurable (default: 5) |
| **NFR-PERF-005** | Agent memory usage during peak scan | < 2 GB |
| **NFR-PERF-006** | API response time (dashboard endpoints) | < 500 ms (p95) |
| **NFR-PERF-007** | Angular dashboard initial load time | < 3 seconds |
| **NFR-PERF-008** | SQL Server query response for paginated lists | < 200 ms |

### **6.2. Security**

| ID | Requirement | Priority |
|---|---|---|
| **NFR-SEC-001** | **No persistence of source code** after scan — clones deleted immediately | 🔴 Critical |
| **NFR-SEC-002** | Encrypted credential storage (DPAPI via `keyring` / AES-256) | 🔴 Critical |
| **NFR-SEC-003** | Credentials **never** logged in plaintext — masked in all log output | 🔴 Critical |
| **NFR-SEC-004** | Read-only access to Azure DevOps (minimal PAT scopes) | 🟠 High |
| **NFR-SEC-005** | HTTPS for all API endpoints (TLS 1.2+) | 🔴 Critical |
| **NFR-SEC-006** | SQL injection prevention via SQLAlchemy parameterized queries | 🔴 Critical |
| **NFR-SEC-007** | Angular XSS protection via built-in sanitization | 🔴 Critical |
| **NFR-SEC-008** | CORS restricted to dashboard origin only | 🟠 High |
| **NFR-SEC-009** | Password hashing with bcrypt (cost factor 12+) | 🔴 Critical |
| **NFR-SEC-010** | PAT rotation reminders at 30/60/90 day thresholds | 🟡 Medium |

### **6.3. Reliability**

| ID | Requirement | Target |
|---|---|---|
| **NFR-REL-001** | Agent scheduling service uptime | > 99% |
| **NFR-REL-002** | FastAPI service uptime | > 99.5% |
| **NFR-REL-003** | Failed scan auto-retry | 3 attempts with exponential backoff |
| **NFR-REL-004** | Grype vulnerability DB auto-update | Daily |
| **NFR-REL-005** | Graceful degradation if external APIs unavailable | Scan with cached data |
| **NFR-REL-006** | SQL Server connection resilience | Auto-reconnect with pool recycling |

### **6.4. Maintainability**

| ID | Requirement | Priority |
|---|---|---|
| **NFR-MNT-001** | Configuration-driven architecture — no hardcoded values | 🔴 Critical |
| **NFR-MNT-002** | Modular Python package structure for agent components | 🟠 High |
| **NFR-MNT-003** | Angular lazy-loaded feature modules for dashboard | 🟠 High |
| **NFR-MNT-004** | Comprehensive structured logging (JSON format + Windows Event Log) | 🟠 High |
| **NFR-MNT-005** | Alembic migrations for all schema changes | 🟠 High |
| **NFR-MNT-006** | Docker Compose available for local development environment | 🟡 Medium |
| **NFR-MNT-007** | Self-update capability for Syft/Grype binaries | 🟡 Medium |
| **NFR-MNT-008** | Unit + integration test coverage > 70% | 🟠 High |

---

## **7. Technical Architecture**

### **7.1. High-Level System Architecture**

:::mermaid
graph TB
    subgraph "🖥️ Windows Server — Vulscan Platform"
        subgraph "🔍 Scanning Agent (Python)"
            SCHED["⏰ APScheduler"]
            DISC["📡 Discovery Engine"]
            CLONE["📂 Git Clone Manager"]
            SYFT["📜 Syft (SBOM)"]
            GRYPE["🛡️ Grype (Vuln Scan)"]
            PERSIST["💾 Result Persister"]

            SCHED --> DISC --> CLONE --> SYFT --> GRYPE --> PERSIST
        end

        subgraph "⚡ FastAPI Backend"
            API["🌐 REST API (FastAPI)"]
            JWT["🔐 JWT Auth Middleware"]
            BG["⚙️ Background Tasks"]

            API --- JWT
            API --- BG
        end

        subgraph "💾 Data Layer"
            SQL["🗄️ SQL Server"]
            ORM["📦 SQLAlchemy ORM"]
            MIG["🔄 Alembic Migrations"]

            ORM --> SQL
            MIG --> SQL
        end

        PERSIST --> ORM
        API --> ORM
        BG -.->|"trigger scan"| SCHED
    end

    subgraph "🌐 External Services"
        DEVOPS1["📡 Azure DevOps<br/>SDD (PAT)"]
        DEVOPS2["📡 Azure DevOps<br/>SIH (Basic Auth)"]
        NVD["🌍 NVD API"]
        GH["🐙 GitHub Advisory DB"]
        MSRC["🔷 Microsoft MSRC"]
        SMTP["📧 SMTP Server"]
        TEAMS["💬 Teams Webhook"]
    end

    subgraph "🖥️ Client"
        ANG["📊 Angular Admin Dashboard"]
    end

    DISC --> DEVOPS1
    DISC --> DEVOPS2
    GRYPE -.-> NVD
    GRYPE -.-> GH
    GRYPE -.-> MSRC
    PERSIST --> SMTP
    PERSIST --> TEAMS
    ANG -->|"HTTPS"| API

    %% Styling
    classDef agent fill:#e8eaf6,stroke:#283593,stroke-width:2px,color:#1a237e
    classDef api fill:#e0f2f1,stroke:#00695c,stroke-width:2px,color:#004d40
    classDef data fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px,color:#1b5e20
    classDef ext fill:#fff3e0,stroke:#e65100,stroke-width:2px,color:#bf360c
    classDef client fill:#fce4ec,stroke:#c62828,stroke-width:2px,color:#b71c1c

    class SCHED,DISC,CLONE,SYFT,GRYPE,PERSIST agent
    class API,JWT,BG api
    class SQL,ORM,MIG data
    class DEVOPS1,DEVOPS2,NVD,GH,MSRC,SMTP,TEAMS ext
    class ANG client
:::

### **7.2. Technology Stack**

| Layer | Technology | Version | Purpose |
|---|---|---|---|
| **Agent Core** | Python | 3.11+ | Scanning orchestration, scheduling |
| **SBOM Generation** | Syft (CLI) | 1.x+ | CycloneDX SBOM from source repos |
| **Vulnerability Scanning** | Grype (CLI) | 0.80+ | SBOM analysis against vuln databases |
| **Fallback Scanner** | Trivy (CLI) | 0.50+ | Backup SBOM generation + scanning |
| **REST API** | FastAPI | 0.110+ | Backend API with OpenAPI docs |
| **ORM** | SQLAlchemy | 2.0+ | Database models, queries, connection pooling |
| **Migrations** | Alembic | 1.13+ | Schema version control |
| **Database** | SQL Server | 2019+ | Central data store |
| **DB Driver** | pyodbc | 5.x | SQL Server ODBC connectivity |
| **Auth (API)** | python-jose + passlib | latest | JWT tokens + bcrypt password hashing |
| **Azure DevOps** | azure-devops | 7.1 | REST API for repo discovery |
| **Credential Store** | keyring | 25.x | Windows Credential Manager integration |
| **Scheduling** | APScheduler | 3.10+ | In-process cron scheduling |
| **Email** | smtplib (stdlib) | — | SMTP notifications |
| **Teams** | pymsteams | 0.2+ | Teams webhook integration |
| **Reporting** | Jinja2 + Pandas | latest | HTML/CSV/JSON report generation |
| **Logging** | structlog | 24.x | Structured JSON logging |
| **Frontend** | Angular | 18+ | Admin dashboard SPA |
| **UI Kit** | Angular Material | 18+ | Material Design components |
| **Charts** | Chart.js + ng2-charts | 4.x / 6.x | Data visualization |
| **State (optional)** | NgRx or Angular Signals | — | Client-side state management |

### **7.3. Project Repository Structure**

```
vulscan/
├── docs/
│   ├── brd-v01.md
│   ├── brd-v02.md
│   └── brd-v03.md
├── agent/                          # Python scanning agent
│   ├── vulscan/
│   │   ├── __init__.py
│   │   ├── main.py                 # Entry point
│   │   ├── config.py               # Configuration loader
│   │   ├── scheduler.py            # APScheduler setup
│   │   ├── discovery/
│   │   │   ├── __init__.py
│   │   │   ├── devops_client.py    # Azure DevOps API client
│   │   │   └── repo_manager.py     # Clone/cleanup logic
│   │   ├── scanning/
│   │   │   ├── __init__.py
│   │   │   ├── sbom_generator.py   # Syft/Trivy wrapper
│   │   │   └── vuln_scanner.py     # Grype wrapper
│   │   ├── persistence/
│   │   │   ├── __init__.py
│   │   │   ├── database.py         # SQLAlchemy engine/session
│   │   │   ├── models.py           # ORM models
│   │   │   └── repository.py       # Data access layer
│   │   ├── notifications/
│   │   │   ├── __init__.py
│   │   │   ├── email_notifier.py   # SMTP logic
│   │   │   └── teams_notifier.py   # Teams webhook logic
│   │   └── reporting/
│   │       ├── __init__.py
│   │       ├── html_report.py      # Jinja2 HTML reports
│   │       └── export.py           # CSV/JSON export
│   ├── alembic/                    # Database migrations
│   │   ├── env.py
│   │   └── versions/
│   ├── alembic.ini
│   ├── config.yaml                 # Agent configuration
│   ├── requirements.txt
│   └── pyproject.toml
├── api/                            # FastAPI backend
│   ├── app/
│   │   ├── __init__.py
│   │   ├── main.py                 # FastAPI app factory
│   │   ├── config.py               # API settings
│   │   ├── dependencies.py         # Dependency injection
│   │   ├── auth/
│   │   │   ├── __init__.py
│   │   │   ├── jwt.py              # JWT creation/validation
│   │   │   ├── models.py           # Auth request/response models
│   │   │   └── router.py           # /auth endpoints
│   │   ├── routers/
│   │   │   ├── dashboard.py
│   │   │   ├── vulnerabilities.py
│   │   │   ├── scans.py
│   │   │   ├── sboms.py
│   │   │   ├── repositories.py
│   │   │   ├── config.py
│   │   │   ├── users.py
│   │   │   └── notifications.py
│   │   └── schemas/                # Pydantic models
│   │       ├── vulnerability.py
│   │       ├── scan.py
│   │       ├── sbom.py
│   │       └── user.py
│   ├── requirements.txt
│   └── pyproject.toml
├── dashboard/                      # Angular admin dashboard
│   ├── src/
│   │   ├── app/
│   │   │   ├── app.component.ts
│   │   │   ├── app.routes.ts
│   │   │   ├── core/
│   │   │   │   ├── auth/
│   │   │   │   │   ├── auth.service.ts
│   │   │   │   │   ├── auth.guard.ts
│   │   │   │   │   └── auth.interceptor.ts
│   │   │   │   └── services/
│   │   │   │       ├── api.service.ts
│   │   │   │       └── notification.service.ts
│   │   │   ├── features/
│   │   │   │   ├── dashboard/
│   │   │   │   ├── vulnerabilities/
│   │   │   │   ├── scans/
│   │   │   │   ├── sboms/
│   │   │   │   ├── repositories/
│   │   │   │   ├── config/
│   │   │   │   ├── users/
│   │   │   │   └── reports/
│   │   │   └── shared/
│   │   │       ├── components/
│   │   │       ├── models/
│   │   │       └── pipes/
│   │   ├── environments/
│   │   └── styles.scss
│   ├── angular.json
│   ├── package.json
│   └── tsconfig.json
└── docker-compose.yml              # Local development environment
```

### **7.4. Infrastructure Requirements**

| Resource | Specification | Purpose |
|---|---|---|
| **Windows Server** | 2019/2022 | Host scanning agent, API, and scheduler |
| **SQL Server** | 2019+ (Standard or Express) | Primary data store |
| **CPU** | 4 cores minimum, 8 recommended | Concurrent scanning |
| **RAM** | 8 GB minimum, 16 GB recommended | Agent + API + SQL Server |
| **Storage** | 100 GB free (50 GB temp clone + 50 GB database) | Repository cloning + data |
| **Git** | 2.40+ | Repository cloning |
| **Node.js** | 20 LTS | npm analysis + Angular build |
| **.NET SDK** | 8.0+ | .NET project analysis |
| **Python** | 3.11+ | Agent + API runtime |
| **ODBC Driver** | ODBC Driver 18 for SQL Server | Database connectivity |
| **Network** | Access to Azure DevOps, NVD, GitHub APIs | Scanning + vulnerability feeds |

---

## **8. Implementation Phases**

### **Phase 1: Foundation & Infrastructure (Weeks 1-3)**

:::mermaid
gantt
    title Vulscan Agent — Implementation Timeline
    dateFormat  YYYY-MM-DD
    axisFormat  %b %d

    section Phase 1 — Foundation
    SQL Server schema + Alembic setup      :p1a, 2026-02-16, 5d
    SQLAlchemy ORM models                  :p1b, after p1a, 3d
    Azure DevOps API client (PAT + Basic)  :p1c, 2026-02-16, 5d
    Credential management (keyring)        :p1d, after p1c, 2d
    Repository discovery + cloning         :p1e, after p1d, 3d
    Configuration framework (YAML)         :p1f, after p1e, 2d

    section Phase 2 — SBOM Core
    Syft integration + SBOM generation     :p2a, 2026-03-09, 5d
    SBOM validation + SQL persistence      :p2b, after p2a, 3d
    Grype integration + vuln scanning      :p2c, after p2b, 5d
    Vulnerability parsing + SQL persist    :p2d, after p2c, 3d

    section Phase 3 — API Layer
    FastAPI project scaffold               :p3a, 2026-04-06, 3d
    JWT authentication + RBAC              :p3b, after p3a, 4d
    Dashboard + vulnerability endpoints    :p3c, after p3b, 5d
    Scan, SBOM, config endpoints           :p3d, after p3c, 4d

    section Phase 4 — Angular Dashboard
    Angular project scaffold + Material    :p4a, 2026-05-04, 3d
    Auth module (login, guards, interceptor) :p4b, after p4a, 4d
    Executive dashboard + charts           :p4c, after p4b, 5d
    Vulnerability explorer + detail        :p4d, after p4c, 5d
    Scan history, SBOM browser, config     :p4e, after p4d, 5d

    section Phase 5 — Notifications & Hardening
    Email (SMTP) notifications             :p5a, 2026-06-08, 3d
    Teams webhook notifications            :p5b, after p5a, 2d
    Scheduling (APScheduler + Task Sched)  :p5c, after p5b, 3d
    Report generation (HTML/CSV/JSON)      :p5d, after p5c, 3d
    Performance tuning + load testing      :p5e, after p5d, 3d
    Security hardening + penetration test  :p5f, after p5e, 3d

    section Phase 6 — UAT & Deployment
    User acceptance testing                :p6a, 2026-07-06, 5d
    Production deployment + monitoring     :p6b, after p6a, 5d
    Documentation + runbooks               :p6c, after p6b, 3d
:::

| Phase | Duration | Key Deliverables |
|---|---|---|
| **Phase 1** — Foundation & Infrastructure | Weeks 1-3 | SQL Server schema, ORM models, Azure DevOps API client, credential management, repo discovery |
| **Phase 2** — SBOM & Scanning Core | Weeks 4-6 | Syft integration, SBOM generation/validation, Grype scanning, vulnerability persistence |
| **Phase 3** — FastAPI Backend | Weeks 7-9 | REST API scaffold, JWT auth, RBAC, all CRUD endpoints, pagination |
| **Phase 4** — Angular Dashboard | Weeks 10-14 | Login, executive dashboard, vulnerability explorer, scan history, SBOM browser, config manager |
| **Phase 5** — Notifications & Hardening | Weeks 15-18 | Email/Teams notifications, scheduling, report generation, performance tuning, security hardening |
| **Phase 6** — UAT & Deployment | Weeks 19-21 | User acceptance testing, production deployment, monitoring, documentation |

---

## **9. Configuration Management**

### **9.1. Agent Configuration (`config.yaml`)**

```yaml
agent:
  name: "VulscanAgent"
  version: "3.0.0"
  temp_clone_dir: "C:\\ProgramData\\VulscanAgent\\temp"

azure_devops:
  instances:
    - name: "SDD"
      url: "https://devops.ishj.ae/SDD"
      auth_method: "pat"  # "pat" | "basic"
      credential_ref: "vulscan/sdd-pat"  # keyring reference
      collection: "DefaultCollection"
      scan_schedule: "0 2 * * *"  # daily at 2 AM
      enabled: true
      exclude_projects: []
      exclude_repos: []

    - name: "SIH"
      url: "https://devops.ishj.ae/sih"
      auth_method: "basic"
      username: "svc_vulscan"
      credential_ref: "vulscan/sih-password"
      domain: "ISHJ"
      collection: "DefaultCollection"
      scan_schedule: "0 3 * * *"  # daily at 3 AM
      enabled: true

sbom:
  generator: "syft"           # "syft" | "trivy"
  format: "cyclonedx-json"
  fallback_generator: "trivy"
  cache_enabled: true
  cache_ttl_days: 7

scanning:
  engine: "grype"
  fail_on_severity: "critical"
  max_concurrent_scans: 5
  clone_timeout_seconds: 300
  scan_timeout_seconds: 600
  retry_attempts: 3
  retry_backoff_seconds: 30

database:
  url: "mssql+pyodbc://sa:${DB_PASSWORD}@localhost:1433/VulscanDB?driver=ODBC+Driver+18+for+SQL+Server&TrustServerCertificate=yes"
  pool_size: 10
  max_overflow: 20
  pool_recycle: 3600

api:
  host: "0.0.0.0"
  port: 8000
  cors_origins:
    - "http://localhost:4200"
    - "https://vulscan.ishj.ae"
  jwt_secret_ref: "vulscan/jwt-secret"
  jwt_algorithm: "HS256"
  jwt_access_token_expire_minutes: 60
  jwt_refresh_token_expire_days: 7

notifications:
  email:
    enabled: true
    smtp_server: "smtp.ishj.ae"
    smtp_port: 587
    use_tls: true
    from_address: "vulscan@ishj.ae"
    username: "vulscan@ishj.ae"
    credential_ref: "vulscan/smtp-password"
    recipients:
      - "security@ishj.ae"
      - "devops@ishj.ae"
    severity_threshold: "high"  # Only notify for high + critical

  teams:
    enabled: true
    webhook_ref: "vulscan/teams-webhook"
    severity_threshold: "critical"

  sla:
    critical_max_age_days: 7
    high_max_age_days: 30
    medium_max_age_days: 90

logging:
  level: "INFO"
  format: "json"
  file: "C:\\ProgramData\\VulscanAgent\\logs\\vulscan.log"
  max_size_mb: 100
  backup_count: 10
  event_log_source: "VulscanAgent"

retention:
  scan_data_days: 365
  sbom_data_days: 90
  notification_log_days: 180
  audit_log_days: 365
```

### **9.2. Credential Management Strategy**

:::mermaid
graph TD
    subgraph "🔐 Credential Flow"
        A["📝 config.yaml<br/>references credential_ref keys"] --> B["🔑 Windows Credential Manager<br/>(via keyring Python package)"]
        B --> C{"Runtime Resolution"}
        C --> D["📡 Azure DevOps Auth<br/>(PAT or Basic)"]
        C --> E["📧 SMTP Auth"]
        C --> F["💬 Teams Webhook URL"]
        C --> G["🔐 JWT Signing Secret"]
        C --> H["🗄️ SQL Server Password"]
    end

    subgraph "⚠️ Security Rules"
        R1["🚫 Never log credentials"]
        R2["🚫 Never store in config file"]
        R3["✅ Use DPAPI encryption at rest"]
        R4["✅ Rotate PATs every 90 days"]
    end

    %% Styling
    classDef config fill:#e3f2fd,stroke:#1565c0,stroke-width:2px
    classDef cred fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px
    classDef rule fill:#fff3e0,stroke:#ef6c00,stroke-width:2px

    class A config
    class B,C,D,E,F,G,H cred
    class R1,R2,R3,R4 rule
:::

```powershell
# Store credentials in Windows Credential Manager
# PAT for Azure DevOps SDD
cmdkey /add:vulscan/sdd-pat /user:vulscan /pass:YOUR_PAT_TOKEN

# Python keyring (preferred)
python -c "import keyring; keyring.set_password('vulscan', 'sdd-pat', 'YOUR_PAT_TOKEN')"
python -c "import keyring; keyring.set_password('vulscan', 'sih-password', 'YOUR_PASSWORD')"
python -c "import keyring; keyring.set_password('vulscan', 'jwt-secret', 'YOUR_JWT_SECRET')"
python -c "import keyring; keyring.set_password('vulscan', 'smtp-password', 'YOUR_SMTP_PASSWORD')"
```

---

## **10. Security Considerations**

### **10.1. Defense-in-Depth Model**

| Layer | Control | Implementation |
|---|---|---|
| **Network** | Firewall rules, network segmentation | Scanning server isolated; only outbound to DevOps + vulnerability DBs |
| **Authentication** | Multi-method auth, JWT tokens | PAT/Basic for DevOps, JWT for API, bcrypt for passwords |
| **Authorization** | RBAC, least privilege | Role-based API access, read-only PAT scopes |
| **Data at Rest** | Encrypted credentials, TDE (optional) | Windows Credential Manager (DPAPI), SQL Server TDE |
| **Data in Transit** | TLS 1.2+ everywhere | HTTPS for API, TLS for SMTP, HTTPS for Azure DevOps |
| **Application** | Input validation, parameterized queries | Pydantic validation, SQLAlchemy ORM, Angular sanitization |
| **Logging & Audit** | Comprehensive audit trail | All actions logged to SQL Server, never include secrets |
| **Cleanup** | Ephemeral source code | Cloned repos deleted immediately after SBOM generation |

### **10.2. RBAC Permission Matrix**

| Resource | `admin` | `security_analyst` | `developer` | `viewer` |
|---|---|---|---|---|
| Dashboard | ✅ Full | ✅ Full | ✅ Full | ✅ Full |
| Vulnerabilities — View | ✅ | ✅ | ✅ | ✅ |
| Vulnerabilities — Update Status | ✅ | ✅ | ❌ | ❌ |
| Vulnerabilities — Suppress | ✅ | ✅ | ❌ | ❌ |
| Scans — View History | ✅ | ✅ | ✅ | ✅ |
| Scans — Trigger Manual | ✅ | ❌ | ❌ | ❌ |
| SBOMs — View/Download | ✅ | ✅ | ✅ | ✅ |
| Repositories — View | ✅ | ✅ | ✅ | ✅ |
| Repositories — Enable/Disable | ✅ | ❌ | ❌ | ❌ |
| Configuration — View | ✅ | ✅ | ❌ | ❌ |
| Configuration — Edit | ✅ | ❌ | ❌ | ❌ |
| Users — Manage | ✅ | ❌ | ❌ | ❌ |
| Reports — Generate/Export | ✅ | ✅ | ✅ | ❌ |
| Notifications — View Log | ✅ | ✅ | ❌ | ❌ |

---

## **11. Success Metrics**

| Metric | Target | Measurement Method |
|---|---|---|
| Repository Scan Coverage | 100% of targeted repos | SQL query vs. Azure DevOps inventory |
| SBOM Generation Success Rate | > 98% | Syft exit code tracking in SQL |
| SBOM CycloneDX Schema Compliance | 100% | JSON schema validation |
| Vulnerability Detection Rate (npm) | > 95% of known vulns | Test suite with known-vulnerable packages |
| Vulnerability Detection Rate (.NET) | > 90% | Test against MSRC known issues |
| False Positive Rate | < 5% | Manual security team review |
| Total Scan Time (100 repos) | < 4 hours | Scan run duration from SQL |
| API Response Time (p95) | < 500 ms | API monitoring |
| Dashboard Load Time | < 3 seconds | Lighthouse/performance testing |
| System Uptime (agent + API) | > 99% | Health check monitoring |
| Critical Vuln SLA Compliance | 100% resolved within 7 days | Age tracking alerts |
| User Adoption (dashboard) | > 80% of stakeholders | Login analytics |

---

## **12. Risks & Mitigations**

| # | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| 1 | **Syft/Grype breaking changes** on upgrade | Medium | High | Pin binary versions; test upgrades in staging before production |
| 2 | **Azure DevOps API changes** in on-prem updates | Medium | High | Abstract API layer behind interface; monitor release notes |
| 3 | **Basic Auth deprecation** in future Azure DevOps versions | Low | High | Support PAT as primary; plan migration path; document in runbook |
| 4 | **SQL Server performance** with growing vulnerability data | Medium | Medium | Implement indexes on hot columns; data retention cleanup jobs |
| 5 | **Credential exposure** through logs or config leaks | Low | Critical | Keyring + DPAPI; credential masking in structured logging |
| 6 | **Large repository clone timeout** | Medium | Medium | Sparse checkout; increased timeouts; concurrent processing limits |
| 7 | **External vulnerability DB rate limiting** | High | Medium | Grype local DB cache; respect rate limits; daily sync schedule |
| 8 | **Angular dashboard security vulnerabilities** | Low | High | Angular built-in XSS protection; CSP headers; regular dependency updates |
| 9 | **Network connectivity loss** to Azure DevOps | Low | High | Graceful degradation; retry with backoff; alert on consecutive failures |
| 10 | **SBOM format specification changes** | Low | Medium | Support multiple CycloneDX versions; schema version detection |

---

## **13. Appendices**

### **Appendix A: Tool Versions & Sources**

| Tool | Minimum Version | Source | Purpose |
|---|---|---|---|
| Syft | 1.0.0+ | [github.com/anchore/syft](https://github.com/anchore/syft) | SBOM Generation |
| Grype | 0.80.0+ | [github.com/anchore/grype](https://github.com/anchore/grype) | Vulnerability Scanning |
| Trivy | 0.50.0+ | [github.com/aquasecurity/trivy](https://github.com/aquasecurity/trivy) | Fallback SBOM + Scanning |
| Python | 3.11+ | [python.org](https://python.org) | Agent + API runtime |
| FastAPI | 0.110+ | [fastapi.tiangolo.com](https://fastapi.tiangolo.com) | REST API framework |
| SQLAlchemy | 2.0+ | [sqlalchemy.org](https://sqlalchemy.org) | ORM + database toolkit |
| Alembic | 1.13+ | [alembic.sqlalchemy.org](https://alembic.sqlalchemy.org) | Database migrations |
| Angular | 18+ | [angular.dev](https://angular.dev) | Admin dashboard SPA |
| Angular Material | 18+ | [material.angular.io](https://material.angular.io) | UI components |
| Chart.js | 4.x | [chartjs.org](https://www.chartjs.org) | Dashboard charts |
| SQL Server | 2019+ | Microsoft | Primary database |

### **Appendix B: Installation Prerequisites Script**

```powershell
# ============================================
# Vulscan Agent — Windows Server Setup Script
# ============================================

# 1. Install prerequisite tools via Chocolatey
choco install -y git nodejs-lts dotnet-sdk python312 sql-server-express

# 2. Install ODBC Driver for SQL Server
choco install -y sqlserver-odbcdriver

# 3. Install Python dependencies
pip install -r agent/requirements.txt
pip install -r api/requirements.txt

# 4. Install Syft
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/anchore/syft/main/install.sh" `
    -OutFile install-syft.sh
bash install-syft.sh -b C:\tools\syft

# 5. Install Grype
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/anchore/grype/main/install.sh" `
    -OutFile install-grype.sh
bash install-grype.sh -b C:\tools\grype

# 6. Add tools to PATH
[Environment]::SetEnvironmentVariable(
    "PATH",
    "$env:PATH;C:\tools\syft;C:\tools\grype",
    [EnvironmentVariableTarget]::Machine
)

# 7. Install Angular CLI globally
npm install -g @angular/cli

# 8. Create application directories
New-Item -ItemType Directory -Force -Path @(
    "C:\ProgramData\VulscanAgent\logs",
    "C:\ProgramData\VulscanAgent\temp",
    "C:\ProgramData\VulscanAgent\grype-db"
)

# 9. Store initial credentials
python -c "import keyring; keyring.set_password('vulscan', 'sdd-pat', 'REPLACE_WITH_PAT')"
python -c "import keyring; keyring.set_password('vulscan', 'jwt-secret', 'REPLACE_WITH_SECRET')"

# 10. Initialize database
python -m alembic upgrade head

Write-Host "✅ Vulscan Agent prerequisites installed successfully!" -ForegroundColor Green
```

### **Appendix C: Authentication Testing Matrix**

| Auth Method | Azure DevOps Version | Status | Notes |
|---|---|---|---|
| PAT (HTTPS) | 2019+ | ✅ Supported | **Recommended** — primary method |
| Basic Auth (HTTPS) | 2019+ | ✅ Supported | Legacy on-prem support |
| Basic Auth (HTTP) | 2019+ | ⚠️ Not Recommended | Requires SSL termination |
| Windows Integrated (NTLM) | 2019+ | 🔄 Planned (Phase 2) | Future enhancement |
| OAuth 2.0 | Azure DevOps Services only | ❌ N/A | Not supported for on-prem |

### **Appendix D: Sample API Responses**

**GET /api/v1/dashboard/summary**

```json
{
  "total_repositories": 87,
  "total_scans": 156,
  "last_scan_at": "2026-02-12T02:15:30Z",
  "vulnerabilities": {
    "total": 247,
    "critical": 12,
    "high": 45,
    "medium": 123,
    "low": 67,
    "new_since_last_scan": 8
  },
  "sla_breaches": {
    "critical_overdue": 2,
    "high_overdue": 5
  },
  "top_vulnerable_repos": [
    {
      "repo": "SDD/ProjectX",
      "critical": 3,
      "high": 8,
      "total": 34
    }
  ]
}
```

**GET /api/v1/vulnerabilities?severity=critical&status=new&page=1&size=20**

```json
{
  "items": [
    {
      "id": 1042,
      "cve_id": "CVE-2024-38819",
      "package_name": "System.Text.Json",
      "installed_version": "6.0.0",
      "fixed_version": "6.0.10",
      "severity": "critical",
      "cvss_score": 9.8,
      "description": "Remote code execution vulnerability in System.Text.Json",
      "source_db": "msrc",
      "status": "new",
      "repository": "SDD/CoreServices",
      "first_detected_at": "2026-02-10T02:15:30Z",
      "age_days": 2
    }
  ],
  "total": 12,
  "page": 1,
  "size": 20,
  "pages": 1
}
```

### **Appendix E: Setup Checklist**

- [ ] Provision Windows Server 2019/2022
- [ ] Install SQL Server 2019+ (Standard or Express)
- [ ] Create `VulscanDB` database
- [ ] Install prerequisites (Git, Node.js, .NET SDK, Python 3.11+)
- [ ] Install ODBC Driver 18 for SQL Server
- [ ] Install Syft and Grype binaries
- [ ] Create Azure DevOps service account(s)
- [ ] Generate and store PAT tokens in Windows Credential Manager
- [ ] Configure network access (Azure DevOps, NVD, GitHub APIs)
- [ ] Clone Vulscan repository
- [ ] Install Python dependencies (`pip install -r requirements.txt`)
- [ ] Configure `config.yaml` with instance details
- [ ] Run Alembic migrations (`alembic upgrade head`)
- [ ] Seed initial admin user
- [ ] Deploy FastAPI backend (uvicorn/gunicorn)
- [ ] Build Angular dashboard (`ng build --configuration production`)
- [ ] Deploy dashboard to IIS or nginx
- [ ] Configure Windows Task Scheduler for agent
- [ ] Test scan with pilot projects (1-2 repos)
- [ ] Configure email notification test
- [ ] Configure Teams webhook test
- [ ] Run full scan on all collections
- [ ] Validate dashboard data
- [ ] Document operational procedures and runbooks
- [ ] Schedule security review

---

## **14. Approval**

| Role | Name | Signature | Date |
|---|---|---|---|
| Project Sponsor | | | |
| Security Lead | | | |
| DevOps Lead | | | |
| Infrastructure Lead | | | |
| Compliance Officer | | | |
| Development Lead | | | |

---

*Document Version: 3.0*
*Last Updated: February 12, 2026*
*Next Review Date: May 12, 2026*

---

### **Key Enhancements from Previous Versions**

| # | Enhancement | BRD Source |
|---|---|---|
| 1 | ✅ **SQL Server as primary database** replacing SQLite | New in v3.0 |
| 2 | ✅ **SQLAlchemy ORM** with Alembic migrations for schema management | New in v3.0 |
| 3 | ✅ **FastAPI REST API** backend with JWT auth and RBAC | New in v3.0 |
| 4 | ✅ **Angular Admin Dashboard** with Material UI, charts, and full CRUD | New in v3.0 |
| 5 | ✅ **SBOM-first architecture** using Syft + Grype | Carried from v2.0 |
| 6 | ✅ **Dual authentication** (PAT + Basic Auth) for Azure DevOps | Carried from v2.0 |
| 7 | ✅ **Microsoft MSRC** integration for .NET vulnerability accuracy | Carried from v2.0 |
| 8 | ✅ **CycloneDX/SPDX compliance** for audit readiness | Carried from v2.0 |
| 9 | ✅ **Comprehensive ER diagram** with full relational schema | New in v3.0 |
| 10 | ✅ **RBAC permission matrix** for dashboard access control | New in v3.0 |
| 11 | ✅ **SLA breach alerting** with configurable age thresholds | Enhanced from v2.0 |
| 12 | ✅ **Structured logging** (JSON format) via structlog | New in v3.0 |
| 13 | ✅ **Gantt chart** implementation timeline | New in v3.0 |
| 14 | ✅ **API endpoint specification** with sample responses | New in v3.0 |
| 15 | ✅ **Project repository structure** with clear separation of concerns | New in v3.0 |

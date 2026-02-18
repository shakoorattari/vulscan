# **Updated Business Requirements Document (BRD): Vulscan Agent**

## **Azure DevOps Local Vulnerability Scanning Agent with SBOM Architecture**

---

## **1. Executive Summary**

This document outlines the requirements for **Vulscan Agent**, a scheduled vulnerability scanning solution for locally hosted Azure DevOps Server instances. The agent leverages **modern SBOM (Software Bill of Materials) architecture** using industry-standard tools (Syft/Trivy + Grype) to automatically generate and scan SBOMs from repositories, detecting vulnerabilities in npm and C#/.NET dependencies. The solution supports **multiple authentication methods** (PAT, Basic Auth) for on-premises Azure DevOps deployments.

---

## **2. Project Overview**

### **2.1. Problem Statement**

- No automated vulnerability detection for projects in on-premises Azure DevOps Server
- Manual dependency checking is time-consuming and error-prone
- Lack of visibility into security risks in dependent libraries
- **No standardized SBOM generation** for compliance/audit purposes
- No centralized reporting or historical tracking of vulnerabilities

### **2.2. Strategic Solution: SBOM-First Architecture**

Instead of building custom parsers for each package manager, Vulscan Agent will:

1. **Generate SBOMs** using Syft/Trivy (CycloneDX/SPDX format)
2. **Scan SBOMs** using Grype against NVD, GitHub Advisories, and **Microsoft MSRC**
3. **Report vulnerabilities** with remediation guidance

**Why this matters:** This aligns with CISA, NTIA, and Executive Order 14028 requirements for software supply chain security.

### **2.3. Objectives**

- Automate SBOM generation for npm and NuGet projects
- Integrate with local Azure DevOps Server using **multiple auth methods**
- Provide scheduled scanning capabilities
- Generate actionable vulnerability reports from SBOM analysis
- Maintain historical vulnerability and SBOM data
- Support multiple collections/projects with **flexible authentication configuration**

---

## **3. Scope**

### **3.1. In Scope**

- **SBOM Generation:**
  - npm projects (package.json, package-lock.json) → CycloneDX/SPDX SBOM
  - C#/.NET projects (.csproj, packages.config, .sln) → CycloneDX/SPDX SBOM
  - Using **Syft** (primary) or **Trivy** (alternative)
  
- **Vulnerability Scanning:**
  - SBOM consumption via **Grype**
  - Vulnerability sources: NVD, GitHub Advisory DB, **Microsoft MSRC (.NET specific)**
  - CVSS scoring and severity classification
  - Remediation recommendations (fixed versions)

- **Azure DevOps Integration:**
  - Collections: `https://devops.ishj.ae/SDD`, `https://devops.ishj.ae/sih`
  - **Authentication Methods:**
    - **PAT (Personal Access Tokens)** - primary method
    - **Basic Auth (Username/Password)** - for on-prem legacy support
    - Configurable per collection/instance
    - Secure credential storage

- **Windows Server Scheduling:**
  - Windows Task Scheduler integration
  - Configurable scan frequency

- **Reporting:**
  - HTML/CSV/JSON reports
  - Email + Teams notifications
  - Historical trend dashboard

### **3.2. Out of Scope**

- Runtime/DAST scanning
- Secret detection
- Container image scanning
- Real-time scanning on commit
- Mobile application scanning
- Custom code analysis

---

## **4. Stakeholders**

| Role              | Responsibility                                       | Contact |
| ----------------- | ---------------------------------------------------- | ------- |
| Security Team     | Define vulnerability thresholds, review SBOM reports | TBD     |
| DevOps Team       | Agent deployment, authentication configuration       | TBD     |
| Development Teams | Remediation actions, SBOM review                     | TBD     |
| IT Infrastructure | Windows Server management, credential security       | TBD     |
| Compliance Team   | SBOM retention, audit requirements                   | TBD     |
| Project Sponsor   | Budget approval, oversight                           | TBD     |

---

## **5. Functional Requirements**

### **5.1. Authentication & Authorization**

| ID              | Requirement                                                                             | Priority |
| --------------- | --------------------------------------------------------------------------------------- | -------- |
| **FR-AUTH-001** | Support **Personal Access Tokens (PAT)** with read-only permissions                     | High     |
| **FR-AUTH-002** | Support **Basic Authentication** (username/password) for on-prem Azure DevOps           | High     |
| **FR-AUTH-003** | Per-instance authentication configuration (different methods for different collections) | High     |
| **FR-AUTH-004** | Secure credential storage using Windows Credential Manager or encrypted config files    | Critical |
| **FR-AUTH-005** | Service account support for scheduled execution                                         | High     |
| **FR-AUTH-006** | Credential validation before scan execution                                             | Medium   |
| **FR-AUTH-007** | Automatic retry with fallback authentication method                                     | Medium   |
| **FR-AUTH-008** | Audit logging of authentication attempts (success/failure)                              | Medium   |

**Configuration Example:**

```json
{
  "azure_devops": {
    "instances": [
      {
        "url": "https://devops.ishj.ae/SDD",
        "auth_method": "pat",
        "pat": "encrypted_or_reference",
        "collection": "DefaultCollection"
      },
      {
        "url": "https://devops.ishj.ae/sih",
        "auth_method": "basic",
        "username": "svc_Vulscan",
        "password": "encrypted_or_reference",
        "domain": "ISHJ" // Optional for Windows integrated auth
      }
    ]
  }
}
```

### **5.2. SBOM Generation & Management**

| ID              | Requirement                                              | Priority |
| --------------- | -------------------------------------------------------- | -------- |
| **FR-SBOM-001** | Integrate **Syft** as primary SBOM generation engine     | High     |
| **FR-SBOM-002** | Support **Trivy** as fallback/alternative SBOM generator | Medium   |
| **FR-SBOM-003** | Generate SBOM in **CycloneDX JSON** format (primary)     | High     |
| **FR-SBOM-004** | Generate SBOM in **SPDX JSON** format (optional)         | Low      |
| **FR-SBOM-005** | Cache SBOMs to avoid regenerating unchanged repos        | Medium   |
| **FR-SBOM-006** | Store historical SBOMs for compliance/audit trails       | Medium   |
| **FR-SBOM-007** | Validate SBOM schema before scanning                     | Medium   |
| **FR-SBOM-008** | Support incremental SBOM generation (delta updates)      | Low      |

**SBOM Generation Command:**

```bash
# For npm/.NET projects
syft dir:/path/to/repo -o cyclonedx-json > sbom.json
# OR
trivy filesystem --format cyclonedx /path/to/repo > sbom.json
```

### **5.3. Vulnerability Scanning (SBOM Analysis)**

| ID              | Requirement                                                  | Priority |
| --------------- | ------------------------------------------------------------ | -------- |
| **FR-SCAN-001** | Integrate **Grype** as primary SBOM vulnerability scanner    | High     |
| **FR-SCAN-002** | Support Trivy as alternative SBOM scanner                    | Medium   |
| **FR-SCAN-003** | Match vulnerabilities against **NVD**                        | High     |
| **FR-SCAN-004** | Match vulnerabilities against **GitHub Advisory Database**   | High     |
| **FR-SCAN-005** | **CRITICAL FOR .NET**: Match against **Microsoft MSRC** data | High     |
| **FR-SCAN-006** | CVSS v2/v3 score calculation and severity mapping            | High     |
| **FR-SCAN-007** | Fixed version detection and remediation suggestions          | High     |
| **FR-SCAN-008** | False positive suppression/whitelisting capability           | Medium   |

**Vulnerability Scan Command:**

```bash
grype sbom.json --output json > vulnerabilities.json
```

### **5.4. Repository Discovery & Management**

| ID              | Requirement                                                      | Priority |
| --------------- | ---------------------------------------------------------------- | -------- |
| **FR-REPO-001** | Enumerate all projects within specified Azure DevOps collections | High     |
| **FR-REPO-002** | Support **PAT and Basic Auth** for Azure DevOps REST API         | High     |
| **FR-REPO-003** | Filter repositories by project name, repo name, or metadata      | High     |
| **FR-REPO-004** | Clone repositories temporarily to local filesystem               | High     |
| **FR-REPO-005** | Automatic cleanup of cloned repositories post-scan               | Critical |
| **FR-REPO-006** | Sparse checkout for large repositories (package files only)      | Medium   |
| **FR-REPO-007** | Git credential helper integration for authentication             | Medium   |

### **5.5. Scheduling & Automation**

| ID               | Requirement                                               | Priority |
| ---------------- | --------------------------------------------------------- | -------- |
| **FR-SCHED-001** | Windows Task Scheduler integration                        | High     |
| **FR-SCHED-002** | Configurable scan frequency (hourly/daily/weekly)         | High     |
| **FR-SCHED-003** | Different schedules for different collections             | Medium   |
| **FR-SCHED-004** | Skip scan if no changes detected (using last commit hash) | Medium   |
| **FR-SCHED-005** | Retry mechanism with exponential backoff                  | Medium   |
| **FR-SCHED-006** | Graceful handling of Azure DevOps maintenance windows     | Low      |

### **5.6. Reporting & Notification**

| ID             | Requirement                                                         | Priority |
| -------------- | ------------------------------------------------------------------- | -------- |
| **FR-REP-001** | Generate **HTML executive summary reports** with severity breakdown | High     |
| **FR-REP-002** | Generate **CSV/JSON detailed reports** for tooling                  | High     |
| **FR-REP-003** | **SBOM attachment** to reports for audit purposes                   | Medium   |
| **FR-REP-004** | Email notifications with SMTP authentication support                | High     |
| **FR-REP-005** | Microsoft Teams webhook integration                                 | High     |
| **FR-REP-006** | Vulnerability age tracking (days since first detection)             | Medium   |
| **FR-REP-007** | SLA breach alerts (e.g., critical vuln > 7 days)                    | Medium   |
| **FR-REP-008** | Web dashboard for historical trends (optional phase)                | Low      |

### **5.7. Data Storage & Retention**

| ID            | Requirement                                               | Priority |
| ------------- | --------------------------------------------------------- | -------- |
| **FR-DB-001** | SQLite database for lightweight deployment                | High     |
| **FR-DB-002** | Optional SQL Server support for enterprise environments   | Medium   |
| **FR-DB-003** | Store **scan metadata** (timestamp, duration, repo count) | High     |
| **FR-DB-004** | Store **vulnerability findings** with status tracking     | High     |
| **FR-DB-005** | Store **SBOMs** with retention policy (30/60/90 days)     | Medium   |
| **FR-DB-006** | Database backup and recovery procedure                    | Medium   |
| **FR-DB-007** | Data export capability for compliance audits              | Medium   |

---

## **6. Non-Functional Requirements**

### **6.1. Performance**

| ID               | Requirement                      | Target                      |
| ---------------- | -------------------------------- | --------------------------- |
| **NFR-PERF-001** | SBOM generation time             | < 30 seconds per repository |
| **NFR-PERF-002** | Vulnerability scan time          | < 20 seconds per SBOM       |
| **NFR-PERF-003** | Total scan time (100 repos)      | < 4 hours                   |
| **NFR-PERF-004** | Concurrent repository processing | Configurable (default: 5)   |
| **NFR-PERF-005** | Memory usage                     | < 2GB during peak           |

### **6.2. Security**

| ID              | Requirement                                    | Priority |
| --------------- | ---------------------------------------------- | -------- |
| **NFR-SEC-001** | **No persistence of source code** post-scan    | Critical |
| **NFR-SEC-002** | Encrypted credential storage (DPAPI/AES-256)   | Critical |
| **NFR-SEC-003** | Credentials never logged in plaintext          | Critical |
| **NFR-SEC-004** | Read-only access to Azure DevOps               | High     |
| **NFR-SEC-005** | Network isolation for scanning server          | High     |
| **NFR-SEC-006** | Regular PAT rotation reminders (30/60/90 days) | Medium   |

### **6.3. Reliability**

| ID              | Requirement                         | Target            |
| --------------- | ----------------------------------- | ----------------- |
| **NFR-REL-001** | Scheduling service uptime           | > 99%             |
| **NFR-REL-002** | Failed scan auto-retry              | 3 attempts        |
| **NFR-REL-003** | Vulnerability DB update frequency   | Daily             |
| **NFR-REL-004** | Graceful degradation (offline mode) | Partial reporting |

### **6.4. Maintainability**

| ID              | Requirement                                             | Priority |
| --------------- | ------------------------------------------------------- | -------- |
| **NFR-MNT-001** | Configuration-driven architecture (no hardcoded values) | High     |
| **NFR-MNT-002** | Modular design for adding new package managers          | High     |
| **NFR-MNT-003** | Comprehensive logging (Windows Event Log + file)        | High     |
| **NFR-MNT-004** | Versioned SBOM schema support                           | Medium   |
| **NFR-MNT-005** | Self-update capability for Syft/Grype binaries          | Medium   |

---

## **7. Technical Architecture**

### **7.1. High-Level Architecture**

```
┌─────────────────────────────────────────────────────────────┐
│                    WINDOWS SERVER                           │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              Vulscan AGENT CORE                     │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │   │
│  │  │  Discovery  │  │    Auth     │  │   Scheduler │  │   │
│  │  │   Engine    │  │   Manager   │  │   Engine    │  │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  │   │
│  │                                                     │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │   │
│  │  │    SBOM     │  │    Grype    │  │  Reporting  │  │   │
│  │  │   Syft      │  │   Scanner   │  │   Engine    │  │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘  │   │
│  └─────────────────────────────────────────────────────┘   │
│                            │                                │
│  ┌─────────────────────────┼─────────────────────────────┐  │
│  │                         ▼                             │  │
│  │  ┌─────────────────────────────────────────────┐     │  │
│  │  │           Local Database (SQLite)           │     │  │
│  │  │  - Scan History  - SBOM Archive            │     │  │
│  │  │  - Vulnerabilities - Configuration         │     │  │
│  │  └─────────────────────────────────────────────┘     │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
        ▼                  ▼                  ▼
┌───────────────┐  ┌───────────────┐  ┌───────────────┐
│  Azure DevOps │  │  Azure DevOps │  │  External     │
│  (PAT Auth)   │  │  (Basic Auth) │  │  Vuln DBs     │
│  SDD          │  │  sih          │  │  NVD/GH/MSRC  │
└───────────────┘  └───────────────┘  └───────────────┘
```

### **7.2. Component Details**

| Component              | Technology                             | Purpose                                    |
| ---------------------- | -------------------------------------- | ------------------------------------------ |
| **Core Agent**         | Python 3.9+                            | Orchestration, authentication, scheduling  |
| **SBOM Generator**     | Syft (CLI)                             | Generate CycloneDX SBOMs from repositories |
| **Vuln Scanner**       | Grype (CLI)                            | Scan SBOMs against vulnerability DBs       |
| **Azure DevOps API**   | `azure-devops` Python package          | Repository discovery, cloning              |
| **Authentication**     | `requests` with auth handlers          | PAT + Basic Auth support                   |
| **Credential Storage** | `keyring` (Windows Credential Manager) | Secure credential encryption               |
| **Database**           | SQLite (`sqlite3`)                     | Local data persistence                     |
| **Scheduling**         | Windows Task Scheduler                 | Trigger scans                              |
| **Reporting**          | Jinja2, Pandas                         | HTML/CSV/JSON report generation            |
| **Notifications**      | SMTP, `pymsteams`                      | Email + Teams alerts                       |

---

## **8. Implementation Phases**

### **Phase 1: Foundation & Authentication (Weeks 1-3)**

- Azure DevOps API integration with PAT + Basic Auth
- Secure credential management (Windows Credential Manager)
- Repository discovery and cloning
- Configuration framework

### **Phase 2: SBOM Core (Weeks 4-6)**

- Syft integration for npm projects
- Syft integration for .NET projects
- SBOM validation and storage
- Performance optimization for cloning

### **Phase 3: Vulnerability Scanning (Weeks 7-9)**

- Grype integration
- NVD + GitHub Advisory + MSRC feed configuration
- Vulnerability matching and severity mapping
- Remediation suggestion engine

### **Phase 4: Reporting & Scheduling (Weeks 10-12)**

- HTML/CSV/JSON report generation
- Email + Teams notifications
- Windows Task Scheduler integration
- Historical data tracking

### **Phase 5: Production Hardening (Weeks 13-16)**

- Concurrent scanning
- Incremental SBOM generation
- Advanced filtering
- Performance tuning
- Documentation & runbooks

---

## **9. Configuration Management**

### **9.1. Master Configuration File**

```json
{
  "agent_name": "VulscanAgent",
  "version": "1.0.0",
  
  "azure_devops": {
    "instances": [
      {
        "name": "SDD",
        "url": "https://devops.ishj.ae/SDD",
        "auth_method": "pat",
        "pat_secret_name": "Vulscan/sdd-pat",
        "collection": "DefaultCollection",
        "scan_schedule": "0 2 * * *",
        "enabled": true
      },
      {
        "name": "SIH",
        "url": "https://devops.ishj.ae/sih",
        "auth_method": "basic",
        "username": "svc_Vulscan",
        "password_secret_name": "xxxx/xxxxxx",
        "domain": "ISHJ",
        "collection": "DefaultCollection",
        "scan_schedule": "0 3 * * *",
        "enabled": true
      }
    ]
  },
  
  "sbom": {
    "generator": "syft",
    "format": "cyclonedx-json",
    "cache_enabled": true,
    "cache_ttl_days": 7
  },
  
  "scanning": {
    "engine": "grype",
    "vulnerability_dbs": ["nvd", "github", "msrc"],
    "fail_on_severity": "critical",
    "max_concurrent_scans": 5,
    "clone_timeout_seconds": 300,
    "retry_attempts": 3
  },
  
  "reporting": {
    "formats": ["html", "csv", "json"],
    "email": {
      "enabled": true,
      "smtp_server": "smtp.ishj.ae",
      "smtp_port": 587,
      "use_tls": true,
      "recipients": ["security@ishj.ae", "devops@ishj.ae"],
      "threshold": "high"
    },
    "teams": {
      "enabled": true,
      "webhook_secret_name": "Vulscan/teams-webhook"
    }
  },
  
  "database": {
    "type": "sqlite",
    "path": "C:\\ProgramData\\VulscanAgent\\Vulscan.db",
    "backup_enabled": true,
    "retention_days": 90
  },
  
  "logging": {
    "level": "INFO",
    "file": "C:\\ProgramData\\VulscanAgent\\logs\\Vulscan.log",
    "max_size_mb": 100,
    "backup_count": 10,
    "event_log_source": "VulscanAgent"
  }
}
```

### **9.2. Credential Management Strategy**

```powershell
# Store credentials in Windows Credential Manager
$cred = Get-Credential
dotnet user-secrets set "Vulscan/sdd-pat" $cred.Password

# OR using keyring Python package
import keyring
keyring.set_password("VulscanAgent", "sdd-pat", "your-pat-token")
```

---

## **10. SBOM-Specific Success Metrics**

| Metric                            | Target               | Measurement                    |
| --------------------------------- | -------------------- | ------------------------------ |
| **SBOM Generation Success Rate**  | > 98%                | Syft exit codes                |
| **SBOM Schema Compliance**        | 100% CycloneDX valid | JSON schema validation         |
| **Vulnerability Coverage (npm)**  | > 95%                | Test against known vulns       |
| **Vulnerability Coverage (.NET)** | > 90%                | Test against MSRC known issues |
| **False Positive Rate**           | < 5%                 | Manual verification            |
| **SBOM Retention Compliance**     | 100%                 | Audit trail                    |

---

## **11. Risks & Mitigations**

| Risk                            | Probability | Impact   | Mitigation                                       |
| ------------------------------- | ----------- | -------- | ------------------------------------------------ |
| **Syft/Grype breaking changes** | Medium      | High     | Pin versions, test upgrades in staging           |
| **Basic Auth deprecation**      | Low         | High     | Monitor Azure DevOps roadmap, plan PAT migration |
| **MSRC API rate limiting**      | Low         | Medium   | Cache vulnerability data locally                 |
| **Large repositories timeout**  | Medium      | Medium   | Implement sparse checkout, increase timeouts     |
| **Credential exposure**         | Low         | Critical | Windows Credential Manager, never log secrets    |
| **SBOM format changes**         | Low         | Medium   | Support multiple SBOM versions                   |

---

## **12. Appendices**

### **Appendix A: Tool Versions & Sources**

| Tool                    | Version   | Source                                | Purpose            |
| ----------------------- | --------- | ------------------------------------- | ------------------ |
| Syft                    | v0.100.0+ | <https://github.com/anchore/syft>       | SBOM Generation    |
| Grype                   | v0.70.0+  | <https://github.com/anchore/grype>      | Vuln Scanning      |
| Trivy                   | v0.48.0+  | <https://github.com/aquasecurity/trivy> | Alternative SBOM   |
| Azure DevOps Python API | v7.1      | `pip install azure-devops`            | API Integration    |
| Keyring                 | v24.3.0+  | `pip install keyring`                 | Credential Storage |

### **Appendix B: Installation Prerequisites Script**

```powershell
# Windows Server Setup Script
choco install -y git nodejs dotnet-sdk python
pip install azure-devops keyring pandas jinja2 requests pymsteams

# Install Syft
curl -sSfL https://raw.githubusercontent.com/anchore/syft/main/install.sh | sh -s -- -b C:\tools

# Install Grype
curl -sSfL https://raw.githubusercontent.com/anchore/grype/main/install.sh | sh -s -- -b C:\tools

# Add to PATH
[Environment]::SetEnvironmentVariable("PATH", $env:PATH + ";C:\tools", [EnvironmentVariableTarget]::Machine)
```

### **Appendix C: Authentication Testing Matrix**

| Auth Method        | Azure DevOps Version | Status            | Notes          |
| ------------------ | -------------------- | ----------------- | -------------- |
| PAT                | 2019+                | ✅ Supported       | Recommended    |
| Basic Auth (HTTPS) | 2019+                | ✅ Supported       | Legacy support |
| Basic Auth (HTTP)  | 2019+                | ⚠️ Not Recommended | Requires SSL   |
| Windows Integrated | 2019+                | 🔄 Planned         | Phase 2        |

---

## **13. Approval**

| Role                | Name | Signature | Date |
| ------------------- | ---- | --------- | ---- |
| Project Sponsor     |      |           |      |
| Security Lead       |      |           |      |
| DevOps Lead         |      |           |      |
| Infrastructure Lead |      |           |      |
| Compliance Officer  |      |           |      |

---

*Document Version: 2.0*
*Last Updated: [Current Date]*
*Next Review Date: [Date + 3 months]*

---

**Key Updates from V1.0:**

1. ✅ **SBOM-First Architecture** using Syft + Grype
2. ✅ **Dual Authentication Support** (PAT + Basic Auth)
3. ✅ **MSRC Integration** for accurate .NET vulnerability detection
4. ✅ **Windows Credential Manager** for secure secrets
5. ✅ **Configuration-driven auth** per Azure DevOps instance
6. ✅ **CycloneDX/SPDX compliance** for audit readiness

# **Business Requirements Document (BRD): Azure DevOps Local Vulnerability Scanning Agent**

## **1. Executive Summary**

This document outlines the requirements for developing and deploying a scheduled vulnerability scanning agent for locally hosted Azure DevOps Server instances. The agent will automatically scan repositories (focusing on npm and C#/.NET projects) for known vulnerabilities in dependencies and libraries, providing centralized reporting and alerting capabilities.

## **2. Project Overview**

### **2.1. Problem Statement**

- No automated vulnerability detection for projects in on-premises Azure DevOps Server
- Manual dependency checking is time-consuming and error-prone
- Lack of visibility into security risks in dependent libraries
- No centralized reporting or historical tracking of vulnerabilities

### **2.2. Objectives**

- Automate scanning of npm and NuGet package dependencies
- Integrate with local Azure DevOps Server instances
- Provide scheduled scanning capabilities
- Generate actionable vulnerability reports
- Maintain historical vulnerability data
- Support multiple collections/projects

## **3. Scope**

### **3.1. In Scope**

- Scanning npm projects (package.json, package-lock.json)
- Scanning C#/.NET projects (.csproj, packages.config, .sln files)
- Integration with Azure DevOps REST APIs
- Windows Server-based scheduling
- Vulnerability databases: NVD, GitHub Advisories, NPM Advisory
- Email/Teams notification system
- Dashboard/reporting interface
- Support for specified collections:
  - `https://devops.ishj.ae/SDD`
  - `https://devops.ishj.ae/sih`

### **3.2. Out of Scope**

- Runtime/DAST scanning
- Secret detection
- Container image scanning
- Real-time scanning on commit
- Integration with third-party commercial scanners
- Mobile application scanning
- Custom code analysis

## **4. Stakeholders**

| Role              | Responsibility                                  | Contact |
| ----------------- | ----------------------------------------------- | ------- |
| Security Team     | Define vulnerability thresholds, review reports | TBD     |
| DevOps Team       | Agent deployment, maintenance                   | TBD     |
| Development Teams | Remediation actions                             | TBD     |
| IT Infrastructure | Windows Server management                       | TBD     |
| Project Sponsor   | Budget approval, oversight                      | TBD     |

## **5. Functional Requirements**

### **5.1. Authentication & Authorization**

- **FR-001**: Support Azure DevOps Basic Authentication with read permissions
- **FR-002**: Secure credential storage using Windows Credential Manager or encrypted config
- **FR-003**: Support single Basic Authentication credentials for different collections
- **FR-004**: Service account with appropriate permissions for scheduled execution

### **5.2. Repository Discovery**

- **FR-005**: Enumerate all projects within specified collections
- **FR-006**: Identify repositories containing supported project types
- **FR-007**: Filter capability (include/exclude specific projects/repos)
- **FR-008**: Clone repositories temporarily for scanning

### **5.3. Dependency Analysis**

#### **npm Projects:**

- **FR-009**: Parse package.json and package-lock.json
- **FR-010**: Identify direct and transitive dependencies
- **FR-011**: Detect package versions with known vulnerabilities
- **FR-012**: Check against NPM Advisory Database

#### **C#/.NET Projects:**

- **FR-013**: Parse .csproj, packages.config, and solution files
- **FR-014**: Identify NuGet package dependencies
- **FR-015**: Support both .NET Framework and .NET Core/5+
- **FR-016**: Check against NVD and GitHub Security Advisories

### **5.4. Vulnerability Detection**

- **FR-017**: Integrate with vulnerability databases:
  - NVD API (National Vulnerability Database)
  - GitHub Advisory Database
  - NPM Advisory API
- **FR-018**: Local vulnerability cache to reduce API calls
- **FR-019**: CVSS scoring and severity classification
- **FR-020**: Remediation recommendation (suggest fixed versions)

### **5.5. Scheduling & Automation**

- **FR-021**: Windows Task Scheduler integration
- **FR-022**: Configurable scan frequency (daily/weekly)
- **FR-023**: Incremental scanning option
- **FR-024**: Retry mechanism for failed scans

### **5.6. Reporting & Notification**

- **FR-025**: HTML report generation
- **FR-026**: CSV/JSON export capability
- **FR-027**: Email notifications with configurable thresholds
- **FR-028**: Microsoft Teams webhook integration
- **FR-029**: Dashboard with historical trends
- **FR-030**: Vulnerability age tracking

### **5.7. Data Storage**

- **FR-031**: Local SQLite/SQL Server database
- **FR-032**: Store scan history and results
- **FR-033**: Data retention policy (configurable)
- **FR-034**: Backup mechanism for scan database

## **6. Non-Functional Requirements**

### **6.1. Performance**

- **NFR-001**: Scan completion within 4 hours for 100 repositories
- **NFR-002**: Memory usage under 2GB during operation
- **NFR-003**: Concurrent processing of repositories (configurable)
- **NFR-004**: Efficient rate limiting for external API calls

### **6.2. Security**

- **NFR-005**: No persistence of repository data post-scan
- **NFR-006**: Encrypted storage of credentials
- **NFR-007**: Read-only access to Azure DevOps
- **NFR-008**: Network isolation for scanning server

### **6.3. Reliability**

- **NFR-009**: 99% uptime for scheduling service
- **NFR-010**: Comprehensive error logging
- **NFR-011**: Automatic recovery from common failures
- **NFR-012**: Alerting for consecutive scan failures

### **6.4. Maintainability**

- **NFR-013**: Modular design for adding new package managers
- **NFR-014**: Configuration via JSON/YAML files
- **NFR-015**: Comprehensive logging (Windows Event Log + file)
- **NFR-016**: Versioned database schema

## **7. Technical Requirements**

### **7.1. Development Stack**

- **Primary Language**: Python 3.9+ (recommended for security tooling) or C#
- **Package Analysis**:
  - npm: `package-json` parser, `npm-audit` alternative
  - .NET: `NuGet.Client`, `dotnet list package`
- **Database**: SQLite (lightweight) or SQL Server Express
- **Scheduling**: Windows Task Scheduler with PowerShell wrapper
- **Frontend**: Flask/Django for dashboard (optional) or static HTML reports

### **7.2. Infrastructure Requirements**

- **Windows Server 2019/2022**
- **Git installed** (for cloning)
- **Node.js** (for npm analysis)
- **.NET SDK** (for C# analysis)
- **Internet access** to vulnerability databases
- **Storage**: 50GB free space for temporary cloning
- **Memory**: 8GB RAM minimum
- **CPU**: 4 cores minimum

### **7.3. Integration Points**

- Azure DevOps REST API v6.0
- NVD API v2
- GitHub Advisory API
- NPM Advisory API
- SMTP server for email notifications
- Microsoft Teams webhooks

## **8. Deployment Architecture**

```text
[Windows Server]
├── Vulnerability Scanning Agent (Windows Service/Console App)
├── Task Scheduler (Triggers scans)
├── Local Database (Scan results)
├── Configuration Files
└── Logs Directory

[External Dependencies]
├── Azure DevOps Server (devops.ishj.ae)
├── NVD API
├── GitHub Security Advisories
└── NPM Advisory DB
```

## **9. Implementation Phases**

### **Phase 1: Core Scanning Engine (Weeks 1-4)**

- Basic repository discovery
- npm package vulnerability scanning
- Simple reporting (console + CSV)
- Manual execution capability

### **Phase 2: Enhanced Features (Weeks 5-8)**

- C#/.NET scanning
- Windows Task Scheduler integration
- Email notifications
- SQLite database storage

### **Phase 3: Reporting & Dashboard (Weeks 9-12)**

- HTML reporting
- Historical tracking
- Dashboard interface
- Teams integration

### **Phase 4: Optimization & Scaling (Weeks 13-16)**

- Performance optimization
- Concurrent scanning
- Advanced filtering
- Production hardening

## **10. Configuration Management**

### **10.1. Configuration File Structure**

```json
{
  "azure_devops": {
    "instances": [
      {
        "url": "https://devops.ishj.ae/SDD",
        "username": "email_or_username",
        "password": "encrypted_or_reference",
        "collections": ["SDD"]
      }
    ]
  },
  "scanning": {
    "frequency": "daily",
    "package_managers": ["npm", "nuget"],
    "max_concurrent": 5
  },
  "notifications": {
    "email": {
      "enabled": true,
      "smtp_server": "smtp.internal",
        "from": "no-reply@ishj.ae",
        "username": "no-reply@ishj.ae",
        "password": "encrypted_or_reference",
        "port": 587,
        "use_tls": true,
      "recipients": ["security@ishj.ae"]
    },
    "teams_webhook": "https://..."
  }
}
```

## **11. Security Considerations**

### **11.1. Access Controls**

- Least privilege service account
- PAT with only read permissions
- Network segmentation for scanning server
- Regular PAT rotation procedure

### **11.2. Data Protection**

- Encrypted configuration at rest
- Secure credential handling
- Automatic cleanup of cloned repositories
- Audit logging of all scan activities

## **12. Success Metrics**

| Metric                       | Target                 | Measurement   |
| ---------------------------- | ---------------------- | ------------- |
| Repository Coverage          | 100% of targeted repos | Weekly audit  |
| False Positive Rate          | < 5%                   | Manual review |
| Scan Completion Time         | < 4 hours              | Log analysis  |
| Vulnerability Detection Rate | > 95% of known vulns   | Test suite    |
| System Uptime                | > 99%                  | Monitoring    |

## **13. Risks & Mitigations**

| Risk                            | Probability | Impact | Mitigation                                      |
| ------------------------------- | ----------- | ------ | ----------------------------------------------- |
| Azure DevOps API changes        | Medium      | High   | Abstract API layer, monitor updates             |
| Rate limiting by external APIs  | High        | Medium | Implement caching, respect rate limits          |
| Increased load on DevOps server | Medium      | Medium | Schedule during off-hours, implement throttling |
| False positives                 | High        | Medium | Tuning, whitelisting capability                 |
| Credential compromise           | Low         | High   | Regular rotation, minimal permissions           |

## **14. Appendices**

### **Appendix A: Sample Vulnerability Report Structure**

```json
{
  "repository": "SDD/ProjectX",
  "scan_date": "2024-01-15",
  "dependencies_scanned": 245,
  "vulnerabilities_found": 12,
  "findings": [
    {
      "package": "lodash",
      "version": "4.17.15",
      "vulnerability_id": "CVE-2020-8203",
      "severity": "HIGH",
      "cvss_score": 7.5,
      "description": "Prototype Pollution vulnerability",
      "remediation": "Upgrade to 4.17.19+",
      "first_detected": "2024-01-01"
    }
  ]
}
```

### **Appendix B: Setup Checklist**

- [ ] Provision Windows Server
- [ ] Install prerequisites (Git, Node.js, .NET SDK)
- [ ] Create Azure DevOps service account
- [ ] Generate and secure PAT tokens
- [ ] Configure network access
- [ ] Deploy scanning agent
- [ ] Configure Task Scheduler
- [ ] Test with pilot projects
- [ ] Configure notifications
- [ ] Document procedures

---

## **Approval**

| Role                | Name | Signature | Date |
| ------------------- | ---- | --------- | ---- |
| Project Sponsor     |      |           |      |
| Security Lead       |      |           |      |
| DevOps Lead         |      |           |      |
| Infrastructure Lead |      |           |      |

---

*Document Version: 1.0*
*Last Updated: [Current Date]*
*Next Review Date: [Date + 3 months]*

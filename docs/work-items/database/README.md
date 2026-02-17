# 🗄️ Database — Work Items

| ID | Requirement | Priority | Status | Notes |
|---|---|---|---|---|
| **FR-DB-001** | SQL Server as primary DB | 🔴 Critical | 🔶 **SQLite** | Using SQLite for development; migration to SQL Server planned |
| **FR-DB-002** | SQLAlchemy ORM | 🔴 Critical | ✅ **EF Core** | Entity Framework Core as .NET equivalent |
| **FR-DB-003** | Alembic migrations | 🟠 High | ✅ **EF Core Migrations** | `InitialCreate` migration applied |
| **FR-DB-004** | Connection pooling | 🔴 Critical | ✅ **Done** | EF Core handles connection pooling |
| **FR-DB-005** | Scan run metadata | 🔴 Critical | ✅ **Done** | `ScanRun` entity: start/end time, duration, repo count, status |
| **FR-DB-006** | Vulnerability findings (CVE, CVSS) | 🔴 Critical | ✅ **Done** | `Vulnerability` entity with full detail |
| **FR-DB-007** | SBOM artifacts | 🟠 High | ✅ **Done** | `Sbom` entity with `SbomJson` field |
| **FR-DB-008** | Repository metadata | 🔴 Critical | ✅ **Done** | `Repository` entity with project/collection info |
| **FR-DB-009** | Agent configuration (dashboard-editable) | 🟠 High | ❌ **Not Done** | |
| **FR-DB-010** | Notification history | 🟡 Medium | ❌ **Not Done** | |
| **FR-DB-011** | User accounts, roles, audit logs | 🔴 Critical | ✅ **Done** | `User` + `AuditLog` entities |
| **FR-DB-012** | Data retention policies | 🟡 Medium | ❌ **Not Done** | |
| **FR-DB-013** | Backup strategy documentation | 🟡 Medium | ❌ **Not Done** | |

## Schema Summary

9 DbSets: `Users`, `AzureDevOpsInstances`, `Projects`, `Repositories`, `ScanRuns`, `Sboms`, `DiscoveredPackages`, `Vulnerabilities`, `AuditLogs`

# 🔐 Authentication & Authorization — Work Items

## 5.1.1 Azure DevOps Authentication

| ID | Requirement | Priority | Status | Notes |
|---|---|---|---|---|
| **FR-AUTH-001** | PAT support with read-only scopes | 🔴 Critical | ✅ **Done** | `AzureDevOpsClient.cs` supports PAT via `AuthMethod.Pat` |
| **FR-AUTH-002** | Basic Auth (username/password) for on-prem | 🔴 Critical | ✅ **Done** | `AzureDevOpsClient.cs` supports `AuthMethod.BasicAuth` |
| **FR-AUTH-003** | Per-instance auth configuration | 🟠 High | ✅ **Done** | `AzureDevOpsInstance` entity has `AuthMethod` field |
| **FR-AUTH-004** | Secure credential storage (keyring/DPAPI) | 🔴 Critical | ❌ **Not Done** | Credentials stored as plain JSON in `CredentialReference` |
| **FR-AUTH-005** | Service account support | 🟠 High | 🔶 **Partial** | Basic Auth works with service accounts; no special handling |
| **FR-AUTH-006** | Credential validation before scan | 🟡 Medium | ❌ **Not Done** | No pre-scan validation step |
| **FR-AUTH-007** | PAT expiry monitoring (30/60/90 day) | 🟡 Medium | ❌ **Not Done** | |
| **FR-AUTH-008** | Audit logging of auth attempts | 🟡 Medium | ❌ **Not Done** | `AuditLog` entity exists but not written to |

## 5.1.2 Dashboard & API Authentication

| ID | Requirement | Priority | Status | Notes |
|---|---|---|---|---|
| **FR-AUTH-010** | JWT-based authentication for API | 🔴 Critical | ✅ **Done** | `JwtTokenService.cs` — HMAC-SHA256 |
| **FR-AUTH-011** | RBAC: admin, security_analyst, developer, viewer | 🟠 High | ✅ **Done** | `UserRole` enum + `[Authorize(Roles)]` on controllers |
| **FR-AUTH-012** | Login page with hashed credentials | 🔴 Critical | ✅ **Done** | Angular `LoginComponent` + BCrypt hashing |
| **FR-AUTH-013** | JWT token refresh mechanism | 🟠 High | ❌ **Not Done** | `RefreshTokenAsync` throws `NotImplementedException` |
| **FR-AUTH-014** | API key for service-to-service | 🟡 Medium | ❌ **Not Done** | |

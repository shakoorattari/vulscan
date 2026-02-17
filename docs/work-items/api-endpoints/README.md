# ⚡ API Endpoints — Work Items

| ID | Requirement | Priority | Status | Notes |
|---|---|---|---|---|
| **FR-API-001** | REST API with OpenAPI/Swagger | 🔴 Critical | ✅ **Done** | ASP.NET Core Web API with Swagger UI |
| **FR-API-002** | JWT auth middleware + RBAC | 🔴 Critical | ✅ **Done** | `[Authorize(Roles)]` attribute-based |
| **FR-API-003** | CORS for Angular dashboard | 🔴 Critical | ✅ **Done** | Configured in `Program.cs` |
| **FR-API-004** | Dashboard endpoints (summary, trend) | 🔴 Critical | 🔶 **Partial** | `GET /dashboard/summary` ✅; trends ❌; top-repos ✅ (in summary) |
| **FR-API-005** | Vulnerability endpoints (CRUD) | 🔴 Critical | ✅ **Done** | list, filter, detail, status update |
| **FR-API-006** | SBOM endpoints (list, download) | 🟠 High | 🔶 **Partial** | SBOM download ✅; compare ❌ |
| **FR-API-007** | Scan endpoints (trigger, history) | 🟠 High | ✅ **Done** | trigger, history (paginated), detail |
| **FR-API-008** | Configuration endpoints | 🟠 High | ❌ **Not Done** | |
| **FR-API-009** | User management endpoints | 🟠 High | ❌ **Not Done** | Only register (admin) exists |
| **FR-API-010** | Report endpoints (HTML/CSV/JSON) | 🟠 High | 🔶 **Partial** | CSV export ✅; JSON SBOM ✅; HTML reports ❌; **IN PROGRESS** |
| **FR-API-011** | Notification endpoints | 🟡 Medium | ❌ **Not Done** | |
| **FR-API-012** | Pagination, sorting, filtering | 🔴 Critical | ✅ **Done** | `PagedResult<T>` on list endpoints |
| **FR-API-013** | Background task support | 🟠 High | ✅ **Done** | `ScanBackgroundWorker` hosted service |
| **FR-API-014** | Health check endpoint | 🟠 High | ✅ **Done** | `GET /api/v1/health` |
| **FR-API-015** | Rate limiting | 🟡 Medium | ❌ **Not Done** | |

## Existing Endpoints

| Controller | Route | Methods |
|---|---|---|
| `AuthController` | `/api/v1/auth` | POST login, POST register, POST refresh |
| `DashboardController` | `/api/v1/dashboard` | GET summary |
| `HealthController` | `/api/v1/health` | GET |
| `InstancesController` | `/api/v1/instances` | GET, GET summaries, GET/:id, POST, PUT/:id, DELETE/:id, POST test |
| `PackagesController` | `/api/packages` | GET scan/:id, GET repo/:id, GET vulnerable, GET stats, GET sbom, GET csv, GET details |
| `ScansController` | `/api/v1/scans` | POST trigger, GET history, GET/:id |
| `VulnerabilitiesController` | `/api/v1/vulnerabilities` | GET (filtered), GET/:id, PATCH/:id/status |

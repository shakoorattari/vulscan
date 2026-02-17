# 📂 Repository Discovery & Management — Work Items

| ID | Requirement | Priority | Status | Notes |
|---|---|---|---|---|
| **FR-REPO-001** | Enumerate all projects via REST API | 🔴 Critical | ✅ **Done** | `GetProjectsAsync()` fetches all projects from collection |
| **FR-REPO-002** | PAT and Basic Auth for REST API v6.0+ | 🔴 Critical | ✅ **Done** | `AzureDevOpsClient.cs` uses API v6.0 |
| **FR-REPO-003** | Include/exclude filter (project/repo/regex) | 🟠 High | ❌ **Not Done** | Scans all projects/repos without filtering |
| **FR-REPO-004** | Clone repos to temp filesystem path | 🔴 Critical | 🔶 **Alt. Approach** | Uses REST API `GetFileContentAsync` instead of cloning |
| **FR-REPO-005** | Auto-cleanup of cloned repos after scan | 🔴 Critical | ✅ **N/A** | No cloning needed — content fetched via API |
| **FR-REPO-006** | Sparse checkout for large repos | 🟡 Medium | ✅ **N/A** | Using targeted API calls instead |
| **FR-REPO-007** | Git credential helper integration | 🟡 Medium | ❌ **Not Done** | |
| **FR-REPO-008** | Track last scanned commit hash | 🟡 Medium | 🔶 **Partial** | `Repository.LastScannedCommit` field exists, not populated |

## Implementation Notes

- The current approach uses Azure DevOps REST API to fetch file content directly (`GetFileContentAsync` with `$format=text`), avoiding the need for full repository cloning. This is more efficient for dependency scanning use cases.
- `ScanProcessor.cs` auto-discovers all projects and repos during each scan run.

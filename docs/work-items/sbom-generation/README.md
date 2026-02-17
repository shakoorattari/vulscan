# 📜 SBOM Generation & Management — Work Items

| ID | Requirement | Priority | Status | Notes |
|---|---|---|---|---|
| **FR-SBOM-001** | Syft as primary SBOM engine | 🔴 Critical | 🔶 **Alt. Approach** | Custom `DependencyScanner.cs` instead of Syft CLI |
| **FR-SBOM-002** | Trivy fallback SBOM generator | 🟡 Medium | ❌ **Not Done** | |
| **FR-SBOM-003** | CycloneDX JSON format (primary) | 🔴 Critical | ✅ **Done** | `PackagesController` generates CycloneDX 1.5 JSON |
| **FR-SBOM-004** | SPDX JSON format (optional) | 🟢 Low | ❌ **Not Done** | |
| **FR-SBOM-005** | Cache SBOMs (skip if commit unchanged) | 🟡 Medium | ❌ **Not Done** | |
| **FR-SBOM-006** | Historical SBOMs with retention policy | 🟠 High | 🔶 **Partial** | `Sbom` entity stores per scan, no retention cleanup |
| **FR-SBOM-007** | Validate SBOM against CycloneDX schema | 🟡 Medium | ❌ **Not Done** | |
| **FR-SBOM-008** | Log SBOM generation duration & stats | 🟠 High | 🔶 **Partial** | Scan duration logged, no per-repo SBOM metrics |

## Implementation Notes

- Custom `DependencyScanner.cs` parses npm (`package.json`), NuGet (`.csproj`, `packages.config`), pip (`requirements.txt`), Go (`go.mod`), Cargo (`Cargo.toml`), Composer (`composer.json`), Ruby (`Gemfile`) dependencies.
- Achieved 28,885 packages from 65 repositories scanning.
- CycloneDX SBOM endpoint at `GET /api/packages/scan/{scanRunId}/sbom`.

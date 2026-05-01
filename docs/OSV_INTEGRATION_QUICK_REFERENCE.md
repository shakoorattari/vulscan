# 🚀 OSV.dev Integration — Quick Reference

> **TL;DR:** We're already using OSV.dev correctly. Add OSV Scanner CLI for container/license scanning.

---

## ✅ What We're Doing Right

- ✅ Using OSV.dev API for vulnerability lookups
- ✅ Batch queries (1000 packages/request)
- ✅ In-memory caching
- ✅ CycloneDX SBOM generation
- ✅ npm & NuGet ecosystem support

**Implementation:** [`DependencyScanner.cs`](../server/src/Vulscan.Infrastructure/Services/DependencyScanner.cs) + [`OsvApiClient.cs`](../server/src/Vulscan.Infrastructure/Clients/OsvApiClient.cs)

---

## 🔴 Critical Gaps

| Gap | Impact | Solution |
|-----|--------|----------|
| Container scanning | **High** | Add OSV Scanner CLI |
| OS package vulns | **High** | Add OSV Scanner CLI |
| License compliance | Medium | Add OSV Scanner CLI |
| Call analysis | Medium | OSV Scanner experimental |
| More ecosystems | Medium | Extend parser logic |

---

## 💡 Recommended Next Steps

### Phase 1 (Q2 2026) — Container Scanning

```bash
# 1. Install OSV Scanner
wget https://github.com/google/osv-scanner/releases/latest/download/osv-scanner_linux_amd64
chmod +x osv-scanner_linux_amd64
mv osv-scanner_linux_amd64 /usr/local/bin/osv-scanner

# 2. Test it
osv-scanner scan image nginx:latest --format json
```

**Code Changes:**

- Add `OsvScannerService.cs` to wrap CLI calls
- Add `ContainerScansController.cs` for API
- Extend database with `ContainerScans` table
- Add container scan tab to Angular dashboard

**Effort:** ~2 weeks  
**Impact:** Enables Docker image vulnerability scanning

---

### Phase 2 (Q3 2026) — License Scanning

```bash
# Test license scanning
osv-scanner scan source /path/to/repo --licenses --format json
```

**Code Changes:**

- Add `LicenseScanService.cs`
- Add `LicenseFindings` table
- Build license compliance rules UI
- Add license tab to dashboard

**Effort:** ~2 weeks  
**Impact:** Legal compliance, GPL detection

---

## 📊 Architecture: Current + Proposed

```
┌─────────────────────────────────────────────────────┐
│                  Vulscan Platform                   │
├─────────────────────────────────────────────────────┤
│  ✅ Lockfile Scanning      → OSV API (Current)      │
│  ✅ SBOM Scanning          → OSV API (Current)      │
│  🆕 Container Scanning     → OSV Scanner CLI (New)  │
│  🆕 OS Package Scanning    → OSV Scanner CLI (New)  │
│  🆕 License Scanning       → OSV Scanner CLI (New)  │
└─────────────────────────────────────────────────────┘
                         ↓
          ┌──────────────────────────────┐
          │      OSV.dev Infrastructure  │
          │  ━━━━━━━━━━━━━━━━━━━━━━━━━━ │
          │  • 100K+ vulnerabilities     │
          │  • 30+ ecosystems            │
          │  • Free, no rate limits      │
          │  • Real-time updates         │
          └──────────────────────────────┘
```

---

## 🏆 Our Competitive Advantages

**What OSV Scanner Can't Do (But We Can):**

1. ✅ Azure DevOps native integration (PAT/Basic Auth)
2. ✅ Web dashboard with historical trends
3. ✅ Scheduled autonomous scanning
4. ✅ Email & Teams notifications
5. ✅ Multi-tenant instance management
6. ✅ Role-based access control
7. ✅ Executive summary reports
8. ✅ CSV exports for compliance

**Conclusion:** OSV.dev is our **foundation**, not our competitor.

---

## 📦 Quick Install Guide (OSV Scanner)

### Linux/macOS

```bash
# Download latest release
VERSION="v2.3.6"
OS="linux"  # or "darwin" for macOS
ARCH="amd64"

curl -L "https://github.com/google/osv-scanner/releases/download/${VERSION}/osv-scanner_${OS}_${ARCH}" \
  -o /usr/local/bin/osv-scanner

chmod +x /usr/local/bin/osv-scanner

# Verify
osv-scanner --version
```

### Windows

```powershell
# Download from releases page
Invoke-WebRequest -Uri "https://github.com/google/osv-scanner/releases/download/v2.3.6/osv-scanner_windows_amd64.exe" `
  -OutFile "C:\Program Files\osv-scanner\osv-scanner.exe"

# Verify
osv-scanner --version
```

---

## 🧪 Quick Test Commands

```bash
# Scan a directory (lockfiles)
osv-scanner scan source /path/to/repo

# Scan SBOM
osv-scanner scan sbom sbom.json

# Scan container image
osv-scanner scan image nginx:latest

# Scan with license check
osv-scanner scan source /path/to/repo --licenses

# Offline mode (after downloading DB)
osv-scanner --offline --download-offline-databases
osv-scanner --offline scan source /path/to/repo

# JSON output for parsing
osv-scanner scan source /path/to/repo --format json > results.json
```

---

## 📚 Key Resources

- **Full Analysis:** [OSV_COMPARISON_AND_RECOMMENDATIONS.md](./OSV_COMPARISON_AND_RECOMMENDATIONS.md)
- **CVE Integration Guide:** [CVE_INTEGRATION_GUIDE.md](./CVE_INTEGRATION_GUIDE.md)
- **OSV.dev Docs:** <https://google.github.io/osv.dev/>
- **OSV Scanner Docs:** <https://google.github.io/osv-scanner/>
- **OSV API Reference:** <https://google.github.io/osv.dev/api/>

---

## ❓ FAQs

**Q: Should we replace OSV API with OSV Scanner CLI?**  
A: ❌ No. Keep using OSV API for lockfile scanning (it's faster). Use CLI only for advanced features.

**Q: Is OSV Scanner free?**  
A: ✅ Yes, completely free. Apache 2.0 license.

**Q: Does it require an API key?**  
A: ✅ No API key needed for OSV.dev API or OSV Scanner.

**Q: What about rate limits?**  
A: ✅ No rate limits on OSV.dev API (unlike NVD which has 50 req/30s).

**Q: Can we run it offline?**  
A: ✅ Yes, OSV Scanner supports offline mode with pre-downloaded database.

**Q: What's the performance impact?**  
A: ⚠️ CLI calls are slower than API (process spawn overhead). Use for features we don't have.

---

**Last Updated:** May 1, 2026  
**Version:** 1.0

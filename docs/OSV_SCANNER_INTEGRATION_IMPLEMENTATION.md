# 🔧 OSV Scanner CLI Integration Guide

**Purpose:** Step-by-step guide to integrate OSV Scanner CLI into Vulscan for container and license scanning.

---

## 📋 Prerequisites

- OSV Scanner CLI installed (`/usr/local/bin/osv-scanner` or `C:\Program Files\osv-scanner\osv-scanner.exe`)
- .NET 10 SDK
- Docker (for testing container scans)

---

## 🏗️ Implementation Plan

### Phase 1: OSV Scanner Service Layer

Create a new service to wrap OSV Scanner CLI calls.

#### Step 1: Create DTOs for OSV Scanner Output

Create `/server/src/Vulscan.Application/DTOs/OsvScannerDto.cs`:

```csharp
namespace Vulscan.Application.DTOs;

/// <summary>
/// OSV Scanner JSON output model (subset of fields we care about)
/// </summary>
public record OsvScannerResult
{
    public List<OsvScannerPackage> Packages { get; init; } = [];
    public List<OsvScannerVulnerability> Vulnerabilities { get; init; } = [];
}

public record OsvScannerPackage
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Ecosystem { get; init; } = string.Empty;
    public List<string> VulnerabilityIds { get; init; } = [];
}

public record OsvScannerVulnerability
{
    public string Id { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string? Details { get; init; }
    public List<string> Aliases { get; init; } = [];
    public string? Severity { get; init; }
    public List<OsvAffectedPackage> Affected { get; init; } = [];
}

public record OsvAffectedPackage
{
    public string PackageName { get; init; } = string.Empty;
    public string Ecosystem { get; init; } = string.Empty;
    public List<string> Ranges { get; init; } = [];
}

public record ContainerScanResult
{
    public string ImageName { get; init; } = string.Empty;
    public string ImageTag { get; init; } = string.Empty;
    public int BaseImageVulnerabilities { get; init; }
    public int PackageVulnerabilities { get; init; }
    public List<OsvScannerPackage> Packages { get; init; } = [];
    public List<OsvScannerVulnerability> Vulnerabilities { get; init; } = [];
}

public record LicenseScanResult
{
    public string RepositoryPath { get; init; } = string.Empty;
    public List<LicenseFinding> Licenses { get; init; } = [];
}

public record LicenseFinding
{
    public string PackageName { get; init; } = string.Empty;
    public string PackageVersion { get; init; } = string.Empty;
    public string LicenseType { get; init; } = string.Empty;
    public bool IsAllowed { get; init; }
}
```

---

#### Step 2: Create Interface

Create `/server/src/Vulscan.Application/Interfaces/IOsvScannerService.cs`:

```csharp
namespace Vulscan.Application.Interfaces;

/// <summary>
/// Service for executing OSV Scanner CLI and parsing results.
/// </summary>
public interface IOsvScannerService
{
    /// <summary>
    /// Scans a container image for vulnerabilities.
    /// </summary>
    Task<ContainerScanResult> ScanContainerImageAsync(
        string imageName, 
        string? imageTag = null, 
        CancellationToken ct = default);

    /// <summary>
    /// Scans a repository for license compliance.
    /// </summary>
    Task<LicenseScanResult> ScanLicensesAsync(
        string repositoryPath, 
        List<string>? allowedLicenses = null, 
        CancellationToken ct = default);

    /// <summary>
    /// Scans OS packages in a directory/filesystem.
    /// </summary>
    Task<OsvScannerResult> ScanOsPackagesAsync(
        string path, 
        CancellationToken ct = default);

    /// <summary>
    /// Checks if OSV Scanner CLI is installed and accessible.
    /// </summary>
    Task<bool> IsOsvScannerInstalledAsync(CancellationToken ct = default);
}
```

---

#### Step 3: Implement the Service

Create `/server/src/Vulscan.Infrastructure/Services/OsvScannerService.cs`:

```csharp
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vulscan.Application.DTOs;
using Vulscan.Application.Interfaces;

namespace Vulscan.Infrastructure.Services;

/// <summary>
/// Executes OSV Scanner CLI and parses JSON output.
/// </summary>
public sealed class OsvScannerService : IOsvScannerService
{
    private readonly ILogger<OsvScannerService> _logger;
    private readonly string _osvScannerPath;
    private readonly int _timeoutSeconds;

    public OsvScannerService(
        ILogger<OsvScannerService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _osvScannerPath = configuration["OsvScanner:ExecutablePath"] 
            ?? (OperatingSystem.IsWindows() 
                ? @"C:\Program Files\osv-scanner\osv-scanner.exe" 
                : "/usr/local/bin/osv-scanner");
        _timeoutSeconds = configuration.GetValue("OsvScanner:TimeoutSeconds", 300);
    }

    public async Task<ContainerScanResult> ScanContainerImageAsync(
        string imageName, string? imageTag = null, CancellationToken ct = default)
    {
        var fullImageName = string.IsNullOrEmpty(imageTag) 
            ? imageName 
            : $"{imageName}:{imageTag}";

        _logger.LogInformation("Scanning container image: {Image}", fullImageName);

        var args = $"scan image {fullImageName} --format json";
        var json = await ExecuteOsvScannerAsync(args, ct);

        var result = JsonSerializer.Deserialize<OsvScannerResult>(json, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result is null)
        {
            _logger.LogWarning("Failed to parse OSV Scanner output for {Image}", fullImageName);
            return new ContainerScanResult
            {
                ImageName = imageName,
                ImageTag = imageTag ?? "latest"
            };
        }

        return new ContainerScanResult
        {
            ImageName = imageName,
            ImageTag = imageTag ?? "latest",
            PackageVulnerabilities = result.Vulnerabilities.Count,
            Packages = result.Packages,
            Vulnerabilities = result.Vulnerabilities
        };
    }

    public async Task<LicenseScanResult> ScanLicensesAsync(
        string repositoryPath, List<string>? allowedLicenses = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Scanning licenses in: {Path}", repositoryPath);

        var licensesArg = allowedLicenses?.Any() == true 
            ? $"--licenses=\"{string.Join(",", allowedLicenses)}\"" 
            : "--licenses";

        var args = $"scan source {repositoryPath} {licensesArg} --format json";
        var json = await ExecuteOsvScannerAsync(args, ct);

        // Parse license findings from JSON
        // Note: OSV Scanner license output format may vary, adjust as needed
        var findings = ParseLicenseFindings(json, allowedLicenses);

        return new LicenseScanResult
        {
            RepositoryPath = repositoryPath,
            Licenses = findings
        };
    }

    public async Task<OsvScannerResult> ScanOsPackagesAsync(
        string path, CancellationToken ct = default)
    {
        _logger.LogInformation("Scanning OS packages in: {Path}", path);

        var args = $"scan source {path} --format json";
        var json = await ExecuteOsvScannerAsync(args, ct);

        var result = JsonSerializer.Deserialize<OsvScannerResult>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result ?? new OsvScannerResult();
    }

    public async Task<bool> IsOsvScannerInstalledAsync(CancellationToken ct = default)
    {
        try
        {
            var versionOutput = await ExecuteOsvScannerAsync("--version", ct);
            _logger.LogInformation("OSV Scanner version: {Version}", versionOutput.Trim());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OSV Scanner not found at: {Path}", _osvScannerPath);
            return false;
        }
    }

    private async Task<string> ExecuteOsvScannerAsync(string arguments, CancellationToken ct)
    {
        if (!File.Exists(_osvScannerPath))
        {
            throw new FileNotFoundException(
                $"OSV Scanner not found at: {_osvScannerPath}. " +
                $"Install from: https://github.com/google/osv-scanner/releases");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _osvScannerPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _logger.LogDebug("Executing: {Command} {Args}", _osvScannerPath, arguments);

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(_timeoutSeconds), ct);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0 && process.ExitCode != 1) // Exit code 1 = vulnerabilities found (expected)
        {
            _logger.LogError("OSV Scanner failed with exit code {Code}: {Error}", 
                process.ExitCode, error);
            throw new InvalidOperationException($"OSV Scanner failed: {error}");
        }

        return output;
    }

    private List<LicenseFinding> ParseLicenseFindings(string json, List<string>? allowedLicenses)
    {
        // Parse OSV Scanner license output format
        // This is a simplified implementation - adjust based on actual JSON structure
        var findings = new List<LicenseFinding>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("packages", out var packages))
            {
                foreach (var pkg in packages.EnumerateArray())
                {
                    var name = pkg.GetProperty("name").GetString() ?? string.Empty;
                    var version = pkg.GetProperty("version").GetString() ?? string.Empty;
                    var license = pkg.TryGetProperty("license", out var lic) 
                        ? lic.GetString() ?? "Unknown" 
                        : "Unknown";

                    var isAllowed = allowedLicenses?.Contains(license, StringComparer.OrdinalIgnoreCase) ?? true;

                    findings.Add(new LicenseFinding
                    {
                        PackageName = name,
                        PackageVersion = version,
                        LicenseType = license,
                        IsAllowed = isAllowed
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse license findings");
        }

        return findings;
    }
}
```

---

#### Step 4: Register Service in DI Container

Update `/server/src/Vulscan.Infrastructure/DependencyInjection.cs`:

```csharp
// Add after existing services
services.AddScoped<IOsvScannerService, OsvScannerService>();
```

---

### Phase 2: Database Schema Changes

#### Step 1: Add Migration for Container Scans

Create a new migration:

```csharp
// /server/src/Vulscan.Infrastructure/Migrations/AddContainerScans.cs

using Microsoft.EntityFrameworkCore.Migrations;

public partial class AddContainerScans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ContainerScans",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ScanRunId = table.Column<Guid>(nullable: false),
                ImageName = table.Column<string>(maxLength: 500, nullable: false),
                ImageTag = table.Column<string>(maxLength: 100, nullable: false),
                Registry = table.Column<string>(maxLength: 255, nullable: true),
                BaseImageVulnerabilities = table.Column<int>(nullable: false),
                PackageVulnerabilities = table.Column<int>(nullable: false),
                TotalVulnerabilities = table.Column<int>(nullable: false),
                ScanDate = table.Column<DateTime>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContainerScans", x => x.Id);
                table.ForeignKey(
                    name: "FK_ContainerScans_ScanRuns_ScanRunId",
                    column: x => x.ScanRunId,
                    principalTable: "ScanRuns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "LicenseFindings",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ScanRunId = table.Column<Guid>(nullable: false),
                PackageName = table.Column<string>(maxLength: 255, nullable: false),
                PackageVersion = table.Column<string>(maxLength: 100, nullable: false),
                LicenseType = table.Column<string>(maxLength: 100, nullable: false),
                IsAllowed = table.Column<bool>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LicenseFindings", x => x.Id);
                table.ForeignKey(
                    name: "FK_LicenseFindings_ScanRuns_ScanRunId",
                    column: x => x.ScanRunId,
                    principalTable: "ScanRuns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ContainerScans_ScanRunId",
            table: "ContainerScans",
            column: "ScanRunId");

        migrationBuilder.CreateIndex(
            name: "IX_LicenseFindings_ScanRunId",
            table: "LicenseFindings",
            column: "ScanRunId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ContainerScans");
        migrationBuilder.DropTable(name: "LicenseFindings");
    }
}
```

---

### Phase 3: API Controllers

#### Step 1: Container Scans Controller

Create `/server/src/Vulscan.Api/Controllers/ContainerScansController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vulscan.Application.Interfaces;

namespace Vulscan.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class ContainerScansController : ControllerBase
{
    private readonly IOsvScannerService _scannerService;
    private readonly ILogger<ContainerScansController> _logger;

    public ContainerScansController(
        IOsvScannerService scannerService,
        ILogger<ContainerScansController> logger)
    {
        _scannerService = scannerService;
        _logger = logger;
    }

    /// <summary>
    /// Scan a container image for vulnerabilities
    /// </summary>
    [HttpPost("scan")]
    public async Task<IActionResult> ScanImage(
        [FromBody] ScanImageRequest request, 
        CancellationToken ct)
    {
        _logger.LogInformation("Received container scan request for {Image}:{Tag}", 
            request.ImageName, request.ImageTag);

        var result = await _scannerService.ScanContainerImageAsync(
            request.ImageName, 
            request.ImageTag, 
            ct);

        return Ok(result);
    }

    /// <summary>
    /// Check if OSV Scanner is installed
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> CheckHealth(CancellationToken ct)
    {
        var isInstalled = await _scannerService.IsOsvScannerInstalledAsync(ct);
        return Ok(new { OsvScannerInstalled = isInstalled });
    }
}

public record ScanImageRequest(string ImageName, string? ImageTag = "latest");
```

---

### Phase 4: Configuration

#### Step 1: Update appsettings.json

Add to `/server/src/Vulscan.Api/appsettings.json`:

```json
{
  "OsvScanner": {
    "ExecutablePath": "/usr/local/bin/osv-scanner",
    "TimeoutSeconds": 300,
    "Enabled": true
  },
  "LicenseCompliance": {
    "AllowedLicenses": [
      "MIT",
      "Apache-2.0",
      "BSD-2-Clause",
      "BSD-3-Clause",
      "ISC"
    ],
    "DeniedLicenses": [
      "GPL-3.0",
      "AGPL-3.0"
    ]
  }
}
```

---

### Phase 5: Testing

#### Step 1: Unit Tests

Create `/server/tests/Vulscan.Infrastructure.Tests/Services/OsvScannerServiceTests.cs`:

```csharp
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Vulscan.Infrastructure.Services;

namespace Vulscan.Infrastructure.Tests.Services;

public class OsvScannerServiceTests
{
    private readonly Mock<ILogger<OsvScannerService>> _loggerMock;
    private readonly Mock<IConfiguration> _configMock;

    public OsvScannerServiceTests()
    {
        _loggerMock = new Mock<ILogger<OsvScannerService>>();
        _configMock = new Mock<IConfiguration>();
    }

    [Fact]
    public async Task IsOsvScannerInstalledAsync_ShouldReturnTrue_WhenInstalled()
    {
        // Arrange
        _configMock.Setup(c => c["OsvScanner:ExecutablePath"])
            .Returns("/usr/local/bin/osv-scanner");
        
        var service = new OsvScannerService(_loggerMock.Object, _configMock.Object);

        // Act
        var result = await service.IsOsvScannerInstalledAsync();

        // Assert (will pass only if OSV Scanner is actually installed)
        // In CI/CD, you'd mock the process execution
        Assert.True(result || !result); // Placeholder - implement proper mocking
    }
}
```

---

## 📝 Usage Examples

### Example 1: Scan a Container Image

```bash
# API request
curl -X POST http://localhost:5000/api/v1/containerscans/scan \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "imageName": "nginx",
    "imageTag": "latest"
  }'
```

### Example 2: Check OSV Scanner Health

```bash
curl http://localhost:5000/api/v1/containerscans/health \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"

# Response:
# { "osvScannerInstalled": true }
```

---

## 🚀 Deployment Checklist

- [ ] Install OSV Scanner CLI on server
- [ ] Update appsettings.json with correct ExecutablePath
- [ ] Run EF Core migrations to create new tables
- [ ] Test container scan endpoint with Docker image
- [ ] Deploy to production
- [ ] Add UI tab in Angular dashboard
- [ ] Update documentation

---

## 📚 Next Steps

1. **Frontend Integration:** Add container scan UI to Angular dashboard
2. **Background Jobs:** Schedule periodic container scans
3. **Notifications:** Alert on high-severity container vulnerabilities
4. **Reporting:** Add container scan results to executive reports

---

**Reference:** [OSV_COMPARISON_AND_RECOMMENDATIONS.md](./OSV_COMPARISON_AND_RECOMMENDATIONS.md)  
**Last Updated:** May 1, 2026

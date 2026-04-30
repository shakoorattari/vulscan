using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Vulscan.Application.DTOs.Common;
using Vulscan.Domain.Entities;
using Vulscan.Infrastructure.Data;

namespace Vulscan.Api.Controllers;

/// <summary>
/// API for accessing scan results, packages, and SBOM exports.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class PackagesController(VulscanDbContext db, ILogger<PackagesController> logger) : ControllerBase
{
    /// <summary>
    /// Get all packages discovered in a scan run.
    /// </summary>
    [HttpGet("scan/{scanRunId:guid}")]
    public async Task<IActionResult> GetPackagesByScan(Guid scanRunId, [FromQuery] string? ecosystem = null)
    {
        logger.LogInformation("Fetching packages for scan {ScanRunId}", scanRunId);

        var query = db.DiscoveredPackages
            .Where(p => p.ScanRunId == scanRunId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(ecosystem))
        {
            query = query.Where(p => p.Ecosystem == ecosystem);
        }

        var packages = await query
            .OrderBy(p => p.Ecosystem)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Ecosystem,
                p.Name,
                p.Version,
                p.SourceFile,
                p.IsDirect,
                p.HasVulnerabilities,
                p.License,
                p.Purl
            })
            .ToListAsync();

        return Ok(new
        {
            scanRunId,
            totalCount = packages.Count,
            ecosystems = packages.GroupBy(p => p.Ecosystem)
                .Select(g => new { ecosystem = g.Key, count = g.Count() }),
            packages
        });
    }

    /// <summary>
    /// Get packages by repository.
    /// </summary>
    [HttpGet("repository/{repositoryId:guid}")]
    public async Task<IActionResult> GetPackagesByRepository(Guid repositoryId, [FromQuery] Guid? scanRunId = null)
    {
        var query = db.DiscoveredPackages
            .Where(p => p.RepositoryId == repositoryId)
            .AsQueryable();

        if (scanRunId.HasValue)
        {
            query = query.Where(p => p.ScanRunId == scanRunId.Value);
        }
        else
        {
            // Get latest scan only
            var latestScanId = await db.ScanRuns
                .Where(s => s.Sboms.Any(sb => sb.RepositoryId == repositoryId))
                .OrderByDescending(s => s.StartedAt)
                .Select(s => s.Id)
                .FirstOrDefaultAsync();

            if (latestScanId != Guid.Empty)
            {
                query = query.Where(p => p.ScanRunId == latestScanId);
            }
        }

        var packages = await query
            .OrderBy(p => p.Ecosystem)
            .ThenBy(p => p.Name)
            .ToListAsync();

        return Ok(packages);
    }

    /// <summary>
    /// Get vulnerable packages only.
    /// </summary>
    [HttpGet("vulnerable")]
    public async Task<IActionResult> GetVulnerablePackages([FromQuery] Guid? scanRunId = null)
    {
        var query = db.DiscoveredPackages
            .Where(p => p.HasVulnerabilities)
            .AsQueryable();

        if (scanRunId.HasValue)
        {
            query = query.Where(p => p.ScanRunId == scanRunId.Value);
        }

        var packages = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .Select(p => new
            {
                p.Id,
                p.ScanRunId,
                p.RepositoryId,
                p.Ecosystem,
                p.Name,
                p.Version,
                p.SourceFile,
                p.Purl
            })
            .ToListAsync();

        return Ok(packages);
    }

    /// <summary>
    /// Get packages enriched with vulnerability severity breakdown, repo and project names.
    /// Supports filtering by scanRunId, repositoryId, projectId, ecosystem, and hasVulnerabilities.
    /// </summary>
    [HttpGet("inventory")]
    public async Task<IActionResult> GetPackageInventory(
        [FromQuery] Guid? scanRunId = null,
        [FromQuery] Guid? repositoryId = null,
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? ecosystem = null,
        [FromQuery] bool? hasVulnerabilities = null)
    {
        // If no scanRunId provided, default to the latest scan run.
        if (!scanRunId.HasValue && !repositoryId.HasValue && !projectId.HasValue)
        {
            scanRunId = await db.ScanRuns
                .OrderByDescending(s => s.StartedAt)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync();
        }

        var query = db.DiscoveredPackages
            .Include(p => p.Repository)
                .ThenInclude(r => r.Project)
            .AsQueryable();

        if (scanRunId.HasValue) query = query.Where(p => p.ScanRunId == scanRunId.Value);
        if (repositoryId.HasValue) query = query.Where(p => p.RepositoryId == repositoryId.Value);
        if (projectId.HasValue) query = query.Where(p => p.Repository.ProjectId == projectId.Value);
        if (!string.IsNullOrWhiteSpace(ecosystem)) query = query.Where(p => p.Ecosystem == ecosystem);
        if (hasVulnerabilities.HasValue) query = query.Where(p => p.HasVulnerabilities == hasVulnerabilities.Value);

        var packages = await query
            .OrderBy(p => p.Ecosystem)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.ScanRunId,
                p.RepositoryId,
                ProjectId = p.Repository.ProjectId,
                ProjectName = p.Repository.Project.Name,
                RepositoryName = p.Repository.Name,
                p.Ecosystem,
                p.Name,
                p.Version,
                p.SourceFile,
                p.IsDirect,
                p.HasVulnerabilities,
                p.License,
                p.Purl
            })
            .ToListAsync();

        // Look up vulnerability severity breakdown per (ScanRunId, RepositoryId, PackageName, InstalledVersion).
        var scanIds = packages.Select(p => p.ScanRunId).Distinct().ToList();
        var vulns = await db.Vulnerabilities
            .Where(v => scanIds.Contains(v.ScanRunId))
            .Select(v => new
            {
                v.ScanRunId,
                v.RepositoryId,
                v.PackageName,
                v.InstalledVersion,
                v.CveId,
                v.Severity,
                v.CvssScore,
                v.FixedVersion
            })
            .ToListAsync();

        var vulnLookup = vulns
            .GroupBy(v => (v.ScanRunId, v.RepositoryId, v.PackageName, v.InstalledVersion))
            .ToDictionary(g => g.Key, g => g.ToList());

        var enriched = packages.Select(p =>
        {
            vulnLookup.TryGetValue((p.ScanRunId, p.RepositoryId, p.Name, p.Version), out var pkgVulns);
            pkgVulns ??= [];
            return new
            {
                p.Id,
                p.ScanRunId,
                p.RepositoryId,
                p.ProjectId,
                p.ProjectName,
                p.RepositoryName,
                p.Ecosystem,
                p.Name,
                p.Version,
                p.SourceFile,
                p.IsDirect,
                p.HasVulnerabilities,
                p.License,
                p.Purl,
                CriticalCount = pkgVulns.Count(v => v.Severity == Vulscan.Domain.Enums.VulnerabilitySeverity.Critical),
                HighCount = pkgVulns.Count(v => v.Severity == Vulscan.Domain.Enums.VulnerabilitySeverity.High),
                MediumCount = pkgVulns.Count(v => v.Severity == Vulscan.Domain.Enums.VulnerabilitySeverity.Medium),
                LowCount = pkgVulns.Count(v => v.Severity == Vulscan.Domain.Enums.VulnerabilitySeverity.Low),
                TotalVulnerabilities = pkgVulns.Count,
                Vulnerabilities = pkgVulns.Select(v => new
                {
                    v.CveId,
                    Severity = v.Severity.ToString(),
                    v.CvssScore,
                    v.FixedVersion
                }).ToList()
            };
        }).ToList();

        var ecosystemGroups = enriched
            .GroupBy(p => p.Ecosystem)
            .Select(g => new
            {
                ecosystem = g.Key,
                totalPackages = g.Count(),
                vulnerablePackages = g.Count(p => p.HasVulnerabilities),
                criticalCount = g.Sum(p => p.CriticalCount),
                highCount = g.Sum(p => p.HighCount),
                mediumCount = g.Sum(p => p.MediumCount),
                lowCount = g.Sum(p => p.LowCount)
            })
            .OrderBy(g => g.ecosystem)
            .ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            scanRunId,
            repositoryId,
            projectId,
            totalPackages = enriched.Count,
            vulnerablePackages = enriched.Count(p => p.HasVulnerabilities),
            ecosystems = ecosystemGroups,
            packages = enriched
        }));
    }

    /// <summary>
    /// Get package statistics across all scans.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetPackageStats()
    {
        var stats = await db.DiscoveredPackages
            .GroupBy(p => p.Ecosystem)
            .Select(g => new
            {
                Ecosystem = g.Key,
                TotalPackages = g.Count(),
                UniquePackages = g.Select(p => p.Name).Distinct().Count(),
                VulnerablePackages = g.Count(p => p.HasVulnerabilities)
            })
            .ToListAsync();

        var totalScans = await db.ScanRuns.CountAsync();
        var totalPackages = await db.DiscoveredPackages.CountAsync();
        var vulnerablePackages = await db.DiscoveredPackages.CountAsync(p => p.HasVulnerabilities);

        return Ok(new
        {
            totalScans,
            totalPackages,
            vulnerablePackages,
            byEcosystem = stats
        });
    }

    /// <summary>
    /// Export SBOM for a scan run in CycloneDX JSON format.
    /// </summary>
    [HttpGet("scan/{scanRunId:guid}/sbom")]
    public async Task<IActionResult> ExportSbom(Guid scanRunId)
    {
        logger.LogInformation("Exporting SBOM for scan {ScanRunId}", scanRunId);

        var sboms = await db.Sboms
            .Where(s => s.ScanRunId == scanRunId)
            .Include(s => s.Repository)
            .ToListAsync();

        if (sboms.Count == 0)
        {
            return NotFound(new { message = "No SBOM data found for this scan" });
        }

        // Combine all SBOMs into one response
        var packages = await db.DiscoveredPackages
            .Where(p => p.ScanRunId == scanRunId)
            .ToListAsync();

        var combinedSbom = new
        {
            bomFormat = "CycloneDX",
            specVersion = "1.5",
            serialNumber = $"urn:uuid:{Guid.NewGuid()}",
            version = 1,
            metadata = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                tools = new[] { new { vendor = "Vulscan", name = "VulnerabilityScanner", version = "1.0.0" } }
            },
            components = packages.Select(p => new
            {
                type = "library",
                name = p.Name,
                version = p.Version,
                purl = p.Purl,
                properties = new[]
                {
                    new { name = "ecosystem", value = p.Ecosystem },
                    new { name = "sourceFile", value = p.SourceFile },
                    new { name = "hasVulnerabilities", value = p.HasVulnerabilities.ToString() }
                }
            })
        };

        return Ok(combinedSbom);
    }

    /// <summary>
    /// Download SBOM as file.
    /// </summary>
    [HttpGet("scan/{scanRunId:guid}/sbom/download")]
    public async Task<IActionResult> DownloadSbom(Guid scanRunId, [FromQuery] string format = "json")
    {
        logger.LogInformation("Downloading SBOM for scan {ScanRunId} (format: {Format})", scanRunId, format);

        var packages = await db.DiscoveredPackages
            .Where(p => p.ScanRunId == scanRunId)
            .ToListAsync();

        if (packages.Count == 0)
        {
            return NotFound(new { message = "No packages found for this scan" });
        }

        var sbom = new
        {
            bomFormat = "CycloneDX",
            specVersion = "1.5",
            serialNumber = $"urn:uuid:{Guid.NewGuid()}",
            version = 1,
            metadata = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                tools = new[] { new { vendor = "Vulscan", name = "VulnerabilityScanner", version = "1.0.0" } }
            },
            components = packages.Select(p => new
            {
                type = "library",
                name = p.Name,
                version = p.Version,
                purl = p.Purl,
                properties = new[]
                {
                    new { name = "ecosystem", value = p.Ecosystem },
                    new { name = "sourceFile", value = p.SourceFile }
                }
            })
        };

        var json = System.Text.Json.JsonSerializer.Serialize(sbom,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);

        return File(bytes, "application/json", $"sbom-scan-{scanRunId}.json");
    }

    /// <summary>
    /// Export packages as CSV.
    /// </summary>
    [HttpGet("scan/{scanRunId:guid}/csv")]
    public async Task<IActionResult> ExportPackagesCsv(Guid scanRunId)
    {
        logger.LogInformation("Exporting packages CSV for scan {ScanRunId}", scanRunId);

        var packages = await db.DiscoveredPackages
            .Where(p => p.ScanRunId == scanRunId)
            .OrderBy(p => p.Ecosystem)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Ecosystem,Name,Version,SourceFile,IsDirect,HasVulnerabilities,License,PURL");

        foreach (var p in packages)
        {
            csv.AppendLine($"{p.Ecosystem},{EscapeCsv(p.Name)},{p.Version},{EscapeCsv(p.SourceFile)},{p.IsDirect},{p.HasVulnerabilities},{p.License ?? ""},{p.Purl ?? ""}");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"packages-scan-{scanRunId}.csv");
    }

    /// <summary>
    /// Get detailed scan results with packages grouped by repository.
    /// </summary>
    [HttpGet("scan/{scanRunId:guid}/details")]
    public async Task<IActionResult> GetScanDetails(Guid scanRunId)
    {
        var scanRun = await db.ScanRuns
            .Include(s => s.Instance)
            .FirstOrDefaultAsync(s => s.Id == scanRunId);

        if (scanRun == null)
        {
            return NotFound();
        }

        var sboms = await db.Sboms
            .Where(s => s.ScanRunId == scanRunId)
            .Include(s => s.Repository)
            .ToListAsync();

        var packages = await db.DiscoveredPackages
            .Where(p => p.ScanRunId == scanRunId)
            .ToListAsync();

        var vulnerabilities = await db.Vulnerabilities
            .Where(v => v.ScanRunId == scanRunId)
            .ToListAsync();

        var result = new
        {
            scan = new
            {
                scanRun.Id,
                scanRun.Status,
                scanRun.StartedAt,
                scanRun.CompletedAt,
                scanRun.DurationSeconds,
                scanRun.ReposScanned,
                scanRun.ReposFailed,
                scanRun.TotalVulnerabilities,
                scanRun.CriticalCount,
                scanRun.HighCount,
                scanRun.MediumCount,
                scanRun.LowCount,
                scanRun.ErrorLog,
                instanceName = scanRun.Instance?.Name
            },
            summary = new
            {
                totalPackages = packages.Count,
                totalVulnerabilities = vulnerabilities.Count,
                ecosystems = packages.GroupBy(p => p.Ecosystem)
                    .Select(g => new { ecosystem = g.Key, count = g.Count() })
            },
            repositories = sboms.Select(sbom => new
            {
                repositoryId = sbom.RepositoryId,
                repositoryName = sbom.Repository?.Name,
                sbomId = sbom.Id,
                componentCount = sbom.ComponentCount,
                packages = packages
                    .Where(p => p.SbomId == sbom.Id)
                    .Select(p => new
                    {
                        p.Ecosystem,
                        p.Name,
                        p.Version,
                        p.SourceFile,
                        p.HasVulnerabilities,
                        p.Purl
                    }),
                vulnerabilities = vulnerabilities
                    .Where(v => v.SbomId == sbom.Id)
                    .Select(v => new
                    {
                        v.CveId,
                        v.PackageName,
                        v.InstalledVersion,
                        v.FixedVersion,
                        v.Severity,
                        v.CvssScore,
                        v.Description
                    })
            })
        };

        return Ok(result);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}

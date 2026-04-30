using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Application.Services;

/// <summary>
/// Processes scan runs - connects to Azure DevOps, fetches repos, scans dependencies.
/// Stores all discovered packages and generates SBOMs.
/// </summary>
public sealed class ScanProcessor(
    DbContext dbContext,
    IAzureDevOpsClient azureDevOpsClient,
    IDependencyScanner dependencyScanner,
    ILogger<ScanProcessor> logger) : IScanProcessor
{
    // Dependency file patterns to scan
    private static readonly string[] DependencyFilePatterns =
    [
        "package.json", "package-lock.json",
        "requirements.txt", "Pipfile", "pyproject.toml",
        "*.csproj", "packages.config",
        "go.mod", "Cargo.toml",
        "composer.json", "Gemfile"
    ];

    public async Task ProcessScanAsync(Guid scanRunId, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("═══════════════════════════════════════════════════════════════");
        logger.LogInformation("🚀 STARTING SCAN #{ScanRunId}", scanRunId);
        logger.LogInformation("═══════════════════════════════════════════════════════════════");

        var scanRun = await dbContext.Set<ScanRun>()
            .Include(s => s.Instance)
                .ThenInclude(i => i!.Projects)
            .FirstOrDefaultAsync(s => s.Id == scanRunId, ct);

        if (scanRun == null)
        {
            logger.LogError("❌ Scan run #{ScanRunId} not found in database", scanRunId);
            return;
        }

        if (scanRun.Instance == null)
        {
            await FailScanAsync(scanRun, "Azure DevOps instance not configured", ct);
            return;
        }

        logger.LogInformation("📋 Instance: {Name}", scanRun.Instance.Name);
        logger.LogInformation("📋 URL: {Url}/{Collection}", scanRun.Instance.Url, scanRun.Instance.Collection);

        try
        {
            // Update status to Running
            scanRun.Status = ScanStatus.Running;
            scanRun.StartedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            // Parse credentials
            var creds = ParseCredentials(scanRun.Instance.CredentialReference);
            if (string.IsNullOrEmpty(creds.Username) || string.IsNullOrEmpty(creds.Password))
            {
                await FailScanAsync(scanRun, "Invalid credentials - username or password missing", ct);
                return;
            }

            logger.LogInformation("🔑 Authenticating as: {Username}", creds.Username);

            var baseUrl = scanRun.Instance.Url;
            var collection = scanRun.Instance.Collection;

            // Test connection
            logger.LogInformation("🔗 Testing connection to Azure DevOps...");
            var (connected, connectionMessage) = await azureDevOpsClient.TestConnectionAsync(
                baseUrl, collection, creds.Username, creds.Password, ct);

            if (!connected)
            {
                logger.LogError("❌ Connection failed: {Message}", connectionMessage);
                await FailScanAsync(scanRun, $"Connection failed: {connectionMessage}", ct);
                return;
            }

            logger.LogInformation("✅ Connection successful: {Message}", connectionMessage);

            // Fetch ALL projects from Azure DevOps
            logger.LogInformation("📋 Fetching projects from Azure DevOps...");
            var azureProjects = await azureDevOpsClient.GetProjectsAsync(
                baseUrl, collection, creds.Username, creds.Password, ct);

            logger.LogInformation("📋 Found {Count} projects in Azure DevOps", azureProjects.Count);

            if (azureProjects.Count == 0)
            {
                await CompleteScanAsync(scanRun, 0, 0, 0, 0, 0, 0, 0,
                    "No projects found in Azure DevOps collection", ct);
                return;
            }

            var scanStats = new ScanStats();

            // Process each Azure DevOps project
            foreach (var azureProject in azureProjects)
            {
                logger.LogInformation("═══════════════════════════════════════════════════════════════");
                logger.LogInformation("📂 PROJECT: {ProjectName}", azureProject.Name);
                logger.LogInformation("═══════════════════════════════════════════════════════════════");

                // Ensure project entity exists in database
                var projectEntity = await dbContext.Set<Project>()
                    .FirstOrDefaultAsync(p => p.InstanceId == scanRun.Instance.Id && p.AzureProjectId == azureProject.Id, ct);

                if (projectEntity == null)
                {
                    projectEntity = new Project
                    {
                        InstanceId = scanRun.Instance.Id,
                        Name = azureProject.Name,
                        AzureProjectId = azureProject.Id,
                        DiscoveredAt = DateTime.UtcNow
                    };
                    dbContext.Set<Project>().Add(projectEntity);
                    await dbContext.SaveChangesAsync(ct);
                    logger.LogInformation("   ✨ Created new project record (ID: {Id})", projectEntity.Id);
                }

                // Fetch repositories for this project
                var repos = await azureDevOpsClient.GetRepositoriesAsync(
                    baseUrl, collection, azureProject.Name, creds.Username, creds.Password, ct);

                logger.LogInformation("   📁 Found {Count} repositories", repos.Count);

                if (repos.Count == 0)
                {
                    continue;
                }

                // Log all repos
                foreach (var r in repos)
                {
                    logger.LogDebug("      📁 {Name} (branch: {Branch}, size: {Size}KB)",
                        r.Name, r.DefaultBranch, r.Size / 1024);
                }

                // Process each repository
                foreach (var repo in repos)
                {
                    try
                    {
                        await ProcessRepositoryAsync(
                            baseUrl, collection, azureProject.Name, repo, creds,
                            scanRun, projectEntity, scanStats, ct);
                    }
                    catch (Exception ex)
                    {
                        scanStats.ReposFailed++;
                        logger.LogError(ex, "❌ Failed to scan repository: {Repo}", repo.Name);
                    }
                }
            }

            // Complete the scan
            await CompleteScanAsync(scanRun,
                scanStats.ReposScanned, scanStats.ReposFailed,
                scanStats.TotalVulnerabilities, scanStats.Critical,
                scanStats.High, scanStats.Medium, scanStats.Low,
                null, ct);

            logger.LogInformation("═══════════════════════════════════════════════════════════════");
            logger.LogInformation("✅ SCAN #{ScanRunId} COMPLETED in {Duration}ms", scanRunId, stopwatch.ElapsedMilliseconds);
            logger.LogInformation("   📊 Projects: {Count} scanned", azureProjects.Count);
            logger.LogInformation("   📊 Repos: {Scanned} scanned, {Failed} failed", scanStats.ReposScanned, scanStats.ReposFailed);
            logger.LogInformation("   📦 Packages: {Count} discovered", scanStats.TotalPackages);
            logger.LogInformation("   🔴 Vulnerabilities: {Total} (C:{Critical} H:{High} M:{Medium} L:{Low})",
                scanStats.TotalVulnerabilities, scanStats.Critical, scanStats.High, scanStats.Medium, scanStats.Low);
            logger.LogInformation("═══════════════════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Scan #{ScanRunId} failed with exception", scanRunId);
            await FailScanAsync(scanRun, ex.Message, ct);
        }
    }

    private async Task ProcessRepositoryAsync(
        string baseUrl, string collection, string project,
        AzureDevOpsRepo repo,
        (string Username, string Password, string Branch) creds,
        ScanRun scanRun, Project projectEntity,
        ScanStats stats, CancellationToken ct)
    {
        logger.LogInformation("───────────────────────────────────────────────────────────────");
        logger.LogInformation("📁 SCANNING REPOSITORY: {RepoName}", repo.Name);
        logger.LogInformation("───────────────────────────────────────────────────────────────");

        var branch = string.IsNullOrEmpty(creds.Branch) || creds.Branch == "main" || creds.Branch == "master"
            ? repo.DefaultBranch
            : creds.Branch;

        logger.LogInformation("   🔀 Branch: {Branch}", branch);

        // Ensure repository entity exists
        var repoEntity = await dbContext.Set<Repository>()
            .FirstOrDefaultAsync(r => r.ProjectId == projectEntity.Id && r.Name == repo.Name, ct);

        if (repoEntity == null)
        {
            repoEntity = new Repository
            {
                ProjectId = projectEntity.Id,
                Name = repo.Name,
                CloneUrl = repo.RemoteUrl,
                DefaultBranch = repo.DefaultBranch,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Set<Repository>().Add(repoEntity);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("   ✨ Created new repository record (ID: {Id})", repoEntity.Id);
        }

        // Get all items in repository
        logger.LogInformation("   📃 Fetching file list...");
        var items = await azureDevOpsClient.GetItemsAsync(
            baseUrl, collection, project, repo.Name, "/", branch,
            creds.Username, creds.Password, ct);

        logger.LogInformation("   📃 Found {Count} files/folders", items.Count);

        // Find dependency files
        var depFiles = items
            .Where(i => i.GitObjectType == "blob" && IsDependencyFile(Path.GetFileName(i.Path)))
            .ToList();

        if (depFiles.Count == 0)
        {
            logger.LogInformation("   ⚠️ No dependency files found in repository");
            stats.ReposScanned++;
            repoEntity.LastScannedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        logger.LogInformation("   📋 Found {Count} dependency files to scan:", depFiles.Count);
        foreach (var f in depFiles)
        {
            logger.LogInformation("      • {Path}", f.Path);
        }

        // Process each dependency file
        int repoPackageCount = 0;
        int repoVulnCount = 0;

        foreach (var file in depFiles)
        {
            try
            {
                var fileName = Path.GetFileName(file.Path);
                logger.LogInformation("   📄 Processing: {Path}", file.Path);

                // Fetch file content
                var content = await azureDevOpsClient.GetFileContentAsync(
                    baseUrl, collection, project, repo.Name, file.Path, branch,
                    creds.Username, creds.Password, ct);

                if (string.IsNullOrEmpty(content))
                {
                    logger.LogWarning("      ⚠️ Empty file content");
                    continue;
                }

                logger.LogDebug("      📝 Content length: {Length} bytes", content.Length);

                // Create SBOM record
                var sbom = new Sbom
                {
                    RepositoryId = repoEntity.Id,
                    ScanRunId = scanRun.Id,
                    Format = "CycloneDX",
                    Generator = "Vulscan",
                    GeneratedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.Set<Sbom>().Add(sbom);
                await dbContext.SaveChangesAsync(ct);

                // Scan dependencies
                var result = await dependencyScanner.ScanDependenciesAsync(
                    fileName, file.Path, content, scanRun.Id, repoEntity.Id, sbom.Id, ct);

                // Update SBOM with results
                sbom.ComponentCount = result.Packages.Count;
                sbom.SbomJson = result.SbomJson;
                sbom.GenerationDurationMs = 0;

                // Save packages
                if (result.Packages.Count > 0)
                {
                    foreach (var pkg in result.Packages)
                    {
                        pkg.SbomId = sbom.Id;
                    }
                    dbContext.Set<DiscoveredPackage>().AddRange(result.Packages);
                    repoPackageCount += result.Packages.Count;
                    stats.TotalPackages += result.Packages.Count;
                    logger.LogInformation("      ✅ Found {Count} packages ({Ecosystem})",
                        result.Packages.Count, result.Ecosystem);
                }

                // Save vulnerabilities
                if (result.Vulnerabilities.Count > 0)
                {
                    foreach (var vuln in result.Vulnerabilities)
                    {
                        vuln.SbomId = sbom.Id;
                    }
                    dbContext.Set<Vulnerability>().AddRange(result.Vulnerabilities);

                    repoVulnCount += result.Vulnerabilities.Count;
                    stats.TotalVulnerabilities += result.Vulnerabilities.Count;
                    stats.Critical += result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Critical);
                    stats.High += result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.High);
                    stats.Medium += result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Medium);
                    stats.Low += result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Low);

                    logger.LogWarning("      🔴 Found {Count} vulnerabilities", result.Vulnerabilities.Count);
                }

                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "      ❌ Failed to process: {Path}", file.Path);
            }
        }

        // Update repository last scanned
        repoEntity.LastScannedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        stats.ReposScanned++;

        logger.LogInformation("   📊 Repository summary: {Packages} packages, {Vulns} vulnerabilities",
            repoPackageCount, repoVulnCount);
    }

    private static bool IsDependencyFile(string fileName)
    {
        foreach (var pattern in DependencyFilePatterns)
        {
            if (pattern.StartsWith('*'))
            {
                if (fileName.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private async Task CompleteScanAsync(
        ScanRun scanRun, int reposScanned, int reposFailed,
        int totalVulns, int critical, int high, int medium, int low,
        string? errorLog, CancellationToken ct)
    {
        scanRun.Status = ScanStatus.Completed;
        scanRun.CompletedAt = DateTime.UtcNow;
        scanRun.DurationSeconds = (int)(DateTime.UtcNow - scanRun.StartedAt).TotalSeconds;
        scanRun.ReposScanned = reposScanned;
        scanRun.ReposFailed = reposFailed;
        scanRun.TotalVulnerabilities = totalVulns;
        scanRun.CriticalCount = critical;
        scanRun.HighCount = high;
        scanRun.MediumCount = medium;
        scanRun.LowCount = low;
        scanRun.ErrorLog = errorLog;
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task FailScanAsync(ScanRun scanRun, string error, CancellationToken ct)
    {
        scanRun.Status = ScanStatus.Failed;
        scanRun.CompletedAt = DateTime.UtcNow;
        scanRun.DurationSeconds = scanRun.StartedAt != default
            ? (int)(DateTime.UtcNow - scanRun.StartedAt).TotalSeconds
            : 0;
        scanRun.ErrorLog = error;
        await dbContext.SaveChangesAsync(ct);

        logger.LogError("═══════════════════════════════════════════════════════════════");
        logger.LogError("❌ SCAN #{ScanRunId} FAILED: {Error}", scanRun.Id, error);
        logger.LogError("═══════════════════════════════════════════════════════════════");
    }

    private static (string Username, string Password, string Branch) ParseCredentials(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return (
                root.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "",
                root.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "",
                root.TryGetProperty("branch", out var b) ? b.GetString() ?? "main" : "main"
            );
        }
        catch
        {
            return ("", "", "main");
        }
    }

    private class ScanStats
    {
        public int ReposScanned { get; set; }
        public int ReposFailed { get; set; }
        public int TotalPackages { get; set; }
        public int TotalVulnerabilities { get; set; }
        public int Critical { get; set; }
        public int High { get; set; }
        public int Medium { get; set; }
        public int Low { get; set; }
    }
}

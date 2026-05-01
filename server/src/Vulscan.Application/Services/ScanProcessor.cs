using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Application.Services;

/// <summary>
/// Project-scoped scan processor. Loads the target Project, picks credentials
/// (project-specific or inherited from instance), enumerates only that project's
/// repositories, scans dependency files and persists SBOMs + vulnerabilities.
/// </summary>
public sealed class ScanProcessor(
    DbContext dbContext,
    IAzureDevOpsClient azureDevOpsClient,
    IDependencyScanner dependencyScanner,
    IEmailService emailService,
    ILogger<ScanProcessor> logger) : IScanProcessor
{
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
            .Include(s => s.Project)
                .ThenInclude(p => p.Instance)
            .FirstOrDefaultAsync(s => s.Id == scanRunId, ct);

        if (scanRun == null)
        {
            logger.LogError("❌ Scan run #{ScanRunId} not found", scanRunId);
            return;
        }

        if (scanRun.Project == null || scanRun.Project.Instance == null)
        {
            await FailScanAsync(scanRun, "Project or parent instance missing", ct);
            return;
        }

        var project = scanRun.Project;
        var instance = project.Instance;

        logger.LogInformation("📂 Project: {ProjectName}  ({Url})", project.Name, project.Url);
        logger.LogInformation("📋 Server : {Url}/{Collection}", instance.Url, instance.Collection);

        try
        {
            scanRun.Status = ScanStatus.Running;
            scanRun.StartedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);

            // Resolve credentials: project-specific overrides instance shared
            var credSource = !string.IsNullOrEmpty(project.CredentialReference)
                ? project.CredentialReference
                : instance.CredentialReference;

            var creds = ParseCredentials(credSource ?? string.Empty);
            if (string.IsNullOrEmpty(creds.Username) || string.IsNullOrEmpty(creds.Password))
            {
                await FailScanAsync(scanRun, "No credentials available for this project (neither project-specific nor instance-shared).", ct);
                return;
            }

            logger.LogInformation("🔑 Auth as: {Username} (source: {Src})",
                creds.Username,
                !string.IsNullOrEmpty(project.CredentialReference) ? "project" : "instance-shared");

            // Connection test
            var (connected, msg) = await azureDevOpsClient.TestConnectionAsync(
                instance.Url, instance.Collection, creds.Username, creds.Password, ct);
            if (!connected)
            {
                await FailScanAsync(scanRun, $"Connection failed: {msg}", ct);
                return;
            }
            logger.LogInformation("✅ Connection OK");

            // Fetch repos for THIS project only
            var repos = await azureDevOpsClient.GetRepositoriesAsync(
                instance.Url, instance.Collection, project.AzureProjectId,
                creds.Username, creds.Password, ct);

            logger.LogInformation("📁 Project '{Project}' has {Count} repositories", project.AzureProjectId, repos.Count);

            if (repos.Count == 0)
            {
                project.LastScannedAt = DateTime.UtcNow;
                await CompleteScanAsync(scanRun, 0, 0, 0, 0, 0, 0, 0, 0, "No repositories in project", ct);
                return;
            }

            var stats = new ScanStats();
            foreach (var repo in repos)
            {
                try
                {
                    await ProcessRepositoryAsync(instance, project, repo, creds, scanRun, stats, ct);
                }
                catch (Exception ex)
                {
                    stats.ReposFailed++;
                    logger.LogError(ex, "❌ Failed to scan repository: {Repo}", repo.Name);
                }
            }

            project.LastScannedAt = DateTime.UtcNow;
            await CompleteScanAsync(scanRun,
                stats.ReposScanned, stats.ReposFailed, stats.BranchesScanned,
                stats.TotalVulnerabilities, stats.Critical,
                stats.High, stats.Medium, stats.Low, null, ct);

            logger.LogInformation("═══════════════════════════════════════════════════════════════");
            logger.LogInformation("✅ SCAN #{ScanRunId} DONE in {Ms}ms — repos:{R}/{T} branches:{B} pkgs:{P} vulns:{V}",
                scanRunId, stopwatch.ElapsedMilliseconds,
                stats.ReposScanned, repos.Count, stats.BranchesScanned, stats.TotalPackages, stats.TotalVulnerabilities);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Scan #{ScanRunId} failed", scanRunId);
            await FailScanAsync(scanRun, ex.Message, ct);
        }
    }

    private async Task ProcessRepositoryAsync(
        AzureDevOpsInstance instance, Project project,
        AzureDevOpsRepo repo,
        (string Username, string Password) creds,
        ScanRun scanRun, ScanStats stats, CancellationToken ct)
    {
        logger.LogInformation("───────────────────────────────────────────────────────────────");
        logger.LogInformation("📁 SCANNING REPO: {RepoName}", repo.Name);

        var repoEntity = await dbContext.Set<Repository>()
            .Include(r => r.ConfiguredBranches)
            .FirstOrDefaultAsync(r => r.ProjectId == project.Id && r.Name == repo.Name, ct);

        if (repoEntity == null)
        {
            repoEntity = new Repository
            {
                ProjectId = project.Id,
                Name = repo.Name,
                CloneUrl = repo.RemoteUrl,
                DefaultBranch = repo.DefaultBranch,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
            };
            dbContext.Set<Repository>().Add(repoEntity);
            await dbContext.SaveChangesAsync(ct);
        }

        // Skip disabled repositories
        if (!repoEntity.IsEnabled)
        {
            logger.LogInformation("   ⚠️ Repository is disabled, skipping");
            return;
        }

        // Determine which branches to scan
        var branchesToScan = new List<string>();
        var enabledConfiguredBranches = repoEntity.ConfiguredBranches
            .Where(b => b.IsEnabled)
            .ToList();

        if (enabledConfiguredBranches.Any())
        {
            // Use configured branches
            branchesToScan.AddRange(enabledConfiguredBranches.Select(b => b.BranchName));
            logger.LogInformation("   🔀 Scanning {Count} configured branches", branchesToScan.Count);
        }
        else
        {
            // Fallback to default branch (project default → repo default)
            var defaultBranch = !string.IsNullOrWhiteSpace(project.DefaultBranch)
                ? project.DefaultBranch!
                : repo.DefaultBranch;
            branchesToScan.Add(defaultBranch);
            logger.LogInformation("   🔀 Scanning default branch: {Branch}", defaultBranch);
        }

        int totalRepoPkgs = 0, totalRepoVulns = 0;

        // Scan each branch
        foreach (var branch in branchesToScan)
        {
            try
            {
                logger.LogInformation("   ├─ Branch: {Branch}", branch);
                var (pkgs, vulns) = await ScanRepositoryBranchAsync(
                    instance, project, repo, repoEntity, branch, creds, scanRun, stats, ct);
                
                totalRepoPkgs += pkgs;
                totalRepoVulns += vulns;
                stats.BranchesScanned++;

                // Update branch scan statistics if this is a configured branch
                var configuredBranch = enabledConfiguredBranches.FirstOrDefault(b => 
                    b.BranchName.Equals(branch, StringComparison.OrdinalIgnoreCase));
                if (configuredBranch != null)
                {
                    configuredBranch.LastScannedAt = DateTime.UtcNow;
                    configuredBranch.ScanCount++;
                    configuredBranch.UpdatedAt = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "   ❌ Failed to scan branch {Branch}", branch);
            }
        }

        repoEntity.LastScannedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        stats.ReposScanned++;
        logger.LogInformation("   📊 Repo summary: {Pkgs} pkgs, {Vulns} vulns across {Branches} branches", 
            totalRepoPkgs, totalRepoVulns, branchesToScan.Count);
    }

    private async Task<(int packages, int vulnerabilities)> ScanRepositoryBranchAsync(
        AzureDevOpsInstance instance, Project project,
        AzureDevOpsRepo repo, Repository repoEntity, string branch,
        (string Username, string Password) creds,
        ScanRun scanRun, ScanStats stats, CancellationToken ct)
    {
        var items = await azureDevOpsClient.GetItemsAsync(
            instance.Url, instance.Collection, project.AzureProjectId, repo.Name,
            "/", branch, creds.Username, creds.Password, ct);

        var depFiles = items
            .Where(i => i.GitObjectType == "blob" && IsDependencyFile(Path.GetFileName(i.Path)))
            .ToList();

        if (depFiles.Count == 0)
        {
            logger.LogInformation("      ⚠️ No dependency files");
            return (0, 0);
        }

        logger.LogInformation("      📋 {Count} dependency files", depFiles.Count);

        int branchPkgs = 0, branchVulns = 0;

        foreach (var file in depFiles)
        {
            try
            {
                var content = await azureDevOpsClient.GetFileContentAsync(
                    instance.Url, instance.Collection, project.AzureProjectId, repo.Name,
                    file.Path, branch, creds.Username, creds.Password, ct);

                if (string.IsNullOrEmpty(content)) continue;

                var sbom = new Sbom
                {
                    RepositoryId = repoEntity.Id,
                    ScanRunId = scanRun.Id,
                    BranchName = branch,
                    Format = "CycloneDX",
                    Generator = "Vulscan",
                    GeneratedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                };
                dbContext.Set<Sbom>().Add(sbom);
                await dbContext.SaveChangesAsync(ct);

                var result = await dependencyScanner.ScanDependenciesAsync(
                    Path.GetFileName(file.Path), file.Path, content,
                    scanRun.Id, repoEntity.Id, sbom.Id, ct);

                sbom.ComponentCount = result.Packages.Count;
                sbom.SbomJson = result.SbomJson;
                sbom.GenerationDurationMs = 0;

                if (result.Packages.Count > 0)
                {
                    foreach (var pkg in result.Packages) pkg.SbomId = sbom.Id;
                    dbContext.Set<DiscoveredPackage>().AddRange(result.Packages);
                    branchPkgs += result.Packages.Count;
                    stats.TotalPackages += result.Packages.Count;
                }

                if (result.Vulnerabilities.Count > 0)
                {
                    // Set branch name for each vulnerability
                    foreach (var v in result.Vulnerabilities)
                    {
                        v.SbomId = sbom.Id;
                        v.BranchName = branch;
                    }
                    dbContext.Set<Vulnerability>().AddRange(result.Vulnerabilities);
                    branchVulns += result.Vulnerabilities.Count;
                    stats.TotalVulnerabilities += result.Vulnerabilities.Count;
                    stats.Critical += result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Critical);
                    stats.High += result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.High);
                    stats.Medium += result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Medium);
                    stats.Low += result.Vulnerabilities.Count(v => v.Severity == VulnerabilitySeverity.Low);
                }

                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "      ❌ Failed processing {Path}", file.Path);
            }
        }

        logger.LogInformation("      📊 Branch {Branch}: {Pkgs} pkgs, {Vulns} vulns", branch, branchPkgs, branchVulns);
        return (branchPkgs, branchVulns);
    }

    private static bool IsDependencyFile(string fileName)
    {
        foreach (var pattern in DependencyFilePatterns)
        {
            if (pattern.StartsWith('*'))
            {
                if (fileName.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)) return true;
            }
            else if (fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private async Task CompleteScanAsync(
        ScanRun scanRun, int reposScanned, int reposFailed, int branchesScanned,
        int totalVulns, int critical, int high, int medium, int low,
        string? errorLog, CancellationToken ct)
    {
        scanRun.Status = ScanStatus.Completed;
        scanRun.CompletedAt = DateTime.UtcNow;
        scanRun.DurationSeconds = (int)(DateTime.UtcNow - scanRun.StartedAt).TotalSeconds;
        scanRun.ReposScanned = reposScanned;
        scanRun.ReposFailed = reposFailed;
        scanRun.BranchesScanned = branchesScanned;
        scanRun.TotalVulnerabilities = totalVulns;
        scanRun.CriticalCount = critical;
        scanRun.HighCount = high;
        scanRun.MediumCount = medium;
        scanRun.LowCount = low;
        scanRun.ErrorLog = errorLog;
        await dbContext.SaveChangesAsync(ct);

        // Send email notification if enabled
        try
        {
            var project = await dbContext.Set<Project>()
                .FirstOrDefaultAsync(p => p.Id == scanRun.ProjectId, ct);

            if (project?.SendEmailNotifications == true && !string.IsNullOrEmpty(project.OwnerEmail))
            {
                logger.LogInformation("Sending email notification for scan {ScanId} to {Email}", 
                    scanRun.Id, project.OwnerEmail);

                var (success, message) = await emailService.SendScanNotificationAsync(
                    scanRun.Id, 
                    includePdfAttachment: false,  // PDF generation not yet implemented
                    includeHtmlAttachment: true,
                    additionalRecipients: null,
                    ct);

                if (success)
                {
                    logger.LogInformation("Email notification sent successfully for scan {ScanId}", scanRun.Id);
                }
                else
                {
                    logger.LogWarning("Failed to send email notification for scan {ScanId}: {Message}", 
                        scanRun.Id, message);
                }
            }
        }
        catch (Exception ex)
        {
            // Don't fail the scan if email fails
            logger.LogError(ex, "Error sending email notification for scan {ScanId}", scanRun.Id);
        }
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
        logger.LogError("❌ SCAN #{ScanRunId} FAILED: {Error}", scanRun.Id, error);
    }

    private static (string Username, string Password) ParseCredentials(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return ("", "");
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return (
                root.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "",
                root.TryGetProperty("password", out var p) ? p.GetString() ?? "" : ""
            );
        }
        catch { return ("", ""); }
    }

    private sealed class ScanStats
    {
        public int ReposScanned;
        public int ReposFailed;
        public int BranchesScanned;
        public int TotalPackages;
        public int TotalVulnerabilities;
        public int Critical, High, Medium, Low;
    }
}

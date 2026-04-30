using Vulscan.Domain.Entities;

namespace Vulscan.Application.Interfaces;

/// <summary>
/// Result of scanning a dependency file.
/// </summary>
public record ScanResult(
    List<DiscoveredPackage> Packages,
    List<Vulnerability> Vulnerabilities,
    string Ecosystem,
    string SbomJson);

/// <summary>
/// Scans dependency files for packages and known vulnerabilities.
/// </summary>
public interface IDependencyScanner
{
    /// <summary>
    /// Scans a dependency file for packages and known vulnerabilities.
    /// </summary>
    /// <param name="fileName">Name of the dependency file (e.g., package.json)</param>
    /// <param name="filePath">Full path in the repository</param>
    /// <param name="content">File content</param>
    /// <param name="scanRunId">The scan run ID</param>
    /// <param name="repositoryId">The repository ID</param>
    /// <param name="sbomId">Optional SBOM ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Scan result with all packages and vulnerabilities</returns>
    Task<ScanResult> ScanDependenciesAsync(
        string fileName,
        string filePath,
        string content,
        Guid scanRunId,
        Guid repositoryId,
        Guid? sbomId = null,
        CancellationToken ct = default);
}

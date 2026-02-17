using Vulscan.Domain.Common;
using Vulscan.Domain.Enums;

namespace Vulscan.Domain.Entities;

/// <summary>
/// Represents a discovered package/dependency from a repository scan.
/// Stores ALL packages, not just vulnerable ones.
/// </summary>
public class DiscoveredPackage : BaseEntity
{
    public int ScanRunId { get; set; }
    public int RepositoryId { get; set; }
    public int? SbomId { get; set; }

    /// <summary>Package ecosystem (npm, nuget, pypi, maven, etc.)</summary>
    public string Ecosystem { get; set; } = string.Empty;

    /// <summary>Package name</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Installed version</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>File where the package was discovered</summary>
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>Whether this is a direct or transitive dependency</summary>
    public bool IsDirect { get; set; } = true;

    /// <summary>Whether this package has known vulnerabilities</summary>
    public bool HasVulnerabilities { get; set; }

    /// <summary>Package license if detected</summary>
    public string? License { get; set; }

    /// <summary>Package homepage/repository URL</summary>
    public string? PackageUrl { get; set; }

    /// <summary>PURL (Package URL) standard identifier</summary>
    public string? Purl { get; set; }

    // Navigation
    public ScanRun ScanRun { get; set; } = null!;
    public Repository Repository { get; set; } = null!;
    public Sbom? Sbom { get; set; }
}

/// <summary>
/// Package ecosystem types
/// </summary>
public static class PackageEcosystem
{
    public const string Npm = "npm";
    public const string NuGet = "nuget";
    public const string PyPi = "pypi";
    public const string Maven = "maven";
    public const string Go = "go";
    public const string Cargo = "cargo";
    public const string Composer = "composer";
    public const string RubyGems = "rubygems";
}

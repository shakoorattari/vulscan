using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;

namespace Vulscan.Infrastructure.Services;

/// <summary>
/// Scans dependency files and checks against known vulnerable packages.
/// Returns all discovered packages and generates CycloneDX-compatible SBOM JSON.
/// </summary>
public sealed partial class DependencyScanner(ILogger<DependencyScanner> logger) : IDependencyScanner
{
    // Known vulnerable packages database - in production, use OSV API, NVD, or commercial database
    private static readonly Dictionary<string, List<KnownVulnerability>> KnownVulnerabilities = new(StringComparer.OrdinalIgnoreCase)
    {
        // npm packages
        ["lodash"] = [
            new("CVE-2021-23337", "4.17.21", VulnerabilitySeverity.High, 7.2, "Prototype pollution in lodash"),
            new("CVE-2020-28500", "4.17.21", VulnerabilitySeverity.Medium, 5.3, "Regular expression DoS in lodash"),
        ],
        ["minimist"] = [new("CVE-2021-44906", "1.2.6", VulnerabilitySeverity.Critical, 9.8, "Prototype pollution")],
        ["axios"] = [new("CVE-2023-45857", "1.6.0", VulnerabilitySeverity.High, 7.5, "CSRF vulnerability")],
        ["express"] = [new("CVE-2024-29041", "4.19.2", VulnerabilitySeverity.Medium, 5.3, "Open redirect")],
        ["jsonwebtoken"] = [new("CVE-2022-23529", "9.0.0", VulnerabilitySeverity.Critical, 9.8, "Insecure signature")],
        ["moment"] = [new("CVE-2022-31129", "2.29.4", VulnerabilitySeverity.High, 7.5, "Path traversal")],
        ["node-fetch"] = [new("CVE-2022-0235", "2.6.7", VulnerabilitySeverity.High, 8.8, "Information exposure")],
        ["tar"] = [new("CVE-2021-37701", "6.1.11", VulnerabilitySeverity.High, 8.6, "Arbitrary file creation")],
        // Python packages
        ["django"] = [new("CVE-2024-27351", "4.2.11", VulnerabilitySeverity.High, 7.5, "Regex DoS")],
        ["requests"] = [new("CVE-2024-35195", "2.32.0", VulnerabilitySeverity.Medium, 5.6, "Proxy-Auth leak")],
        ["flask"] = [new("CVE-2023-30861", "2.3.2", VulnerabilitySeverity.High, 7.5, "Cookie security issue")],
        ["pillow"] = [new("CVE-2024-28219", "10.3.0", VulnerabilitySeverity.High, 8.1, "Buffer overflow")],
        ["pyyaml"] = [new("CVE-2020-14343", "5.4", VulnerabilitySeverity.Critical, 9.8, "Code execution")],
        // .NET packages
        ["newtonsoft.json"] = [new("CVE-2024-21907", "13.0.2", VulnerabilitySeverity.High, 7.5, "Stack overflow")],
        ["system.text.json"] = [new("CVE-2024-21319", "8.0.1", VulnerabilitySeverity.Medium, 5.9, "DoS")],
    };

    public async Task<ScanResult> ScanDependenciesAsync(
        string fileName, string filePath, string content,
        int scanRunId, int repositoryId, int? sbomId = null, CancellationToken ct = default)
    {
        var packages = new List<DiscoveredPackage>();
        var vulnerabilities = new List<Vulnerability>();
        var ecosystem = DetermineEcosystem(fileName);

        logger.LogInformation(
            "Scanning dependency file {FileName} (ecosystem: {Ecosystem}) for scan {ScanRunId}",
            filePath, ecosystem, scanRunId);

        try
        {
            var dependencies = ParseDependencies(fileName, content);
            logger.LogInformation("Parsed {Count} dependencies from {File}", dependencies.Count, filePath);

            foreach (var dep in dependencies)
            {
                // Create package record for ALL dependencies
                var package = new DiscoveredPackage
                {
                    ScanRunId = scanRunId,
                    RepositoryId = repositoryId,
                    SbomId = sbomId,
                    Ecosystem = ecosystem,
                    Name = dep.Name,
                    Version = dep.Version,
                    SourceFile = filePath,
                    IsDirect = dep.IsDirect,
                    HasVulnerabilities = false,
                    Purl = GeneratePurl(ecosystem, dep.Name, dep.Version),
                    CreatedAt = DateTime.UtcNow
                };

                // Check for vulnerabilities
                if (KnownVulnerabilities.TryGetValue(dep.Name, out var knownVulns))
                {
                    foreach (var vuln in knownVulns)
                    {
                        if (IsVersionVulnerable(dep.Version, vuln.FixedInVersion))
                        {
                            package.HasVulnerabilities = true;

                            vulnerabilities.Add(new Vulnerability
                            {
                                ScanRunId = scanRunId,
                                RepositoryId = repositoryId,
                                SbomId = sbomId,
                                CveId = vuln.CveId,
                                PackageName = dep.Name,
                                InstalledVersion = dep.Version,
                                FixedVersion = vuln.FixedInVersion,
                                Severity = vuln.Severity,
                                CvssScore = vuln.CvssScore,
                                Description = vuln.Description,
                                SourceDb = "Internal",
                                Status = VulnerabilityStatus.New,
                                FirstDetectedAt = DateTime.UtcNow,
                                CreatedAt = DateTime.UtcNow
                            });

                            logger.LogWarning(
                                "🔴 VULNERABILITY: {CVE} in {Package}@{Version} (Severity: {Severity})",
                                vuln.CveId, dep.Name, dep.Version, vuln.Severity);
                        }
                    }
                }

                packages.Add(package);
                logger.LogDebug("📦 Package: {Name}@{Version} (vuln: {HasVuln})",
                    dep.Name, dep.Version, package.HasVulnerabilities);
            }

            // Generate SBOM JSON
            var sbomJson = GenerateSbomJson(packages, filePath, ecosystem);

            logger.LogInformation(
                "Scan complete for {File}: {PackageCount} packages, {VulnCount} vulnerabilities",
                filePath, packages.Count, vulnerabilities.Count);

            return new ScanResult(packages, vulnerabilities, ecosystem, sbomJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scan {File}", filePath);
            return new ScanResult([], [], ecosystem, "{}");
        }
    }

    private List<DependencyInfo> ParseDependencies(string fileName, string content)
    {
        return fileName.ToLowerInvariant() switch
        {
            "package.json" => ParsePackageJson(content),
            "package-lock.json" => ParsePackageLockJson(content),
            "requirements.txt" => ParseRequirementsTxt(content),
            "pyproject.toml" => ParsePyprojectToml(content),
            "pipfile" => ParsePipfile(content),
            "go.mod" => ParseGoMod(content),
            "cargo.toml" => ParseCargoToml(content),
            "composer.json" => ParseComposerJson(content),
            "gemfile" => ParseGemfile(content),
            _ when fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) => ParseCsproj(content),
            _ when fileName == "packages.config" => ParsePackagesConfig(content),
            _ => []
        };
    }

    private static string DetermineEcosystem(string fileName)
    {
        return fileName.ToLowerInvariant() switch
        {
            "package.json" or "package-lock.json" => PackageEcosystem.Npm,
            "requirements.txt" or "pyproject.toml" or "pipfile" or "pipfile.lock" => PackageEcosystem.PyPi,
            "go.mod" or "go.sum" => PackageEcosystem.Go,
            "cargo.toml" or "cargo.lock" => PackageEcosystem.Cargo,
            "composer.json" or "composer.lock" => PackageEcosystem.Composer,
            "gemfile" or "gemfile.lock" => PackageEcosystem.RubyGems,
            "pom.xml" or "build.gradle" => PackageEcosystem.Maven,
            _ when fileName.EndsWith(".csproj") || fileName == "packages.config" => PackageEcosystem.NuGet,
            _ => "unknown"
        };
    }

    private static string GeneratePurl(string ecosystem, string name, string version)
    {
        // Package URL (PURL) standard format: pkg:type/namespace/name@version
        var type = ecosystem switch
        {
            PackageEcosystem.Npm => "npm",
            PackageEcosystem.NuGet => "nuget",
            PackageEcosystem.PyPi => "pypi",
            PackageEcosystem.Maven => "maven",
            PackageEcosystem.Go => "golang",
            PackageEcosystem.Cargo => "cargo",
            PackageEcosystem.Composer => "composer",
            PackageEcosystem.RubyGems => "gem",
            _ => "generic"
        };
        return $"pkg:{type}/{name}@{version}";
    }

    private string GenerateSbomJson(List<DiscoveredPackage> packages, string sourceFile, string ecosystem)
    {
        var sbom = new
        {
            bomFormat = "CycloneDX",
            specVersion = "1.5",
            serialNumber = $"urn:uuid:{Guid.NewGuid()}",
            version = 1,
            metadata = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                tools = new[] { new { vendor = "Vulscan", name = "DependencyScanner", version = "1.0.0" } },
                component = new { type = "application", name = sourceFile }
            },
            components = packages.Select(p => new
            {
                type = "library",
                name = p.Name,
                version = p.Version,
                purl = p.Purl,
                properties = new[] { new { name = "ecosystem", value = ecosystem } }
            }).ToArray()
        };

        return JsonSerializer.Serialize(sbom, new JsonSerializerOptions { WriteIndented = true });
    }

    #region Parsers

    private List<DependencyInfo> ParsePackageJson(string content)
    {
        var deps = new List<DependencyInfo>();
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("dependencies", out var dependencies))
            {
                logger.LogDebug("Found 'dependencies' section in package.json");
                foreach (var dep in dependencies.EnumerateObject())
                {
                    var version = dep.Value.GetString() ?? "";
                    deps.Add(new DependencyInfo(dep.Name, CleanVersion(version), true));
                    logger.LogDebug("  Found: {Name}@{Version}", dep.Name, version);
                }
            }

            if (root.TryGetProperty("devDependencies", out var devDeps))
            {
                logger.LogDebug("Found 'devDependencies' section in package.json");
                foreach (var dep in devDeps.EnumerateObject())
                {
                    var version = dep.Value.GetString() ?? "";
                    deps.Add(new DependencyInfo(dep.Name, CleanVersion(version), true));
                    logger.LogDebug("  Found: {Name}@{Version} (dev)", dep.Name, version);
                }
            }

            if (deps.Count == 0)
            {
                logger.LogWarning("No dependencies found in package.json. Content preview: {Content}",
                    content.Length > 500 ? content[..500] : content);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse package.json. Content preview: {Content}",
                content.Length > 200 ? content[..200] : content);
        }
        return deps;
    }

    private List<DependencyInfo> ParsePackageLockJson(string content)
    {
        var deps = new List<DependencyInfo>();
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // npm v2/v3 lockfile format
            if (root.TryGetProperty("packages", out var packages))
            {
                logger.LogDebug("Parsing npm v2/v3 lockfile format (packages)");
                foreach (var pkg in packages.EnumerateObject())
                {
                    if (string.IsNullOrEmpty(pkg.Name) || pkg.Name == "") continue;
                    var name = pkg.Name.Replace("node_modules/", "");
                    if (name.Contains('/')) name = name.Split('/').Last();
                    if (pkg.Value.TryGetProperty("version", out var version))
                    {
                        deps.Add(new DependencyInfo(name, version.GetString() ?? "", false));
                    }
                }
            }
            else if (root.TryGetProperty("dependencies", out var dependencies))
            {
                ParseLockDependencies(dependencies, deps, false);
            }

            if (deps.Count == 0)
            {
                logger.LogWarning("No packages found in package-lock.json. Content preview: {Content}",
                    content.Length > 500 ? content[..500] : content);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse package-lock.json");
        }
        return deps;
    }

    private void ParseLockDependencies(JsonElement deps, List<DependencyInfo> result, bool isDirect)
    {
        foreach (var dep in deps.EnumerateObject())
        {
            if (dep.Value.TryGetProperty("version", out var version))
            {
                result.Add(new DependencyInfo(dep.Name, version.GetString() ?? "", isDirect));
            }
            if (dep.Value.TryGetProperty("dependencies", out var nested))
            {
                ParseLockDependencies(nested, result, false);
            }
        }
    }

    private List<DependencyInfo> ParseRequirementsTxt(string content)
    {
        var deps = new List<DependencyInfo>();
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith('-')) continue;

            var match = RequirementRegex().Match(trimmed);
            if (match.Success)
            {
                deps.Add(new DependencyInfo(match.Groups[1].Value.ToLowerInvariant(), match.Groups[2].Value, true));
            }
            else if (!trimmed.Contains('['))
            {
                // Package without version specifier
                var name = trimmed.Split(';')[0].Trim();
                if (!string.IsNullOrEmpty(name) && char.IsLetter(name[0]))
                {
                    deps.Add(new DependencyInfo(name.ToLowerInvariant(), "*", true));
                }
            }
        }
        return deps;
    }

    private List<DependencyInfo> ParsePyprojectToml(string content)
    {
        var deps = new List<DependencyInfo>();
        var matches = PyprojectDependencyRegex().Matches(content);
        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                deps.Add(new DependencyInfo(match.Groups[1].Value, match.Groups[2].Value, true));
            }
        }
        return deps;
    }

    private List<DependencyInfo> ParsePipfile(string content)
    {
        var deps = new List<DependencyInfo>();
        var matches = PipfileDependencyRegex().Matches(content);
        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 2)
            {
                var version = match.Groups.Count >= 3 ? match.Groups[2].Value : "*";
                deps.Add(new DependencyInfo(match.Groups[1].Value, version, true));
            }
        }
        return deps;
    }

    private List<DependencyInfo> ParseCsproj(string content)
    {
        var deps = new List<DependencyInfo>();

        // SDK-style PackageReference: <PackageReference Include="..." Version="..." />
        var inlineMatches = PackageReferenceRegex().Matches(content);
        foreach (Match match in inlineMatches)
        {
            if (match.Groups.Count >= 3)
            {
                deps.Add(new DependencyInfo(match.Groups[1].Value, match.Groups[2].Value, true));
            }
        }

        // SDK-style with Version element: <PackageReference Include="..."><Version>...</Version></PackageReference>
        var elementMatches = PackageReferenceElementRegex().Matches(content);
        foreach (Match match in elementMatches)
        {
            if (match.Groups.Count >= 3)
            {
                deps.Add(new DependencyInfo(match.Groups[1].Value, match.Groups[2].Value, true));
            }
        }

        if (deps.Count == 0)
        {
            logger.LogWarning("No PackageReference found in csproj. Content preview: {Content}",
                content.Length > 500 ? content[..500] : content);
        }

        return deps;
    }

    private List<DependencyInfo> ParsePackagesConfig(string content)
    {
        var deps = new List<DependencyInfo>();

        // Match packages.config format: <package id="..." version="..." ... />
        // Handle attributes in any order with optional additional attributes
        var matches = PackagesConfigRegex().Matches(content);
        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                deps.Add(new DependencyInfo(match.Groups[1].Value, match.Groups[2].Value, true));
            }
        }

        // Alternative: version before id
        var altMatches = PackagesConfigAltRegex().Matches(content);
        foreach (Match match in altMatches)
        {
            if (match.Groups.Count >= 3)
            {
                deps.Add(new DependencyInfo(match.Groups[2].Value, match.Groups[1].Value, true));
            }
        }

        if (deps.Count == 0)
        {
            logger.LogWarning("No packages found in packages.config. Content preview: {Content}",
                content.Length > 500 ? content[..500] : content);
        }

        return deps.DistinctBy(d => d.Name).ToList();
    }

    private List<DependencyInfo> ParseGoMod(string content)
    {
        var deps = new List<DependencyInfo>();
        var matches = GoModRegex().Matches(content);
        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                var name = match.Groups[1].Value;
                var version = match.Groups[2].Value.TrimStart('v');
                deps.Add(new DependencyInfo(name, version, true));
            }
        }
        return deps;
    }

    private List<DependencyInfo> ParseCargoToml(string content)
    {
        var deps = new List<DependencyInfo>();
        var matches = CargoTomlRegex().Matches(content);
        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                deps.Add(new DependencyInfo(match.Groups[1].Value, match.Groups[2].Value, true));
            }
        }
        return deps;
    }

    private List<DependencyInfo> ParseComposerJson(string content)
    {
        var deps = new List<DependencyInfo>();
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("require", out var require))
            {
                foreach (var dep in require.EnumerateObject())
                {
                    if (dep.Name.StartsWith("php") || dep.Name.StartsWith("ext-")) continue;
                    deps.Add(new DependencyInfo(dep.Name, CleanVersion(dep.Value.GetString() ?? ""), true));
                }
            }

            if (root.TryGetProperty("require-dev", out var requireDev))
            {
                foreach (var dep in requireDev.EnumerateObject())
                {
                    if (dep.Name.StartsWith("php") || dep.Name.StartsWith("ext-")) continue;
                    deps.Add(new DependencyInfo(dep.Name, CleanVersion(dep.Value.GetString() ?? ""), true));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse composer.json");
        }
        return deps;
    }

    private List<DependencyInfo> ParseGemfile(string content)
    {
        var deps = new List<DependencyInfo>();
        var matches = GemfileRegex().Matches(content);
        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value;
            var version = match.Groups.Count >= 3 && !string.IsNullOrEmpty(match.Groups[2].Value)
                ? match.Groups[2].Value : "*";
            deps.Add(new DependencyInfo(name, version, true));
        }
        return deps;
    }

    #endregion

    #region Helpers

    private static string CleanVersion(string version)
    {
        return version.TrimStart('^', '~', '>', '<', '=', ' ', 'v');
    }

    private static bool IsVersionVulnerable(string installed, string fixedIn)
    {
        try
        {
            var installedV = ParseVersion(installed);
            var fixedV = ParseVersion(fixedIn);
            return installedV.CompareTo(fixedV) < 0;
        }
        catch
        {
            return true; // Assume vulnerable if can't parse
        }
    }

    private static Version ParseVersion(string version)
    {
        var clean = version.Split('-')[0].Split('+')[0];
        var parts = clean.Split('.');
        var major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
        var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;
        return new Version(major, minor, patch);
    }

    #endregion

    #region Regex Patterns

    [GeneratedRegex(@"^([a-zA-Z0-9_\-\.]+)[=<>!~]+(.+)$")]
    private static partial Regex RequirementRegex();

    [GeneratedRegex(@"""([a-zA-Z0-9_\-]+)""\s*=\s*""([^""]+)""")]
    private static partial Regex PyprojectDependencyRegex();

    [GeneratedRegex(@"([a-zA-Z0-9_\-]+)\s*=\s*""([^""]+)""")]
    private static partial Regex PipfileDependencyRegex();

    [GeneratedRegex(@"<PackageReference\s+Include=""([^""]+)""[^>]*Version=""([^""]+)""[^>]*/?>|<PackageReference\s+Include=""([^""]+)""[^>]*Version=""([^""]+)""[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PackageReferenceRegex();

    [GeneratedRegex(@"<PackageReference\s+Include=""([^""]+)""[^>]*>\s*<Version>([^<]+)</Version>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PackageReferenceElementRegex();

    [GeneratedRegex(@"<package[^>]+id=""([^""]+)""[^>]+version=""([^""]+)""[^>]*/?", RegexOptions.IgnoreCase)]
    private static partial Regex PackagesConfigRegex();

    [GeneratedRegex(@"<package[^>]+version=""([^""]+)""[^>]+id=""([^""]+)""[^>]*/?", RegexOptions.IgnoreCase)]
    private static partial Regex PackagesConfigAltRegex();

    [GeneratedRegex(@"^\s*([a-zA-Z0-9_\-\.\/]+)\s+(v?[\d\.]+)", RegexOptions.Multiline)]
    private static partial Regex GoModRegex();

    [GeneratedRegex(@"([a-zA-Z0-9_\-]+)\s*=\s*""([^""]+)""")]
    private static partial Regex CargoTomlRegex();

    [GeneratedRegex(@"gem\s+['""]([a-zA-Z0-9_\-]+)['""](?:\s*,\s*['""]([^'""]+)['""])?")]
    private static partial Regex GemfileRegex();

    #endregion

    private record DependencyInfo(string Name, string Version, bool IsDirect);
    private record KnownVulnerability(string CveId, string FixedInVersion, VulnerabilitySeverity Severity, double CvssScore, string Description);
}

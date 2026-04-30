using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.Clients;

namespace Vulscan.Infrastructure.Services;

/// <summary>
/// Scans dependency files for packages and queries OSV.dev for known vulnerabilities.
/// Returns all discovered packages and generates CycloneDX-compatible SBOM JSON.
/// </summary>
public sealed partial class DependencyScanner(
    ILogger<DependencyScanner> logger,
    IOsvApiClient osvClient,
    IVulnerabilityCacheService cache) : IDependencyScanner
{
    private const int OsvBatchSize = 1000;

    public async Task<ScanResult> ScanDependenciesAsync(
        string fileName, string filePath, string content,
        Guid scanRunId, Guid repositoryId, Guid? sbomId = null, CancellationToken ct = default)
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

            // Look up vulnerabilities for all dependencies via OSV (cached)
            var vulnRefsByIndex = await LookupVulnerabilityRefsAsync(dependencies, ecosystem, ct);

            for (var i = 0; i < dependencies.Count; i++)
            {
                var dep = dependencies[i];
                var refs = vulnRefsByIndex[i];

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
                    HasVulnerabilities = refs.Count > 0,
                    Purl = GeneratePurl(ecosystem, dep.Name, dep.Version),
                    CreatedAt = DateTime.UtcNow
                };

                // Hydrate full vulnerability details for each match
                foreach (var vref in refs)
                {
                    var vuln = await GetVulnerabilityDetailsAsync(vref.Id, ct);
                    if (vuln is null) continue;

                    var entity = MapToVulnerability(vuln, dep, scanRunId, repositoryId, sbomId);
                    vulnerabilities.Add(entity);

                    logger.LogWarning(
                        "🔴 VULNERABILITY: {CVE} in {Package}@{Version} (Severity: {Severity})",
                        entity.CveId, dep.Name, dep.Version, entity.Severity);
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

    #region OSV Integration

    /// <summary>
    /// Returns vulnerability refs (id+modified) per dependency, in input order.
    /// Uses cache first; remaining packages are batched to OSV /v1/querybatch.
    /// </summary>
    private async Task<List<IReadOnlyList<OsvVulnerabilityRef>>> LookupVulnerabilityRefsAsync(
        List<DependencyInfo> deps, string ecosystem, CancellationToken ct)
    {
        var osvEcosystem = MapToOsvEcosystem(ecosystem);
        var results = new List<IReadOnlyList<OsvVulnerabilityRef>>(deps.Count);
        for (var i = 0; i < deps.Count; i++) results.Add(Array.Empty<OsvVulnerabilityRef>());

        // Skip lookup for unknown ecosystems
        if (string.IsNullOrEmpty(osvEcosystem))
        {
            logger.LogDebug("Ecosystem {Ecosystem} not supported by OSV; skipping lookup", ecosystem);
            return results;
        }

        // Build batch from cache misses
        var pendingIndexes = new List<int>();
        var pendingQueries = new List<OsvBatchQuery>();

        for (var i = 0; i < deps.Count; i++)
        {
            var dep = deps[i];
            if (string.IsNullOrWhiteSpace(dep.Version) || dep.Version == "*")
            {
                continue; // OSV requires a concrete version for matching
            }

            var cached = cache.GetCachedRefs(osvEcosystem, dep.Name, dep.Version);
            if (cached is not null)
            {
                results[i] = cached;
                continue;
            }

            pendingIndexes.Add(i);
            pendingQueries.Add(new OsvBatchQuery
            {
                Package = new OsvPackage { Name = dep.Name, Ecosystem = osvEcosystem },
                Version = dep.Version
            });
        }

        if (pendingQueries.Count == 0)
        {
            logger.LogInformation("OSV lookup served entirely from cache ({Count} deps)", deps.Count);
            return results;
        }

        // OSV batch endpoint accepts up to 1000 queries per request
        for (var offset = 0; offset < pendingQueries.Count; offset += OsvBatchSize)
        {
            var slice = pendingQueries.Skip(offset).Take(OsvBatchSize).ToList();
            var response = await osvClient.QueryBatchAsync(slice, ct);

            if (response?.Results is null) continue;

            for (var j = 0; j < response.Results.Count; j++)
            {
                var origIdx = pendingIndexes[offset + j];
                var dep = deps[origIdx];
                var refs = (IReadOnlyList<OsvVulnerabilityRef>)response.Results[j].Vulns;
                results[origIdx] = refs;
                cache.SetCachedRefs(osvEcosystem, dep.Name, dep.Version, refs);
            }
        }

        return results;
    }

    /// <summary>
    /// Returns full OSV vulnerability details, served from cache when possible.
    /// </summary>
    private async Task<OsvVulnerability?> GetVulnerabilityDetailsAsync(string id, CancellationToken ct)
    {
        var cached = cache.GetCachedVulnerability(id);
        if (cached is not null) return cached;

        var fetched = await osvClient.GetVulnerabilityAsync(id, ct);
        if (fetched is not null) cache.SetCachedVulnerability(fetched);
        return fetched;
    }

    private static Vulnerability MapToVulnerability(
        OsvVulnerability vuln, DependencyInfo dep,
        Guid scanRunId, Guid repositoryId, Guid? sbomId)
    {
        var cveId = vuln.Aliases.FirstOrDefault(a => a.StartsWith("CVE-", StringComparison.OrdinalIgnoreCase))
                    ?? vuln.Id;
        var cvss = ExtractCvssScore(vuln.Severity);
        var severity = MapSeverity(cvss, vuln);
        var fixedVersion = ExtractFixedVersion(vuln.Affected, dep.Name);
        var description = !string.IsNullOrWhiteSpace(vuln.Summary) ? vuln.Summary : vuln.Details;

        return new Vulnerability
        {
            ScanRunId = scanRunId,
            RepositoryId = repositoryId,
            SbomId = sbomId,
            CveId = cveId,
            PackageName = dep.Name,
            InstalledVersion = dep.Version,
            FixedVersion = fixedVersion,
            Severity = severity,
            CvssScore = cvss,
            Description = Truncate(description, 4000),
            SourceDb = "OSV",
            Status = VulnerabilityStatus.New,
            FirstDetectedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Parses the base score out of a CVSS vector string (e.g., "CVSS:3.1/AV:N/...").
    /// Returns null if no parseable CVSS data is available.
    /// </summary>
    private static double? ExtractCvssScore(List<OsvSeverity> severities)
    {
        // Prefer CVSS_V3, fall back to V4/V2
        var entry = severities.FirstOrDefault(s => s.Type == "CVSS_V3")
                    ?? severities.FirstOrDefault(s => s.Type == "CVSS_V4")
                    ?? severities.FirstOrDefault(s => s.Type == "CVSS_V2")
                    ?? severities.FirstOrDefault();

        if (entry?.Score is null) return null;

        // OSV CVSS "score" is typically a vector string; compute base from impact metrics.
        // We do not implement a full CVSS calculator here; return a conservative estimate
            // using vector flags so callers get a reasonable severity bucket.
        return EstimateCvssFromVector(entry.Score);
    }

    private static double? EstimateCvssFromVector(string vector)
    {
        // If the score is already a numeric value, use it directly
        if (double.TryParse(vector, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var direct))
        {
            return Math.Clamp(direct, 0, 10);
        }

        // Otherwise approximate from impact metrics in the vector string
        var hasHighImpact = vector.Contains("C:H") || vector.Contains("I:H") || vector.Contains("A:H");
        var hasLowImpact = vector.Contains("C:L") || vector.Contains("I:L") || vector.Contains("A:L");
        var networkAttack = vector.Contains("AV:N");
        var noPrivs = vector.Contains("PR:N");

        if (hasHighImpact && networkAttack && noPrivs) return 9.0;
        if (hasHighImpact) return 7.5;
        if (hasLowImpact) return 5.0;
        return 3.5;
    }

    private static VulnerabilitySeverity MapSeverity(double? cvss, OsvVulnerability vuln)
    {
        // Try GitHub-style severity strings first (e.g., "CRITICAL")
        var ghsa = vuln.DatabaseSpecific?.Severity
                   ?? vuln.Affected.Select(a => a.DatabaseSpecific?.Severity).FirstOrDefault(s => !string.IsNullOrEmpty(s));

        if (!string.IsNullOrEmpty(ghsa))
        {
            return ghsa.ToUpperInvariant() switch
            {
                "CRITICAL" => VulnerabilitySeverity.Critical,
                "HIGH" => VulnerabilitySeverity.High,
                "MODERATE" or "MEDIUM" => VulnerabilitySeverity.Medium,
                "LOW" => VulnerabilitySeverity.Low,
                _ => MapSeverityFromCvss(cvss)
            };
        }

        return MapSeverityFromCvss(cvss);
    }

    private static VulnerabilitySeverity MapSeverityFromCvss(double? cvss) => cvss switch
    {
        >= 9.0 => VulnerabilitySeverity.Critical,
        >= 7.0 => VulnerabilitySeverity.High,
        >= 4.0 => VulnerabilitySeverity.Medium,
        > 0 => VulnerabilitySeverity.Low,
        _ => VulnerabilitySeverity.Medium
    };

    private static string? ExtractFixedVersion(List<OsvAffected> affected, string packageName)
    {
        var match = affected.FirstOrDefault(a =>
            a.Package.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase));
        if (match is null) match = affected.FirstOrDefault();

        return match?.Ranges
            .SelectMany(r => r.Events)
            .FirstOrDefault(e => !string.IsNullOrEmpty(e.Fixed))?.Fixed;
    }

    /// <summary>
    /// Maps internal ecosystem identifier to OSV.dev ecosystem identifier.
    /// See: https://ossf.github.io/osv-schema/#affectedpackage-field
    /// </summary>
    private static string MapToOsvEcosystem(string ecosystem) => ecosystem switch
    {
        PackageEcosystem.Npm => "npm",
        PackageEcosystem.NuGet => "NuGet",
        PackageEcosystem.PyPi => "PyPI",
        PackageEcosystem.Maven => "Maven",
        PackageEcosystem.Go => "Go",
        PackageEcosystem.Cargo => "crates.io",
        PackageEcosystem.Composer => "Packagist",
        PackageEcosystem.RubyGems => "RubyGems",
        _ => string.Empty
    };

    private static string? Truncate(string? input, int max)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input.Length <= max ? input : input[..max];
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
}

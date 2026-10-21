using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.Clients;
using Vulscan.Infrastructure.Services;

namespace Vulscan.Tests.Intel;

/// <summary>
/// How an OSV advisory becomes a finding. The scanner used to guess a CVSS score from a few vector flags and to
/// take the first "fixed" event it found; both are checked here through the public scan entry point.
/// </summary>
public class ScannerAdvisoryMappingTests
{
    private static async Task<List<Vulnerability>> ScanAsync(string manifest, string content, OsvVulnerability advisory)
    {
        var osv = new FakeOsv { BatchAnswer = _ => [advisory.Id] };
        osv.Records[advisory.Id] = advisory;
        var cache = new VulnerabilityCacheService(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 }),
            Options.Create(new VulnerabilityCacheOptions()), NullLogger<VulnerabilityCacheService>.Instance);
        var scanner = new DependencyScanner(NullLogger<DependencyScanner>.Instance, osv, cache);

        var result = await scanner.ScanDependenciesAsync(manifest, "/" + manifest, content, Guid.NewGuid(), Guid.NewGuid());
        return result.Vulnerabilities;
    }

    private static string PackageJson(string name, string version) =>
        "{\"name\":\"app\",\"dependencies\":{\"" + name + "\":\"" + version + "\"}}";

    private static OsvVulnerability Advisory(string vector, string? label = null) => new()
    {
        Id = "GHSA-aaaa-bbbb-cccc",
        Aliases = ["CVE-2026-0001"],
        Summary = "Something bad",
        Severity = vector.Length == 0 ? [] : [new OsvSeverity { Type = vector.StartsWith("CVSS:4") ? "CVSS_V4" : "CVSS_V3", Score = vector }],
        DatabaseSpecific = label is null ? null : new OsvDatabaseSpecific { Severity = label },
        Affected = [new OsvAffected { Package = new OsvPackage { Name = "left-pad", Ecosystem = "npm" } }],
    };

    // ── CVSS ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_cvss_score_is_calculated_from_the_vector_not_guessed()
    {
        // High confidentiality impact + network + no privileges: the old heuristic said 9.0 for anything like this.
        // The specification says 6.5 here, because user interaction is required and nothing else is affected.
        var vector = "CVSS:3.1/AV:N/AC:L/PR:N/UI:R/S:U/C:H/I:N/A:N";

        var finding = Assert.Single(await ScanAsync("package.json", PackageJson("left-pad", "1.0.0"), Advisory(vector)));

        Assert.Equal(6.5, finding.CvssScore);
        Assert.Equal(vector, finding.CvssVector);                           // kept, so it can be explained and re-scored
        Assert.Equal("CVE-2026-0001", finding.CveId);
    }

    [Fact]
    public async Task Another_vector_the_old_heuristic_got_wrong()
    {
        // High impact but privileges required: the heuristic returned 7.5; the real score is 8.1.
        var finding = Assert.Single(await ScanAsync("package.json", PackageJson("left-pad", "1.0.0"),
            Advisory("CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:U/C:H/I:H/A:N")));

        Assert.Equal(8.1, finding.CvssScore);
    }

    [Fact]
    public async Task A_critical_label_from_the_source_still_decides_the_severity_bucket()
    {
        var finding = Assert.Single(await ScanAsync("package.json", PackageJson("left-pad", "1.0.0"),
            Advisory("CVSS:3.1/AV:N/AC:L/PR:N/UI:R/S:U/C:H/I:N/A:N", label: "CRITICAL")));

        Assert.Equal(VulnerabilitySeverity.Critical, finding.Severity);     // GitHub's rating, as before
        Assert.Equal(6.5, finding.CvssScore);                               // but the number is the real one
    }

    [Fact]
    public async Task Without_a_label_the_severity_follows_the_real_score()
    {
        var finding = Assert.Single(await ScanAsync("package.json", PackageJson("left-pad", "1.0.0"),
            Advisory("CVSS:3.1/AV:N/AC:L/PR:N/UI:R/S:U/C:H/I:N/A:N")));

        Assert.Equal(VulnerabilitySeverity.Medium, finding.Severity);        // 6.5 is Medium (the heuristic made it Critical)
    }

    [Fact]
    public async Task A_cvss_4_vector_is_kept_but_not_scored_and_the_source_label_sets_the_severity()
    {
        var v4 = "CVSS:4.0/AV:N/AC:L/AT:N/PR:N/UI:N/VC:H/VI:H/VA:H/SC:N/SI:N/SA:N";

        var finding = Assert.Single(await ScanAsync("package.json", PackageJson("left-pad", "1.0.0"), Advisory(v4, label: "HIGH")));

        Assert.Null(finding.CvssScore);                                      // not invented
        Assert.Equal(v4, finding.CvssVector);
        Assert.Equal(VulnerabilitySeverity.High, finding.Severity);
    }

    [Fact]
    public async Task A_plain_numeric_score_is_still_accepted()
    {
        var advisory = Advisory("") with { Severity = [new OsvSeverity { Type = "CVSS_V3", Score = "7.5" }] };

        var finding = Assert.Single(await ScanAsync("package.json", PackageJson("left-pad", "1.0.0"), advisory));

        Assert.Equal(7.5, finding.CvssScore);
        Assert.Null(finding.CvssVector);
    }

    [Fact]
    public async Task Version_3_is_preferred_over_2_when_both_are_given()
    {
        var advisory = Advisory("") with
        {
            Severity =
            [
                new OsvSeverity { Type = "CVSS_V2", Score = "AV:N/AC:L/Au:N/C:P/I:P/A:P" },
                new OsvSeverity { Type = "CVSS_V3", Score = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H" },
            ],
        };

        var finding = Assert.Single(await ScanAsync("package.json", PackageJson("left-pad", "1.0.0"), advisory));

        Assert.Equal(9.8, finding.CvssScore);
    }

    // ── the fixed version ──────────────────────────────────────────────────────

    private static OsvVulnerability BraceExpansion() => new()
    {
        Id = "GHSA-aaaa-bbbb-cccc",
        Aliases = ["CVE-2026-0002"],
        Summary = "ReDoS",
        Affected =
        [
            Range("brace-expansion", ("introduced", "0"), ("fixed", "1.1.18")),
            Range("brace-expansion", ("introduced", "2.0.0"), ("fixed", "2.0.3")),
            Range("brace-expansion", ("introduced", "5.0.0"), ("fixed", "5.0.5")),
        ],
    };

    private static OsvAffected Range(string name, params (string Kind, string Version)[] events) => new()
    {
        Package = new OsvPackage { Name = name, Ecosystem = "npm" },
        Ranges =
        [
            new OsvRange
            {
                Type = "SEMVER",
                Events = events.Select(e => new OsvEvent
                {
                    Introduced = e.Kind == "introduced" ? e.Version : null,
                    Fixed = e.Kind == "fixed" ? e.Version : null,
                }).ToList(),
            },
        ],
    };

    [Theory]
    [InlineData("2.0.1", "2.0.3")]      // was reported as 1.1.18 — older than what is installed
    [InlineData("1.1.2", "1.1.18")]
    [InlineData("5.0.1", "5.0.5")]
    public async Task The_fixed_version_is_the_one_for_the_release_line_that_is_installed(string installed, string expected)
    {
        var finding = Assert.Single(await ScanAsync("package.json", PackageJson("brace-expansion", installed), BraceExpansion()));

        Assert.Equal(expected, finding.FixedVersion);
    }

    [Fact]
    public async Task When_the_advisory_names_the_package_differently_it_falls_back_to_the_first_listed_fix()
    {
        var advisory = new OsvVulnerability
        {
            Id = "GHSA-aaaa-bbbb-cccc",
            Affected = [Range("completely-different-name", ("introduced", "0"), ("fixed", "9.9.9"))],
        };

        var finding = Assert.Single(await ScanAsync("package.json", PackageJson("left-pad", "1.0.0"), advisory));

        Assert.Equal("9.9.9", finding.FixedVersion);                         // nothing to match against, so as before
    }

    [Fact]
    public async Task No_fix_is_reported_when_the_advisory_has_none_for_the_installed_line()
    {
        var advisory = new OsvVulnerability
        {
            Id = "GHSA-aaaa-bbbb-cccc",
            Affected = [Range("left-pad", ("introduced", "0"))],             // affected in every release, no fix
        };

        var finding = Assert.Single(await ScanAsync("package.json", PackageJson("left-pad", "1.0.0"), advisory));

        Assert.Null(finding.FixedVersion);
    }
}

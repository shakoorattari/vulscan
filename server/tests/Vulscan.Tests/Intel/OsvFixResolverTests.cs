using Vulscan.Infrastructure.Clients;
using Vulscan.Infrastructure.Intel;

namespace Vulscan.Tests.Intel;

public class OsvFixResolverTests
{
    private static OsvAffected Affected(string ecosystem, string name, string type, params (string Kind, string Version)[] events) => new()
    {
        Package = new OsvPackage { Ecosystem = ecosystem, Name = name },
        Ranges =
        [
            new OsvRange
            {
                Type = type,
                Events = events.Select(e => new OsvEvent
                {
                    Introduced = e.Kind == "introduced" ? e.Version : null,
                    Fixed = e.Kind == "fixed" ? e.Version : null,
                    LastAffected = e.Kind == "last_affected" ? e.Version : null,
                }).ToList(),
            },
        ],
    };

    // ── the real Log4Shell advisory: three separate ranges for one package ──────

    private static readonly OsvVulnerability Log4Shell = Fixtures.Json<OsvVulnerability>("osv-GHSA-jfh8-c2jp-5v3q.json");
    private const string Log4jCore = "org.apache.logging.log4j:log4j-core";

    [Theory]
    [InlineData("2.14.1", "2.15.0")]    // the 2.13.0 – 2.15.0 line
    [InlineData("2.13.0", "2.15.0")]
    [InlineData("2.8.2", "2.12.2")]     // the 2.4 – 2.12.2 line: its own fix, not 2.15.0
    [InlineData("2.4", "2.12.2")]
    [InlineData("2.0", "2.3.1")]        // the 2.0-beta9 – 2.3.1 line (2.0 is after 2.0-beta9)
    [InlineData("2.0-beta9", "2.3.1")]
    public void Picks_the_fix_for_the_release_line_the_installed_version_is_on(string installed, string expectedFix)
    {
        var fix = OsvFixResolver.Resolve(Log4Shell.Affected, "Maven", Log4jCore, installed);

        Assert.Equal(expectedFix, fix.FixedVersion);
        Assert.Equal(FixMatch.Range, fix.Match);
    }

    [Fact]
    public void Lists_every_fixed_version_for_the_package_oldest_first()
    {
        var fix = OsvFixResolver.Resolve(Log4Shell.Affected, "Maven", Log4jCore, "2.14.1");

        Assert.Equal(["2.3.1", "2.12.2", "2.15.0"], fix.AllFixedVersions);
    }

    [Fact]
    public void An_already_fixed_version_gets_no_fix_to_upgrade_to()
    {
        // 2.17.1 is past every listed range: nothing to upgrade to, and nothing at or above it is offered.
        var fix = OsvFixResolver.Resolve(Log4Shell.Affected, "Maven", Log4jCore, "2.17.1");

        Assert.Null(fix.FixedVersion);
        Assert.Equal(FixMatch.None, fix.Match);
    }

    // ── the situation behind "installed 2.0.1, fixed in 1.1.18" ────────────────

    [Fact]
    public void A_fix_on_another_release_line_is_never_offered_for_the_installed_line()
    {
        var affected = new[]
        {
            Affected("npm", "brace-expansion", "SEMVER", ("introduced", "0"), ("fixed", "1.1.18")),
            Affected("npm", "brace-expansion", "SEMVER", ("introduced", "2.0.0"), ("fixed", "2.0.3")),
            Affected("npm", "brace-expansion", "SEMVER", ("introduced", "5.0.0"), ("fixed", "5.0.5")),
        };

        var fix = OsvFixResolver.Resolve(affected, "npm", "brace-expansion", "2.0.1");

        Assert.Equal("2.0.3", fix.FixedVersion);     // not 1.1.18 (older than installed) and not 5.0.5 (another major)
    }

    // ── range mechanics ───────────────────────────────────────────────────────

    [Fact]
    public void The_fixed_version_itself_is_not_affected()
    {
        var affected = new[] { Affected("npm", "lib", "SEMVER", ("introduced", "0"), ("fixed", "2.0.0")) };

        Assert.Equal("2.0.0", OsvFixResolver.Resolve(affected, "npm", "lib", "1.9.9").FixedVersion);
        Assert.Null(OsvFixResolver.Resolve(affected, "npm", "lib", "2.0.0").FixedVersion);
    }

    [Fact]
    public void Last_affected_means_no_fix_is_known()
    {
        var affected = new[] { Affected("npm", "lib", "SEMVER", ("introduced", "0"), ("last_affected", "3.1.0")) };

        var fix = OsvFixResolver.Resolve(affected, "npm", "lib", "3.0.0");

        Assert.Null(fix.FixedVersion);
        Assert.Equal(FixMatch.None, fix.Match);
    }

    [Fact]
    public void A_range_with_no_end_is_still_affected_in_the_newest_release()
    {
        var affected = new[] { Affected("npm", "lib", "SEMVER", ("introduced", "1.0.0")) };

        Assert.Null(OsvFixResolver.Resolve(affected, "npm", "lib", "9.9.9").FixedVersion);
    }

    [Fact]
    public void Versions_before_the_introduced_version_are_not_in_the_range()
    {
        var affected = new[] { Affected("npm", "lib", "SEMVER", ("introduced", "2.0.0"), ("fixed", "2.5.0")) };

        // Outside the range: falls back to the nearest fix above, rather than claiming a range match.
        var fix = OsvFixResolver.Resolve(affected, "npm", "lib", "1.0.0");

        Assert.Equal("2.5.0", fix.FixedVersion);
        Assert.Equal(FixMatch.Fallback, fix.Match);
    }

    [Fact]
    public void Where_two_ranges_cover_the_version_the_higher_fix_wins()
    {
        var affected = new[]
        {
            Affected("npm", "lib", "SEMVER", ("introduced", "0"), ("fixed", "1.5.0")),
            Affected("npm", "lib", "SEMVER", ("introduced", "1.0.0"), ("fixed", "1.8.0")),
        };

        Assert.Equal("1.8.0", OsvFixResolver.Resolve(affected, "npm", "lib", "1.2.0").FixedVersion);
    }

    [Fact]
    public void Events_given_out_of_order_are_still_read_in_version_order()
    {
        var affected = new[] { Affected("npm", "lib", "SEMVER", ("fixed", "2.0.0"), ("introduced", "0")) };

        Assert.Equal("2.0.0", OsvFixResolver.Resolve(affected, "npm", "lib", "1.0.0").FixedVersion);
    }

    [Fact]
    public void Git_ranges_are_ignored_because_commit_hashes_are_not_versions()
    {
        var affected = new[] { Affected("npm", "lib", "GIT", ("introduced", "0"), ("fixed", "a1b2c3d4e5f6")) };

        var fix = OsvFixResolver.Resolve(affected, "npm", "lib", "1.0.0");

        Assert.Null(fix.FixedVersion);
        Assert.Empty(fix.AllFixedVersions);
    }

    [Fact]
    public void Only_the_named_package_in_the_named_ecosystem_is_considered()
    {
        var affected = new[]
        {
            Affected("npm", "uglify-js", "SEMVER", ("introduced", "0"), ("fixed", "2.4.24")),
            Affected("RubyGems", "uglifier", "ECOSYSTEM", ("introduced", "0"), ("fixed", "2.7.2")),
        };

        Assert.Equal("2.4.24", OsvFixResolver.Resolve(affected, "npm", "uglify-js", "1.3.5").FixedVersion);
        Assert.Equal("2.7.2", OsvFixResolver.Resolve(affected, "RubyGems", "uglifier", "1.0").FixedVersion);

        var unrelated = OsvFixResolver.Resolve(affected, "npm", "other", "1.0.0");
        Assert.False(unrelated.PackageMatched);          // lets the scanner fall back to its old behaviour
    }

    [Fact]
    public void Matches_package_names_the_way_each_ecosystem_does()
    {
        var pypi = new[] { Affected("PyPI", "Django-Rest.Framework", "ECOSYSTEM", ("introduced", "0"), ("fixed", "3.0")) };
        Assert.Equal("3.0", OsvFixResolver.Resolve(pypi, "PyPI", "django_rest_framework", "2.0").FixedVersion);   // PEP 503

        var npm = new[] { Affected("npm", "@Angular/Core", "SEMVER", ("introduced", "0"), ("fixed", "2.0.0")) };
        Assert.Equal("2.0.0", OsvFixResolver.Resolve(npm, "npm", "@angular/core", "1.0.0").FixedVersion);          // case-insensitive
    }

    [Fact]
    public void Real_uglify_js_record_resolves_to_its_npm_fix_and_not_the_rubygems_one()
    {
        var ghsa = Fixtures.Json<OsvVulnerability>("osv-GHSA-34r7-q49f-h37c.json");

        Assert.Equal("2.4.24", OsvFixResolver.Resolve(ghsa.Affected, "npm", "uglify-js", "1.3.5").FixedVersion);
    }
}

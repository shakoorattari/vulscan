using Vulscan.Application.Intel;

namespace Vulscan.Tests.Intel;

public class VersionComparerTests
{
    [Theory]
    // Numeric, not textual: 1.10 is newer than 1.9.
    [InlineData("npm", "1.10.0", "1.9.0", 1)]
    [InlineData("npm", "2.0.0", "2.0.0", 0)]
    [InlineData("npm", "2.0", "2.0.0", 0)]                     // missing segments are zero
    [InlineData("npm", "v1.2.3", "1.2.3", 0)]                  // Go-style prefix
    [InlineData("npm", "1.2.3+build.5", "1.2.3", 0)]           // build metadata never counts
    // A pre-release is older than its release, and pre-releases order among themselves.
    [InlineData("npm", "2.0.0-rc.1", "2.0.0", -1)]
    [InlineData("npm", "2.0.0-alpha", "2.0.0-beta", -1)]
    [InlineData("npm", "2.0.0-rc.2", "2.0.0-rc.10", -1)]       // numeric identifiers compare numerically
    [InlineData("npm", "2.0.0-1", "2.0.0-alpha", -1)]          // numeric sorts below alphanumeric
    [InlineData("npm", "2.0.0-alpha", "2.0.0-alpha.1", -1)]
    // NuGet's fourth segment.
    [InlineData("NuGet", "1.0.0.1", "1.0.0", 1)]
    [InlineData("NuGet", "6.0.0-preview.3", "6.0.0", -1)]
    public void Orders_semver_like_versions(string ecosystem, string a, string b, int expected)
    {
        Assert.Equal(expected, VersionComparer.Compare(ecosystem, a, b));
        Assert.Equal(-expected, VersionComparer.Compare(ecosystem, b, a));       // antisymmetric
    }

    [Theory]
    [InlineData("1.0", "1.0.0", 0)]
    [InlineData("1.0rc1", "1.0", -1)]
    [InlineData("1.0a1", "1.0b1", -1)]
    [InlineData("1.0b1", "1.0rc1", -1)]
    [InlineData("1.0.dev1", "1.0a1", -1)]                       // a dev release sorts before any pre-release
    [InlineData("1.0", "1.0.post1", -1)]
    [InlineData("1.0.post1", "1.0.1", -1)]
    [InlineData("1!1.0", "2.0", 1)]                             // epochs outrank everything
    [InlineData("2.0", "10.0", -1)]
    [InlineData("1.0+local.1", "1.0", 0)]
    public void Orders_pypi_versions_by_pep_440(string a, string b, int expected) =>
        Assert.Equal(expected, VersionComparer.Compare("PyPI", a, b));

    [Theory]
    [InlineData("1.0.0.pre", "1.0.0", -1)]                      // letters mark a pre-release
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("2.10", "2.9", 1)]
    [InlineData("1.0.0-beta", "1.0.0", -1)]
    public void Orders_rubygems_versions(string a, string b, int expected) =>
        Assert.Equal(expected, VersionComparer.Compare("RubyGems", a, b));

    [Theory]
    [InlineData("1.0-alpha", "1.0", -1)]
    [InlineData("1.0-rc1", "1.0", -1)]
    [InlineData("1.0", "1.0-sp1", -1)]
    [InlineData("2.0-beta9", "2.0", -1)]
    [InlineData("2.4", "2.12.2", -1)]
    [InlineData("2.15.0", "2.15.0", 0)]
    public void Orders_maven_versions(string a, string b, int expected) =>
        Assert.Equal(expected, VersionComparer.Compare("Maven", a, b));

    [Fact]
    public void Unparseable_versions_do_not_throw()
    {
        var result = VersionComparer.Compare("PyPI", "not-a-version", "1.0");
        Assert.InRange(result, -1, 1);
    }
}

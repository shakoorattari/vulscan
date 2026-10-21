using System.Text.RegularExpressions;

namespace Vulscan.Application.Intel;

/// <summary>Validation of advisory ids. They end up in URLs sent to third-party services, so the shape is strict.</summary>
public static partial class IntelId
{
    // CVE-2015-8857, GHSA-34r7-q49f-h37c, PYSEC-2021-1, GO-2022-0001, RUSTSEC-2020-0001, OSV-2020-1 …
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]{1,15}-[A-Za-z0-9][A-Za-z0-9._-]{0,80}$")]
    private static partial Regex Pattern();

    [GeneratedRegex(@"^CVE-\d{4}-\d{4,19}$", RegexOptions.IgnoreCase)]
    private static partial Regex CvePattern();

    public static bool IsValid(string? id) => !string.IsNullOrWhiteSpace(id) && Pattern().IsMatch(id.Trim());

    public static bool IsCve(string? id) => !string.IsNullOrWhiteSpace(id) && CvePattern().IsMatch(id.Trim());

    /// <summary>CVE ids are upper-case; other databases (GHSA-xxxx-xxxx-xxxx) are case-sensitive and kept as given.</summary>
    public static string Normalize(string id) => IsCve(id) ? id.Trim().ToUpperInvariant() : id.Trim();
}

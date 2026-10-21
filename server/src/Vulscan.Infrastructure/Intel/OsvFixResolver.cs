using System.Text.RegularExpressions;
using Vulscan.Application.Intel;
using Vulscan.Infrastructure.Clients;

namespace Vulscan.Infrastructure.Intel;

/// <summary>How a fixed version was determined.</summary>
public enum FixMatch
{
    /// <summary>The installed version lies inside an affected range, and that range ends at the fixed version.</summary>
    Range,

    /// <summary>The installed version is in no listed range; the nearest fixed version above it is offered.</summary>
    Fallback,

    /// <summary>No fixed version is known for this installed version.</summary>
    None,
}

/// <param name="FixedVersion">The version to upgrade to for the installed version, or null if none is known.</param>
/// <param name="AllFixedVersions">Every fixed version the advisory lists for the package, oldest first.</param>
/// <param name="PackageMatched">False when the advisory lists no package with this name and ecosystem at all.</param>
public sealed record FixResolution(string? FixedVersion, IReadOnlyList<string> AllFixedVersions, FixMatch Match, bool PackageMatched = true);

/// <summary>
/// Works out which version fixes an advisory <i>for the version actually installed</i>.
/// </summary>
/// <remarks>
/// An advisory can list several affected ranges for one package, one per release line — for example Log4Shell is
/// fixed in 2.3.1, 2.12.2 and 2.15.0 depending on whether you run 2.0, 2.4–2.12 or 2.13+. Taking the first
/// <c>fixed</c> event would tell everyone to jump to whichever line is listed first. Here the installed version is
/// located inside its range and that range's own fix is returned.
/// </remarks>
public static partial class OsvFixResolver
{
    private sealed record Interval(string Start, string? ExclusiveEnd, string? InclusiveEnd, string Ecosystem)
    {
        public bool IsBounded => ExclusiveEnd is not null;
    }

    public static FixResolution Resolve(
        IEnumerable<OsvAffected> affected, string ecosystem, string packageName, string installedVersion)
    {
        var candidates = affected.Where(a => Matches(a.Package, ecosystem, packageName)).ToList();
        if (candidates.Count == 0) return new FixResolution(null, [], FixMatch.None, PackageMatched: false);

        var intervals = candidates.SelectMany(a => a.Ranges).SelectMany(r => IntervalsOf(r, ecosystem)).ToList();

        var allFixed = intervals.Where(i => i.ExclusiveEnd is not null)
            .Select(i => i.ExclusiveEnd!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, Comparer<string>.Create((x, y) => VersionComparer.Compare(ecosystem, x, y)))
            .ToList();

        var containing = intervals.Where(i => Contains(i, installedVersion)).ToList();
        if (containing.Count > 0)
        {
            // Every range that covers the installed version must be left behind: take the highest fix, and if any
            // of them has no fix there is no version we can promise.
            if (containing.Any(i => !i.IsBounded)) return new FixResolution(null, allFixed, FixMatch.None);

            var fixedVersion = containing.Select(i => i.ExclusiveEnd!)
                .OrderByDescending(v => v, Comparer<string>.Create((x, y) => VersionComparer.Compare(ecosystem, x, y)))
                .First();
            return new FixResolution(fixedVersion, allFixed, FixMatch.Range);
        }

        // The installed version is in no range (the advisory lists it only in a "versions" array, or the data is
        // inconsistent). Offer the nearest fix above it rather than none.
        var above = allFixed.Where(v => VersionComparer.Compare(ecosystem, v, installedVersion) > 0).ToList();
        return above.Count > 0
            ? new FixResolution(above[0], allFixed, FixMatch.Fallback)
            : new FixResolution(null, allFixed, FixMatch.None);
    }

    private static IEnumerable<Interval> IntervalsOf(OsvRange range, string ecosystem)
    {
        // GIT ranges are commit hashes: they cannot be compared with a package version.
        if (!range.Type.Equals("SEMVER", StringComparison.OrdinalIgnoreCase) &&
            !range.Type.Equals("ECOSYSTEM", StringComparison.OrdinalIgnoreCase))
            yield break;

        // SEMVER ranges are always ordered as semantic versions; ECOSYSTEM ranges by the package ecosystem's rules.
        var order = range.Type.Equals("SEMVER", StringComparison.OrdinalIgnoreCase) ? "npm" : ecosystem;
        var comparer = Comparer<string>.Create((x, y) => VersionComparer.Compare(order, x, y));

        // Events are keyed by version; "introduced: 0" is the beginning of time.
        var events = range.Events
            .Select(e => (Version: e.Introduced ?? e.Fixed ?? e.LastAffected, Event: e))
            .Where(x => x.Version is not null)
            .OrderBy(x => x.Version == "0" ? null : x.Version, Comparer<string?>.Create((x, y) =>
                x is null ? (y is null ? 0 : -1) : y is null ? 1 : comparer.Compare(x, y)))
            .ToList();

        string? start = null;
        foreach (var (_, e) in events)
        {
            if (e.Introduced is not null) start ??= e.Introduced;
            else if (e.Fixed is not null && start is not null)
            {
                yield return new Interval(start, e.Fixed, null, order);
                start = null;
            }
            else if (e.LastAffected is not null && start is not null)
            {
                yield return new Interval(start, null, e.LastAffected, order);
                start = null;
            }
        }

        if (start is not null) yield return new Interval(start, null, null, order);   // still affected in the newest release
    }

    private static bool Contains(Interval interval, string installed)
    {
        if (interval.Start != "0" && VersionComparer.Compare(interval.Ecosystem, installed, interval.Start) < 0) return false;
        if (interval.ExclusiveEnd is not null) return VersionComparer.Compare(interval.Ecosystem, installed, interval.ExclusiveEnd) < 0;
        if (interval.InclusiveEnd is not null) return VersionComparer.Compare(interval.Ecosystem, installed, interval.InclusiveEnd) <= 0;
        return true;
    }

    private static bool Matches(OsvPackage package, string ecosystem, string name)
    {
        // OSV ecosystems can carry a release suffix ("Debian:11"); the package ecosystems we scan do not.
        var pkgEcosystem = package.Ecosystem.Split(':')[0];
        return pkgEcosystem.Equals(ecosystem, StringComparison.OrdinalIgnoreCase)
               && Normalize(ecosystem, package.Name) == Normalize(ecosystem, name);
    }

    /// <summary>PyPI names are case-insensitive and treat "-", "_" and "." as the same character (PEP 503).</summary>
    private static string Normalize(string ecosystem, string name) =>
        ecosystem.Equals("PyPI", StringComparison.OrdinalIgnoreCase)
            ? SeparatorRun().Replace(name.Trim(), "-").ToLowerInvariant()
            : name.Trim().ToLowerInvariant();

    [GeneratedRegex("[-_.]+")]
    private static partial Regex SeparatorRun();
}

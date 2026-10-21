using System.Text.RegularExpressions;

namespace Vulscan.Application.Intel;

/// <summary>
/// Orders package versions the way each ecosystem does. Ranges in advisories ("introduced 2.0.0, fixed 2.0.3") are
/// only meaningful under the right ordering: <c>1.10.0</c> is newer than <c>1.9.0</c>, and a pre-release such as
/// <c>2.0.0-rc.1</c> is older than <c>2.0.0</c>.
/// </summary>
/// <remarks>
/// Ecosystem names are OSV's: npm, NuGet, PyPI, Maven, Go, crates.io, Packagist, RubyGems. SemVer ordering is used
/// for npm, NuGet, Go, crates.io and Packagist (NuGet's four-part versions are handled by zero-padding). PyPI follows
/// PEP 440, RubyGems follows <c>Gem::Version</c>, and Maven uses an approximation of <c>ComparableVersion</c>.
/// </remarks>
public static partial class VersionComparer
{
    public static int Compare(string ecosystem, string a, string b)
    {
        var result = ecosystem.ToLowerInvariant() switch
        {
            "pypi" => ComparePep440(a, b),
            "rubygems" => CompareRubyGems(a, b),
            "maven" => CompareMaven(a, b),
            _ => CompareSemVerLike(a, b),
        };
        return Math.Sign(result);
    }

    // ── SemVer (npm, NuGet, Go, crates.io, Packagist) ───────────────────────

    private static int CompareSemVerLike(string a, string b)
    {
        var (coreA, preA) = SplitSemVer(a);
        var (coreB, preB) = SplitSemVer(b);

        for (var i = 0; i < Math.Max(coreA.Count, coreB.Count); i++)
        {
            var x = i < coreA.Count ? coreA[i] : 0;
            var y = i < coreB.Count ? coreB[i] : 0;
            if (x != y) return x < y ? -1 : 1;
        }

        // A version without a pre-release tag is newer than the same version with one.
        if (preA.Count == 0 || preB.Count == 0)
            return preA.Count == preB.Count ? 0 : preA.Count == 0 ? 1 : -1;

        for (var i = 0; i < Math.Min(preA.Count, preB.Count); i++)
        {
            var x = preA[i];
            var y = preB[i];
            var xNum = ulong.TryParse(x, out var xn);
            var yNum = ulong.TryParse(y, out var yn);

            int c;
            if (xNum && yNum) c = xn.CompareTo(yn);
            else if (xNum) c = -1;                  // numeric identifiers sort below alphanumeric ones
            else if (yNum) c = 1;
            else c = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);

            if (c != 0) return c;
        }

        return preA.Count.CompareTo(preB.Count);
    }

    private static (List<ulong> Core, List<string> Pre) SplitSemVer(string version)
    {
        var v = version.Trim();
        if (v.Length > 0 && (v[0] == 'v' || v[0] == 'V')) v = v[1..];

        var plus = v.IndexOf('+');
        if (plus >= 0) v = v[..plus];                // build metadata never affects precedence

        var dash = v.IndexOf('-');
        var core = dash >= 0 ? v[..dash] : v;
        var pre = dash >= 0 ? v[(dash + 1)..] : "";

        var nums = core.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => ulong.TryParse(p, out var n) ? n : 0UL)
            .ToList();
        var ids = pre.Length == 0 ? [] : pre.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        return (nums, ids);
    }

    // ── PyPI: PEP 440 ───────────────────────────────────────────────────────

    [GeneratedRegex(
        @"^v?(?:(?<epoch>\d+)!)?(?<release>\d+(?:\.\d+)*)" +
        @"(?:[-_.]?(?<pre>alpha|a|beta|b|preview|pre|c|rc)[-_.]?(?<pren>\d*))?" +
        @"(?:-(?<postn1>\d+)|[-_.]?(?<post>post|rev|r)[-_.]?(?<postn2>\d*))?" +
        @"(?:[-_.]?(?<dev>dev)[-_.]?(?<devn>\d*))?(?:\+[a-z0-9]+(?:[-_.][a-z0-9]+)*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pep440();

    private sealed record Pep(int Epoch, List<ulong> Release, int PreKind, ulong PreNum, bool HasPre, long? Post, long? Dev);

    private static Pep? ParsePep(string version)
    {
        var m = Pep440().Match(version.Trim());
        if (!m.Success) return null;

        int Num(string s) => s.Length == 0 ? 0 : int.Parse(s);
        var release = m.Groups["release"].Value.Split('.').Select(ulong.Parse).ToList();

        var hasPre = m.Groups["pre"].Success;
        var preKind = 0;
        if (hasPre)
        {
            preKind = m.Groups["pre"].Value.ToLowerInvariant() switch
            {
                "a" or "alpha" => 0,
                "b" or "beta" => 1,
                _ => 2,                              // c, rc, pre, preview
            };
        }

        long? post = null;
        if (m.Groups["postn1"].Success) post = long.Parse(m.Groups["postn1"].Value);
        else if (m.Groups["post"].Success) post = Num(m.Groups["postn2"].Value);

        long? dev = m.Groups["dev"].Success ? Num(m.Groups["devn"].Value) : null;

        return new Pep(
            m.Groups["epoch"].Success ? int.Parse(m.Groups["epoch"].Value) : 0,
            release, preKind, hasPre ? ulong.Parse(m.Groups["pren"].Value.Length == 0 ? "0" : m.Groups["pren"].Value) : 0,
            hasPre, post, dev);
    }

    private static int ComparePep440(string a, string b)
    {
        var x = ParsePep(a);
        var y = ParsePep(b);
        if (x is null || y is null) return CompareSemVerLike(a, b);   // not PEP 440; do the best we can

        if (x.Epoch != y.Epoch) return x.Epoch.CompareTo(y.Epoch);

        for (var i = 0; i < Math.Max(x.Release.Count, y.Release.Count); i++)
        {
            var p = i < x.Release.Count ? x.Release[i] : 0;
            var q = i < y.Release.Count ? y.Release[i] : 0;
            if (p != q) return p.CompareTo(q);
        }

        // A dev release of a final version sorts before any pre-release of it; a final release sorts after them.
        static (int Rank, int Kind, ulong Num) PreKey(Pep v) =>
            v.HasPre ? (1, v.PreKind, v.PreNum)
            : v.Post is null && v.Dev is not null ? (0, 0, 0UL)
            : (2, 0, 0UL);

        var pre = PreKey(x).CompareTo(PreKey(y));
        if (pre != 0) return pre;

        var post = (x.Post ?? long.MinValue).CompareTo(y.Post ?? long.MinValue);
        if (post != 0) return post;

        return (x.Dev ?? long.MaxValue).CompareTo(y.Dev ?? long.MaxValue);
    }

    // ── RubyGems ────────────────────────────────────────────────────────────

    private static int CompareRubyGems(string a, string b)
    {
        static List<string> Segments(string v) =>
            [.. Regex.Matches(v.Trim().Replace("-", ".pre."), @"\d+|[a-zA-Z]+").Select(m => m.Value)];

        var x = Segments(a);
        var y = Segments(b);
        for (var i = 0; i < Math.Max(x.Count, y.Count); i++)
        {
            var p = i < x.Count ? x[i] : "0";
            var q = i < y.Count ? y[i] : "0";
            var pNum = ulong.TryParse(p, out var pn);
            var qNum = ulong.TryParse(q, out var qn);

            int c;
            if (pNum && qNum) c = pn.CompareTo(qn);
            else if (!pNum && !qNum) c = string.Compare(p, q, StringComparison.OrdinalIgnoreCase);
            else c = pNum ? 1 : -1;                  // letters mark a pre-release, which sorts below the release
            if (c != 0) return c;
        }

        return 0;
    }

    // ── Maven (approximation of ComparableVersion) ──────────────────────────

    private static int CompareMaven(string a, string b)
    {
        static List<string> Tokens(string v) =>
            [.. Regex.Matches(v.Trim().ToLowerInvariant(), @"\d+|[a-z]+").Select(m => m.Value)];

        static int Rank(string qualifier) => qualifier switch
        {
            "alpha" or "a" => 1,
            "beta" or "b" => 2,
            "milestone" or "m" => 3,
            "rc" or "cr" => 4,
            "snapshot" => 5,
            "" or "ga" or "final" or "release" => 6,
            "sp" => 7,
            _ => 8,                                  // unknown qualifiers sort after the known ones
        };

        var x = Tokens(a);
        var y = Tokens(b);
        for (var i = 0; i < Math.Max(x.Count, y.Count); i++)
        {
            var p = i < x.Count ? x[i] : null;
            var q = i < y.Count ? y[i] : null;

            var pNum = p is not null && ulong.TryParse(p, out _);
            var qNum = q is not null && ulong.TryParse(q, out _);

            int c;
            if (p is null) c = qNum ? (ulong.Parse(q!) == 0 ? 0 : -1) : Rank("").CompareTo(Rank(q!));
            else if (q is null) c = pNum ? (ulong.Parse(p) == 0 ? 0 : 1) : Rank(p).CompareTo(Rank(""));
            else if (pNum && qNum) c = ulong.Parse(p).CompareTo(ulong.Parse(q));
            else if (pNum) c = 1;                    // 1.1 > 1-sp
            else if (qNum) c = -1;
            else
            {
                var rp = Rank(p);
                var rq = Rank(q);
                c = rp != rq ? rp.CompareTo(rq) : rp == 8 ? string.CompareOrdinal(p, q) : 0;
            }

            if (c != 0) return c;
        }

        return 0;
    }
}

using System.Globalization;

namespace Vulscan.Application.Intel;

/// <summary>One decoded CVSS metric, e.g. <c>Attack Vector = Network</c>.</summary>
public sealed record CvssMetric(string Code, string Name, string Value);

/// <summary>A parsed CVSS vector. <see cref="Score"/> is null when this class cannot compute it (CVSS 4.0).</summary>
public sealed record CvssResult(string Version, string Vector, double? Score, string? Severity, IReadOnlyList<CvssMetric> Metrics);

/// <summary>
/// CVSS base-score calculator for versions 3.0, 3.1 and 2.0, implemented from the FIRST specifications.
/// CVSS 4.0 vectors are decoded into readable metrics but not scored (the 4.0 score comes from a lookup table of
/// pre-computed macro-vectors); use the score a source such as NVD publishes for those.
/// </summary>
public static class CvssCalculator
{
    /// <summary>Parses a vector string of any supported version; returns null if it is not a valid CVSS vector.</summary>
    public static CvssResult? Parse(string? vector)
    {
        if (string.IsNullOrWhiteSpace(vector)) return null;
        vector = vector.Trim();

        if (vector.StartsWith("CVSS:3.", StringComparison.OrdinalIgnoreCase)) return ParseV3(vector);
        if (vector.StartsWith("CVSS:4.0", StringComparison.OrdinalIgnoreCase)) return ParseV4(vector);
        if (vector.StartsWith("CVSS:", StringComparison.OrdinalIgnoreCase)) return null;
        return ParseV2(vector);
    }

    /// <summary>Qualitative rating for a CVSS 3.x/4.0 score (None, Low, Medium, High, Critical).</summary>
    public static string SeverityV3(double score) => score switch
    {
        0 => "None",
        < 4.0 => "Low",
        < 7.0 => "Medium",
        < 9.0 => "High",
        _ => "Critical",
    };

    /// <summary>Qualitative rating for a CVSS 2.0 score (Low, Medium, High).</summary>
    public static string SeverityV2(double score) => score switch
    {
        < 4.0 => "Low",
        < 7.0 => "Medium",
        _ => "High",
    };

    // ── CVSS 3.x ────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, (string Name, Dictionary<string, (string Label, double Weight)> Values)> V3Base = new()
    {
        ["AV"] = ("Attack Vector", new() { ["N"] = ("Network", 0.85), ["A"] = ("Adjacent", 0.62), ["L"] = ("Local", 0.55), ["P"] = ("Physical", 0.2) }),
        ["AC"] = ("Attack Complexity", new() { ["L"] = ("Low", 0.77), ["H"] = ("High", 0.44) }),
        ["PR"] = ("Privileges Required", new() { ["N"] = ("None", 0.85), ["L"] = ("Low", 0.62), ["H"] = ("High", 0.27) }),
        ["UI"] = ("User Interaction", new() { ["N"] = ("None", 0.85), ["R"] = ("Required", 0.62) }),
        ["S"] = ("Scope", new() { ["U"] = ("Unchanged", 0), ["C"] = ("Changed", 0) }),
        ["C"] = ("Confidentiality", new() { ["H"] = ("High", 0.56), ["L"] = ("Low", 0.22), ["N"] = ("None", 0) }),
        ["I"] = ("Integrity", new() { ["H"] = ("High", 0.56), ["L"] = ("Low", 0.22), ["N"] = ("None", 0) }),
        ["A"] = ("Availability", new() { ["H"] = ("High", 0.56), ["L"] = ("Low", 0.22), ["N"] = ("None", 0) }),
    };

    private static CvssResult? ParseV3(string vector)
    {
        var parts = vector.Split('/');
        var version = parts[0][5..];                       // "3.0" / "3.1"
        if (version is not ("3.0" or "3.1")) return null;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts.Skip(1))
        {
            var kv = part.Split(':');
            if (kv.Length != 2 || values.ContainsKey(kv[0])) return null;
            values[kv[0]] = kv[1].ToUpperInvariant();
        }

        var metrics = new List<CvssMetric>();
        foreach (var (code, (name, allowed)) in V3Base)
        {
            if (!values.TryGetValue(code, out var v) || !allowed.TryGetValue(v, out var entry)) return null;
            metrics.Add(new CvssMetric(code, name, entry.Label));
        }

        var scopeChanged = values["S"] == "C";
        double W(string code) => V3Base[code].Values[values[code]].Weight;

        // Privileges Required has different weights when the scope changes.
        var pr = values["PR"] switch
        {
            "N" => 0.85,
            "L" => scopeChanged ? 0.68 : 0.62,
            _ => scopeChanged ? 0.5 : 0.27,
        };

        var iss = 1 - ((1 - W("C")) * (1 - W("I")) * (1 - W("A")));
        var impact = scopeChanged
            ? 7.52 * (iss - 0.029) - 3.25 * Math.Pow(iss - 0.02, 15)
            : 6.42 * iss;
        var exploitability = 8.22 * W("AV") * W("AC") * pr * W("UI");

        double score;
        if (impact <= 0) score = 0;
        else
        {
            var raw = scopeChanged ? Math.Min(1.08 * (impact + exploitability), 10) : Math.Min(impact + exploitability, 10);
            score = version == "3.1" ? Roundup31(raw) : Math.Ceiling(raw * 10) / 10;
        }

        return new CvssResult(version, vector, score, SeverityV3(score), metrics);
    }

    /// <summary>The "Roundup" function of CVSS 3.1 (spec appendix A), which avoids floating-point surprises.</summary>
    private static double Roundup31(double input)
    {
        var intInput = (long)Math.Round(input * 100000, MidpointRounding.AwayFromZero);
        return intInput % 10000 == 0
            ? intInput / 100000.0
            : (Math.Floor(intInput / 10000.0) + 1) / 10.0;
    }

    // ── CVSS 2.0 ────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, (string Name, Dictionary<string, (string Label, double Weight)> Values)> V2Base = new()
    {
        ["AV"] = ("Access Vector", new() { ["L"] = ("Local", 0.395), ["A"] = ("Adjacent Network", 0.646), ["N"] = ("Network", 1.0) }),
        ["AC"] = ("Access Complexity", new() { ["H"] = ("High", 0.35), ["M"] = ("Medium", 0.61), ["L"] = ("Low", 0.71) }),
        ["Au"] = ("Authentication", new() { ["M"] = ("Multiple", 0.45), ["S"] = ("Single", 0.56), ["N"] = ("None", 0.704) }),
        ["C"] = ("Confidentiality", new() { ["N"] = ("None", 0), ["P"] = ("Partial", 0.275), ["C"] = ("Complete", 0.660) }),
        ["I"] = ("Integrity", new() { ["N"] = ("None", 0), ["P"] = ("Partial", 0.275), ["C"] = ("Complete", 0.660) }),
        ["A"] = ("Availability", new() { ["N"] = ("None", 0), ["P"] = ("Partial", 0.275), ["C"] = ("Complete", 0.660) }),
    };

    private static CvssResult? ParseV2(string vector)
    {
        // Some sources prefix the vector with parentheses or "CVSS2#".
        var trimmed = vector.Trim('(', ')');
        if (trimmed.StartsWith("CVSS2#", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed[6..];

        var values = new Dictionary<string, string>();
        foreach (var part in trimmed.Split('/'))
        {
            var kv = part.Split(':');
            if (kv.Length != 2 || values.ContainsKey(kv[0])) return null;
            values[kv[0]] = kv[1];
        }

        var metrics = new List<CvssMetric>();
        foreach (var (code, (name, allowed)) in V2Base)
        {
            if (!values.TryGetValue(code, out var v) || !allowed.TryGetValue(v, out var entry)) return null;
            metrics.Add(new CvssMetric(code, name, entry.Label));
        }

        double W(string code) => V2Base[code].Values[values[code]].Weight;
        var impact = 10.41 * (1 - ((1 - W("C")) * (1 - W("I")) * (1 - W("A"))));
        var exploitability = 20 * W("AV") * W("AC") * W("Au");
        var f = impact == 0 ? 0 : 1.176;
        var score = Math.Round(((0.6 * impact) + (0.4 * exploitability) - 1.5) * f, 1, MidpointRounding.AwayFromZero);

        return new CvssResult("2.0", trimmed, score, SeverityV2(score), metrics);
    }

    // ── CVSS 4.0 (decode only) ──────────────────────────────────────────────

    private static readonly (string Code, string Name, Dictionary<string, string> Values)[] V4Base =
    [
        ("AV", "Attack Vector", new() { ["N"] = "Network", ["A"] = "Adjacent", ["L"] = "Local", ["P"] = "Physical" }),
        ("AC", "Attack Complexity", new() { ["L"] = "Low", ["H"] = "High" }),
        ("AT", "Attack Requirements", new() { ["N"] = "None", ["P"] = "Present" }),
        ("PR", "Privileges Required", new() { ["N"] = "None", ["L"] = "Low", ["H"] = "High" }),
        ("UI", "User Interaction", new() { ["N"] = "None", ["P"] = "Passive", ["A"] = "Active" }),
        ("VC", "Vulnerable System Confidentiality", new() { ["H"] = "High", ["L"] = "Low", ["N"] = "None" }),
        ("VI", "Vulnerable System Integrity", new() { ["H"] = "High", ["L"] = "Low", ["N"] = "None" }),
        ("VA", "Vulnerable System Availability", new() { ["H"] = "High", ["L"] = "Low", ["N"] = "None" }),
        ("SC", "Subsequent System Confidentiality", new() { ["H"] = "High", ["L"] = "Low", ["N"] = "None" }),
        ("SI", "Subsequent System Integrity", new() { ["H"] = "High", ["L"] = "Low", ["N"] = "None" }),
        ("SA", "Subsequent System Availability", new() { ["H"] = "High", ["L"] = "Low", ["N"] = "None" }),
    ];

    private static CvssResult? ParseV4(string vector)
    {
        var values = new Dictionary<string, string>();
        foreach (var part in vector.Split('/').Skip(1))
        {
            var kv = part.Split(':');
            if (kv.Length != 2 || values.ContainsKey(kv[0])) return null;
            values[kv[0]] = kv[1].ToUpperInvariant();
        }

        var metrics = new List<CvssMetric>();
        foreach (var (code, name, allowed) in V4Base)
        {
            if (!values.TryGetValue(code, out var v) || !allowed.TryGetValue(v, out var label)) return null;
            metrics.Add(new CvssMetric(code, name, label));
        }

        return new CvssResult("4.0", vector, null, null, metrics);
    }

    /// <summary>Formats a score the way advisories do (one decimal, invariant culture).</summary>
    public static string Format(double score) => score.ToString("0.0", CultureInfo.InvariantCulture);
}

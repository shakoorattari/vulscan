using System.Globalization;
using System.Text.RegularExpressions;
using Vulscan.Application.DTOs.Intel;
using Vulscan.Application.Intel;
using Vulscan.Infrastructure.Clients;

namespace Vulscan.Infrastructure.Intel;

/// <summary>What each source answered for one advisory.</summary>
public sealed record IntelInputs(
    string Id,
    string? CveId,
    SourceResult<OsvVulnerability> Osv,
    IReadOnlyList<OsvVulnerability> OsvRelated,
    SourceResult<NvdCve> Nvd,
    SourceResult<IReadOnlyDictionary<string, EpssScore>> Epss,
    SourceResult<KevCatalog> Kev,
    DateTime Now);

public sealed record IntelMergeResult(VulnerabilityIntelDto Dto, bool HasData, bool NeedsRetry);

/// <summary>
/// Combines what OSV, NVD, EPSS and CISA each say about an advisory into one record. Pure: no I/O, so every rule
/// here is covered by tests against recorded real responses.
/// </summary>
public static partial class IntelMerger
{
    private const int MaxReferences = 150;
    private const int MaxAffectedPackages = 60;
    private const int MaxProducts = 40;
    private const int MaxDescription = 100_000;

    public static IntelMergeResult Merge(IntelInputs i)
    {
        var osv = new List<OsvVulnerability>();
        if (i.Osv.IsOk && i.Osv.Value is not null) osv.Add(i.Osv.Value);
        osv.AddRange(i.OsvRelated);

        var nvd = i.Nvd.IsOk ? i.Nvd.Value : null;
        var github = osv.FirstOrDefault(o => o.Id.StartsWith("GHSA-", StringComparison.OrdinalIgnoreCase));

        // Title and description ------------------------------------------------------------------
        var (description, format, descriptionSource) = PickDescription(osv, nvd);
        var title = FirstNonEmpty(github?.Summary, osv.Select(o => o.Summary).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)))
                    ?? FirstSentence(description);

        // Dates ----------------------------------------------------------------------------------
        var published = ParseDate(nvd?.Published)
                        ?? ParseDate(osv.FirstOrDefault(o => o.Id.StartsWith("CVE-", StringComparison.OrdinalIgnoreCase))?.Published)
                        ?? osv.Select(o => ParseDate(o.Published)).Where(d => d is not null).Min();
        var modified = new[] { ParseDate(nvd?.LastModified) }.Concat(osv.Select(o => ParseDate(o.Modified)))
            .Where(d => d is not null).Max();
        var withdrawn = osv.Select(o => ParseDate(o.Withdrawn)).FirstOrDefault(d => d is not null);

        // Aliases --------------------------------------------------------------------------------
        var aliases = osv.SelectMany(o => o.Aliases.Append(o.Id))
            .Append(nvd?.Id)
            .Where(a => !string.IsNullOrWhiteSpace(a) && !a!.Equals(i.Id, StringComparison.OrdinalIgnoreCase))
            .Select(a => IntelId.Normalize(a!))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(a => IntelId.IsCve(a) ? 0 : 1).ThenBy(a => a, StringComparer.Ordinal)
            .ToList();

        // Scores ---------------------------------------------------------------------------------
        var ratings = BuildRatings(osv, nvd);
        var primary = ratings.FirstOrDefault(r => r.IsPrimary);
        var vendorSeverity = NormalizeVendorSeverity(osv.Select(o => o.DatabaseSpecific?.Severity)
            .Concat(osv.SelectMany(o => o.Affected.Select(a => a.DatabaseSpecific?.Severity)))
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)));
        var severity = primary?.Severity ?? vendorSeverity;

        // Everything else ------------------------------------------------------------------------
        var epss = i.CveId is not null && i.Epss.IsOk && i.Epss.Value is not null && i.Epss.Value.TryGetValue(i.CveId, out var e)
            ? new EpssDto(e.Score, e.Percentile, e.Date)
            : null;

        var kev = i.CveId is not null && i.Kev.IsOk && i.Kev.Value is not null && i.Kev.Value.Entries.TryGetValue(i.CveId, out var k)
            ? ToKevDto(k)
            : null;

        var dto = new VulnerabilityIntelDto
        {
            Id = i.Id,
            Title = Truncate(title, 300),
            Description = Truncate(description, MaxDescription),
            DescriptionFormat = format,
            DescriptionSource = descriptionSource,
            Published = published,
            Modified = modified,
            Withdrawn = withdrawn,
            Aliases = aliases,
            Severity = severity,
            Score = primary?.Score,
            VendorSeverity = vendorSeverity,
            Cvss = ratings,
            Weaknesses = BuildWeaknesses(osv, nvd),
            Epss = epss,
            Kev = kev,
            References = BuildReferences(osv, nvd),
            Affected = BuildAffected(osv),
            Products = BuildProducts(nvd),
            Sources = BuildSources(i, nvd),
            FetchedAt = i.Now,
        };

        var hasData = !string.IsNullOrWhiteSpace(dto.Description) || dto.Cvss.Count > 0 || dto.References.Count > 0
                      || dto.Affected.Count > 0 || dto.Kev is not null;
        var needsRetry = dto.Sources.Any(s => s.Status is "error" or "skipped");
        return new IntelMergeResult(dto, hasData, needsRetry);
    }

    internal static KevDto ToKevDto(KevEntry k) => new()
    {
        Name = k.Name, VendorProject = k.VendorProject, Product = k.Product, ShortDescription = k.ShortDescription,
        DateAdded = k.DateAdded, DueDate = k.DueDate, RequiredAction = k.RequiredAction,
        RansomwareUse = k.RansomwareUse, Notes = k.Notes,
    };

    // ── Description ─────────────────────────────────────────────────────────

    private static (string? Text, string Format, string? Source) PickDescription(List<OsvVulnerability> osv, NvdCve? nvd)
    {
        // GitHub and other package databases write Markdown with upgrade advice; the CVE record is the bare NVD text.
        var rich = osv.Where(o => !o.Id.StartsWith("CVE-", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(o.Details))
            .OrderBy(o => o.Id.StartsWith("GHSA-", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .FirstOrDefault();
        if (rich is not null)
            return (rich.Details!.Trim(), "markdown", rich.Id.StartsWith("GHSA-", StringComparison.OrdinalIgnoreCase) ? "GitHub Advisory" : "OSV");

        var nvdText = nvd?.Descriptions.FirstOrDefault(d => d.Lang == "en" && !string.IsNullOrWhiteSpace(d.Value))?.Value;
        if (!string.IsNullOrWhiteSpace(nvdText)) return (nvdText.Trim(), "text", "NVD");

        var cveRecord = osv.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.Details));
        return cveRecord is not null ? (cveRecord.Details!.Trim(), "text", "OSV") : (null, "text", null);
    }

    private static string? FirstSentence(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var flat = WhitespaceRun().Replace(text, " ").Trim();
        var end = flat.IndexOf(". ", StringComparison.Ordinal);
        var sentence = end > 0 ? flat[..(end + 1)] : flat;
        return sentence.Length > 200 ? sentence[..197] + "…" : sentence;
    }

    // ── CVSS ────────────────────────────────────────────────────────────────

    private sealed record Candidate(string Vector, double? ProvidedScore, string Source, int Rank);

    private static List<CvssRatingDto> BuildRatings(List<OsvVulnerability> osv, NvdCve? nvd)
    {
        var candidates = new List<Candidate>();

        // Rank decides which score is "the" score: NVD's own analysis first, then anyone's CVSS 3.x, then 4.0, then 2.0.
        void AddNvd(List<NvdCvssMetric> metrics, int baseRank)
        {
            foreach (var m in metrics)
            {
                if (string.IsNullOrWhiteSpace(m.CvssData?.VectorString)) continue;
                var isNvd = string.Equals(m.Source, "nvd@nist.gov", StringComparison.OrdinalIgnoreCase);
                candidates.Add(new Candidate(m.CvssData.VectorString!, m.CvssData.BaseScore, SourceLabel(m.Source),
                    baseRank + (isNvd && m.Type == "Primary" ? 0 : 1)));
            }
        }

        if (nvd?.Metrics is { } nm)
        {
            AddNvd(nm.V31, 0);
            AddNvd(nm.V30, 2);
            AddNvd(nm.V40, 6);
            AddNvd(nm.V2, 8);
        }

        foreach (var o in osv)
        {
            var label = o.Id.StartsWith("GHSA-", StringComparison.OrdinalIgnoreCase) ? "GitHub"
                : o.Id.StartsWith("CVE-", StringComparison.OrdinalIgnoreCase) ? "CVE record (via OSV)" : "OSV";
            foreach (var s in o.Severity)
            {
                if (string.IsNullOrWhiteSpace(s.Score)) continue;
                var parsed = CvssCalculator.Parse(s.Score);
                var rank = parsed?.Version switch { "3.1" or "3.0" => 4, "4.0" => 7, "2.0" => 9, _ => 10 };
                candidates.Add(new Candidate(s.Score!, null, label, rank));
            }
        }

        var ratings = new List<(CvssRatingDto Rating, int Rank)>();
        foreach (var group in candidates.GroupBy(c => (c.Vector.Trim(), CvssCalculator.Parse(c.Vector)?.Version ?? "?")))
        {
            var parsed = CvssCalculator.Parse(group.Key.Item1);
            if (parsed is null) continue;                                  // not a vector we understand

            var provided = group.Select(c => c.ProvidedScore).FirstOrDefault(s => s is not null);
            var score = provided ?? parsed.Score;                          // CVSS 4.0 has only the published score
            var severity = score is null ? null
                : parsed.Version == "2.0" ? CvssCalculator.SeverityV2(score.Value)
                : CvssCalculator.SeverityV3(score.Value);

            ratings.Add((new CvssRatingDto
            {
                Version = parsed.Version,
                Vector = parsed.Vector,
                Score = score,
                Severity = severity,
                Source = string.Join(", ", group.Select(c => c.Source).Distinct()),
                Metrics = parsed.Metrics.Select(m => new CvssMetricDto(m.Code, m.Name, m.Value)).ToList(),
            }, group.Min(c => c.Rank)));
        }

        var ordered = ratings.OrderBy(r => r.Rank).ThenByDescending(r => r.Rating.Version, StringComparer.Ordinal).ToList();
        return ordered.Select((r, index) => r.Rating with { IsPrimary = index == 0 }).ToList();
    }

    private static string SourceLabel(string? source) => source?.ToLowerInvariant() switch
    {
        "nvd@nist.gov" => "NVD",
        "cve@mitre.org" => "MITRE",
        "security-advisories@github.com" => "GitHub",
        null or "" => "NVD",
        _ => "CNA",
    };

    private static string? NormalizeVendorSeverity(string? label) => label?.Trim().ToUpperInvariant() switch
    {
        "CRITICAL" => "Critical",
        "HIGH" => "High",
        "MODERATE" or "MEDIUM" => "Medium",
        "LOW" => "Low",
        _ => null,
    };

    // ── Weaknesses ──────────────────────────────────────────────────────────

    private static List<WeaknessDto> BuildWeaknesses(List<OsvVulnerability> osv, NvdCve? nvd)
    {
        var found = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void Add(string? id, string source)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            id = id.Trim();
            if (!found.TryGetValue(id, out var sources)) found[id] = sources = [];
            if (!sources.Contains(source)) sources.Add(source);
        }

        foreach (var w in nvd?.Weaknesses ?? [])
            foreach (var d in w.Description) Add(d.Value, "NVD");
        foreach (var o in osv)
            foreach (var cwe in o.DatabaseSpecific?.CweIds ?? []) Add(cwe, o.Id.StartsWith("GHSA-", StringComparison.OrdinalIgnoreCase) ? "GitHub" : "OSV");

        return found
            .Select(kv => new WeaknessDto
            {
                Id = kv.Key,
                Url = CweNumber().Match(kv.Key) is { Success: true } m ? $"https://cwe.mitre.org/data/definitions/{m.Groups[1].Value}.html" : null,
                Sources = kv.Value,
            })
            .OrderBy(w => w.Url is null ? 1 : 0).ThenBy(w => w.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ── References ──────────────────────────────────────────────────────────

    private static readonly string[] KindOrder = ["fix", "advisory", "exploit", "issue", "package", "article", "other"];

    private static List<IntelReferenceDto> BuildReferences(List<OsvVulnerability> osv, NvdCve? nvd)
    {
        var byKey = new Dictionary<string, (string Url, string Host, SortedSet<string> Kinds, List<string> Labels, List<string> Sources)>();

        void Add(string? rawUrl, string? label, string kind, string source)
        {
            var url = SafeUrl(rawUrl, out var host, out var key);
            if (url is null) return;

            if (!byKey.TryGetValue(key, out var entry))
                byKey[key] = entry = (url, host, new SortedSet<string>(Comparer<string>.Create((a, b) => Array.IndexOf(KindOrder, a).CompareTo(Array.IndexOf(KindOrder, b)))), [], []);
            entry.Kinds.Add(kind);
            if (!string.IsNullOrWhiteSpace(label) && !entry.Labels.Contains(label)) entry.Labels.Add(label);
            if (!entry.Sources.Contains(source)) entry.Sources.Add(source);
        }

        foreach (var o in osv)
            foreach (var r in o.References)
                Add(r.Url, r.Type, OsvKind(r.Type), "OSV");

        foreach (var r in nvd?.References ?? [])
        {
            if (r.Tags.Count == 0) Add(r.Url, null, "other", "NVD");
            foreach (var tag in r.Tags) Add(r.Url, tag, NvdKind(tag), "NVD");
        }

        return byKey.Values
            .Select(v => new IntelReferenceDto
            {
                Url = v.Url,
                Host = v.Host,
                // A link that is known to be something specific shouldn't also be filed under "other".
                Kinds = v.Kinds.Count > 1 ? v.Kinds.Where(k => k != "other").ToList() : v.Kinds.ToList(),
                Labels = v.Labels,
                Sources = v.Sources,
            })
            .OrderBy(r => Array.IndexOf(KindOrder, r.Kinds[0])).ThenBy(r => r.Host, StringComparer.Ordinal).ThenBy(r => r.Url, StringComparer.Ordinal)
            .Take(MaxReferences)
            .ToList();
    }

    private static string OsvKind(string? type) => type?.ToUpperInvariant() switch
    {
        "FIX" => "fix",
        "ADVISORY" => "advisory",
        "REPORT" or "DISCUSSION" => "issue",
        "ARTICLE" => "article",
        "PACKAGE" => "package",
        _ => "other",                              // WEB, EVIDENCE, DETECTION, GIT, INTRODUCED
    };

    private static string NvdKind(string tag) => tag switch
    {
        "Patch" => "fix",
        "Vendor Advisory" or "Third Party Advisory" or "US Government Resource" or "Mitigation" => "advisory",
        "Exploit" => "exploit",
        "Issue Tracking" => "issue",
        "Product" => "package",
        "Mailing List" or "VDB Entry" or "Press/Media Coverage" or "Technical Description" or "Release Notes" => "article",
        _ => "other",
    };

    /// <summary>
    /// Only absolute http(s) links are kept. References come from third-party data, and a <c>javascript:</c> or
    /// <c>data:</c> URL that reached a link in the page would be an injection vector.
    /// </summary>
    internal static string? SafeUrl(string? raw, out string host, out string key)
    {
        host = key = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var text = raw.Trim();
        if (text.Length > 2000 || text.Any(char.IsControl) || text.Any(char.IsWhiteSpace)) return null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        if (string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)) return null;

        host = uri.Host.ToLowerInvariant();
        key = $"{uri.Scheme}://{host}{(uri.IsDefaultPort ? "" : ":" + uri.Port)}{uri.AbsolutePath.TrimEnd('/')}{uri.Query}";
        return text;
    }

    // ── Affected packages and products ──────────────────────────────────────

    private static List<AffectedPackageDto> BuildAffected(List<OsvVulnerability> osv)
    {
        var byPackage = new Dictionary<(string, string), (string? Purl, List<AffectedRangeDto> Ranges, int Versions)>();

        foreach (var a in osv.SelectMany(o => o.Affected))
        {
            if (string.IsNullOrWhiteSpace(a.Package.Name)) continue;
            var key = (a.Package.Ecosystem, a.Package.Name);
            if (!byPackage.TryGetValue(key, out var entry)) entry = (a.Package.Purl, [], 0);

            foreach (var range in a.Ranges)
            {
                if (!range.Type.Equals("SEMVER", StringComparison.OrdinalIgnoreCase) &&
                    !range.Type.Equals("ECOSYSTEM", StringComparison.OrdinalIgnoreCase)) continue;   // GIT: commit hashes

                string? introduced = null;
                foreach (var e in range.Events)
                {
                    if (e.Introduced is not null) introduced ??= e.Introduced;
                    else if (e.Fixed is not null || e.LastAffected is not null)
                    {
                        AddRange(entry.Ranges, new AffectedRangeDto { Introduced = introduced, Fixed = e.Fixed, LastAffected = e.LastAffected });
                        introduced = null;
                    }
                }
                if (introduced is not null) AddRange(entry.Ranges, new AffectedRangeDto { Introduced = introduced });
            }

            byPackage[key] = (entry.Purl ?? a.Package.Purl, entry.Ranges, Math.Max(entry.Versions, a.Versions.Count));
        }

        return byPackage
            .Select(kv => new AffectedPackageDto
            {
                Ecosystem = kv.Key.Item1,
                Name = kv.Key.Item2,
                Purl = kv.Value.Purl,
                Ranges = kv.Value.Ranges,
                VersionsListed = kv.Value.Versions,
            })
            .OrderBy(p => p.Ecosystem, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxAffectedPackages)
            .ToList();

        static void AddRange(List<AffectedRangeDto> list, AffectedRangeDto range)
        {
            if (!list.Contains(range)) list.Add(range);
        }
    }

    private static List<AffectedProductDto> BuildProducts(NvdCve? nvd)
    {
        var products = new List<AffectedProductDto>();
        foreach (var m in nvd?.Configurations.SelectMany(c => c.Nodes).SelectMany(n => n.CpeMatch) ?? [])
        {
            if (!m.Vulnerable || string.IsNullOrWhiteSpace(m.Criteria)) continue;

            // cpe:2.3:<part>:<vendor>:<product>:<version>:<update>:…
            var parts = m.Criteria.Split(':');
            if (parts.Length < 6 || parts[1] != "2.3") continue;

            var product = new AffectedProductDto
            {
                Vendor = Unescape(parts[3]),
                Product = Unescape(parts[4]),
                Version = parts[5] is "*" or "-" ? null : Unescape(parts[5]),
                StartIncluding = m.VersionStartIncluding,
                StartExcluding = m.VersionStartExcluding,
                EndIncluding = m.VersionEndIncluding,
                EndExcluding = m.VersionEndExcluding,
            };
            if (!products.Contains(product)) products.Add(product);
            if (products.Count >= MaxProducts) break;
        }
        return products;

        static string Unescape(string s) => s.Replace("\\", "");
    }

    // ── Sources ─────────────────────────────────────────────────────────────

    private static List<IntelSourceDto> BuildSources(IntelInputs i, NvdCve? nvd)
    {
        var sources = new List<IntelSourceDto>
        {
            new()
            {
                Name = "OSV",
                Status = Status(i.Osv.Status),
                FetchedAt = i.Now,
                Message = i.Osv.Message,
                Url = i.Osv.IsOk ? $"https://osv.dev/vulnerability/{Uri.EscapeDataString(i.Osv.Value!.Id)}" : null,
                Modified = ParseDate(i.Osv.Value?.Modified),
            },
        };

        if (i.CveId is null)
        {
            // Not a failure and nothing to retry: these sources are keyed by CVE id, and this advisory has none.
            sources.Add(new IntelSourceDto { Name = "NVD", Status = "not_applicable", Message = "Not a CVE, so NVD, EPSS and CISA KEV do not apply." });
            return sources;
        }

        sources.Add(new IntelSourceDto
        {
            Name = "NVD",
            Status = Status(i.Nvd.Status),
            FetchedAt = i.Now,
            Message = i.Nvd.Message,
            Url = $"https://nvd.nist.gov/vuln/detail/{i.CveId}",
            Modified = ParseDate(nvd?.LastModified),
        });
        sources.Add(new IntelSourceDto
        {
            Name = "EPSS",
            Status = i.Epss.IsOk ? (i.Epss.Value!.ContainsKey(i.CveId) ? "ok" : "not_found") : Status(i.Epss.Status),
            FetchedAt = i.Now,
            Message = i.Epss.IsOk && !i.Epss.Value!.ContainsKey(i.CveId) ? "EPSS has not scored this CVE." : i.Epss.Message,
            Url = $"https://api.first.org/data/v1/epss?cve={i.CveId}",
        });
        sources.Add(new IntelSourceDto
        {
            Name = "CISA KEV",
            Status = Status(i.Kev.Status),
            FetchedAt = i.Now,
            Message = i.Kev.IsOk && i.Kev.Value!.Entries.ContainsKey(i.CveId) ? i.Kev.Message : i.Kev.IsOk ? "Not in the catalog." : i.Kev.Message,
            Url = "https://www.cisa.gov/known-exploited-vulnerabilities-catalog",
            Modified = i.Kev.Value?.Released,
        });
        return sources;

        static string Status(SourceStatus s) => s switch
        {
            SourceStatus.Ok => "ok",
            SourceStatus.NotFound => "not_found",
            SourceStatus.Error => "error",
            SourceStatus.Skipped => "skipped",
            _ => "disabled",
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    internal static DateTime? ParseDate(string? s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : null;

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    [GeneratedRegex(@"^CWE-(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex CweNumber();
}

namespace Vulscan.Application.DTOs.Intel;

/// <summary>
/// Everything known about one advisory, merged from OSV, NVD, FIRST EPSS and the CISA KEV catalog.
/// This is also the persisted document format (<see cref="CurrentSchemaVersion"/>).
/// </summary>
public sealed record VulnerabilityIntelDto
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string Id { get; init; } = string.Empty;

    /// <summary>One-line title (GitHub advisory summary, or the first sentence of the description).</summary>
    public string? Title { get; init; }

    public string? Description { get; init; }

    /// <summary>"markdown" for GitHub/OSV text, "text" for NVD descriptions.</summary>
    public string DescriptionFormat { get; init; } = "text";
    public string? DescriptionSource { get; init; }

    public DateTime? Published { get; init; }
    public DateTime? Modified { get; init; }
    public DateTime? Withdrawn { get; init; }

    public List<string> Aliases { get; init; } = [];

    /// <summary>Qualitative rating from the primary CVSS score.</summary>
    public string? Severity { get; init; }

    /// <summary>Primary CVSS base score.</summary>
    public double? Score { get; init; }

    /// <summary>The label a source database assigns itself, e.g. GitHub's "CRITICAL" / "MODERATE".</summary>
    public string? VendorSeverity { get; init; }

    public List<CvssRatingDto> Cvss { get; init; } = [];
    public List<WeaknessDto> Weaknesses { get; init; } = [];
    public EpssDto? Epss { get; init; }
    public KevDto? Kev { get; init; }
    public List<IntelReferenceDto> References { get; init; } = [];
    public List<AffectedPackageDto> Affected { get; init; } = [];
    public List<AffectedProductDto> Products { get; init; } = [];
    public List<IntelSourceDto> Sources { get; init; } = [];

    public DateTime FetchedAt { get; init; }

    /// <summary>Set when served: the record is past its refresh time and a refresh is pending or failed.</summary>
    public bool Stale { get; init; }
}

public sealed record CvssRatingDto
{
    /// <summary>"4.0", "3.1", "3.0" or "2.0".</summary>
    public string Version { get; init; } = string.Empty;
    public string Vector { get; init; } = string.Empty;
    public double? Score { get; init; }
    public string? Severity { get; init; }

    /// <summary>Who assigned it: "NVD", "GitHub", "OSV", …</summary>
    public string Source { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public List<CvssMetricDto> Metrics { get; init; } = [];
}

public sealed record CvssMetricDto(string Code, string Name, string Value);

public sealed record WeaknessDto
{
    /// <summary>"CWE-79". NVD also uses "NVD-CWE-Other" / "NVD-CWE-noinfo", which have no page to link to.</summary>
    public string Id { get; init; } = string.Empty;
    public string? Url { get; init; }
    public List<string> Sources { get; init; } = [];
}

public sealed record EpssDto(double Score, double Percentile, DateTime? Date);

/// <summary>An entry in CISA's Known Exploited Vulnerabilities catalog — exploitation in the wild is confirmed.</summary>
public sealed record KevDto
{
    public string? Name { get; init; }
    public string? VendorProject { get; init; }
    public string? Product { get; init; }
    public string? ShortDescription { get; init; }
    public DateTime? DateAdded { get; init; }

    /// <summary>Deadline CISA gave US federal agencies to remediate. A useful benchmark for others too.</summary>
    public DateTime? DueDate { get; init; }
    public string? RequiredAction { get; init; }

    /// <summary>"Known" or "Unknown".</summary>
    public string? RansomwareUse { get; init; }
    public string? Notes { get; init; }
}

public sealed record IntelReferenceDto
{
    public string Url { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;

    /// <summary>Normalised: fix, advisory, exploit, issue, package, article, other.</summary>
    public List<string> Kinds { get; init; } = [];

    /// <summary>The labels the sources used (OSV types and NVD tags), e.g. ADVISORY, Patch, Vendor Advisory.</summary>
    public List<string> Labels { get; init; } = [];
    public List<string> Sources { get; init; } = [];
}

public sealed record AffectedPackageDto
{
    public string Ecosystem { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Purl { get; init; }
    public List<AffectedRangeDto> Ranges { get; init; } = [];

    /// <summary>How many individual versions the advisory lists (can be hundreds); they are not all returned.</summary>
    public int VersionsListed { get; init; }
}

/// <summary>One affected interval: <c>[Introduced, Fixed)</c>, or up to and including <c>LastAffected</c>.</summary>
public sealed record AffectedRangeDto
{
    public string? Introduced { get; init; }
    public string? Fixed { get; init; }
    public string? LastAffected { get; init; }
}

/// <summary>Affected software from NVD's CPE configuration (vendor/product and version bounds).</summary>
public sealed record AffectedProductDto
{
    public string Vendor { get; init; } = string.Empty;
    public string Product { get; init; } = string.Empty;
    public string? Version { get; init; }
    public string? StartIncluding { get; init; }
    public string? StartExcluding { get; init; }
    public string? EndIncluding { get; init; }
    public string? EndExcluding { get; init; }
}

/// <summary>What happened when one source was asked.</summary>
public sealed record IntelSourceDto
{
    /// <summary>"OSV", "NVD", "EPSS", "CISA KEV".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// ok, not_found, not_applicable (the source is keyed by CVE and this advisory has none), disabled, and the two that
    /// are retried soon: error (unreachable) and skipped (rate limited).
    /// </summary>
    public string Status { get; init; } = string.Empty;
    public DateTime? FetchedAt { get; init; }
    public string? Message { get; init; }

    /// <summary>Where to read the original record.</summary>
    public string? Url { get; init; }

    /// <summary>When the source last changed its record.</summary>
    public DateTime? Modified { get; init; }
}

namespace Vulscan.Infrastructure.Intel;

/// <summary>Settings for vulnerability intelligence (<c>Intel</c> section). Everything has a working default.</summary>
public sealed class IntelOptions
{
    public const string SectionName = "Intel";

    /// <summary>Master switch. Off: nothing is fetched and only what is already stored is shown.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How long a stored record is considered fresh.</summary>
    public int RefreshHours { get; set; } = 24;

    /// <summary>When a source failed, how soon to try again.</summary>
    public int RetryMinutes { get; set; } = 15;

    /// <summary>How long an id that no source knows is remembered, so it is not asked for on every page view.</summary>
    public int NotFoundHours { get; set; } = 72;

    /// <summary>How often the background refresher looks for advisories that need fetching or refreshing.</summary>
    public int WorkerIntervalMinutes { get; set; } = 15;

    /// <summary>The most advisories the background refresher fetches in one pass.</summary>
    public int MaxPerCycle { get; set; } = 200;

    /// <summary>Advisories fetched in parallel by the background refresher.</summary>
    public int Concurrency { get; set; } = 4;

    public NvdOptions Nvd { get; set; } = new();
    public EpssOptions Epss { get; set; } = new();
    public KevOptions Kev { get; set; } = new();
}

public sealed class NvdOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://services.nvd.nist.gov/";

    /// <summary>
    /// Optional NVD API key (free: https://nvd.nist.gov/developers/request-an-api-key). Without one NVD allows 5
    /// requests per 30 seconds; with one, 50. Supply it from the environment (<c>Intel__Nvd__ApiKey</c>), never a file.
    /// </summary>
    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Requests allowed per 30 seconds. Defaults to NVD's published limit for the key situation.</summary>
    public int? RequestsPer30Seconds { get; set; }

    public int EffectiveLimit => RequestsPer30Seconds ?? (string.IsNullOrWhiteSpace(ApiKey) ? 5 : 50);
}

public sealed class EpssOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://api.first.org/";
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class KevOptions
{
    public bool Enabled { get; set; } = true;
    public string CatalogUrl { get; set; } =
        "https://www.cisa.gov/sites/default/files/feeds/known_exploited_vulnerabilities.json";

    /// <summary>How long the downloaded catalog (about 2 MB) is reused.</summary>
    public int CacheHours { get; set; } = 6;
    public int TimeoutSeconds { get; set; } = 60;
}

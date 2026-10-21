using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulscan.Infrastructure.Intel;

namespace Vulscan.Infrastructure.Clients;

/// <summary>
/// NVD CVE API 2.0 (https://nvd.nist.gov/developers/vulnerabilities): the official CVSS scores, CWE weaknesses,
/// publication dates and tagged references for a CVE.
/// </summary>
public interface INvdApiClient
{
    /// <param name="maxWait">
    /// How long to wait for a rate-limit slot before giving up (NVD allows only 5 requests per 30 s without a key).
    /// A page view waits briefly; the background refresher can afford to wait a long time.
    /// </param>
    Task<SourceResult<NvdCve>> GetCveAsync(string cveId, TimeSpan maxWait, CancellationToken ct = default);
}

public sealed class NvdApiClient(
    HttpClient http, IOptions<IntelOptions> options, NvdRateLimiter limiter, ILogger<NvdApiClient> logger) : INvdApiClient
{
    public async Task<SourceResult<NvdCve>> GetCveAsync(string cveId, TimeSpan maxWait, CancellationToken ct = default)
    {
        var nvd = options.Value.Nvd;
        if (!options.Value.Enabled || !nvd.Enabled) return SourceResult<NvdCve>.Disabled();

        try
        {
            if (!await limiter.TryAcquireAsync(nvd.EffectiveLimit, TimeSpan.FromSeconds(30), maxWait, ct))
                return SourceResult<NvdCve>.Skipped("NVD rate limit reached; will be fetched in the background.");

            var url = $"rest/json/cves/2.0?cveId={Uri.EscapeDataString(cveId)}";
            using var response = await OutboundHttp.SendWithRetryAsync(http, () =>
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(nvd.ApiKey)) req.Headers.Add("apiKey", nvd.ApiKey);
                return req;
            }, attempts: 2, retryOnTooManyRequests: false, ct);

            if (response.StatusCode == HttpStatusCode.NotFound) return SourceResult<NvdCve>.NotFound();
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                // NVD answers 403/429 when the window is exceeded. Back everyone off for a window.
                limiter.BlockFor(TimeSpan.FromSeconds(30));
                return SourceResult<NvdCve>.Error($"NVD rate limit ({(int)response.StatusCode}).");
            }
            if (!response.IsSuccessStatusCode) return SourceResult<NvdCve>.Error($"NVD returned HTTP {(int)response.StatusCode}.");

            var body = await response.Content.ReadFromJsonAsync<NvdResponse>(cancellationToken: ct);
            var cve = body?.Vulnerabilities.FirstOrDefault()?.Cve;
            return cve is null ? SourceResult<NvdCve>.NotFound() : SourceResult<NvdCve>.Ok(cve);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "NVD lookup failed for {Cve}", cveId);
            return SourceResult<NvdCve>.Error($"NVD unreachable: {ex.GetType().Name}.");
        }
    }
}

/// <summary>A sliding-window rate limiter shared by every NVD call in the process.</summary>
public sealed class NvdRateLimiter(TimeProvider time)
{
    private readonly Queue<DateTimeOffset> _stamps = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _blockedUntil = DateTimeOffset.MinValue;

    public void BlockFor(TimeSpan duration)
    {
        var until = time.GetUtcNow() + duration;
        if (until > _blockedUntil) _blockedUntil = until;
    }

    /// <summary>Takes a slot, waiting up to <paramref name="maxWait"/> for one. False means none became free in time.</summary>
    public async Task<bool> TryAcquireAsync(int limit, TimeSpan window, TimeSpan maxWait, CancellationToken ct)
    {
        var deadline = time.GetUtcNow() + maxWait;
        while (true)
        {
            TimeSpan wait;
            await _gate.WaitAsync(ct);
            try
            {
                var now = time.GetUtcNow();
                while (_stamps.Count > 0 && now - _stamps.Peek() >= window) _stamps.Dequeue();

                if (now >= _blockedUntil && _stamps.Count < limit)
                {
                    _stamps.Enqueue(now);
                    return true;
                }

                wait = now < _blockedUntil ? _blockedUntil - now : _stamps.Peek() + window - now;
                if (now + wait > deadline) return false;
            }
            finally
            {
                _gate.Release();
            }

            await Task.Delay(wait + TimeSpan.FromMilliseconds(5), time, ct);
        }
    }
}

// ── NVD 2.0 response model (only the parts we use) ───────────────────────────

public sealed record NvdResponse
{
    [JsonPropertyName("vulnerabilities")] public List<NvdVulnerability> Vulnerabilities { get; init; } = [];
}

public sealed record NvdVulnerability
{
    [JsonPropertyName("cve")] public NvdCve? Cve { get; init; }
}

public sealed record NvdCve
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("published")] public string? Published { get; init; }
    [JsonPropertyName("lastModified")] public string? LastModified { get; init; }
    [JsonPropertyName("vulnStatus")] public string? VulnStatus { get; init; }
    [JsonPropertyName("descriptions")] public List<NvdLangString> Descriptions { get; init; } = [];
    [JsonPropertyName("metrics")] public NvdMetrics? Metrics { get; init; }
    [JsonPropertyName("weaknesses")] public List<NvdWeakness> Weaknesses { get; init; } = [];
    [JsonPropertyName("configurations")] public List<NvdConfiguration> Configurations { get; init; } = [];
    [JsonPropertyName("references")] public List<NvdReference> References { get; init; } = [];
}

public sealed record NvdLangString
{
    [JsonPropertyName("lang")] public string? Lang { get; init; }
    [JsonPropertyName("value")] public string? Value { get; init; }
}

public sealed record NvdMetrics
{
    [JsonPropertyName("cvssMetricV40")] public List<NvdCvssMetric> V40 { get; init; } = [];
    [JsonPropertyName("cvssMetricV31")] public List<NvdCvssMetric> V31 { get; init; } = [];
    [JsonPropertyName("cvssMetricV30")] public List<NvdCvssMetric> V30 { get; init; } = [];
    [JsonPropertyName("cvssMetricV2")] public List<NvdCvssMetric> V2 { get; init; } = [];
}

public sealed record NvdCvssMetric
{
    /// <summary>Who scored it: <c>nvd@nist.gov</c> or the CNA that published the CVE.</summary>
    [JsonPropertyName("source")] public string? Source { get; init; }

    /// <summary>"Primary" (NVD's own analysis) or "Secondary" (the CNA's).</summary>
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("cvssData")] public NvdCvssData? CvssData { get; init; }
}

public sealed record NvdCvssData
{
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("vectorString")] public string? VectorString { get; init; }
    [JsonPropertyName("baseScore")] public double? BaseScore { get; init; }
    [JsonPropertyName("baseSeverity")] public string? BaseSeverity { get; init; }
}

public sealed record NvdWeakness
{
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("description")] public List<NvdLangString> Description { get; init; } = [];
}

public sealed record NvdConfiguration
{
    [JsonPropertyName("nodes")] public List<NvdNode> Nodes { get; init; } = [];
}

public sealed record NvdNode
{
    [JsonPropertyName("cpeMatch")] public List<NvdCpeMatch> CpeMatch { get; init; } = [];
}

public sealed record NvdCpeMatch
{
    [JsonPropertyName("vulnerable")] public bool Vulnerable { get; init; }
    [JsonPropertyName("criteria")] public string? Criteria { get; init; }
    [JsonPropertyName("versionStartIncluding")] public string? VersionStartIncluding { get; init; }
    [JsonPropertyName("versionStartExcluding")] public string? VersionStartExcluding { get; init; }
    [JsonPropertyName("versionEndIncluding")] public string? VersionEndIncluding { get; init; }
    [JsonPropertyName("versionEndExcluding")] public string? VersionEndExcluding { get; init; }
}

public sealed record NvdReference
{
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("tags")] public List<string> Tags { get; init; } = [];
}

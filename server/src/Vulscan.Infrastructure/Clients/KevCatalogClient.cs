using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulscan.Infrastructure.Intel;

namespace Vulscan.Infrastructure.Clients;

public sealed record KevEntry(
    string CveId, string? VendorProject, string? Product, string? Name, string? ShortDescription,
    DateTime? DateAdded, DateTime? DueDate, string? RequiredAction, string? RansomwareUse, string? Notes);

public sealed record KevCatalog(IReadOnlyDictionary<string, KevEntry> Entries, DateTime? Released, DateTimeOffset LoadedAt);

/// <summary>
/// CISA's Known Exploited Vulnerabilities catalog (https://www.cisa.gov/known-exploited-vulnerabilities-catalog):
/// the CVEs for which exploitation in the wild is confirmed. It is one file, so it is downloaded once and reused.
/// </summary>
public interface IKevCatalogClient
{
    /// <summary>The catalog, from memory while fresh. If a refresh fails, the previous copy is still returned.</summary>
    Task<SourceResult<KevCatalog>> GetCatalogAsync(CancellationToken ct = default);
}

public sealed class KevCatalogClient(
    IHttpClientFactory httpFactory, IOptions<IntelOptions> options, TimeProvider time, ILogger<KevCatalogClient> logger) : IKevCatalogClient
{
    public const string HttpClientName = "cisa-kev";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private KevCatalog? _catalog;

    public async Task<SourceResult<KevCatalog>> GetCatalogAsync(CancellationToken ct = default)
    {
        var kev = options.Value.Kev;
        if (!options.Value.Enabled || !kev.Enabled) return SourceResult<KevCatalog>.Disabled();

        var ttl = TimeSpan.FromHours(Math.Max(1, kev.CacheHours));
        if (_catalog is { } fresh && time.GetUtcNow() - fresh.LoadedAt < ttl) return SourceResult<KevCatalog>.Ok(fresh);

        await _gate.WaitAsync(ct);
        try
        {
            if (_catalog is { } again && time.GetUtcNow() - again.LoadedAt < ttl) return SourceResult<KevCatalog>.Ok(again);

            try
            {
                var http = httpFactory.CreateClient(HttpClientName);
                using var response = await OutboundHttp.SendWithRetryAsync(
                    http, () => new HttpRequestMessage(HttpMethod.Get, kev.CatalogUrl), attempts: 3, retryOnTooManyRequests: true, ct);
                if (!response.IsSuccessStatusCode)
                    return Stale($"CISA returned HTTP {(int)response.StatusCode}.");

                var file = await response.Content.ReadFromJsonAsync<KevFile>(cancellationToken: ct);
                var entries = new Dictionary<string, KevEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var v in file?.Vulnerabilities ?? [])
                {
                    if (string.IsNullOrWhiteSpace(v.CveId)) continue;
                    entries[v.CveId] = new KevEntry(
                        v.CveId, v.VendorProject, v.Product, v.VulnerabilityName, v.ShortDescription,
                        ParseDate(v.DateAdded), ParseDate(v.DueDate), v.RequiredAction, v.KnownRansomwareCampaignUse, v.Notes);
                }

                if (entries.Count == 0) return Stale("CISA's catalog was empty.");

                _catalog = new KevCatalog(entries, ParseDate(file?.DateReleased), time.GetUtcNow());
                logger.LogInformation("Loaded the CISA KEV catalog: {Count} entries", entries.Count);
                return SourceResult<KevCatalog>.Ok(_catalog);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not download the CISA KEV catalog");
                return Stale($"CISA unreachable: {ex.GetType().Name}.");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // A failed refresh should not make exploited vulnerabilities look unlisted: keep serving the last good copy.
    private SourceResult<KevCatalog> Stale(string message) =>
        _catalog is { } old
            ? new SourceResult<KevCatalog>(SourceStatus.Ok, old, $"{message} Using the copy from {old.LoadedAt:u}.")
            : SourceResult<KevCatalog>.Error(message);

    private static DateTime? ParseDate(string? s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : null;

    private sealed record KevFile
    {
        [JsonPropertyName("dateReleased")] public string? DateReleased { get; init; }
        [JsonPropertyName("vulnerabilities")] public List<KevRow> Vulnerabilities { get; init; } = [];
    }

    private sealed record KevRow
    {
        [JsonPropertyName("cveID")] public string? CveId { get; init; }
        [JsonPropertyName("vendorProject")] public string? VendorProject { get; init; }
        [JsonPropertyName("product")] public string? Product { get; init; }
        [JsonPropertyName("vulnerabilityName")] public string? VulnerabilityName { get; init; }
        [JsonPropertyName("shortDescription")] public string? ShortDescription { get; init; }
        [JsonPropertyName("dateAdded")] public string? DateAdded { get; init; }
        [JsonPropertyName("dueDate")] public string? DueDate { get; init; }
        [JsonPropertyName("requiredAction")] public string? RequiredAction { get; init; }
        [JsonPropertyName("knownRansomwareCampaignUse")] public string? KnownRansomwareCampaignUse { get; init; }
        [JsonPropertyName("notes")] public string? Notes { get; init; }
    }
}

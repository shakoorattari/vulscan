using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulscan.Infrastructure.Intel;

namespace Vulscan.Infrastructure.Clients;

public sealed record EpssScore(double Score, double Percentile, DateTime? Date);

/// <summary>
/// FIRST's Exploit Prediction Scoring System (https://www.first.org/epss/): the estimated probability that a CVE
/// is exploited in the wild in the next 30 days, and how that ranks against every other CVE. Updated daily.
/// </summary>
public interface IEpssApiClient
{
    /// <summary>Scores for the given CVEs (up to 100 per call). CVEs EPSS has not scored are absent from the result.</summary>
    Task<SourceResult<IReadOnlyDictionary<string, EpssScore>>> GetScoresAsync(
        IReadOnlyCollection<string> cveIds, CancellationToken ct = default);
}

public sealed class EpssApiClient(HttpClient http, IOptions<IntelOptions> options, ILogger<EpssApiClient> logger) : IEpssApiClient
{
    public const int MaxPerRequest = 100;

    public async Task<SourceResult<IReadOnlyDictionary<string, EpssScore>>> GetScoresAsync(
        IReadOnlyCollection<string> cveIds, CancellationToken ct = default)
    {
        if (!options.Value.Enabled || !options.Value.Epss.Enabled)
            return SourceResult<IReadOnlyDictionary<string, EpssScore>>.Disabled();
        if (cveIds.Count == 0)
            return SourceResult<IReadOnlyDictionary<string, EpssScore>>.Ok(new Dictionary<string, EpssScore>());

        var result = new Dictionary<string, EpssScore>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var batch in cveIds.Chunk(MaxPerRequest))
            {
                var url = $"data/v1/epss?cve={Uri.EscapeDataString(string.Join(',', batch))}";
                using var response = await OutboundHttp.SendWithRetryAsync(
                    http, () => new HttpRequestMessage(HttpMethod.Get, url), attempts: 3, retryOnTooManyRequests: true, ct);

                if (!response.IsSuccessStatusCode)
                    return SourceResult<IReadOnlyDictionary<string, EpssScore>>.Error($"EPSS returned HTTP {(int)response.StatusCode}.");

                var body = await response.Content.ReadFromJsonAsync<EpssResponse>(cancellationToken: ct);
                foreach (var row in body?.Data ?? [])
                {
                    if (row.Cve is null || !TryDouble(row.Epss, out var score) || !TryDouble(row.Percentile, out var percentile)) continue;
                    result[row.Cve] = new EpssScore(score, percentile,
                        DateTime.TryParse(row.Date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : null);
                }
            }

            return SourceResult<IReadOnlyDictionary<string, EpssScore>>.Ok(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EPSS lookup failed");
            return SourceResult<IReadOnlyDictionary<string, EpssScore>>.Error($"EPSS unreachable: {ex.GetType().Name}.");
        }
    }

    private static bool TryDouble(string? s, out double value) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private sealed record EpssResponse
    {
        [JsonPropertyName("data")] public List<EpssRow> Data { get; init; } = [];
    }

    private sealed record EpssRow
    {
        [JsonPropertyName("cve")] public string? Cve { get; init; }
        [JsonPropertyName("epss")] public string? Epss { get; init; }
        [JsonPropertyName("percentile")] public string? Percentile { get; init; }
        [JsonPropertyName("date")] public string? Date { get; init; }
    }
}

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Vulscan.Infrastructure.Clients;

/// <summary>
/// Client for the Open Source Vulnerabilities (OSV.dev) API.
/// Docs: https://osv.dev/docs/
/// </summary>
public interface IOsvApiClient
{
    /// <summary>
    /// Query a single package@version for vulnerabilities.
    /// </summary>
    Task<OsvQueryResponse?> QueryAsync(
        string packageName, string ecosystem, string? version = null, CancellationToken ct = default);

    /// <summary>
    /// Batch query up to 1000 packages in a single request.
    /// </summary>
    Task<OsvBatchResponse?> QueryBatchAsync(
        IReadOnlyList<OsvBatchQuery> queries, CancellationToken ct = default);

    /// <summary>
    /// Fetch full vulnerability details by ID (e.g., GHSA-xxx, CVE-xxx).
    /// The /querybatch endpoint returns only IDs/modified dates; use this to hydrate.
    /// </summary>
    Task<OsvVulnerability?> GetVulnerabilityAsync(string id, CancellationToken ct = default);
}

public sealed class OsvApiClient(HttpClient httpClient, ILogger<OsvApiClient> logger) : IOsvApiClient
{
    public async Task<OsvQueryResponse?> QueryAsync(
        string packageName, string ecosystem, string? version = null, CancellationToken ct = default)
    {
        try
        {
            var request = new OsvQueryRequest
            {
                Package = new OsvPackage { Name = packageName, Ecosystem = ecosystem },
                Version = version
            };

            var response = await httpClient.PostAsJsonAsync("v1/query", request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OsvQueryResponse>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OSV query failed for {Package}@{Version} ({Ecosystem})",
                packageName, version, ecosystem);
            return null;
        }
    }

    public async Task<OsvBatchResponse?> QueryBatchAsync(
        IReadOnlyList<OsvBatchQuery> queries, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("Querying OSV batch API with {Count} packages", queries.Count);
            var request = new OsvBatchRequest { Queries = queries };
            var response = await httpClient.PostAsJsonAsync("v1/querybatch", request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OsvBatchResponse>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OSV batch query failed for {Count} packages", queries.Count);
            return null;
        }
    }

    public async Task<OsvVulnerability?> GetVulnerabilityAsync(string id, CancellationToken ct = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<OsvVulnerability>($"v1/vulns/{id}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OSV GET vulnerability {Id} failed", id);
            return null;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DTOs (https://google.github.io/osv.dev/api/)
// ─────────────────────────────────────────────────────────────────────────────

public sealed record OsvQueryRequest
{
    [JsonPropertyName("package")] public OsvPackage Package { get; init; } = new();
    [JsonPropertyName("version")] public string? Version { get; init; }
}

public sealed record OsvBatchRequest
{
    [JsonPropertyName("queries")] public IReadOnlyList<OsvBatchQuery> Queries { get; init; } = [];
}

public sealed record OsvBatchQuery
{
    [JsonPropertyName("package")] public OsvPackage Package { get; init; } = new();
    [JsonPropertyName("version")] public string? Version { get; init; }
}

public sealed record OsvPackage
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("ecosystem")] public string Ecosystem { get; init; } = string.Empty;
}

public sealed record OsvQueryResponse
{
    [JsonPropertyName("vulns")] public List<OsvVulnerability> Vulns { get; init; } = [];
}

/// <summary>
/// Batch response — each result mirrors the order of the request queries.
/// Note: batch results contain only id+modified; call <see cref="IOsvApiClient.GetVulnerabilityAsync"/> for full data.
/// </summary>
public sealed record OsvBatchResponse
{
    [JsonPropertyName("results")] public List<OsvBatchResult> Results { get; init; } = [];
}

public sealed record OsvBatchResult
{
    [JsonPropertyName("vulns")] public List<OsvVulnerabilityRef> Vulns { get; init; } = [];
}

public sealed record OsvVulnerabilityRef
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("modified")] public string? Modified { get; init; }
}

public sealed record OsvVulnerability
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    [JsonPropertyName("details")] public string? Details { get; init; }
    [JsonPropertyName("aliases")] public List<string> Aliases { get; init; } = [];
    [JsonPropertyName("severity")] public List<OsvSeverity> Severity { get; init; } = [];
    [JsonPropertyName("affected")] public List<OsvAffected> Affected { get; init; } = [];
    [JsonPropertyName("database_specific")] public OsvDatabaseSpecific? DatabaseSpecific { get; init; }
    [JsonPropertyName("modified")] public string? Modified { get; init; }
    [JsonPropertyName("published")] public string? Published { get; init; }
}

public sealed record OsvSeverity
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("score")] public string? Score { get; init; }
}

public sealed record OsvAffected
{
    [JsonPropertyName("package")] public OsvPackage Package { get; init; } = new();
    [JsonPropertyName("ranges")] public List<OsvRange> Ranges { get; init; } = [];
    [JsonPropertyName("database_specific")] public OsvDatabaseSpecific? DatabaseSpecific { get; init; }
}

public sealed record OsvRange
{
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("events")] public List<OsvEvent> Events { get; init; } = [];
}

public sealed record OsvEvent
{
    [JsonPropertyName("introduced")] public string? Introduced { get; init; }
    [JsonPropertyName("fixed")] public string? Fixed { get; init; }
    [JsonPropertyName("last_affected")] public string? LastAffected { get; init; }
}

public sealed record OsvDatabaseSpecific
{
    [JsonPropertyName("severity")] public string? Severity { get; init; }
}

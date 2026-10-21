using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using Microsoft.Extensions.Logging;
using Vulscan.Infrastructure.Intel;

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

    /// <summary>
    /// Like <see cref="GetVulnerabilityAsync"/>, but tells "OSV does not know this id" apart from "OSV could not be
    /// reached" — the first is remembered, the second is retried.
    /// </summary>
    Task<SourceResult<OsvVulnerability>> LookupVulnerabilityAsync(string id, CancellationToken ct = default);
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
        var result = await LookupVulnerabilityAsync(id, ct);
        if (result.Status == SourceStatus.Error) logger.LogWarning("OSV GET vulnerability {Id} failed: {Message}", id, result.Message);
        return result.Value;
    }

    public async Task<SourceResult<OsvVulnerability>> LookupVulnerabilityAsync(string id, CancellationToken ct = default)
    {
        try
        {
            using var response = await OutboundHttp.SendWithRetryAsync(
                httpClient, () => new HttpRequestMessage(HttpMethod.Get, $"v1/vulns/{Uri.EscapeDataString(id)}"),
                attempts: 3, retryOnTooManyRequests: true, ct);

            if (response.StatusCode == HttpStatusCode.NotFound) return SourceResult<OsvVulnerability>.NotFound();
            if (!response.IsSuccessStatusCode) return SourceResult<OsvVulnerability>.Error($"OSV returned HTTP {(int)response.StatusCode}.");

            var vuln = await response.Content.ReadFromJsonAsync<OsvVulnerability>(cancellationToken: ct);
            return vuln is null ? SourceResult<OsvVulnerability>.NotFound() : SourceResult<OsvVulnerability>.Ok(vuln);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return SourceResult<OsvVulnerability>.Error($"OSV unreachable: {ex.GetType().Name}.");
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
    [JsonPropertyName("purl"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Purl { get; init; }
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
    [JsonPropertyName("related")] public List<string> Related { get; init; } = [];
    [JsonPropertyName("severity")] public List<OsvSeverity> Severity { get; init; } = [];
    [JsonPropertyName("affected")] public List<OsvAffected> Affected { get; init; } = [];
    [JsonPropertyName("references")] public List<OsvReference> References { get; init; } = [];
    [JsonPropertyName("database_specific")] public OsvDatabaseSpecific? DatabaseSpecific { get; init; }
    [JsonPropertyName("modified")] public string? Modified { get; init; }
    [JsonPropertyName("published")] public string? Published { get; init; }
    [JsonPropertyName("withdrawn")] public string? Withdrawn { get; init; }
}

public sealed record OsvReference
{
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
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

    /// <summary>Explicit list of affected versions (can be hundreds of entries for some ecosystems).</summary>
    [JsonPropertyName("versions")] public List<string> Versions { get; init; } = [];
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

/// <summary>
/// The free-form <c>database_specific</c> object. Its shape differs per source database, so every field is read
/// tolerantly: a value of an unexpected type is ignored rather than failing the whole record.
/// </summary>
public sealed record OsvDatabaseSpecific
{
    [JsonPropertyName("severity"), JsonConverter(typeof(TolerantStringConverter))]
    public string? Severity { get; init; }

    [JsonPropertyName("cwe_ids"), JsonConverter(typeof(TolerantStringListConverter))]
    public List<string>? CweIds { get; init; }

    [JsonPropertyName("nvd_published_at"), JsonConverter(typeof(TolerantStringConverter))]
    public string? NvdPublishedAt { get; init; }
}

/// <summary>Reads a JSON string, and returns null for any other token type instead of throwing.</summary>
public sealed class TolerantStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return reader.GetString();
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

/// <summary>Reads a JSON array of strings (ignoring non-string items); any other token type yields null.</summary>
public sealed class TolerantStringListConverter : JsonConverter<List<string>?>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }

        var list = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } s) list.Add(s);
            else reader.Skip();
        }
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var s in value ?? []) writer.WriteStringValue(s);
        writer.WriteEndArray();
    }
}

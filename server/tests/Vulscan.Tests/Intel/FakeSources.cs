using Vulscan.Infrastructure.Clients;
using Vulscan.Infrastructure.Intel;

namespace Vulscan.Tests.Intel;

/// <summary>Scriptable stand-ins for the four external sources. Each records how often it was asked.</summary>
public sealed class FakeOsv : IOsvApiClient
{
    public Dictionary<string, OsvVulnerability> Records { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Down { get; set; }
    public List<string> Lookups { get; } = [];
    public Action<string>? OnLookup { get; set; }

    public Task<SourceResult<OsvVulnerability>> LookupVulnerabilityAsync(string id, CancellationToken ct = default)
    {
        Lookups.Add(id);
        OnLookup?.Invoke(id);
        if (Down) return Task.FromResult(SourceResult<OsvVulnerability>.Error("OSV unreachable: HttpRequestException."));
        return Task.FromResult(Records.TryGetValue(id, out var r) ? SourceResult<OsvVulnerability>.Ok(r) : SourceResult<OsvVulnerability>.NotFound());
    }

    public async Task<OsvVulnerability?> GetVulnerabilityAsync(string id, CancellationToken ct = default) =>
        (await LookupVulnerabilityAsync(id, ct)).Value;

    public List<IReadOnlyList<OsvBatchQuery>> Batches { get; } = [];
    public Func<OsvBatchQuery, IReadOnlyList<string>> BatchAnswer { get; set; } = _ => [];

    public Task<OsvBatchResponse?> QueryBatchAsync(IReadOnlyList<OsvBatchQuery> queries, CancellationToken ct = default)
    {
        Batches.Add(queries);
        return Task.FromResult<OsvBatchResponse?>(new OsvBatchResponse
        {
            Results = queries.Select(q => new OsvBatchResult
            {
                Vulns = BatchAnswer(q).Select(id => new OsvVulnerabilityRef { Id = id }).ToList(),
            }).ToList(),
        });
    }

    public Task<OsvQueryResponse?> QueryAsync(string packageName, string ecosystem, string? version = null, CancellationToken ct = default) =>
        throw new NotSupportedException();
}

public sealed class FakeNvd : INvdApiClient
{
    public Dictionary<string, NvdCve> Records { get; } = new(StringComparer.OrdinalIgnoreCase);
    public SourceStatus? Force { get; set; }
    public List<string> Calls { get; } = [];

    public Task<SourceResult<NvdCve>> GetCveAsync(string cveId, TimeSpan maxWait, CancellationToken ct = default)
    {
        Calls.Add(cveId);
        return Task.FromResult(Force switch
        {
            SourceStatus.Error => SourceResult<NvdCve>.Error("NVD unreachable: HttpRequestException."),
            SourceStatus.Skipped => SourceResult<NvdCve>.Skipped("NVD rate limit reached; will be fetched in the background."),
            _ => Records.TryGetValue(cveId, out var r) ? SourceResult<NvdCve>.Ok(r) : SourceResult<NvdCve>.NotFound(),
        });
    }
}

public sealed class FakeEpss : IEpssApiClient
{
    public Dictionary<string, EpssScore> Scores { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Down { get; set; }
    public List<IReadOnlyCollection<string>> Calls { get; } = [];

    public Task<SourceResult<IReadOnlyDictionary<string, EpssScore>>> GetScoresAsync(IReadOnlyCollection<string> cveIds, CancellationToken ct = default)
    {
        Calls.Add(cveIds);
        if (Down) return Task.FromResult(SourceResult<IReadOnlyDictionary<string, EpssScore>>.Error("EPSS unreachable: HttpRequestException."));
        IReadOnlyDictionary<string, EpssScore> found = Scores.Where(kv => cveIds.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(SourceResult<IReadOnlyDictionary<string, EpssScore>>.Ok(found));
    }
}

public sealed class FakeKev : IKevCatalogClient
{
    public Dictionary<string, KevEntry> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Down { get; set; }
    public int Calls { get; private set; }

    public Task<SourceResult<KevCatalog>> GetCatalogAsync(CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(Down
            ? SourceResult<KevCatalog>.Error("CISA unreachable: HttpRequestException.")
            : SourceResult<KevCatalog>.Ok(new KevCatalog(Entries, null, DateTimeOffset.UtcNow)));
    }

    public static KevEntry Entry(string cve) => new(
        cve, "Apache", "Log4j2", "Apache Log4j2 Remote Code Execution Vulnerability", "…",
        new DateTime(2021, 12, 10), new DateTime(2021, 12, 24), "Apply updates.", "Known", null);
}

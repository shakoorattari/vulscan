using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vulscan.Application.Intel;
using Vulscan.Domain.Entities;
using Vulscan.Infrastructure.Clients;
using Vulscan.Infrastructure.Intel;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Intel;

/// <summary>Runs only when <c>VULSCAN_TEST_LIVE=1</c>: the real service against the real OSV, NVD, EPSS and CISA APIs.</summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("VULSCAN_TEST_LIVE") != "1")
            Skip = "Set VULSCAN_TEST_LIVE=1 to call the real external APIs.";
    }
}

public class LiveIntelTests
{
    private static HttpClient Http(string baseAddress) =>
        new() { BaseAddress = new Uri(baseAddress), Timeout = TimeSpan.FromSeconds(60), DefaultRequestHeaders = { { "User-Agent", "Vulscan-tests/1.0" } } };

    private static (VulnerabilityIntelService Service, TestDb Db) Build(VulscanDbContextHolder holder)
    {
        var options = Options.Create(new IntelOptions());
        var time = TimeProvider.System;
        var osv = new OsvApiClient(Http("https://api.osv.dev/"), NullLogger<OsvApiClient>.Instance);
        var nvd = new NvdApiClient(Http("https://services.nvd.nist.gov/"), options, new NvdRateLimiter(time), NullLogger<NvdApiClient>.Instance);
        var epss = new EpssApiClient(Http("https://api.first.org/"), options, NullLogger<EpssApiClient>.Instance);
        var kev = new KevCatalogClient(new LiveFactory(), options, time, NullLogger<KevCatalogClient>.Instance);
        var service = new VulnerabilityIntelService(holder.Db.NewContext(), osv, nvd, epss, kev, options, time, NullLogger<VulnerabilityIntelService>.Instance);
        return (service, holder.Db);
    }

    private sealed class LiveFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => Http("https://www.cisa.gov/");
    }

    private sealed class VulscanDbContextHolder : IDisposable
    {
        public TestDb Db { get; } = new();
        public void Dispose() => Db.Dispose();
    }

    [LiveFact]
    public async Task A_real_cve_comes_back_complete_from_all_four_sources()
    {
        using var holder = new VulscanDbContextHolder();
        var (service, _) = Build(holder);

        var dto = await service.GetAsync("CVE-2015-8857");

        Assert.NotNull(dto);
        Assert.All(dto!.Sources, s => Assert.True(s.Status == "ok", $"{s.Name}: {s.Status} {s.Message}"));

        Assert.Contains("uglify-js", dto.Title);
        Assert.Equal(new DateTime(2017, 1, 23), dto.Published!.Value.Date);
        Assert.Contains("GHSA-34r7-q49f-h37c", dto.Aliases);

        // NVD's published base score must equal what our calculator computes from the same vector.
        var primary = dto.Cvss.First(c => c.IsPrimary);
        Assert.Equal("3.1", primary.Version);
        Assert.Equal(CvssCalculator.Parse(primary.Vector)!.Score, primary.Score);
        Assert.Equal(9.8, dto.Score);

        Assert.NotEmpty(dto.Weaknesses);
        Assert.NotEmpty(dto.References);
        Assert.Contains(dto.Affected, a => a.Ecosystem == "npm" && a.Name == "uglify-js" && a.Ranges.Any(r => r.Fixed == "2.4.24"));
        Assert.InRange(dto.Epss!.Score, 0.0, 1.0);
        Assert.Null(dto.Kev);                                               // not an exploited-in-the-wild CVE
    }

    [LiveFact]
    public async Task Log4Shell_is_listed_as_known_exploited_with_a_near_certain_epss()
    {
        using var holder = new VulscanDbContextHolder();
        var (service, _) = Build(holder);

        var dto = await service.GetAsync("CVE-2021-44228");

        Assert.NotNull(dto);
        Assert.Equal(10.0, dto!.Score);
        Assert.Equal("Critical", dto.Severity);
        Assert.NotNull(dto.Kev);
        Assert.Equal("Apache", dto.Kev!.VendorProject);
        Assert.Equal(new DateTime(2021, 12, 24), dto.Kev.DueDate);
        Assert.Equal("Known", dto.Kev.RansomwareUse);
        Assert.True(dto.Epss!.Score > 0.9);
        Assert.Contains(dto.Affected, a => a.Name == "org.apache.logging.log4j:log4j-core" && a.Ranges.Count >= 3);
        Assert.Contains(dto.References, r => r.Kinds.Contains("fix"));
    }

    [LiveFact]
    public async Task Asking_by_the_github_id_reaches_the_same_cve_data()
    {
        using var holder = new VulscanDbContextHolder();
        var (service, _) = Build(holder);

        var dto = await service.GetAsync("GHSA-jfh8-c2jp-5v3q");

        Assert.NotNull(dto);
        Assert.Equal("GHSA-jfh8-c2jp-5v3q", dto!.Id);
        Assert.Contains("CVE-2021-44228", dto.Aliases);
        Assert.NotNull(dto.Kev);                                            // resolved through the CVE alias
        Assert.Equal(10.0, dto.Score);
    }

    [LiveFact]
    public async Task An_invented_cve_is_reported_as_unknown()
    {
        using var holder = new VulscanDbContextHolder();
        var (service, _) = Build(holder);

        Assert.Null(await service.GetAsync("CVE-1999-9999999"));
    }

    [LiveFact]
    public async Task The_real_calculator_agrees_with_nvd_on_a_sample_of_current_cves()
    {
        // Fresh data, not the recorded fixtures: ask NVD for a page of recent CVEs and recompute their scores.
        using var http = Http("https://services.nvd.nist.gov/");
        var json = await http.GetStringAsync("rest/json/cves/2.0?resultsPerPage=300&startIndex=250000");
        var parsed = System.Text.Json.JsonSerializer.Deserialize<NvdResponse>(json)!;

        var pairs = parsed.Vulnerabilities.SelectMany(v => (v.Cve?.Metrics?.V31 ?? []).Concat(v.Cve?.Metrics?.V30 ?? []).Concat(v.Cve?.Metrics?.V2 ?? []))
            .Where(m => m.CvssData?.VectorString is not null && m.CvssData.BaseScore is not null)
            .Select(m => (Vector: m.CvssData!.VectorString!, Score: m.CvssData.BaseScore!.Value))
            .Distinct().ToList();

        Assert.True(pairs.Count > 50, $"only {pairs.Count} vectors in the sample");
        var wrong = pairs.Where(p => CvssCalculator.Parse(p.Vector)?.Score != p.Score).ToList();
        Assert.True(wrong.Count == 0, string.Join('\n', wrong.Take(5).Select(w => $"{w.Vector}: NVD {w.Score}")));
    }
}

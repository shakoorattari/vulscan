using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vulscan.Infrastructure.Clients;
using Vulscan.Infrastructure.Intel;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Intel;

public class IntelClientTests
{
    static IntelClientTests() => OutboundHttp.RetryDelayUnit = TimeSpan.FromMilliseconds(1);   // don't wait real seconds

    private static IOptions<IntelOptions> Opts(Action<IntelOptions>? configure = null)
    {
        var o = new IntelOptions();
        configure?.Invoke(o);
        return Options.Create(o);
    }

    // ── OSV ────────────────────────────────────────────────────────────────────

    private static OsvApiClient Osv(FakeHttpHandler h) => new(h.Client("https://api.osv.dev/"), NullLogger<OsvApiClient>.Instance);

    [Fact]
    public async Task Osv_lookup_parses_a_real_record_including_references_and_cwe()
    {
        var h = new FakeHttpHandler(_ => FakeHttpHandler.Json(Fixtures.Text("osv-GHSA-34r7-q49f-h37c.json")));

        var result = await Osv(h).LookupVulnerabilityAsync("GHSA-34r7-q49f-h37c");

        Assert.True(result.IsOk);
        Assert.Equal("https://api.osv.dev/v1/vulns/GHSA-34r7-q49f-h37c", h.Requests[0].Uri.ToString());
        var v = result.Value!;
        Assert.Equal(["CVE-2015-8857"], v.Aliases);
        Assert.Equal(8, v.References.Count);
        Assert.Equal(["CWE-1254", "CWE-670"], v.DatabaseSpecific!.CweIds);
        Assert.Equal("CRITICAL", v.DatabaseSpecific.Severity);
        Assert.Equal(2, v.Affected.Count);
    }

    [Fact]
    public async Task Osv_distinguishes_unknown_ids_from_failures()
    {
        Assert.Equal(SourceStatus.NotFound,
            (await Osv(new FakeHttpHandler(_ => FakeHttpHandler.Status(HttpStatusCode.NotFound))).LookupVulnerabilityAsync("CVE-2099-1")).Status);

        var failing = new FakeHttpHandler(_ => FakeHttpHandler.Status(HttpStatusCode.InternalServerError));
        var result = await Osv(failing).LookupVulnerabilityAsync("CVE-2015-8857");
        Assert.Equal(SourceStatus.Error, result.Status);
        Assert.Equal(3, failing.Count);                                      // transient failures are retried

        var unreachable = new FakeHttpHandler(_ => throw new HttpRequestException("no route"));
        Assert.Equal(SourceStatus.Error, (await Osv(unreachable).LookupVulnerabilityAsync("CVE-2015-8857")).Status);
    }

    [Fact]
    public async Task Osv_database_specific_of_an_unexpected_shape_does_not_fail_the_record()
    {
        // Sources format this free-form object differently; one that puts a number where GitHub puts a string must not
        // throw the whole advisory away.
        var json = """{"id":"X-1","database_specific":{"severity":7,"cwe_ids":"CWE-79","nvd_published_at":{"a":1}},"affected":[{"package":{"name":"p","ecosystem":"npm"},"database_specific":{"severity":["x"]}}]}""";
        var h = new FakeHttpHandler(_ => FakeHttpHandler.Json(json));

        var result = await Osv(h).LookupVulnerabilityAsync("X-1");

        Assert.True(result.IsOk);
        Assert.Null(result.Value!.DatabaseSpecific!.Severity);
        Assert.Null(result.Value.DatabaseSpecific.CweIds);
    }

    [Fact]
    public async Task Osv_ids_are_url_encoded_so_they_cannot_alter_the_request_path()
    {
        var h = new FakeHttpHandler(_ => FakeHttpHandler.Status(HttpStatusCode.NotFound));

        await Osv(h).LookupVulnerabilityAsync("CVE-2015-8857/../../v1/query?x=1");

        Assert.DoesNotContain("../", h.Requests[0].Uri.AbsolutePath);
        Assert.StartsWith("/v1/vulns/", h.Requests[0].Uri.AbsolutePath);
    }

    // ── NVD ────────────────────────────────────────────────────────────────────

    private static NvdApiClient Nvd(FakeHttpHandler h, IOptions<IntelOptions> o, NvdRateLimiter? limiter = null) =>
        new(h.Client("https://services.nvd.nist.gov/"), o, limiter ?? new NvdRateLimiter(TimeProvider.System), NullLogger<NvdApiClient>.Instance);

    [Fact]
    public async Task Nvd_parses_a_real_record()
    {
        var h = new FakeHttpHandler(_ => FakeHttpHandler.Json(Fixtures.Text("nvd-CVE-2015-8857.json")));

        var result = await Nvd(h, Opts()).GetCveAsync("CVE-2015-8857", TimeSpan.Zero);

        Assert.True(result.IsOk);
        Assert.Equal("https://services.nvd.nist.gov/rest/json/cves/2.0?cveId=CVE-2015-8857", h.Requests[0].Uri.ToString());
        var cve = result.Value!;
        Assert.Equal("CVE-2015-8857", cve.Id);
        Assert.Equal(9.8, cve.Metrics!.V31[0].CvssData!.BaseScore);
        Assert.Equal("CWE-254", cve.Weaknesses[0].Description[0].Value);
        Assert.Equal(7.5, cve.Metrics.V2[0].CvssData!.BaseScore);
        Assert.NotEmpty(cve.References);
    }

    [Fact]
    public async Task Nvd_sends_the_api_key_only_when_one_is_configured()
    {
        var h = new FakeHttpHandler(_ => FakeHttpHandler.Json(Fixtures.Text("nvd-CVE-2015-8857.json")));

        await Nvd(h, Opts()).GetCveAsync("CVE-2015-8857", TimeSpan.Zero);
        await Nvd(h, Opts(o => o.Nvd.ApiKey = "secret-key")).GetCveAsync("CVE-2015-8857", TimeSpan.Zero);

        Assert.False(h.Requests[0].Headers.ContainsKey("apiKey"));
        Assert.Equal("secret-key", h.Requests[1].Headers["apiKey"]);
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData("a-key", 50)]
    public void Nvd_rate_limit_defaults_follow_whether_a_key_is_set(string? key, int expected) =>
        Assert.Equal(expected, new NvdOptions { ApiKey = key }.EffectiveLimit);

    [Fact]
    public async Task Nvd_reports_unknown_cves_as_not_found()
    {
        var empty = new FakeHttpHandler(_ => FakeHttpHandler.Json("""{"resultsPerPage":0,"totalResults":0,"vulnerabilities":[]}"""));
        Assert.Equal(SourceStatus.NotFound, (await Nvd(empty, Opts()).GetCveAsync("CVE-2099-1", TimeSpan.Zero)).Status);

        var notFound = new FakeHttpHandler(_ => FakeHttpHandler.Status(HttpStatusCode.NotFound));
        Assert.Equal(SourceStatus.NotFound, (await Nvd(notFound, Opts()).GetCveAsync("CVE-2099-1", TimeSpan.Zero)).Status);
    }

    [Fact]
    public async Task Nvd_backs_off_everyone_after_being_rate_limited_instead_of_hammering_it()
    {
        var h = new FakeHttpHandler(_ => FakeHttpHandler.Status(HttpStatusCode.Forbidden));
        var limiter = new NvdRateLimiter(TimeProvider.System);
        var client = Nvd(h, Opts(), limiter);

        var first = await client.GetCveAsync("CVE-2015-8857", TimeSpan.Zero);
        var second = await client.GetCveAsync("CVE-2015-8857", TimeSpan.Zero);

        Assert.Equal(SourceStatus.Error, first.Status);
        Assert.Contains("rate limit", first.Message);
        Assert.Equal(SourceStatus.Skipped, second.Status);                   // never sent: the limiter is blocked
        Assert.Equal(1, h.Count);
    }

    [Fact]
    public async Task Nvd_never_exceeds_its_allowance()
    {
        var h = new FakeHttpHandler(_ => FakeHttpHandler.Json(Fixtures.Text("nvd-CVE-2015-8857.json")));
        var client = Nvd(h, Opts(o => o.Nvd.RequestsPer30Seconds = 2));

        var results = new List<SourceResult<NvdCve>>();
        for (var i = 0; i < 4; i++) results.Add(await client.GetCveAsync("CVE-2015-8857", TimeSpan.Zero));

        Assert.Equal([SourceStatus.Ok, SourceStatus.Ok, SourceStatus.Skipped, SourceStatus.Skipped], results.Select(r => r.Status));
        Assert.Equal(2, h.Count);
        Assert.Contains("background", results[2].Message);
    }

    [Fact]
    public async Task Nvd_can_be_switched_off_and_never_throws_on_network_failure()
    {
        var h = new FakeHttpHandler(_ => throw new HttpRequestException("down"));

        Assert.Equal(SourceStatus.Disabled, (await Nvd(h, Opts(o => o.Nvd.Enabled = false)).GetCveAsync("CVE-1-1", TimeSpan.Zero)).Status);
        Assert.Equal(SourceStatus.Disabled, (await Nvd(h, Opts(o => o.Enabled = false)).GetCveAsync("CVE-1-1", TimeSpan.Zero)).Status);
        Assert.Equal(0, h.Count);

        var result = await Nvd(h, Opts()).GetCveAsync("CVE-2015-8857", TimeSpan.Zero);
        Assert.Equal(SourceStatus.Error, result.Status);
        Assert.Contains("unreachable", result.Message);
    }

    [Fact]
    public async Task Nvd_cancellation_propagates_instead_of_being_reported_as_a_source_failure()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var h = new FakeHttpHandler(_ => FakeHttpHandler.Json(Fixtures.Text("nvd-CVE-2015-8857.json")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Nvd(h, Opts()).GetCveAsync("CVE-2015-8857", TimeSpan.Zero, cts.Token));
    }

    // ── the rate limiter on its own ─────────────────────────────────────────────

    [Fact]
    public async Task Rate_limiter_makes_the_next_caller_wait_for_a_slot_to_free_up()
    {
        var limiter = new NvdRateLimiter(TimeProvider.System);
        var window = TimeSpan.FromMilliseconds(300);

        Assert.True(await limiter.TryAcquireAsync(2, window, TimeSpan.Zero, default));
        Assert.True(await limiter.TryAcquireAsync(2, window, TimeSpan.Zero, default));
        Assert.False(await limiter.TryAcquireAsync(2, window, TimeSpan.Zero, default));        // full, and not willing to wait

        var started = DateTime.UtcNow;
        Assert.True(await limiter.TryAcquireAsync(2, window, TimeSpan.FromSeconds(5), default)); // willing to wait: gets in after the window
        Assert.InRange((DateTime.UtcNow - started).TotalMilliseconds, 200, 2000);
    }

    [Fact]
    public async Task Rate_limiter_refuses_when_the_wait_would_exceed_what_the_caller_allows()
    {
        var limiter = new NvdRateLimiter(TimeProvider.System);
        Assert.True(await limiter.TryAcquireAsync(1, TimeSpan.FromSeconds(30), TimeSpan.Zero, default));

        var started = DateTime.UtcNow;
        Assert.False(await limiter.TryAcquireAsync(1, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(100), default));
        Assert.True((DateTime.UtcNow - started).TotalMilliseconds < 1000);                      // gave up at once, did not wait 30 s
    }

    // ── EPSS ───────────────────────────────────────────────────────────────────

    private static EpssApiClient Epss(FakeHttpHandler h, IOptions<IntelOptions>? o = null) =>
        new(h.Client("https://api.first.org/"), o ?? Opts(), NullLogger<EpssApiClient>.Instance);

    [Fact]
    public async Task Epss_parses_scores_and_percentiles_from_the_real_response()
    {
        var h = new FakeHttpHandler(_ => FakeHttpHandler.Json(Fixtures.Text("epss-sample.json")));

        var result = await Epss(h).GetScoresAsync(["CVE-2015-8857", "CVE-2021-44228"]);

        Assert.True(result.IsOk);
        Assert.Equal("https://api.first.org/data/v1/epss?cve=CVE-2015-8857%2CCVE-2021-44228", h.Requests[0].Uri.ToString());
        var scores = result.Value!;
        Assert.Equal(0.03559, scores["CVE-2015-8857"].Score, 5);
        Assert.Equal(0.89202, scores["CVE-2015-8857"].Percentile, 5);
        Assert.Equal(new DateTime(2026, 10, 20), scores["CVE-2015-8857"].Date);
        Assert.Equal(0.99999, scores["CVE-2021-44228"].Score, 5);
    }

    [Fact]
    public async Task Epss_asks_for_at_most_100_cves_per_request()
    {
        var h = new FakeHttpHandler(_ => FakeHttpHandler.Json("""{"data":[]}"""));
        var ids = Enumerable.Range(1, 250).Select(i => $"CVE-2020-{i:0000}").ToList();

        var result = await Epss(h).GetScoresAsync(ids);

        Assert.True(result.IsOk);
        Assert.Equal(3, h.Count);                                           // 100 + 100 + 50
        Assert.Empty(result.Value!);                                        // unscored CVEs are simply absent
    }

    [Fact]
    public async Task Epss_failure_is_a_result_not_an_exception_and_nothing_is_asked_for_nothing()
    {
        var failing = new FakeHttpHandler(_ => FakeHttpHandler.Status(HttpStatusCode.ServiceUnavailable));
        Assert.Equal(SourceStatus.Error, (await Epss(failing).GetScoresAsync(["CVE-2015-8857"])).Status);

        var h = new FakeHttpHandler(_ => FakeHttpHandler.Json("""{"data":[]}"""));
        Assert.True((await Epss(h).GetScoresAsync([])).IsOk);
        Assert.Equal(0, h.Count);

        Assert.Equal(SourceStatus.Disabled, (await Epss(h, Opts(o => o.Epss.Enabled = false)).GetScoresAsync(["CVE-2015-8857"])).Status);
    }

    // ── CISA KEV ───────────────────────────────────────────────────────────────

    private static KevCatalogClient Kev(FakeHttpHandler h, FakeTime time, IOptions<IntelOptions>? o = null) =>
        new(new FakeHttpClientFactory(h), o ?? Opts(), time, NullLogger<KevCatalogClient>.Instance);

    [Fact]
    public async Task Kev_catalog_is_downloaded_once_and_reused_until_it_expires()
    {
        var h = new FakeHttpHandler(_ => FakeHttpHandler.Json(Fixtures.Text("kev-sample.json")));
        var time = new FakeTime();
        var kev = Kev(h, time);

        var first = await kev.GetCatalogAsync();
        await kev.GetCatalogAsync();
        time.Advance(TimeSpan.FromHours(5));
        await kev.GetCatalogAsync();
        Assert.Equal(1, h.Count);                                           // still fresh

        time.Advance(TimeSpan.FromHours(2));
        await kev.GetCatalogAsync();
        Assert.Equal(2, h.Count);                                           // past the 6-hour lifetime

        var entry = first.Value!.Entries["CVE-2021-44228"];
        Assert.Equal("Apache", entry.VendorProject);
        Assert.Equal(new DateTime(2021, 12, 10), entry.DateAdded);
        Assert.Equal(new DateTime(2021, 12, 24), entry.DueDate);
        Assert.Equal("Known", entry.RansomwareUse);
        Assert.Equal(3, first.Value.Entries.Count);
    }

    [Fact]
    public async Task A_failed_refresh_keeps_serving_the_last_good_catalog_so_exploited_cves_do_not_vanish()
    {
        var fail = false;
        var h = new FakeHttpHandler(_ => fail ? FakeHttpHandler.Status(HttpStatusCode.BadGateway) : FakeHttpHandler.Json(Fixtures.Text("kev-sample.json")));
        var time = new FakeTime();
        var kev = Kev(h, time);

        Assert.True((await kev.GetCatalogAsync()).IsOk);
        fail = true;
        time.Advance(TimeSpan.FromHours(7));

        var stale = await kev.GetCatalogAsync();

        Assert.True(stale.IsOk);
        Assert.Contains("CVE-2021-44228", stale.Value!.Entries.Keys);
        Assert.Contains("Using the copy from", stale.Message);
    }

    [Fact]
    public async Task Kev_with_no_copy_at_all_reports_an_error_and_empty_or_disabled_catalogs_are_handled()
    {
        var down = new FakeHttpHandler(_ => FakeHttpHandler.Status(HttpStatusCode.BadGateway));
        Assert.Equal(SourceStatus.Error, (await Kev(down, new FakeTime()).GetCatalogAsync()).Status);

        var empty = new FakeHttpHandler(_ => FakeHttpHandler.Json("""{"vulnerabilities":[]}"""));
        Assert.Equal(SourceStatus.Error, (await Kev(empty, new FakeTime()).GetCatalogAsync()).Status);

        Assert.Equal(SourceStatus.Disabled, (await Kev(down, new FakeTime(), Opts(o => o.Kev.Enabled = false)).GetCatalogAsync()).Status);
    }

    // ── outbound proxy ─────────────────────────────────────────────────────────

    [Fact]
    public void Proxy_url_with_credentials_is_split_into_address_and_credentials()
    {
        var proxy = OutboundHttp.CreateProxy("http://svc-vulscan:p%40ss%3Aword@proxy.sdd.local:3128");

        Assert.Equal("http://proxy.sdd.local:3128/", proxy.GetProxy(new Uri("https://api.osv.dev/"))!.ToString());
        var credential = proxy.Credentials!.GetCredential(new Uri("http://proxy.sdd.local:3128"), "Basic")!;
        Assert.Equal("svc-vulscan", credential.UserName);
        Assert.Equal("p@ss:word", credential.Password);
    }

    [Fact]
    public void Proxy_url_without_credentials_has_none()
    {
        var proxy = OutboundHttp.CreateProxy("http://proxy.sdd.local:8080");

        Assert.Equal("http://proxy.sdd.local:8080/", proxy.GetProxy(new Uri("https://nvd.nist.gov/"))!.ToString());
        Assert.Null(proxy.Credentials);
    }
}

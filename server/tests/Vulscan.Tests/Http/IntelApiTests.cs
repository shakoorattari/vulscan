using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vulscan.Application.DTOs.Intel;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure;
using Vulscan.Infrastructure.BackgroundServices;
using Vulscan.Infrastructure.Intel;

namespace Vulscan.Tests.Http;

public class IntelApiTests : IClassFixture<ApiFactory>
{
    private sealed class FakeIntel : IVulnerabilityIntelService
    {
        public Dictionary<string, VulnerabilityIntelDto> Known { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(string Id, bool Refresh)> Calls { get; } = [];

        public Task<VulnerabilityIntelDto?> GetAsync(string advisoryId, bool refresh = false, CancellationToken ct = default)
        {
            Calls.Add((advisoryId, refresh));
            return Task.FromResult(Known.GetValueOrDefault(advisoryId));
        }
    }

    private readonly ApiFactory _f;
    private readonly FakeIntel _intel = new();

    public IntelApiTests(ApiFactory factory)
    {
        _f = factory;
        _intel.Known["CVE-2021-44228"] = new VulnerabilityIntelDto
        {
            Id = "CVE-2021-44228",
            Title = "Log4Shell",
            Score = 10.0,
            Severity = "Critical",
            Kev = new KevDto { Product = "Log4j2", DueDate = new DateTime(2021, 12, 24, 0, 0, 0, DateTimeKind.Utc) },
            Epss = new EpssDto(0.99999, 1.0, null),
            References = [new IntelReferenceDto { Url = "https://logging.apache.org/log4j/2.x/security.html", Host = "logging.apache.org", Kinds = ["advisory"] }],
            FetchedAt = new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc),
        };
    }

    private HttpClient Client() => _f.WithWebHostBuilder(b => b.ConfigureServices(services =>
    {
        services.RemoveAll<IVulnerabilityIntelService>();
        services.AddSingleton<IVulnerabilityIntelService>(_intel);
    })).CreateClient();

    private async Task<HttpClient> ClientAsync(string username, UserRole role)
    {
        username = $"{username}-{Guid.NewGuid():N}"[..28];      // the fixture's database is shared by every test in the class
        await _f.AddUserAsync(username, role);
        var client = Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await _f.LoginAsync(username));
        return client;
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage res) =>
        JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

    [Fact]
    public async Task Requires_authentication()
    {
        var anonymous = Client();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/intel/CVE-2021-44228")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/v1/intel/CVE-2021-44228/refresh", null)).StatusCode);
        Assert.Empty(_intel.Calls);
    }

    [Fact]
    public async Task A_viewer_can_read_the_record_in_camel_case_json()
    {
        var res = await (await ClientAsync("intel-viewer", UserRole.Viewer)).GetAsync("/api/v1/intel/CVE-2021-44228");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var data = (await BodyAsync(res)).GetProperty("data");
        Assert.Equal("CVE-2021-44228", data.GetProperty("id").GetString());
        Assert.Equal(10.0, data.GetProperty("score").GetDouble());
        Assert.Equal("Log4j2", data.GetProperty("kev").GetProperty("product").GetString());
        Assert.Equal(0.99999, data.GetProperty("epss").GetProperty("score").GetDouble());
        Assert.Equal("https://logging.apache.org/log4j/2.x/security.html", data.GetProperty("references")[0].GetProperty("url").GetString());
        Assert.Equal([("CVE-2021-44228", false)], _intel.Calls);
    }

    [Fact]
    public async Task An_unknown_advisory_is_a_404_with_a_message()
    {
        var res = await (await ClientAsync("intel-viewer2", UserRole.Viewer)).GetAsync("/api/v1/intel/CVE-2099-0001");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Contains("CVE-2099-0001", (await BodyAsync(res)).GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("not%20an%20id")]
    [InlineData("CVE-2021-44228%3Fx%3D1")]
    [InlineData("CVE")]
    [InlineData("a")]
    public async Task Ids_that_are_not_advisory_ids_are_rejected_without_calling_the_service(string id)
    {
        var res = await (await ClientAsync("intel-viewer3", UserRole.Viewer)).GetAsync($"/api/v1/intel/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Empty(_intel.Calls);
    }

    [Fact]
    public async Task Path_traversal_in_the_id_never_reaches_the_service()
    {
        var res = await (await ClientAsync("intel-viewer4", UserRole.Viewer)).GetAsync("/api/v1/intel/..%2f..%2fetc%2fpasswd");

        Assert.True(res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, res.StatusCode.ToString());
        Assert.Empty(_intel.Calls);
    }

    [Fact]
    public async Task Only_analysts_and_admins_can_force_a_refresh_and_it_is_audited()
    {
        var viewer = await ClientAsync("intel-viewer5", UserRole.Viewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/v1/intel/CVE-2021-44228/refresh", null)).StatusCode);
        Assert.Empty(_intel.Calls);

        foreach (var (name, role) in new[] { ("intel-analyst", UserRole.SecurityAnalyst), ("intel-admin", UserRole.Admin) })
        {
            var res = await (await ClientAsync(name, role)).PostAsync("/api/v1/intel/CVE-2021-44228/refresh", null);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        Assert.Equal(2, _intel.Calls.Count(c => c is { Id: "CVE-2021-44228", Refresh: true }));

        // A refresh is recorded as a state change; a refused one is recorded as an access denial.
        var audit = await _f.WithDbAsync(db => db.AuditLogs.Select(a => new { a.Action, a.Outcome, a.Username, a.EntityId, a.Details }).ToListAsync());
        Assert.Contains(audit, a => a.Action == "Intel.Refresh" && a.Outcome == "Success" && a.Username!.StartsWith("intel-analyst") && a.EntityId == "CVE-2021-44228");
        Assert.Contains(audit, a => a.Action == "Intel.Refresh" && a.Outcome == "Success" && a.Username!.StartsWith("intel-admin"));
        Assert.Contains(audit, a => a.Action == "Access.Denied" && a.Username!.StartsWith("intel-viewer5") && a.Details!.Contains("/intel/CVE-2021-44228/refresh"));
    }

    // ── wiring ─────────────────────────────────────────────────────────────────

    [Fact]
    public void The_real_container_resolves_the_service_and_the_trigger()
    {
        using var scope = _f.Services.CreateScope();

        Assert.IsType<VulnerabilityIntelService>(scope.ServiceProvider.GetRequiredService<IVulnerabilityIntelService>());
        Assert.IsType<IntelTrigger>(_f.Services.GetRequiredService<IIntelTrigger>());   // scans need it even with background jobs off
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IntelRefresher>());
        Assert.DoesNotContain(_f.Services.GetServices<IHostedService>(), s => s is IntelBackgroundWorker);
    }

    [Fact]
    public void The_background_worker_is_registered_only_when_background_jobs_are_enabled()
    {
        static IServiceProvider Build(bool jobs)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseSqlite"] = "true",
                ["ConnectionStrings:VulscanDb"] = "DataSource=:memory:",
                ["BackgroundJobs:Enabled"] = jobs.ToString(),
                ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "vulscan-di-keys-" + Guid.NewGuid().ToString("N")),
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInfrastructureServices(config);
            return services.BuildServiceProvider();
        }

        Assert.Contains(Build(true).GetServices<IHostedService>(), s => s is IntelBackgroundWorker);
        Assert.DoesNotContain(Build(false).GetServices<IHostedService>(), s => s is IntelBackgroundWorker);
    }
}

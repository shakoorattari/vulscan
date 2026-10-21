using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vulscan.Infrastructure.Clients;
using Vulscan.Infrastructure.Data;
using Vulscan.Infrastructure.Intel;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Intel;

/// <summary>Real service and database (in-memory SQLite) over scripted sources, with a controllable clock.</summary>
public sealed class IntelHarness(Func<VulscanDbContext>? contextFactory = null) : IDisposable
{
    public TestDb Db { get; } = new();

    /// <summary>A new context on whichever database this harness runs against (SQLite unless a factory was given).</summary>
    public VulscanDbContext NewContext() => contextFactory?.Invoke() ?? Db.NewContext();
    public FakeTime Time { get; } = new();
    public FakeOsv Osv { get; } = new();
    public FakeNvd Nvd { get; } = new();
    public FakeEpss Epss { get; } = new();
    public FakeKev Kev { get; } = new();
    public IntelOptions Options { get; } = new() { Concurrency = contextFactory is null ? 1 : 4 };   // SQLite shares one connection; SQL Server gets real parallelism

    /// <summary>The real record of CVE-2015-8857 in all four sources.</summary>
    public IntelHarness WithUglifyJs()
    {
        Osv.Records["CVE-2015-8857"] = Fixtures.Json<OsvVulnerability>("osv-CVE-2015-8857.json");
        Osv.Records["GHSA-34r7-q49f-h37c"] = Fixtures.Json<OsvVulnerability>("osv-GHSA-34r7-q49f-h37c.json");
        Nvd.Records["CVE-2015-8857"] = Fixtures.Json<NvdResponse>("nvd-CVE-2015-8857.json").Vulnerabilities[0].Cve!;
        Epss.Scores["CVE-2015-8857"] = new EpssScore(0.03559, 0.89202, new DateTime(2026, 10, 20));
        return this;
    }

    public VulnerabilityIntelService Service(VulscanDbContext db) =>
        new(db, Osv, Nvd, Epss, Kev, Microsoft.Extensions.Options.Options.Create(Options), Time, NullLogger<VulnerabilityIntelService>.Instance);

    public IntelRefresher Refresher(VulscanDbContext db)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOsvApiClient>(Osv);
        services.AddSingleton<INvdApiClient>(Nvd);
        services.AddSingleton<IEpssApiClient>(Epss);
        services.AddSingleton<IKevCatalogClient>(Kev);
        services.AddSingleton<IOptions<IntelOptions>>(Microsoft.Extensions.Options.Options.Create(Options));
        services.AddSingleton<TimeProvider>(Time);
        services.AddScoped(_ => NewContext());
        services.AddScoped<VulnerabilityIntelService>();
        var provider = services.BuildServiceProvider();

        return new IntelRefresher(db, provider.GetRequiredService<IServiceScopeFactory>(), Epss, Kev,
            Microsoft.Extensions.Options.Options.Create(Options), Time, NullLogger<IntelRefresher>.Instance);
    }

    public void Dispose() => Db.Dispose();
}

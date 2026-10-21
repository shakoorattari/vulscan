using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vulscan.Application.Interfaces;
using Vulscan.Domain.Entities;
using Vulscan.Domain.Enums;
using Vulscan.Infrastructure.Data;
using Vulscan.Tests.Support;

namespace Vulscan.Tests.Http;

/// <summary>Hosts the real API in-process on an in-memory SQLite database.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Correct-Horse-9-Battery";
    public const string JwtKey = "integration-test-signing-key-integration-test-signing-key-0123456789";

    private readonly SqliteConnection _keepAlive;
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), "vulscan-test-keys-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> _settings;

    public StubEntraValidator Entra { get; } = new();

    // xUnit class fixtures need a parameterless constructor (it ignores C# default arguments).
    public ApiFactory() : this(1000, JwtKey) { }

    internal ApiFactory(int authRateLimitPerMinute, string? jwtKey = JwtKey)
    {
        var db = $"file:vulscan-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection($"DataSource={db}");
        _keepAlive.Open();

        _settings = new()
        {
            ["UseSqlite"] = "true",
            ["ConnectionStrings:VulscanDb"] = $"DataSource={db}",
            ["Jwt:SecretKey"] = jwtKey,
            ["Jwt:ExpirationMinutes"] = "30",
            ["BackgroundJobs:Enabled"] = "false",
            ["DataProtection:KeysPath"] = _keysPath,
            ["Security:AuthRateLimitPerMinute"] = authRateLimitPerMinute.ToString(),
            ["Auth:Entra:Enabled"] = "true",
            ["Auth:Entra:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["Auth:Entra:ClientId"] = "22222222-2222-2222-2222-222222222222",
        };
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Program.cs reads configuration eagerly while it builds, i.e. before ConfigureAppConfiguration overrides apply,
        // so these must go in as host settings (this is the documented route for minimal-hosting apps).
        foreach (var (key, value) in _settings)
            builder.UseSetting(key, value);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEntraTokenValidator>();
            services.AddSingleton<IEntraTokenValidator>(Entra);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<VulscanDbContext>().Database.EnsureCreated();
        return host;
    }

    // ── helpers ─────────────────────────────────────────────────────────

    public async Task<T> WithDbAsync<T>(Func<VulscanDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<VulscanDbContext>());
    }

    /// <summary>Adds a ready-to-use (no forced password change) local user directly in the database.</summary>
    public Task AddUserAsync(string username, UserRole role, string password = Password) => WithDbAsync(async db =>
    {
        db.Users.Add(new User
        {
            Username = username, Email = $"{username}@sdd.test", Role = role,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
            AccessGrantedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return 0;
    });

    public async Task<(HttpResponseMessage Response, JsonElement Body)> PostAsync(HttpClient client, string url, object body)
    {
        var res = await client.PostAsJsonAsync(url, body);
        var json = await res.Content.ReadAsStringAsync();
        return (res, string.IsNullOrWhiteSpace(json) ? default : JsonDocument.Parse(json).RootElement.Clone());
    }

    public async Task<string> LoginAsync(string username, string password = Password)
    {
        var (res, body) = await PostAsync(CreateClient(), "/api/v1/auth/login", new { username, password });
        Assert.True(res.IsSuccessStatusCode, $"login failed for {username}: {res.StatusCode} {body}");
        return body.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    public async Task<HttpClient> ClientForAsync(string username, string password = Password)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(username, password));
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
            try { Directory.Delete(_keysPath, recursive: true); } catch { /* best effort */ }
        }
    }
}

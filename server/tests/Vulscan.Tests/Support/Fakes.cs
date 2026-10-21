using Microsoft.Extensions.Configuration;
using Vulscan.Application.Auth;
using Vulscan.Application.Interfaces;
using Vulscan.Application.Services;
using Vulscan.Infrastructure.Data;
using Vulscan.Infrastructure.Services;

namespace Vulscan.Tests.Support;

public sealed class FakeTime(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public FakeTime() : this(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero)) { }
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

public sealed class RecordingAudit : IAuditService
{
    public List<AuditEntry> Entries { get; } = [];
    public Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
    public IEnumerable<AuditEntry> Of(string action) => Entries.Where(e => e.Action == action);
}

public sealed class FakeCurrentUser : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public string? Username { get; set; } = "admin";
    public string? Role { get; set; } = "Admin";
    public bool IsAuthenticated => UserId is not null;
}

public sealed class StubEntraValidator : IEntraTokenValidator
{
    public EntraIdentity? Identity { get; set; }
    public string? FailWith { get; set; }

    public Task<EntraIdentity> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (FailWith is not null) throw new UnauthorizedAccessException(FailWith);
        return Task.FromResult(Identity ?? throw new UnauthorizedAccessException("no identity configured"));
    }
}

/// <summary>An AuthService wired to a real (SQLite) database and real JWT service, with controllable time/audit/Entra.</summary>
public sealed class AuthHarness : IDisposable
{
    public const string AdminPassword = "Correct-Horse-9-Battery";

    public TestDb Db { get; } = new();
    public FakeTime Time { get; } = new();
    public RecordingAudit Audit { get; } = new();
    public StubEntraValidator Entra { get; } = new();
    public AuthOptions Options { get; }
    public JwtTokenService Jwt { get; }
    public VulscanDbContext Context { get; }
    public FakeCurrentUser CurrentUser { get; } = new();

    public AuthHarness(Action<AuthOptions>? configure = null)
    {
        Options = new AuthOptions();
        Options.Entra.Enabled = true;
        Options.Entra.TenantId = "11111111-1111-1111-1111-111111111111";
        Options.Entra.ClientId = "22222222-2222-2222-2222-222222222222";
        configure?.Invoke(Options);

        Jwt = new JwtTokenService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = new string('k', 64),
            ["Jwt:ExpirationMinutes"] = "30",
            ["Jwt:RefreshTokenHours"] = "12",
        }).Build());

        Context = Db.NewContext();
    }

    public AuthService Auth => new(Context, Jwt, Audit, Entra, Options, Time);
    public UserService Users => new(Context, Audit, CurrentUser, Options, Time);

    /// <summary>Adds an active local user whose password is <see cref="AdminPassword"/> unless another is given.</summary>
    public async Task<Vulscan.Domain.Entities.User> AddLocalUserAsync(
        string username, Vulscan.Domain.Enums.UserRole role = Vulscan.Domain.Enums.UserRole.SecurityAnalyst,
        string? password = null, bool mustChange = false)
    {
        var user = new Vulscan.Domain.Entities.User
        {
            Username = username,
            Email = $"{username}@sdd.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password ?? AdminPassword, workFactor: 4),   // low cost: tests only
            Role = role,
            MustChangePassword = mustChange,
            AccessGrantedAt = Time.GetUtcNow().UtcDateTime,
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    public void Dispose()
    {
        Context.Dispose();
        Db.Dispose();
    }
}

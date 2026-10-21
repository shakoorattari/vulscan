namespace Vulscan.Application.Auth;

/// <summary>Bound from the <c>Auth</c> configuration section.</summary>
public sealed class AuthOptions
{
    public LocalAuthOptions Local { get; set; } = new();
    public EntraAuthOptions Entra { get; set; } = new();
    public LockoutOptions Lockout { get; set; } = new();

    /// <summary>
    /// Accounts with no sign-in for this many days are deactivated automatically (removal of unused access).
    /// 0 disables the sweep.
    /// </summary>
    public int DormantDays { get; set; } = 90;

    /// <summary>An access review older than this many days is reported as overdue.</summary>
    public int AccessReviewIntervalDays { get; set; } = 90;
}

public sealed class LocalAuthOptions
{
    /// <summary>Username/password sign-in. Keep enabled until Entra is verified, then consider turning it off.</summary>
    public bool Enabled { get; set; } = true;
}

public sealed class LockoutOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int DurationMinutes { get; set; } = 15;
}

public sealed class EntraAuthOptions
{
    public bool Enabled { get; set; }

    /// <summary>Directory (tenant) id of the SDD Entra tenant. Single-tenant by design.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Application (client) id of the Vulscan app registration.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Maps Entra <em>app role</em> values (the <c>roles</c> claim) to Vulscan roles. A user holding none of these
    /// is refused, which is what limits access to the people SDD assigns to the application in Entra.
    /// </summary>
    public Dictionary<string, string> RoleMapping { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Vulscan.Admin"] = "Admin",
        ["Vulscan.SecurityAnalyst"] = "SecurityAnalyst",
        ["Vulscan.Viewer"] = "Viewer",
    };

    public string Authority => $"https://login.microsoftonline.com/{TenantId}/v2.0";

    public bool IsConfigured =>
        Enabled && Guid.TryParse(TenantId, out _) && Guid.TryParse(ClientId, out _);
}

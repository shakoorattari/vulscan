using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Vulscan.Application.Auth;
using Vulscan.Application.Interfaces;

namespace Vulscan.Infrastructure.Security;

/// <summary>
/// Validates Microsoft Entra ID (v2.0) ID tokens for a single tenant: RS256 signature against the tenant's published
/// signing keys (fetched from, and rotated with, its OpenID discovery document), issuer, audience (this app's
/// client id), tenant, lifetime. The algorithm is pinned so a token cannot downgrade validation to "none" or HMAC.
/// </summary>
public sealed class EntraTokenValidator : IEntraTokenValidator
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);

    private readonly AuthOptions _options;
    private readonly ILogger<EntraTokenValidator> _logger;
    private readonly Lazy<IConfigurationManager<OpenIdConnectConfiguration>> _configManager;

    public EntraTokenValidator(
        AuthOptions options,
        ILogger<EntraTokenValidator> logger,
        Func<HttpClient>? httpClientFactory = null,
        IConfigurationManager<OpenIdConnectConfiguration>? configurationManager = null)
    {
        _options = options;
        _logger = logger;

        // Created lazily: the instance exists even when Entra is disabled/unconfigured, and the authority URL
        // cannot be built until a tenant id is set.
        _configManager = new Lazy<IConfigurationManager<OpenIdConnectConfiguration>>(() =>
            configurationManager ?? new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{options.Entra.Authority}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever(httpClientFactory?.Invoke() ?? new HttpClient()) { RequireHttps = true }));
    }

    public async Task<EntraIdentity> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (!_options.Entra.IsConfigured)
            throw new UnauthorizedAccessException("Microsoft sign-in is not enabled.");

        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > 16_384)
            throw new UnauthorizedAccessException("Malformed token.");

        OpenIdConnectConfiguration config;
        try
        {
            config = await _configManager.Value.GetConfigurationAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load Entra OpenID configuration from {Authority}", _options.Entra.Authority);
            throw new UnauthorizedAccessException("Could not reach Microsoft to verify the sign-in.");
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Entra.Authority,
            ValidateAudience = true,
            ValidAudience = _options.Entra.ClientId,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = ClockSkew,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        };

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, parameters);
        if (!result.IsValid)
        {
            // A key may have just rotated: refresh the cached configuration so the next attempt succeeds.
            if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
                _configManager.Value.RequestRefresh();

            throw new UnauthorizedAccessException(result.Exception?.Message ?? "Token validation failed.");
        }

        string? Claim(string type) => result.ClaimsIdentity.FindFirst(type)?.Value;

        if (!string.Equals(Claim("tid"), _options.Entra.TenantId, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Token was issued for a different tenant.");

        var oid = Claim("oid");
        if (string.IsNullOrWhiteSpace(oid) || !Guid.TryParse(oid, out _))
            throw new UnauthorizedAccessException("Token has no valid object id.");

        var username = Claim("preferred_username") ?? Claim("upn") ?? Claim("email");
        if (string.IsNullOrWhiteSpace(username))
            throw new UnauthorizedAccessException("Token has no username.");

        return new EntraIdentity(
            ObjectId: oid.ToLowerInvariant(),
            Username: username.Trim(),
            Email: (Claim("email") ?? username).Trim(),
            DisplayName: Claim("name") ?? username,
            Roles: [.. result.ClaimsIdentity.FindAll("roles").Select(c => c.Value)]);
    }
}

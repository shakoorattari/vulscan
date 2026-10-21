namespace Vulscan.Application.Interfaces;

/// <summary>What Vulscan needs to know about someone who authenticated with Microsoft Entra ID.</summary>
public sealed record EntraIdentity(
    string ObjectId,
    string Username,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);

public interface IEntraTokenValidator
{
    /// <summary>
    /// Validates an Entra-issued ID token (signature against Entra's published keys, issuer, audience, tenant, lifetime).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The token is invalid, or Entra sign-in is not enabled.</exception>
    Task<EntraIdentity> ValidateAsync(string idToken, CancellationToken ct = default);
}

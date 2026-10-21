using System.Text.Json;
using Vulscan.Application.Interfaces;

namespace Vulscan.Application.Common;

/// <summary>A username/secret pair for authenticating against a source-control server.</summary>
public readonly record struct StoredCredentials(string Username, string Password)
{
    public bool IsEmpty => string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password);
}

/// <summary>
/// Single place that packs credentials into, and unpacks them from, the <c>CredentialReference</c>
/// columns. Values are always written encrypted; values written by earlier versions (plaintext JSON)
/// are still readable so existing data keeps working until <see cref="NeedsEncryption"/> rows are migrated.
/// </summary>
public sealed class CredentialVault(ISecretProtector protector)
{
    public string Pack(string username, string password) =>
        protector.Protect(JsonSerializer.Serialize(new { username, password }));

    /// <summary>Reads credentials from a stored value. Returns an empty pair for null/blank input.</summary>
    /// <exception cref="InvalidOperationException">The value is encrypted but cannot be decrypted.</exception>
    public StoredCredentials Unpack(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return default;

        var json = protector.IsProtected(stored) ? protector.Unprotect(stored) : stored;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new StoredCredentials(
                root.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "",
                root.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "");
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>True for a non-empty stored value that is still in the legacy plaintext format.</summary>
    public bool NeedsEncryption(string? stored) =>
        !string.IsNullOrWhiteSpace(stored) && !protector.IsProtected(stored);
}

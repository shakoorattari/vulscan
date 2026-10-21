using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Vulscan.Application.Interfaces;

namespace Vulscan.Infrastructure.Security;

/// <summary>
/// <see cref="ISecretProtector"/> backed by ASP.NET Core Data Protection (AES-256-CBC + HMAC-SHA256,
/// with a rotating key ring). Protected values are prefixed so plaintext and ciphertext can be told apart.
/// </summary>
public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    public const string Prefix = "enc:v1:";
    public const string Purpose = "Vulscan.Secrets.v1";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string plaintext) => Prefix + _protector.Protect(plaintext);

    public string Unprotect(string protectedValue)
    {
        if (!IsProtected(protectedValue))
            throw new InvalidOperationException("Value is not an encrypted secret.");

        try
        {
            return _protector.Unprotect(protectedValue[Prefix.Length..]);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "A stored secret could not be decrypted. The data-protection key ring has probably changed " +
                "or is missing (see DataProtection:KeysPath). Re-enter the credentials to recover.", ex);
        }
    }

    public bool IsProtected(string? value) =>
        value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);
}

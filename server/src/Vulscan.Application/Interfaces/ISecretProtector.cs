namespace Vulscan.Application.Interfaces;

/// <summary>
/// Encrypts secrets (SCM credentials, SMTP passwords) before they are persisted.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts <paramref name="plaintext"/>. The result is self-describing (see <see cref="IsProtected"/>).</summary>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypts a value produced by <see cref="Protect"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The value is not protected, or cannot be decrypted with the current keys.</exception>
    string Unprotect(string protectedValue);

    /// <summary>True when <paramref name="value"/> carries the protected-value marker.</summary>
    bool IsProtected(string? value);
}

namespace Vulscan.Domain.Enums;

/// <summary>Where a user's identity and credentials are managed.</summary>
public enum AuthProvider
{
    /// <summary>Username/password held (BCrypt-hashed) in Vulscan.</summary>
    Local = 0,

    /// <summary>Microsoft Entra ID. Role and enablement are governed in Entra; Vulscan holds no password.</summary>
    Entra = 1
}

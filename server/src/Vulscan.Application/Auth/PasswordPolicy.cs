namespace Vulscan.Application.Auth;

/// <summary>
/// Password rules from SECURITY.md: at least 12 characters, mixing upper case, lower case, digits and symbols,
/// and no personal information. Also refuses well-known and published passwords (including the seeded admin's).
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;

    private static readonly HashSet<string> Denied = new(StringComparer.OrdinalIgnoreCase)
    {
        "Vulscan@2025",          // seeded admin password, published in the README
        "Password123!", "Password@123", "Passw0rd!2025", "Welcome@12345", "Admin@123456", "Qwerty@123456",
        "ChangeMe123!", "Sharjah@2025", "Sharjah@2026", "Letmein@12345",
    };

    /// <summary>Returns the rules the password breaks; empty when it is acceptable.</summary>
    public static IReadOnlyList<string> Validate(string? password, string? username = null, string? email = null)
    {
        var problems = new List<string>();
        if (string.IsNullOrEmpty(password))
            return ["Password is required."];

        if (password.Length < MinLength) problems.Add($"Must be at least {MinLength} characters.");
        if (!password.Any(char.IsUpper)) problems.Add("Must contain an upper-case letter.");
        if (!password.Any(char.IsLower)) problems.Add("Must contain a lower-case letter.");
        if (!password.Any(char.IsDigit)) problems.Add("Must contain a digit.");
        if (!password.Any(c => !char.IsLetterOrDigit(c))) problems.Add("Must contain a symbol.");

        // A 12+ character password built from fewer than 6 distinct characters is a repeated pattern, not a secret.
        if (Denied.Contains(password) || password.Distinct().Count() < 6)
            problems.Add("Is too common or too simple.");

        foreach (var personal in PersonalTokens(username, email))
        {
            if (password.Contains(personal, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("Must not contain your username or email.");
                break;
            }
        }

        return problems;
    }

    // Whole username / email local-part, and each name part within them ("shakoor.hussain" → shakoor, hussain), so
    // "Shakoor-Hussain-9!" is caught as well as "shakoor.hussain1!". Parts shorter than 4 characters are ignored.
    private static IEnumerable<string> PersonalTokens(string? username, string? email)
    {
        foreach (var raw in new[] { username, email?.Split('@')[0] })
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var whole = raw.Trim();
            if (whole.Length >= 4) yield return whole;

            foreach (var part in whole.Split(['.', '-', '_', '+', ' '], StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Length >= 4 && part != whole) yield return part;
            }
        }
    }
}

namespace Yurt.Application.Common.Helpers;

/// <summary>
/// Admin usernames are stored trimmed and in lower case, and matched the same way, so "Anna" and "anna" are one person.
/// Accounts created before this rule may still have capitals; sign-in matches them case-insensitively.
/// </summary>
public static class UsernameNormalizer
{
    public static string Normalize(string? username) => (username ?? string.Empty).Trim().ToLowerInvariant();
}

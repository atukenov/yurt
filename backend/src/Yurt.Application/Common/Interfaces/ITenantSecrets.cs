namespace Yurt.Application.Common.Interfaces;

/// <summary>Encrypts and decrypts one secret. The cafe and key are bound into the cipher text, so a row cannot be moved to another cafe or key.</summary>
public interface ISecretProtector
{
    /// <summary>False when no master key is configured; nothing can be stored then.</summary>
    bool IsAvailable { get; }

    string Protect(Guid tenantId, string key, string plainText);

    /// <summary>Null when the value cannot be read (wrong master key, damaged or moved row).</summary>
    string? Unprotect(Guid tenantId, string key, string cipherText);
}

/// <summary>
/// Per-cafe secrets (payments, iiko, push, SMS codes). Reading a secret for the default cafe falls back to the
/// server-wide configuration, so the first cafe keeps working with its environment variables; any other cafe never
/// sees those values.
/// </summary>
public interface ITenantSecrets
{
    /// <summary>The current cafe's value, or null when it has none.</summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Which keys the given cafe has a stored value for (names only).</summary>
    Task<IReadOnlyList<string>> ConfiguredKeysAsync(Guid tenantId, CancellationToken ct = default);

    Task SetAsync(Guid tenantId, string key, string value, CancellationToken ct = default);

    Task<bool> ClearAsync(Guid tenantId, string key, CancellationToken ct = default);
}

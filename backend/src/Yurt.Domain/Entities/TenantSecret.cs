using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// One secret of one cafe (a payment key, an iiko key, ...). Only the encrypted form is stored; the value is never
/// returned by any API, only replaced or cleared.
/// </summary>
public class TenantSecret : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    /// <summary>Configuration-style name such as "Iiko:ApiKey". Must be one of <see cref="TenantSecretKeys.All"/>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Output of the secret protector (version, nonce, tag and cipher text, Base64).</summary>
    public string Ciphertext { get; set; } = string.Empty;
}

/// <summary>The secrets a cafe can have. Same names as the server-wide configuration keys they replace.</summary>
public static class TenantSecretKeys
{
    public static readonly string[] All =
    [
        "Stripe:SecretKey",
        "Stripe:WebhookSecret",
        "Firebase:ServiceAccountJson",
        "Otp:IdInstance",
        "Otp:ApiTokenInstance",
        "Fiscal:Login",
        "Fiscal:Password",
        "Fiscal:ApiKey",
        "Fiscal:Pin",
    ];
}

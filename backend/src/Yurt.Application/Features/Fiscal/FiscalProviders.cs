namespace Yurt.Application.Features.Fiscal;

public static class FiscalProviders
{
    public const string Webkassa = "webkassa";
    public const string ReKassa = "rekassa";

    public static readonly string[] All = [Webkassa, ReKassa];

    /// <summary>Secret names of the cafe's login at the provider (stored encrypted, see <c>TenantSecretKeys</c>).</summary>
    public const string LoginKey = "Fiscal:Login";
    public const string PasswordKey = "Fiscal:Password";
    public const string ApiKeyKey = "Fiscal:ApiKey";
    /// <summary>Register PIN; ReKassa asks for it to close the shift.</summary>
    public const string PinKey = "Fiscal:Pin";

    /// <summary>
    /// Whether the provider has one login for the whole cafe (Webkassa). ReKassa has none: each register logs in with
    /// its own serial number (the branch's register number) and the password.
    /// </summary>
    public static bool NeedsLogin(string? provider) => provider != ReKassa;
}

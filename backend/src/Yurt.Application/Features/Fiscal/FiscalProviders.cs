namespace Yurt.Application.Features.Fiscal;

public static class FiscalProviders
{
    public const string Webkassa = "webkassa";

    public static readonly string[] All = [Webkassa];

    /// <summary>Secret names of the cafe's login at the provider (stored encrypted, see <c>TenantSecretKeys</c>).</summary>
    public const string LoginKey = "Fiscal:Login";
    public const string PasswordKey = "Fiscal:Password";
    public const string ApiKeyKey = "Fiscal:ApiKey";
}

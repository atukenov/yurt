using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests;

public class YurtWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    // Stable constants referenced by all test helpers
    public const string JwtSecret    = "integration-test-jwt-secret-key-at-least-32-chars-ok";
    // The first cafe's Stripe test account (server-wide settings, like a single-cafe deployment).
    public const string StripeSecretKey = "sk_test_integration_server_wide";
    public const string StripeWebhookSecret = "whsec_integration_server_wide_secret";
    // The platform owner's own Stripe account, which bills the cafes monthly.
    public const string BillingSecretKey = "sk_test_integration_platform_billing";
    public const string BillingWebhookSecret = "whsec_integration_platform_billing";

    /// <summary>Plays the Stripe API for every test; no request leaves the machine.</summary>
    public FakeStripe Stripe { get; } = new();

    public async Task InitializeAsync()
    {
        // Prevent Railway env vars from overriding the test container connection string
        Environment.SetEnvironmentVariable("DATABASE_URL", null);
        Environment.SetEnvironmentVariable("RAILWAY_DATABASE_URL", null);

        // Env-var config takes effect before AddInfrastructure reads the secret.
        // "__" is the hierarchy separator for ASP.NET Core env-var config.
        Environment.SetEnvironmentVariable("Jwt__Secret", JwtSecret);

        await _pg.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _pg.StopAsync();
        await base.DisposeAsync();
        Environment.SetEnvironmentVariable("Jwt__Secret", null);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"]       = _pg.GetConnectionString(),
                ["Jwt:Secret"]                               = JwtSecret,
                ["Jwt:Issuer"]                               = "YurtTest",
                ["Jwt:Audience"]                             = "YurtTest",
                ["Jwt:AccessTokenMinutes"]                   = "60",
                ["Jwt:RefreshTokenDays"]                     = "7",
                ["Stripe:SecretKey"]                         = StripeSecretKey,
                ["Stripe:WebhookSecret"]                     = StripeWebhookSecret,
                ["Stripe:ApiBaseUrl"]                        = "https://stripe.test.invalid",
                ["Billing:Stripe:SecretKey"]                 = BillingSecretKey,
                ["Billing:Stripe:WebhookSecret"]             = BillingWebhookSecret,
                ["Payment:CustomerAppUrl"]                   = "https://app.test.invalid",
                ["Secrets:MasterKey"]                        = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray()),
                ["AllowedHosts"]                             = "*",
                // Rate-limit rules in appsettings use old paths that don't match /api/v1/…
                // so they never fire; disable endpoint mode for belt-and-suspenders
                ["IpRateLimiting:EnableEndpointRateLimiting"] = "false",
            }));

        builder.ConfigureServices(services =>
        {
            // Remove background workers that advance order statuses and interfere with assertions
            var toRemove = services
                .Where(s =>
                    s.ImplementationType?.FullName?.EndsWith("OrderArchivalService") == true ||
                    s.ImplementationType?.FullName?.EndsWith("OrderTimerService")    == true ||
                    s.ImplementationType?.FullName?.EndsWith("LoyaltyRetryService")  == true ||
                    s.ImplementationType?.FullName?.EndsWith("FiscalReceiptJob")     == true)
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddHttpClient<Yurt.Infrastructure.Payments.StripePaymentProvider>()
                .ConfigurePrimaryHttpMessageHandler(() => Stripe)
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
            services.AddHttpClient<Yurt.Infrastructure.Payments.StripeBillingGateway>()
                .ConfigurePrimaryHttpMessageHandler(() => Stripe)
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        });
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<YurtWebAppFactory> { }

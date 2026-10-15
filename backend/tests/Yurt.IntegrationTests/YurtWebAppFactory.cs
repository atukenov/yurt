using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Yurt.IntegrationTests;

public class YurtWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    // Stable constants referenced by all test helpers
    public const string JwtSecret    = "integration-test-jwt-secret-key-at-least-32-chars-ok";
    public const string WebhookSecret = "integration-test-webhook-secret-32-chars-ok-abc123";

    public async Task InitializeAsync()
    {
        // Prevent Railway env vars from overriding the test container connection string
        Environment.SetEnvironmentVariable("DATABASE_URL", null);
        Environment.SetEnvironmentVariable("RAILWAY_DATABASE_URL", null);

        // Env-var config takes effect before AddInfrastructure reads the secret.
        // "__" is the hierarchy separator for ASP.NET Core env-var config.
        Environment.SetEnvironmentVariable("Jwt__Secret", JwtSecret);
        Environment.SetEnvironmentVariable("Payment__WebhookSecret", WebhookSecret);

        await _pg.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _pg.StopAsync();
        await base.DisposeAsync();
        Environment.SetEnvironmentVariable("Jwt__Secret", null);
        Environment.SetEnvironmentVariable("Payment__WebhookSecret", null);
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
                ["Payment:WebhookSecret"]                    = WebhookSecret,
                ["Payment:WebhookCallbackUrl"]               = "http://test.invalid/webhook",
                ["Payment:SandboxSecret"]                    = "test-sandbox",
                // Delay so long the sandbox never fires during a test
                ["Payment:ProcessingDelaySecondsMin"]        = "99999",
                ["Payment:ProcessingDelaySecondsMax"]        = "99999",
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
                    s.ImplementationType?.FullName?.EndsWith("LoyaltyRetryService")  == true)
                .ToList();
            foreach (var d in toRemove) services.Remove(d);
        });
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<YurtWebAppFactory> { }

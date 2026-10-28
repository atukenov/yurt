using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Auth;
using Yurt.Application.Features.Auth.Services;
using Yurt.Application.Features.Favorites.Services;
using Yurt.Application.Features.Locations.Services;
using Yurt.Application.Features.Menu.Services;
using Yurt.Application.Features.Analytics.Services;
using Yurt.Application.Features.Orders.Services;
using Yurt.Application.Features.Customers;
using Yurt.Application.Features.GroupOrders;
using Yurt.Application.Features.Workers;
using Yurt.Application.Features.Payments;
using Yurt.Application.Features.Payments.Services;
using Yurt.Application.Features.Promotions.Services;
using Yurt.Application.Features.DiscountCodes.Services;
using Yurt.Application.Features.AuditLog.Services;
using Yurt.Application.Features.Reports;
using Yurt.Application.Features.AppUpdate;
using Yurt.Application.Features.Modules;
using Yurt.Application.Features.Platform;
using Yurt.Application.Features.Fiscal;
using Yurt.Application.Features.Shifts;
using Yurt.Application.Features.Stations;
using Yurt.Application.Features.Printing;
using Yurt.Infrastructure.Printing;
using Yurt.Application.Features.Stock;
using Yurt.Application.Features.IikoIntegration.Services;
using Yurt.Application.Features.Loyalty;
using Yurt.Application.Features.Loyalty.Services;
using Yurt.Application.Features.Push;
using Yurt.Application.Features.Push.Services;
using Yurt.Infrastructure.Fiscal;
using Yurt.Infrastructure.Health;
using Yurt.Infrastructure.Monitoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Infrastructure.Hubs;
using Yurt.Infrastructure.Iiko;
using Yurt.Infrastructure.Messaging;
using Yurt.Infrastructure.Payments;
using Yurt.Infrastructure.Persistence;
using Yurt.Infrastructure.Push;
using Yurt.Infrastructure.Services;

namespace Yurt.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                GetPostgreSqlConnectionString(configuration),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // HTTP context
        services.AddHttpContextAccessor();

        // Health checks — factory lambda resolves the connection string lazily (after all
        // configuration providers, including test overrides, have been applied)
        services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                "postgres",
                _ => new NpgsqlHealthCheck(GetPostgreSqlConnectionString(configuration)),
                HealthStatus.Unhealthy,
                ["ready"]));

        // Menu cache (singleton — wraps IMemoryCache which is also singleton)
        services.AddSingleton<IMenuCacheService, MemoryMenuCacheService>();

        // Services
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton<ITenantDirectory, TenantDirectory>();
        services.AddScoped<IOrdersHubService, OrdersHubService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        // Online payments: Stripe, chosen per cafe (Tenant.PaymentProvider), keys in the cafe's secrets
        services.AddHttpClient();
        // Read when first used, so settings added after startup wiring (tests, hosting) are seen.
        services.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection("Stripe").Get<StripeOptions>() ?? new StripeOptions());
        services.AddHttpClient<StripePaymentProvider>();
        services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<StripePaymentProvider>());
        services.AddScoped<IPaymentProviderFactory, PaymentProviderFactory>();
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new PaymentReturnOptions
            {
                AllowedOrigins = config.GetSection("AllowedOrigins").Get<List<string>>() ?? [],
                CustomerAppUrl = config["Payment:CustomerAppUrl"],
            };
        });
        services.AddScoped<PaymentSettingsService>();

        // WhatsApp OTP for customer registration (feature-flagged via Otp:Enabled; secrets via env vars)
        services.AddSingleton(configuration.GetSection("Otp").Get<OtpOptions>() ?? new OtpOptions());
        // Scoped, so the sender can read the current cafe's own WhatsApp credentials.
        services.AddHttpClient("whatsapp-otp");
        services.AddScoped<IOtpSender>(sp => new WhatsAppOtpSender(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("whatsapp-otp"),
            sp.GetRequiredService<OtpOptions>(),
            sp.GetRequiredService<ITenantSecrets>()));

        // iiko loyalty (feature-flagged via Iiko:Enabled; secrets via env vars)
        services.AddSingleton(configuration.GetSection("Iiko").Get<IikoOptions>() ?? new IikoOptions());
        services.AddSingleton<IikoTokenStore>();

        // Forced-update gate (blank MinVersion* disables the gate for that platform)
        services.AddSingleton(configuration.GetSection("AppUpdate").Get<AppUpdateOptions>() ?? new AppUpdateOptions());

        // Remote push notifications (feature-flagged via Firebase:Enabled; secrets via env vars)
        services.AddSingleton(configuration.GetSection("Firebase").Get<FirebaseOptions>() ?? new FirebaseOptions());
        services.AddSingleton<FirebaseAppCache>();
        services.AddScoped<IPushNotificationSender, FcmPushNotificationSender>();
        services.AddScoped<PushNotificationService>();

        services.AddHttpClient<IIikoApiClient, IikoApiClient>();
        services.AddScoped<IikoLoyaltyLedger>();
        services.AddScoped<NativeLoyaltyLedger>();
        services.AddScoped<SwitchingLoyaltyLedger>();
        services.AddScoped<ILoyaltyLedger>(sp => sp.GetRequiredService<SwitchingLoyaltyLedger>());
        services.AddScoped<ILoyaltyLedgerChoice>(sp => sp.GetRequiredService<SwitchingLoyaltyLedger>());
        services.AddScoped<LoyaltyLedgerSwitchService>();
        services.AddScoped<LoyaltyService>();
        services.AddScoped<IikoOrderSyncService>();
        services.AddScoped<IPosOrderSink>(sp => sp.GetRequiredService<IikoOrderSyncService>());
        services.AddScoped<IIikoWebhookValidator, IikoWebhookValidator>();

        // Application services
        services.AddScoped<AnalyticsService>();
        services.AddScoped<AuthService>();
        services.AddScoped<LocationService>();
        services.AddScoped<AppReleaseService>();

        // Paid-module licensing (fail-open; see docs/MODULARIZATION_TODO.md Phase 1)
        services.AddSingleton(configuration.GetSection("Modules").Get<ModuleOptions>() ?? new ModuleOptions());
        services.AddSingleton<IModuleGate, ModuleGate>();
        services.AddSingleton<IMaintenanceGate, MaintenanceGate>();
        services.AddScoped<PlatformService>();
        services.AddScoped<CafeProvisioningService>();
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddScoped<ITenantSecrets, TenantSecrets>();
        services.AddScoped<CafeSecretsService>();
        services.AddScoped<LoyaltyImportService>();
        services.AddScoped<StockAlertPushService>();
        services.AddScoped<StockService>();
        services.AddScoped<IngredientService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<SplitPaymentService>();

        // Official tax receipts through a cloud cash register (own_pos; off per cafe until a provider is set)
        services.AddSingleton(configuration.GetSection("Webkassa").Get<WebkassaOptions>() ?? new WebkassaOptions());
        services.AddSingleton<WebkassaTokenCache>();
        services.AddHttpClient<WebkassaFiscalRegistrar>();
        services.AddScoped<IFiscalRegistrar>(sp => sp.GetRequiredService<WebkassaFiscalRegistrar>());
        services.AddScoped<FiscalReceiptService>();
        services.AddScoped<FiscalSettingsService>();
        services.AddScoped<StationService>();
        services.AddScoped<StationReadyService>();
        services.AddSingleton<PrintQueueSignal>();
        services.AddSingleton<IPrinterTransport, TcpPrinterTransport>();
        services.AddSingleton<IQrCodeImages, QrCodeImages>();
        services.AddScoped<PrinterService>();
        services.AddScoped<PrintService>();
        services.AddScoped<PrintAgentService>();
        services.AddScoped<BluetoothPrintService>();
        services.AddScoped<MenuService>();
        services.AddScoped<OrderService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<PromotionService>();
        services.AddScoped<DiscountCodeService>();
        services.AddScoped<FavoriteService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<WorkerService>();
        services.AddScoped<GroupOrderService>();
        services.AddScoped<ReportService>();

        // Background services
        services.AddHostedService<OrderArchivalService>();
        services.AddHostedService<OrderTimerService>();
        services.AddHostedService<LoyaltyRetryService>();
        services.AddHostedService<IikoOrderSyncRetryService>();
        services.AddHostedService<FiscalReceiptJob>();
        services.AddHostedService<PrintJobService>();

        // Error alerts to Telegram (off until Alerts:TelegramBotToken and Alerts:TelegramChatId are set; docs/monitoring.md)
        services.AddSingleton(configuration.GetSection("Alerts").Get<AlertOptions>() ?? new AlertOptions());
        services.AddSingleton<ErrorAlertSink>();
        services.AddHostedService(sp => new ErrorAlertService(
            sp.GetRequiredService<ErrorAlertSink>(),
            sp.GetRequiredService<AlertOptions>(),
            sp.GetRequiredService<IHostEnvironment>(),
            sp.GetRequiredService<ILogger<ErrorAlertService>>()));

        // JWT Auth — fail-fast secret validation
        var jwtSecret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
            throw new InvalidOperationException(
                "Jwt:Secret is not configured. Set the Jwt__Secret environment variable.");
        if (jwtSecret.StartsWith("CHANGE_THIS", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Jwt:Secret is still set to the placeholder value. Generate a real secret and set it via Jwt__Secret environment variable.");
        if (jwtSecret.Length < 32)
            throw new InvalidOperationException(
                $"Jwt:Secret must be at least 32 characters long (currently {jwtSecret.Length}).");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSecret))
                };

                // Allow SignalR to use token from query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireClaim("user_type", "admin"));
            options.AddPolicy("CustomerOnly", policy =>
                policy.RequireClaim("user_type", "customer"));
            options.AddPolicy("AdminRoleAdmin", policy =>
            {
                policy.RequireClaim("user_type", "admin");
                policy.RequireRole("Admin", "SuperAdmin");
            });
            options.AddPolicy("SuperAdminOnly", policy =>
            {
                policy.RequireClaim("user_type", "admin");
                policy.RequireRole("SuperAdmin");
            });
        });

        // SignalR — detailed errors only in development
        var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = isDev;
        }).AddJsonProtocol(opts =>
        {
            opts.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            opts.PayloadSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        return services;
    }

    private static string GetPostgreSqlConnectionString(IConfiguration configuration)
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("RAILWAY_DATABASE_URL")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new InvalidOperationException("PostgreSQL connection string must be configured via DATABASE_URL, RAILWAY_DATABASE_URL, or DefaultConnection.");
        }

        if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(databaseUrl);
            var userInfoParts = uri.UserInfo.Split(':', 2);
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = uri.AbsolutePath.TrimStart('/'),
                Username = userInfoParts.Length > 0 ? userInfoParts[0] : string.Empty,
                Password = userInfoParts.Length > 1 ? userInfoParts[1] : string.Empty,
                SslMode = SslMode.Prefer
            };
            return builder.ConnectionString;
        }

        return databaseUrl;
    }
}

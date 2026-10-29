using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Monthly billing of cafes on the platform owner's Stripe account (test mode, Stripe played by
/// <see cref="FakeStripe"/>): the plan, the checkout page, and paid invoices moving the modules' expiry dates.
/// Each test opens its own cafe so the first cafe's modules are never touched.</summary>
[Collection("Integration")]
public class CafeBillingTests(YurtWebAppFactory factory)
{
    private const string AdminOrigin = "http://localhost:4300";

    private async Task<(Func<HttpClient> Op, Guid CafeId, string Slug)> NewCafeAsync()
    {
        var token = await ApiHelpers.CreateAdminTokenAsync(factory.Services, factory.CreateClient(),
            username: "platform-owner", password: "PlatformOwner@123", role: AdminRole.SuperAdmin);
        var id = Guid.NewGuid().ToString("N")[..8];
        var slug = $"bill-{id}";
        HttpClient Client(string? tenant)
        {
            var c = factory.CreateClient();
            ApiHelpers.Authorize(c, token);
            if (tenant is not null) c.DefaultRequestHeaders.Add("X-Tenant", tenant);
            return c;
        }
        var created = await Client(null).PostAsJsonAsync("/api/v1/platform/cafes",
            new { slug, name = $"Bill {id}", domain = (string?)null, adminUsername = $"owner-{id}", adminPassword = "FirstLogin@123" });
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var cafeId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (() => Client(slug), cafeId, slug);
    }

    private static async Task SetPlanAsync(HttpClient op, decimal price, string[]? modules = null)
    {
        var res = await op.PutAsJsonAsync("/api/v1/platform/billing", new { monthlyPrice = price, currency = "usd", modules });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    private async Task<HttpStatusCode> SendAsync(string body, string? secret = null)
        => (await factory.CreateClient().SendAsync(StripeWebhooks.BillingRequest(body, secret))).StatusCode;

    private static string CheckoutCompleted(Guid cafeId, string sub, string customer)
        => StripeWebhooks.Event("checkout.session.completed", new
        {
            id = $"cs_test_{Guid.NewGuid():N}", @object = "checkout.session", mode = "subscription",
            client_reference_id = cafeId.ToString(), subscription = sub, customer,
            metadata = new { tenant_id = cafeId.ToString() },
        });

    /// <summary>An invoice in the 2025 API shape: the subscription sits under parent.subscription_details.</summary>
    private static string InvoicePaid(string sub, string customer, DateTimeOffset periodEnd, Guid? cafeId = null, long amount = 4900)
        => StripeWebhooks.Event("invoice.paid", new
        {
            id = $"in_{Guid.NewGuid():N}", @object = "invoice", customer, amount_paid = amount,
            parent = new
            {
                type = "subscription_details",
                subscription_details = new { subscription = sub, metadata = cafeId is null ? null : new { tenant_id = cafeId.ToString() } },
            },
            lines = new
            {
                data = new[]
                {
                    new { period = new { start = periodEnd.AddMonths(-1).ToUnixTimeSeconds(), end = periodEnd.ToUnixTimeSeconds() } },
                },
            },
        });

    private async Task<List<ModuleLicense>> LicensesAsync(Guid cafeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        return await db.ModuleLicenses.IgnoreQueryFilters().AsNoTracking().Where(l => l.TenantId == cafeId).ToListAsync();
    }

    [Fact]
    public async Task OwnerStartsPaying_OnThePlatformStripeAccount_WithTheCafePrice()
    {
        var (op, cafeId, slug) = await NewCafeAsync();

        // No price yet: the owner cannot start.
        var before = await op().GetFromJsonAsync<JsonElement>("/api/v1/admin/subscription/billing");
        Assert.False(before.GetProperty("canSubscribe").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict,
            (await op().PostAsJsonAsync("/api/v1/admin/subscription/billing/checkout", new { returnUrl = $"{AdminOrigin}/subscription" })).StatusCode);

        await SetPlanAsync(op(), 49m);
        var info = await op().GetFromJsonAsync<JsonElement>("/api/v1/admin/subscription/billing");
        Assert.True(info.GetProperty("canSubscribe").GetBoolean());
        Assert.True(info.GetProperty("testMode").GetBoolean());

        var res = await op().PostAsJsonAsync("/api/v1/admin/subscription/billing/checkout", new { returnUrl = $"{AdminOrigin}/subscription" });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        Assert.StartsWith("https://checkout.stripe.com/", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString());

        var call = factory.Stripe.Calls.Last(c => c.Path == "/v1/checkout/sessions" && c.Form.GetValueOrDefault("metadata[cafe]") == slug);
        Assert.Equal(YurtWebAppFactory.BillingSecretKey, call.ApiKey);
        Assert.Equal("subscription", call.Form["mode"]);
        Assert.Equal("4900", call.Form["line_items[0][price_data][unit_amount]"]);
        Assert.Equal("month", call.Form["line_items[0][price_data][recurring][interval]"]);
        Assert.Equal(cafeId.ToString(), call.Form["subscription_data[metadata][tenant_id]"]);
        Assert.Equal($"{AdminOrigin}/subscription?billing=done", call.Form["success_url"]);
    }

    [Fact]
    public async Task ReturnAddressOutsideOurApps_IsRefused()
    {
        var (op, _, _) = await NewCafeAsync();
        await SetPlanAsync(op(), 10m);
        var res = await op().PostAsJsonAsync("/api/v1/admin/subscription/billing/checkout", new { returnUrl = "https://evil.example/steal" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PaidInvoice_MovesTheCoveredModulesToTheEndOfThePaidMonth()
    {
        var (op, cafeId, _) = await NewCafeAsync();
        await SetPlanAsync(op(), 49m, [ModuleKeys.Marketing, ModuleKeys.Loyalty]);
        var sub = $"sub_{Guid.NewGuid():N}";
        var customer = $"cus_{Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.OK, await SendAsync(CheckoutCompleted(cafeId, sub, customer)));
        var periodEnd = DateTimeOffset.UtcNow.AddDays(30);
        periodEnd = DateTimeOffset.FromUnixTimeSeconds(periodEnd.ToUnixTimeSeconds());
        // No metadata on the invoice: the cafe is found by the subscription id from checkout.
        Assert.Equal(HttpStatusCode.OK, await SendAsync(InvoicePaid(sub, customer, periodEnd)));

        var licenses = await LicensesAsync(cafeId);
        foreach (var key in new[] { ModuleKeys.Marketing, ModuleKeys.Loyalty })
        {
            var l = Assert.Single(licenses, x => x.ModuleKey == key);
            Assert.True(l.Enabled);
            Assert.Equal(ModuleLicenseSource.Paid, l.Source);
            Assert.Equal(periodEnd.UtcDateTime, l.ValidUntil);
        }
        Assert.DoesNotContain(licenses, x => x.ModuleKey == ModuleKeys.Push && x.Source == ModuleLicenseSource.Paid);

        // Limited mode reads the cafe's own paid-until date, so a payment lifts it.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            Assert.Equal(periodEnd.UtcDateTime, (await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == cafeId)).PaidUntil);
        }

        var platform = await op().GetFromJsonAsync<JsonElement>("/api/v1/platform/billing");
        Assert.Equal("active", platform.GetProperty("status").GetString());
        Assert.Equal(49m, platform.GetProperty("lastPaidAmount").GetDecimal());
        Assert.Equal(periodEnd.UtcDateTime, platform.GetProperty("paidUntil").GetDateTime().ToUniversalTime());

        // A late copy of an older month's invoice never moves the date back.
        Assert.Equal(HttpStatusCode.OK, await SendAsync(InvoicePaid(sub, customer, periodEnd.AddMonths(-1))));
        Assert.Equal(periodEnd.UtcDateTime, (await LicensesAsync(cafeId)).Single(x => x.ModuleKey == ModuleKeys.Marketing).ValidUntil);

        // Already subscribed: a second checkout is refused, the billing page opens instead.
        Assert.Equal(HttpStatusCode.Conflict,
            (await op().PostAsJsonAsync("/api/v1/admin/subscription/billing/checkout", new { returnUrl = $"{AdminOrigin}/subscription" })).StatusCode);
        var portal = await op().PostAsJsonAsync("/api/v1/admin/subscription/billing/portal", new { returnUrl = $"{AdminOrigin}/subscription" });
        Assert.True(portal.IsSuccessStatusCode, await portal.Content.ReadAsStringAsync());
        Assert.Contains(factory.Stripe.Calls, c => c.Path == "/v1/billing_portal/sessions" && c.Form["customer"] == customer);
    }

    [Fact]
    public async Task InvoiceBeforeCheckoutWebhook_IsPlacedByMetadata_AndUnknownOnesAreRetried()
    {
        var (op, cafeId, _) = await NewCafeAsync();
        await SetPlanAsync(op(), 20m);
        var periodEnd = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddDays(31).ToUnixTimeSeconds());

        // Unknown subscription and no cafe id: Stripe gets an error and sends it again later.
        Assert.Equal(HttpStatusCode.Conflict, await SendAsync(InvoicePaid($"sub_{Guid.NewGuid():N}", "cus_x", periodEnd)));

        // Invoice first (Stripe does not promise the order): the cafe id in the metadata is enough. Plan covers all modules.
        Assert.Equal(HttpStatusCode.OK, await SendAsync(InvoicePaid($"sub_{Guid.NewGuid():N}", $"cus_{Guid.NewGuid():N}", periodEnd, cafeId)));
        var licenses = await LicensesAsync(cafeId);
        Assert.Equal(ModuleKeys.All.Count - 1, licenses.Count(l => l.Source == ModuleLicenseSource.Paid && l.ValidUntil == periodEnd.UtcDateTime));
        Assert.DoesNotContain(licenses, l => l.ModuleKey == ModuleKeys.Core && l.Source == ModuleLicenseSource.Paid);
    }

    [Fact]
    public async Task SubscriptionEnded_IsShown_ButModulesRunToTheEndOfThePaidMonth()
    {
        var (op, cafeId, _) = await NewCafeAsync();
        await SetPlanAsync(op(), 20m, [ModuleKeys.Analytics]);
        var sub = $"sub_{Guid.NewGuid():N}";
        var customer = $"cus_{Guid.NewGuid():N}";
        var periodEnd = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddDays(10).ToUnixTimeSeconds());
        await SendAsync(CheckoutCompleted(cafeId, sub, customer));
        await SendAsync(InvoicePaid(sub, customer, periodEnd));

        var deleted = StripeWebhooks.Event("customer.subscription.deleted", new
        {
            id = sub, @object = "subscription", customer, status = "canceled", cancel_at_period_end = false,
            metadata = new { tenant_id = cafeId.ToString() },
        });
        Assert.Equal(HttpStatusCode.OK, await SendAsync(deleted));

        var info = await op().GetFromJsonAsync<JsonElement>("/api/v1/admin/subscription/billing");
        Assert.Equal("canceled", info.GetProperty("status").GetString());
        Assert.True(info.GetProperty("canSubscribe").GetBoolean()); // can start again
        Assert.Equal(periodEnd.UtcDateTime, (await LicensesAsync(cafeId)).Single(l => l.ModuleKey == ModuleKeys.Analytics).ValidUntil);
    }

    [Fact]
    public async Task WebhookWithWrongSignature_IsRefused()
    {
        var body = InvoicePaid("sub_x", "cus_x", DateTimeOffset.UtcNow.AddDays(30), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync(body, secret: "whsec_someone_else"));
    }

    [Fact]
    public async Task Plan_RejectsUnknownModulesAndBadPrices()
    {
        var (op, _, _) = await NewCafeAsync();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await op().PutAsJsonAsync("/api/v1/platform/billing", new { monthlyPrice = 10m, currency = "usd", modules = new[] { "core" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await op().PutAsJsonAsync("/api/v1/platform/billing", new { monthlyPrice = -5m, currency = "usd" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await op().PutAsJsonAsync("/api/v1/platform/billing", new { monthlyPrice = 10m, currency = "rub" })).StatusCode);
    }
}

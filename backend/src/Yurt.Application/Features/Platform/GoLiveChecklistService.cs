using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Auth;
using Yurt.Application.Features.Billing;
using Yurt.Application.Features.Fiscal;
using Yurt.Application.Features.Payments;
using Yurt.Application.Features.Payments.Services;
using Yurt.Application.Features.Push;
using Yurt.Domain;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Platform;

/// <summary>
/// Read-only list of what a cafe still needs before it can go live: server settings shared by every cafe, then the
/// chosen cafe's own setup (domain, brand, menu, branches, payments, push, WhatsApp codes, tax receipts, billing).
/// Secrets are only checked for being there; no value ever leaves this class. An item for a module that is switched
/// off is "off", so it never counts as missing.
/// </summary>
public class GoLiveChecklistService(
    IApplicationDbContext db,
    ITenantContext tenant,
    ITenantSecrets secrets,
    ISecretProtector protector,
    IModuleGate modules,
    IBillingGateway billingGateway,
    PaymentReturnOptions returnOptions,
    FirebaseOptions firebase,
    OtpOptions otp)
{
    /// <summary>Stripe statuses in which the cafe's monthly payment runs.</summary>
    private static readonly string[] PayingStatuses = ["active", "trialing"];

    public async Task<GoLiveChecklistDto?> GetAsync(bool telegramAlertsSet, CancellationToken ct = default)
    {
        var cafe = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenant.TenantId, ct);
        if (cafe is null) return null;

        var server = new List<GoLiveItemDto>
        {
            Item("master_key", protector.IsAvailable),
            Item("payment_return_url", !string.IsNullOrWhiteSpace(returnOptions.CustomerAppUrl)),
            Item("telegram_alerts", telegramAlertsSet),
            billingGateway.IsConfigured
                ? new GoLiveItemDto("billing_keys", billingGateway.TestMode ? GoLiveState.Warn : GoLiveState.Done,
                    billingGateway.TestMode ? ["test_mode"] : [])
                : Missing("billing_keys"),
        };

        var items = new List<GoLiveItemDto>
        {
            // The first cafe is served on the main address, so it needs no domain of its own.
            Item("domain", cafe.Domain is not null || cafe.Id == Tenant.DefaultId),
            await BrandAsync(ct),
            await MenuAsync(ct),
            await BranchesAsync(ct),
            await PaymentsAsync(cafe, ct),
            await PushAsync(ct),
            await WhatsAppAsync(ct),
            await FiscalAsync(cafe, ct),
            await BillingAsync(cafe, ct),
        };

        return new GoLiveChecklistDto(cafe.Slug, cafe.Name, server, items);
    }

    private async Task<GoLiveItemDto> BrandAsync(CancellationToken ct)
    {
        var brand = await BrandReader.ReadAsync(db, ct);
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(brand.LogoUrl)) missing.Add("logo");
        if (string.IsNullOrWhiteSpace(brand.IconUrl)) missing.Add("icon");
        return Parts("brand", missing);
    }

    private async Task<GoLiveItemDto> MenuAsync(CancellationToken ct)
    {
        var count = await db.MenuItems.CountAsync(m => m.IsAvailable, ct);
        return new GoLiveItemDto("menu", count > 0 ? GoLiveState.Done : GoLiveState.Missing, [], count);
    }

    private async Task<GoLiveItemDto> BranchesAsync(CancellationToken ct)
    {
        var count = await db.Locations.CountAsync(l => l.IsActive, ct);
        return new GoLiveItemDto("branches", count > 0 ? GoLiveState.Done : GoLiveState.Missing, [], count);
    }

    private async Task<GoLiveItemDto> PaymentsAsync(Tenant cafe, CancellationToken ct)
    {
        if (cafe.PaymentProvider is null && !await modules.IsEnabledAsync(ModuleKeys.OnlinePayments, ct))
            return Off("stripe");

        var missing = new List<string>();
        if (cafe.PaymentProvider != PaymentProviders.Stripe) missing.Add("provider");
        // Read through the secrets service, so the first cafe's server-wide keys count too.
        var secretKey = await secrets.GetAsync(PaymentProviders.StripeSecretKey, ct);
        if (string.IsNullOrWhiteSpace(secretKey)) missing.Add(PaymentProviders.StripeSecretKey);
        if (string.IsNullOrWhiteSpace(await secrets.GetAsync(PaymentProviders.StripeWebhookSecret, ct)))
            missing.Add(PaymentProviders.StripeWebhookSecret);

        if (missing.Count > 0) return Parts("stripe", missing);
        return PaymentSettingsService.IsTestKey(secretKey!)
            ? new GoLiveItemDto("stripe", GoLiveState.Warn, ["test_mode"])
            : Item("stripe", true);
    }

    private async Task<GoLiveItemDto> PushAsync(CancellationToken ct)
    {
        if (!await modules.IsEnabledAsync(ModuleKeys.Push, ct)) return Off("firebase");
        var missing = new List<string>();
        if (!firebase.Enabled) missing.Add("server_switch");
        if (string.IsNullOrWhiteSpace(await secrets.GetAsync("Firebase:ServiceAccountJson", ct)))
            missing.Add("Firebase:ServiceAccountJson");
        return Parts("firebase", missing);
    }

    private async Task<GoLiveItemDto> WhatsAppAsync(CancellationToken ct)
    {
        // Codes are asked for only while the server requires them and the cafe has the verification module.
        if (!otp.Required || !await modules.IsEnabledAsync(ModuleKeys.Verification, ct)) return Off("whatsapp");
        var missing = new List<string>();
        if (!otp.Enabled) missing.Add("server_switch");
        foreach (var key in new[] { "Otp:IdInstance", "Otp:ApiTokenInstance" })
            if (string.IsNullOrWhiteSpace(await secrets.GetAsync(key, ct))) missing.Add(key);
        return Parts("whatsapp", missing);
    }

    private async Task<GoLiveItemDto> FiscalAsync(Tenant cafe, CancellationToken ct)
    {
        // Tax receipts are needed once the cafe sells through its own POS instead of iiko.
        if (cafe.FiscalProvider is null && !await modules.IsEnabledAsync(ModuleKeys.OwnPos, ct)) return Off("fiscal");

        var missing = new List<string>();
        if (cafe.FiscalProvider is null) missing.Add("provider");
        else
        {
            var keys = (await secrets.ConfiguredKeysAsync(cafe.Id, ct)).ToHashSet();
            if (FiscalProviders.NeedsLogin(cafe.FiscalProvider) && !keys.Contains(FiscalProviders.LoginKey))
                missing.Add(FiscalProviders.LoginKey);
            if (!keys.Contains(FiscalProviders.PasswordKey)) missing.Add(FiscalProviders.PasswordKey);
            if (cafe.FiscalProvider == FiscalProviders.ReKassa && !keys.Contains(FiscalProviders.PinKey))
                missing.Add(FiscalProviders.PinKey);
        }

        // Branch names, so the operator sees which register number is still missing.
        var withoutRegister = await db.Locations.AsNoTracking()
            .Where(l => l.IsActive && (l.FiscalRegisterNumber == null || l.FiscalRegisterNumber == ""))
            .OrderBy(l => l.Name)
            .Select(l => l.Name)
            .ToListAsync(ct);
        return new GoLiveItemDto("fiscal", missing.Count == 0 && withoutRegister.Count == 0 ? GoLiveState.Done : GoLiveState.Missing,
            missing, null, withoutRegister);
    }

    private async Task<GoLiveItemDto> BillingAsync(Tenant cafe, CancellationToken ct)
    {
        // The first cafe is the platform owner's own and does not pay itself.
        if (cafe.Id == Tenant.DefaultId) return Off("billing");
        var row = await db.CafeBillings.AsNoTracking().FirstOrDefaultAsync(ct);
        var missing = new List<string>();
        if (row?.MonthlyPrice is null) missing.Add("price");
        if (row?.Status is null || !PayingStatuses.Contains(row.Status)) missing.Add("subscription");
        return Parts("billing", missing);
    }

    private static GoLiveItemDto Item(string key, bool done) => done ? new(key, GoLiveState.Done, []) : Missing(key);
    private static GoLiveItemDto Missing(string key) => new(key, GoLiveState.Missing, []);
    private static GoLiveItemDto Off(string key) => new(key, GoLiveState.Off, []);
    private static GoLiveItemDto Parts(string key, List<string> missing)
        => new(key, missing.Count == 0 ? GoLiveState.Done : GoLiveState.Missing, missing);
}

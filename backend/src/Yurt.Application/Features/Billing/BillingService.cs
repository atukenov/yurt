using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Payments.Services;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Billing;

/// <summary>
/// Monthly billing of cafes by the platform owner through the owner's own Stripe account. The owner sets each cafe's price and
/// the modules it covers on the Platform screen; the cafe owner starts the subscription from the Subscription page.
/// Every paid invoice moves the covered modules' expiry date (<see cref="ModuleLicense.ValidUntil"/>) to the end of the
/// paid month. When payments stop, nothing is switched off here: the modules simply expire and the usual grace period
/// applies.
/// </summary>
public class BillingService(
    IApplicationDbContext db,
    ITenantContext tenant,
    IBillingGateway gateway,
    IModuleGate modules,
    IAuditLogService audit,
    PaymentReturnOptions returnOptions,
    ILogger<BillingService> logger)
{
    public static readonly string[] Currencies = ["usd", "eur", "kzt"];
    public const string WebhookPath = "/api/v1/billing/stripe/webhook";
    public const decimal MaxMonthlyPrice = 10_000_000m;

    /// <summary>Stripe statuses in which the cafe already has a subscription that is (or may again be) charged.</summary>
    private static readonly string[] RunningStatuses = ["active", "trialing", "past_due", "unpaid", "incomplete", "paused"];

    /// <summary>Every module a plan can cover (core is always on and never billed).</summary>
    public static List<string> BillableModules => ModuleKeys.All.Where(k => k != ModuleKeys.Core).ToList();

    // ── Platform screen (SuperAdmin, for the cafe being managed) ──

    public async Task<PlatformBillingDto> GetPlatformAsync(string serverUrl, CancellationToken ct = default)
    {
        var row = await db.CafeBillings.AsNoTracking().FirstOrDefaultAsync(ct);
        return ToPlatformDto(row, serverUrl);
    }

    public async Task<Result<PlatformBillingDto>> SavePlanAsync(SaveBillingPlanDto dto, string serverUrl, CancellationToken ct = default)
    {
        var currency = (dto.Currency ?? "usd").Trim().ToLowerInvariant();
        if (!Currencies.Contains(currency))
            return Result<PlatformBillingDto>.Failure($"Currency must be one of: {string.Join(", ", Currencies)}.", 400);
        if (dto.MonthlyPrice is { } price && (price <= 0 || price > MaxMonthlyPrice || decimal.Round(price, 2) != price))
            return Result<PlatformBillingDto>.Failure("Monthly price must be more than 0, with at most 2 decimals.", 400);

        var keys = (dto.Modules ?? []).Select(k => k.Trim()).Where(k => k.Length > 0).Distinct().ToList();
        var unknown = keys.Where(k => !BillableModules.Contains(k)).ToList();
        if (unknown.Count > 0)
            return Result<PlatformBillingDto>.Failure($"Unknown or not billable module: {string.Join(", ", unknown)}.", 400);

        var row = await db.CafeBillings.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new CafeBilling();
            db.CafeBillings.Add(row);
        }
        row.MonthlyPrice = dto.MonthlyPrice;
        row.Currency = currency;
        // Every billable module ticked is the same as "all", and stays "all" when new modules are added later.
        row.ModuleKeys = keys.Count == 0 || keys.Count == BillableModules.Count ? null : string.Join(",", keys);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync("BillingPlanSaved", "CafeBilling", tenant.TenantId.ToString(),
            $"price={row.MonthlyPrice?.ToString() ?? "none"} {row.Currency}; modules={row.ModuleKeys ?? "all"}", ct);
        return Result<PlatformBillingDto>.Success(ToPlatformDto(row, serverUrl));
    }

    // ── Subscription page (the cafe's own admin) ──

    public async Task<CafeBillingDto> GetForCafeAsync(CancellationToken ct = default)
    {
        var row = await db.CafeBillings.AsNoTracking().FirstOrDefaultAsync(ct);
        return new CafeBillingDto(
            row?.MonthlyPrice,
            row?.Currency ?? "usd",
            CoveredModules(row),
            row?.Status,
            row?.CancelAtPeriodEnd ?? false,
            row?.PaidUntil,
            row?.LastPaidAt,
            CanSubscribe: gateway.IsConfigured && row?.MonthlyPrice is not null && !IsRunning(row),
            CanManage: gateway.IsConfigured && !string.IsNullOrEmpty(row?.StripeCustomerId),
            gateway.TestMode);
    }

    public async Task<Result<BillingRedirectDto>> StartCheckoutAsync(string? returnUrl, CancellationToken ct = default)
    {
        if (!gateway.IsConfigured)
            return Result<BillingRedirectDto>.Failure("Card payments for subscriptions are not set up on this server yet.", 503);
        var row = await db.CafeBillings.FirstOrDefaultAsync(ct);
        if (row?.MonthlyPrice is not { } price)
            return Result<BillingRedirectDto>.Failure("No monthly price is set for this cafe yet. Contact your Yurt account manager.", 409);
        if (IsRunning(row))
            return Result<BillingRedirectDto>.Failure("This cafe already has a subscription. Use \"Manage card and invoices\" to change it.", 409);
        if (ReturnBase(returnUrl) is not { } back)
            return Result<BillingRedirectDto>.Failure("Unknown return address.", 400);

        var cafe = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == tenant.TenantId, ct);
        try
        {
            var url = await gateway.CreateCheckoutAsync(new BillingCheckoutRequest(
                cafe.Id, cafe.Slug, cafe.Name, price, row.Currency, row.StripeCustomerId,
                SuccessUrl: back + (back.Contains('?') ? "&" : "?") + "billing=done",
                CancelUrl: back), ct);
            await audit.LogAsync("BillingCheckoutStarted", "CafeBilling", cafe.Id.ToString(), $"{price} {row.Currency}", ct);
            return Result<BillingRedirectDto>.Success(new BillingRedirectDto(url));
        }
        catch (BillingGatewayException ex)
        {
            return Result<BillingRedirectDto>.Failure(ex.Message, 502);
        }
    }

    public async Task<Result<BillingRedirectDto>> OpenPortalAsync(string? returnUrl, CancellationToken ct = default)
    {
        if (!gateway.IsConfigured)
            return Result<BillingRedirectDto>.Failure("Card payments for subscriptions are not set up on this server yet.", 503);
        var row = await db.CafeBillings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(row?.StripeCustomerId))
            return Result<BillingRedirectDto>.Failure("This cafe has not paid by card yet.", 409);
        if (ReturnBase(returnUrl) is not { } back)
            return Result<BillingRedirectDto>.Failure("Unknown return address.", 400);
        try
        {
            return Result<BillingRedirectDto>.Success(new BillingRedirectDto(await gateway.CreatePortalAsync(row.StripeCustomerId, back, ct)));
        }
        catch (BillingGatewayException ex)
        {
            return Result<BillingRedirectDto>.Failure(ex.Message, 502);
        }
    }

    // ── Stripe webhook (platform-wide address) ──

    public async Task<Result> HandleWebhookAsync(string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
    {
        if (!gateway.IsConfigured)
            return Result.Failure("Billing is not set up on this server.", 503);
        var parsed = gateway.ParseWebhook(rawBody, headers);
        if (!parsed.Succeeded) return Result.Failure(parsed.Error!, parsed.StatusCode);
        if (parsed.Data is not { } evt) return Result.Success();

        var row = await FindRowAsync(evt, ct);
        if (row is null)
        {
            // A paid invoice for a cafe we cannot place must not be lost: a failure makes Stripe send it again later,
            // by which time checkout.session.completed has usually told us the subscription id.
            if (evt.Kind == BillingEventKind.InvoicePaid)
            {
                logger.LogWarning("Stripe billing event {EventId} (invoice {InvoiceId}) matches no cafe yet", evt.EventId, evt.InvoiceId);
                return Result.Failure("No cafe has this subscription yet.", 409);
            }
            return Result.Success();
        }

        // From here on everything (module licenses, audit log) belongs to that cafe.
        var slug = await db.Tenants.AsNoTracking().Where(t => t.Id == row.TenantId).Select(t => t.Slug).FirstAsync(ct);
        tenant.Set(row.TenantId, slug);

        // News about an older subscription (say its final "deleted") must not overwrite the one now running.
        if (evt.Kind == BillingEventKind.SubscriptionChanged && IsRunning(row)
            && evt.SubscriptionId is not null && row.StripeSubscriptionId != evt.SubscriptionId)
            return Result.Success();

        if (evt.CustomerId is not null) row.StripeCustomerId = evt.CustomerId;
        if (evt.SubscriptionId is not null) row.StripeSubscriptionId = evt.SubscriptionId;

        switch (evt.Kind)
        {
            case BillingEventKind.CheckoutCompleted:
                row.Status ??= "incomplete";
                await db.SaveChangesAsync(ct);
                await audit.LogAsync("BillingSubscribed", "CafeBilling", row.TenantId.ToString(), evt.SubscriptionId, ct);
                break;

            case BillingEventKind.SubscriptionChanged:
                row.Status = evt.Status;
                row.CancelAtPeriodEnd = evt.CancelAtPeriodEnd;
                await db.SaveChangesAsync(ct);
                await audit.LogAsync("BillingSubscriptionChanged", "CafeBilling", row.TenantId.ToString(),
                    $"status={evt.Status}; cancelAtPeriodEnd={evt.CancelAtPeriodEnd}", ct);
                break;

            case BillingEventKind.InvoicePaid:
                await ApplyInvoicePaidAsync(row, evt, ct);
                break;

            case BillingEventKind.InvoicePaymentFailed:
                await audit.LogAsync("BillingPaymentFailed", "CafeBilling", row.TenantId.ToString(), evt.InvoiceId, ct);
                break;
        }
        return Result.Success();
    }

    private async Task ApplyInvoicePaidAsync(CafeBilling row, BillingEvent evt, CancellationToken ct)
    {
        if (row.Status is null or "incomplete") row.Status = "active";
        row.LastPaidAt = DateTime.UtcNow;
        row.LastPaidAmount = evt.Amount;
        row.LastInvoiceId = evt.InvoiceId;

        if (evt.PeriodEnd is { } end)
        {
            var paidUntil = DateTime.SpecifyKind(end, DateTimeKind.Utc);
            // A late or repeated webhook for an older month never moves the date back.
            if (row.PaidUntil is null || paidUntil > row.PaidUntil) row.PaidUntil = paidUntil;

            var licenses = await db.ModuleLicenses.ToListAsync(ct);
            foreach (var key in CoveredModules(row))
            {
                var license = licenses.FirstOrDefault(l => l.ModuleKey == key);
                if (license is null)
                {
                    license = new ModuleLicense { ModuleKey = key };
                    db.ModuleLicenses.Add(license);
                }
                license.Enabled = true;
                if (license.ValidUntil is null || license.ValidUntil < row.PaidUntil) license.ValidUntil = row.PaidUntil;
                license.Source = ModuleLicenseSource.Paid;
                license.Notes = $"Paid by card (Stripe invoice {evt.InvoiceId})";
            }
        }
        await db.SaveChangesAsync(ct);
        modules.Invalidate();

        await audit.LogAsync("BillingInvoicePaid", "CafeBilling", row.TenantId.ToString(),
            $"invoice={evt.InvoiceId}; amount={evt.Amount} {row.Currency}; paidUntil={row.PaidUntil:O}", ct);
    }

    /// <summary>The cafe of a webhook: by the cafe id Yurt put in the subscription metadata, else by the Stripe ids.
    /// Searches every cafe, because Stripe's request carries no cafe of its own.</summary>
    private async Task<CafeBilling?> FindRowAsync(BillingEvent evt, CancellationToken ct)
    {
        var all = db.CafeBillings.IgnoreQueryFilters();
        if (evt.TenantId is { } id && await all.FirstOrDefaultAsync(b => b.TenantId == id, ct) is { } byCafe)
            return byCafe;
        if (evt.SubscriptionId is { } sub && await all.FirstOrDefaultAsync(b => b.StripeSubscriptionId == sub, ct) is { } bySub)
            return bySub;
        if (evt.CustomerId is { } customer && await all.FirstOrDefaultAsync(b => b.StripeCustomerId == customer, ct) is { } byCustomer)
            return byCustomer;
        return null;
    }

    private static bool IsRunning(CafeBilling? row)
        => row is { StripeSubscriptionId: not null, Status: { } status } && RunningStatuses.Contains(status);

    private static List<string> CoveredModules(CafeBilling? row)
        => string.IsNullOrEmpty(row?.ModuleKeys)
            ? BillableModules
            : row.ModuleKeys.Split(',', StringSplitOptions.RemoveEmptyEntries).Where(BillableModules.Contains).ToList();

    /// <summary>Stripe may only send the owner back to an address of our own apps (the CORS list).</summary>
    private string? ReturnBase(string? requested)
    {
        if (!Uri.TryCreate(requested, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return null;
        var origin = uri.GetLeftPart(UriPartial.Authority);
        return returnOptions.AllowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase))
            ? uri.GetLeftPart(UriPartial.Path)
            : null;
    }

    private PlatformBillingDto ToPlatformDto(CafeBilling? row, string serverUrl) => new(
        row?.MonthlyPrice,
        row?.Currency ?? "usd",
        CoveredModules(row),
        BillableModules,
        Currencies.ToList(),
        row?.Status,
        row?.CancelAtPeriodEnd ?? false,
        row?.PaidUntil,
        row?.LastPaidAt,
        row?.LastPaidAmount,
        !string.IsNullOrEmpty(row?.StripeCustomerId),
        gateway.IsConfigured,
        gateway.TestMode,
        serverUrl.TrimEnd('/') + WebhookPath);
}

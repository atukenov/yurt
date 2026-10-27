using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Payments.DTOs;

namespace Yurt.Application.Features.Payments;

/// <summary>
/// Operator tool for a cafe's online payments: which provider takes them (Stripe, or off). The cafe's Stripe keys go
/// in its encrypted secrets (Stripe:SecretKey, Stripe:WebhookSecret); the webhook address shown here is pasted into
/// the cafe's Stripe dashboard.
/// </summary>
public class PaymentSettingsService(
    IApplicationDbContext db,
    ITenantContext tenant,
    ITenantDirectory directory,
    ITenantSecrets secrets,
    IAuditLogService audit)
{
    /// <summary>Path of the cafe's Stripe webhook, after the server address.</summary>
    public static string StripeWebhookPath(string slug) => $"/api/v1/payments/stripe/webhook/{slug}";

    public async Task<Result<PaymentSettingsDto>> GetAsync(string serverUrl, CancellationToken ct = default)
    {
        var cafe = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.TenantId, ct);
        return cafe is null
            ? Result<PaymentSettingsDto>.Failure("Cafe not found.", 404)
            : Result<PaymentSettingsDto>.Success(await ToDtoAsync(cafe, serverUrl, ct));
    }

    public async Task<Result<PaymentSettingsDto>> SaveAsync(SavePaymentSettingsDto dto, string serverUrl, CancellationToken ct = default)
    {
        var provider = string.IsNullOrWhiteSpace(dto.Provider) || dto.Provider.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)
            ? null
            : dto.Provider.Trim().ToLowerInvariant();
        if (provider is not null && !PaymentProviders.All.Contains(provider))
            return Result<PaymentSettingsDto>.Failure($"Provider must be one of: none, {string.Join(", ", PaymentProviders.All)}.", 400);

        var cafe = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.TenantId, ct);
        if (cafe is null) return Result<PaymentSettingsDto>.Failure("Cafe not found.", 404);

        cafe.PaymentProvider = provider;
        cafe.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        directory.Invalidate();

        await audit.LogAsync("PaymentSettingsSaved", "Tenant", cafe.Id.ToString(), $"provider {provider ?? "none"}", ct);
        return Result<PaymentSettingsDto>.Success(await ToDtoAsync(cafe, serverUrl, ct));
    }

    private async Task<PaymentSettingsDto> ToDtoAsync(Domain.Entities.Tenant cafe, string serverUrl, CancellationToken ct)
    {
        // Read through the secrets service (not only the list of stored keys), so the first cafe's server-wide keys count.
        var secretKey = await secrets.GetAsync(PaymentProviders.StripeSecretKey, ct);
        var webhookSecret = await secrets.GetAsync(PaymentProviders.StripeWebhookSecret, ct);
        return new PaymentSettingsDto(
            cafe.PaymentProvider,
            PaymentProviders.All.ToList(),
            !string.IsNullOrWhiteSpace(secretKey),
            !string.IsNullOrWhiteSpace(webhookSecret),
            secretKey is null || IsTestKey(secretKey),
            serverUrl.TrimEnd('/') + StripeWebhookPath(cafe.Slug));
    }

    /// <summary>Stripe test keys start with sk_test_ (or rk_test_ for a restricted key); they never move real money.</summary>
    public static bool IsTestKey(string key) => key.StartsWith("sk_test_", StringComparison.Ordinal) || key.StartsWith("rk_test_", StringComparison.Ordinal);
}

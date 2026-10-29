namespace Yurt.Application.Features.Billing;

/// <summary>The cafe's monthly plan and Stripe subscription as the platform owner sees it on the Platform screen.</summary>
public record PlatformBillingDto(
    decimal? MonthlyPrice,
    string Currency,
    List<string> Modules,
    List<string> AvailableModules,
    List<string> Currencies,
    string? Status,
    bool CancelAtPeriodEnd,
    DateTime? PaidUntil,
    DateTime? LastPaidAt,
    decimal? LastPaidAmount,
    bool StripeCustomer,
    bool KeysSet,
    bool TestMode,
    string WebhookUrl);

/// <summary>Plan of the cafe. A null price removes the plan (the cafe can no longer start paying).
/// Null or empty modules means every module except core.</summary>
public record SaveBillingPlanDto(decimal? MonthlyPrice, string? Currency, List<string>? Modules);

/// <summary>What the cafe owner sees on the Subscription page.</summary>
public record CafeBillingDto(
    decimal? MonthlyPrice,
    string Currency,
    List<string> Modules,
    string? Status,
    bool CancelAtPeriodEnd,
    DateTime? PaidUntil,
    DateTime? LastPaidAt,
    bool CanSubscribe,
    bool CanManage,
    bool TestMode);

/// <summary>Where the admin app wants Stripe to send the owner back to (its Subscription page).</summary>
public record BillingRedirectRequest(string? ReturnUrl);

public record BillingRedirectDto(string Url);

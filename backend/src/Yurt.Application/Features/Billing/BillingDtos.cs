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

/// <summary>One module on the Platform screen: switched on for the cafe, and its monthly price (null = not priced).</summary>
public record ModulePlanItemDto(string Key, bool On, decimal? Price);

/// <summary>The cafe's modules and what they cost per month. <see cref="Total"/> is core plus every switched-on
/// module; <see cref="MonthlyPrice"/> is what the cafe is charged (the total, or a price typed by hand).</summary>
public record ModulePlanDto(
    List<ModulePlanItemDto> Modules,
    string Currency,
    List<string> Currencies,
    decimal Total,
    decimal? MonthlyPrice,
    bool PriceByHand);

/// <summary>Modules not listed keep their state and price. Without <see cref="PriceByHand"/> the monthly price is the total.</summary>
public record SaveModulePlanDto(List<ModulePlanItemDto>? Modules, string? Currency, bool PriceByHand, decimal? MonthlyPrice);

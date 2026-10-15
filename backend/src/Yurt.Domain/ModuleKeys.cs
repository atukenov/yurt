namespace Yurt.Domain;

/// <summary>Keys of the sellable platform modules (see docs/MODULARIZATION_TODO.md, Phase 3).</summary>
public static class ModuleKeys
{
    public const string Core = "core";
    public const string MultiLocation = "multi_location";
    public const string Marketing = "marketing";
    public const string Loyalty = "loyalty";
    public const string IikoSync = "iiko_sync";
    public const string OwnPos = "own_pos";
    public const string Push = "push";
    public const string Analytics = "analytics";
    public const string GroupOrders = "group_orders";
    public const string Engagement = "engagement";
    public const string StaffAudit = "staff_audit";
    public const string OnlinePayments = "online_payments";
    public const string Verification = "verification";
    public const string Branding = "branding";

    /// <summary>Every known module, in display order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Core, MultiLocation, Marketing, Loyalty, IikoSync, OwnPos, Push, Analytics,
        GroupOrders, Engagement, StaffAudit, OnlinePayments, Verification, Branding
    ];

    public static bool IsKnown(string key) => All.Contains(key);
}

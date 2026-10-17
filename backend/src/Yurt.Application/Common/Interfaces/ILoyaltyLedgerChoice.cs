namespace Yurt.Application.Common.Interfaces;

/// <summary>Which bonus wallet the current cafe uses (Phase 6): the iiko one, or the one in our own database.</summary>
public interface ILoyaltyLedgerChoice
{
    /// <summary>True when the current cafe's wallet is the native one, so loyalty works without iiko.</summary>
    bool UsesNativeLedger { get; }
}

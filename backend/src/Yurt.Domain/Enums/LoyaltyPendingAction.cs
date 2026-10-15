namespace Yurt.Domain.Enums;

/// <summary>
/// Outstanding iiko wallet operation for an order, retried in the background
/// when iiko was unavailable at the moment the order flow needed it.
/// </summary>
public enum LoyaltyPendingAction
{
    None = 0,

    /// <summary>Completion ran but the hold could not be released yet — rerun the full finalize (release + chargeoff).</summary>
    FinalizeSpend = 1,

    /// <summary>Hold was released but the chargeoff failed — points still need to be debited.</summary>
    ChargeOff = 2,

    /// <summary>Order was declined/cancelled but the hold could not be released — points are stranded until released.</summary>
    Release = 3,

    /// <summary>Order completed but the earn-credit topup failed — retry the credit.</summary>
    Credit = 4
}

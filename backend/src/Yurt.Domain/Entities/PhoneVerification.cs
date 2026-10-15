using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

/// <summary>
/// A pending phone-number verification, for either customer registration or an existing
/// customer changing their number (see <see cref="Purpose"/>). Holds the one-time code
/// (hashed); registration additionally carries the pending signup payload, so the
/// CustomerUser is only created once the code is confirmed. One row per
/// (MobileNumber, Purpose) pair — a new request overwrites the previous one for that purpose.
/// </summary>
public class PhoneVerification : BaseEntity
{
    public string MobileNumber { get; set; } = string.Empty;
    public VerificationPurpose Purpose { get; set; } = VerificationPurpose.Registration;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime LastSentAt { get; set; }
    public int Attempts { get; set; }

    // Pending registration payload — applied when the code is confirmed. Unused for Purpose = PhoneChange.
    public string PinHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    /// <summary>Which existing customer requested the change. Null for Purpose = Registration,
    /// where no CustomerUser exists yet.</summary>
    public Guid? CustomerUserId { get; set; }
}

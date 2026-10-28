using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

public class Payment : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

  public Guid OrderId { get; set; }
  public Order Order { get; set; } = null!;
  public PaymentProvider Provider { get; set; }
  public string InvoiceId { get; set; } = null!;
  public decimal Amount { get; set; }
  public Currency Currency { get; set; } = Currency.KZT;
  public PaymentRecordStatus Status { get; set; } = PaymentRecordStatus.Pending;
  public string PaymentUrl { get; set; } = null!;
  public string QrCode { get; set; } = null!;
  public string? RawResponse { get; set; }
  public DateTime? ExpiresAt { get; set; }

  /// <summary>The provider's id of the money movement (Stripe payment intent, pi_...), known once paid. Refunds use it.</summary>
  public string? ProviderPaymentId { get; set; }
}

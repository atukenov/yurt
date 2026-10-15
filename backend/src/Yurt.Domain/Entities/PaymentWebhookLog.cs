using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

public class PaymentWebhookLog : BaseEntity
{
  public PaymentProvider Provider { get; set; }
  public string Payload { get; set; } = null!;
  public string Headers { get; set; } = null!;
  public bool Processed { get; set; }
}

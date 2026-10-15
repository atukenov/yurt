namespace Yurt.Infrastructure.Payments;

public class PaymentOptions
{
  public string WebhookCallbackUrl { get; set; } = string.Empty;
  public string WebhookSecret { get; set; } = string.Empty;
  public string SandboxSecret { get; set; } = string.Empty;
  public string SandboxBaseUrl { get; set; } = "https://kaspi.sandbox.example";
  public int ProcessingDelaySecondsMin { get; set; } = 2;
  public int ProcessingDelaySecondsMax { get; set; } = 10;
  public double DuplicateWebhookChance { get; set; } = 0.1;
  public double NetworkFailureChance { get; set; } = 0.1;
}

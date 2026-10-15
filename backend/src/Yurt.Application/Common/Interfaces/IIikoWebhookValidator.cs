namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// Validates the shared auth token iiko sends back on webhook calls (registered via
/// IIikoApiClient.RegisterWebhookAsync) — no HMAC/signature scheme, just a token match.
/// </summary>
public interface IIikoWebhookValidator
{
    bool Validate(IReadOnlyDictionary<string, string> headers);
}

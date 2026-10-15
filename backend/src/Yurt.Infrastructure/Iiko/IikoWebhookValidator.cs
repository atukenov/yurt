using System.Security.Cryptography;
using System.Text;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;

namespace Yurt.Infrastructure.Iiko;

/// <summary>
/// iiko webhooks carry the registered authToken back in the "Authorization" header
/// (plain value, not "Bearer "-prefixed) rather than an HMAC signature — this is not
/// precisely documented in the public spec, so double-check against real traffic on
/// first webhook registration; a mismatch here just means webhooks are rejected (401),
/// logged by the controller, never silently accepted.
/// </summary>
public class IikoWebhookValidator : IIikoWebhookValidator
{
    private readonly IikoOptions _options;

    public IikoWebhookValidator(IikoOptions options) => _options = options;

    public bool Validate(IReadOnlyDictionary<string, string> headers)
    {
        if (string.IsNullOrEmpty(_options.WebhookAuthToken)) return false;
        if (!headers.TryGetValue("Authorization", out var received) || string.IsNullOrEmpty(received))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(_options.WebhookAuthToken);
        var receivedBytes = Encoding.UTF8.GetBytes(received);
        return expectedBytes.Length == receivedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes);
    }
}

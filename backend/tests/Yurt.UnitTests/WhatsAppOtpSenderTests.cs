using System.Net;
using Yurt.Application.Features.Auth;
using Yurt.Infrastructure.Messaging;

namespace Yurt.UnitTests;

public class WhatsAppOtpSenderTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static async Task<string> SendAsync(string? brand)
    {
        var handler = new CaptureHandler();
        var sender = new WhatsAppOtpSender(new HttpClient(handler), new OtpOptions { IdInstance = "1", ApiTokenInstance = "t" });
        await sender.SendAsync("+77001234567", "1234", brand);
        return handler.Body!;
    }

    [Fact]
    public async Task Message_uses_the_brand_name()
        => Assert.Contains("Your Brew House verification code: 1234", await SendAsync("Brew House"));

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Message_falls_back_to_Yurt_without_a_brand(string? brand)
        => Assert.Contains("Your Yurt verification code: 1234", await SendAsync(brand));
}

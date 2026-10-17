using System.Net;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
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

    private static ITenantSecrets Secrets(string? id, string? token)
    {
        var secrets = Substitute.For<ITenantSecrets>();
        secrets.GetAsync("Otp:IdInstance", Arg.Any<CancellationToken>()).Returns(id);
        secrets.GetAsync("Otp:ApiTokenInstance", Arg.Any<CancellationToken>()).Returns(token);
        return secrets;
    }

    private static async Task<string> SendAsync(string? brand)
    {
        var handler = new CaptureHandler();
        var sender = new WhatsAppOtpSender(new HttpClient(handler), new OtpOptions(), Secrets("1", "t"));
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

    [Theory]
    [InlineData(null, "t")]
    [InlineData("1", null)]
    [InlineData("", "")]
    public async Task CafeWithoutItsOwnWhatsAppCredentials_CannotSend(string? id, string? token)
    {
        var handler = new CaptureHandler();
        var sender = new WhatsAppOtpSender(new HttpClient(handler), new OtpOptions(), Secrets(id, token));

        await Assert.ThrowsAsync<OtpSendException>(() => sender.SendAsync("+77001234567", "1234"));
        Assert.Null(handler.Body);
    }
}

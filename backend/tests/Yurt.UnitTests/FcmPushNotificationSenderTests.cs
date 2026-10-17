using Microsoft.Extensions.Logging;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Push;
using Yurt.Infrastructure.Push;

namespace Yurt.UnitTests;

public class FcmPushNotificationSenderTests
{
    private static (FcmPushNotificationSender sender, ITenantSecrets secrets) Build(bool enabled, string? json)
    {
        var secrets = Substitute.For<ITenantSecrets>();
        secrets.GetAsync(FcmPushNotificationSender.SecretKey, Arg.Any<CancellationToken>()).Returns(json);
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(Guid.NewGuid());
        var sender = new FcmPushNotificationSender(
            new FirebaseOptions { Enabled = enabled }, Substitute.For<ILogger<FcmPushNotificationSender>>(),
            secrets, tenant, new FirebaseAppCache());
        return (sender, secrets);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, "  ")]
    public async Task CafeWithoutItsOwnServiceAccount_SendsNothing(bool enabled, string? json)
    {
        var (sender, _) = Build(enabled, json);
        var result = await sender.SendAsync(["token"], "t", "b", null);
        Assert.Empty(result.DeadTokens);
    }

    [Fact]
    public async Task Disabled_NeverReadsTheCafesSecret()
    {
        var (sender, secrets) = Build(enabled: false, json: "{}");
        await sender.SendAsync(["token"], "t", "b", null);
        await secrets.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnusableServiceAccountJson_DegradesToNoOp()
    {
        var (sender, _) = Build(enabled: true, json: "not json");
        var result = await sender.SendAsync(["token"], "t", "b", null);
        Assert.Empty(result.DeadTokens);
    }
}

using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Push;
using Yurt.Application.Features.Push.Services;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

public class PushNotificationServiceTests
{
    private static (PushNotificationService svc, IApplicationDbContext db, IPushNotificationSender sender) Build(
        IEnumerable<CustomerDeviceToken>? tokens = null, bool enabled = true, bool moduleOn = true)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var set = (tokens ?? []).ToList().BuildMockDbSet();
        db.CustomerDeviceTokens.Returns(set);

        var sender = Substitute.For<IPushNotificationSender>();
        var options = new FirebaseOptions { Enabled = enabled };
        var logger = Substitute.For<ILogger<PushNotificationService>>();

        var modules = Substitute.For<IModuleGate>();
        modules.IsEnabledAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(moduleOn);

        return (new PushNotificationService(db, sender, options, logger, modules), db, sender);
    }

    [Fact]
    public async Task SendOrderStatusPushAsync_Disabled_NeverCallsSender()
    {
        var token = new CustomerDeviceToken
        {
            CustomerUserId = Guid.NewGuid(),
            Token = "t1",
            Platform = DevicePlatform.Ios,
        };
        var (svc, _, sender) = Build([token], enabled: false);

        await svc.SendOrderStatusPushAsync(token.CustomerUserId, "title", "body");

        await sender.DidNotReceive().SendAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendOrderStatusPushAsync_PushModuleOff_NeverCallsSender()
    {
        var token = new CustomerDeviceToken
        {
            CustomerUserId = Guid.NewGuid(),
            Token = "t1",
            Platform = DevicePlatform.Android,
        };
        var (svc, _, sender) = Build([token], enabled: true, moduleOn: false);

        await svc.SendOrderStatusPushAsync(token.CustomerUserId, "title", "body");

        await sender.DidNotReceive().SendAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendOrderStatusPushAsync_DeadTokenReported_RemovesThatToken()
    {
        var customerId = Guid.NewGuid();
        var deadToken = new CustomerDeviceToken
        {
            CustomerUserId = customerId,
            Token = "dead-token",
            Platform = DevicePlatform.Ios,
        };
        var aliveToken = new CustomerDeviceToken
        {
            CustomerUserId = customerId,
            Token = "alive-token",
            Platform = DevicePlatform.Android,
        };
        var (svc, db, sender) = Build([deadToken, aliveToken]);
        sender.SendAsync(
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new PushSendResult(["dead-token"]));

        await svc.SendOrderStatusPushAsync(customerId, "title", "body");

        db.CustomerDeviceTokens.Received(1).RemoveRange(
            Arg.Is<IEnumerable<CustomerDeviceToken>>(ts => ts.Count() == 1 && ts.Single().Token == "dead-token"));
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendOrderStatusPushAsync_NoDeadTokens_NeverSaves()
    {
        var customerId = Guid.NewGuid();
        var token = new CustomerDeviceToken { CustomerUserId = customerId, Token = "t1", Platform = DevicePlatform.Ios };
        var (svc, db, sender) = Build([token]);
        sender.SendAsync(
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(new PushSendResult([]));

        await svc.SendOrderStatusPushAsync(customerId, "title", "body");

        db.CustomerDeviceTokens.DidNotReceive().RemoveRange(Arg.Any<IEnumerable<CustomerDeviceToken>>());
        await db.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendOrderStatusPushAsync_SenderThrows_DoesNotPropagate()
    {
        var customerId = Guid.NewGuid();
        var token = new CustomerDeviceToken { CustomerUserId = customerId, Token = "t1", Platform = DevicePlatform.Ios };
        var (svc, _, sender) = Build([token]);
        sender.SendAsync(
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PushSendResult>(new InvalidOperationException("boom")));

        await svc.SendOrderStatusPushAsync(customerId, "title", "body");
        // Reaching this line without an exception is the assertion.
    }

    [Fact]
    public async Task SendOrderStatusPushAsync_NoTokensForCustomer_NeverCallsSender()
    {
        var (svc, _, sender) = Build();

        await svc.SendOrderStatusPushAsync(Guid.NewGuid(), "title", "body");

        await sender.DidNotReceive().SendAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<CancellationToken>());
    }
}

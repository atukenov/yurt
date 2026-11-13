using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Approvals;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

public class ManagerApprovalServiceTests
{
    private static (ManagerApprovalService svc, IAuditLogService audit) Build(bool gateOn, params AdminUser[] users)
    {
        ManagerApprovalService.ResetFailures();
        var db = Substitute.For<IApplicationDbContext>();
        var set = users.ToList().BuildMockDbSet();
        db.AdminUsers.Returns(set);
        var hasher = Substitute.For<IPasswordHasher>();
        hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(ci => $"h:{ci.ArgAt<string>(0)}" == ci.ArgAt<string>(1));
        var audit = Substitute.For<IAuditLogService>();
        var modules = Substitute.For<IModuleGate>();
        modules.IsEnabledAsync(ModuleKeys.OwnPos, Arg.Any<CancellationToken>()).Returns(gateOn);
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Guid.NewGuid());
        return (new ManagerApprovalService(db, hasher, audit, modules, user), audit);
    }

    private static AdminUser Manager(string name, string pin, AdminRole role = AdminRole.Admin)
        => new() { Username = name, Role = role, IsActive = true, ManagerPinHash = $"h:{pin}" };

    [Fact]
    public async Task WithoutOwnPos_TheGateIsOff_AndNothingIsAsked()
    {
        var (svc, _) = Build(gateOn: false);
        var result = await svc.RequireAsync("Refund", null);
        Assert.True(result.Succeeded);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task RightPin_ReturnsTheManagerWhoApproved_AndTheReason()
    {
        var (svc, _) = Build(true, Manager("amina", "1234"), Manager("boss", "9876", AdminRole.Ceo));
        var result = await svc.RequireAsync("Refund", new ManagerApprovalDto("9876", "  Guest left "));
        Assert.True(result.Succeeded);
        Assert.Equal("boss", result.Data!.Username);
        Assert.Equal("Guest left · approved by boss", result.Data.Describe());
    }

    [Fact]
    public async Task MissingReason_MissingPin_WrongPin_AreRefused_AndAWrongPinIsAudited()
    {
        var (svc, audit) = Build(true, Manager("amina", "1234"));
        Assert.Equal(400, (await svc.RequireAsync("Refund", new ManagerApprovalDto("1234", " "))).StatusCode);
        Assert.Equal(422, (await svc.RequireAsync("Refund", new ManagerApprovalDto(null, "why"))).StatusCode);
        Assert.Equal(422, (await svc.RequireAsync("Refund", new ManagerApprovalDto("0000", "why"))).StatusCode);
        await audit.Received(1).LogAsync("ManagerPinRejected", "Approval", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WorkersAndSwitchedOffManagers_CannotApprove()
    {
        var worker = Manager("cashier", "1111", AdminRole.Worker);
        var gone = Manager("former", "2222"); gone.IsActive = false;
        var (svc, _) = Build(true, worker, gone, Manager("amina", "1234"));
        Assert.Equal(422, (await svc.RequireAsync("Refund", new ManagerApprovalDto("1111", "why"))).StatusCode);
        Assert.Equal(422, (await svc.RequireAsync("Refund", new ManagerApprovalDto("2222", "why"))).StatusCode);
    }

    [Fact]
    public async Task NoManagerWithAPin_SaysSo()
    {
        var (svc, _) = Build(true, new AdminUser { Username = "amina", Role = AdminRole.Admin, IsActive = true });
        var result = await svc.RequireAsync("Refund", new ManagerApprovalDto("1234", "why"));
        Assert.False(result.Succeeded);
        Assert.Contains("No manager has set a PIN", result.Error);
    }

    [Fact]
    public async Task FiveWrongPins_LockTheStaffMember_EvenForTheRightPin()
    {
        var (svc, _) = Build(true, Manager("amina", "1234"));
        for (var i = 0; i < ManagerApprovalService.MaxFailures; i++)
            Assert.Equal(422, (await svc.RequireAsync("Refund", new ManagerApprovalDto("0000", "why"))).StatusCode);
        Assert.Equal(429, (await svc.RequireAsync("Refund", new ManagerApprovalDto("1234", "why"))).StatusCode);
    }
}

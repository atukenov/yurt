using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Modules;
using Yurt.Application.Features.Platform;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

public class PlatformServiceTests
{
    private static (PlatformService svc, IApplicationDbContext db, IAuditLogService audit, IModuleGate gate, List<ModuleLicense> rows) Build(
        IEnumerable<ModuleLicense>? licenses = null)
    {
        var rows = (licenses ?? []).ToList();
        var db = Substitute.For<IApplicationDbContext>();
        var set = rows.BuildMockDbSet();
        set.When(x => x.Add(Arg.Any<ModuleLicense>())).Do(c => rows.Add(c.Arg<ModuleLicense>()));
        db.ModuleLicenses.Returns(set);
        var settings = new List<PlatformSetting>().BuildMockDbSet();
        db.PlatformSettings.Returns(settings);

        var audit = Substitute.For<IAuditLogService>();
        var gate = Substitute.For<IModuleGate>();
        gate.GetStatusesAsync(Arg.Any<CancellationToken>()).Returns(callInfo =>
            (IReadOnlyList<ModuleStatus>)ModuleKeys.All
                .Select(k => ModuleEvaluator.Evaluate(k, rows.FirstOrDefault(r => r.ModuleKey == k), DateTime.UtcNow, 14))
                .ToList());
        var maintenance = Substitute.For<IMaintenanceGate>();
        return (new PlatformService(db, audit, gate, maintenance), db, audit, gate, rows);
    }

    [Fact]
    public async Task UpdateModule_UnknownKey_Is404()
    {
        var (svc, _, _, _, _) = Build();
        var r = await svc.UpdateModuleAsync("nope", new(true, null, ModuleLicenseSource.Manual, null));
        Assert.False(r.Succeeded);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task UpdateModule_CoreCannotBeDisabled()
    {
        var (svc, _, audit, _, _) = Build();
        var r = await svc.UpdateModuleAsync(ModuleKeys.Core, new(false, null, ModuleLicenseSource.Manual, null));
        Assert.False(r.Succeeded);
        Assert.Equal(400, r.StatusCode);
        await audit.DidNotReceiveWithAnyArgs().LogAsync(default!, default!);
    }

    [Fact]
    public async Task UpdateModule_ExistingRow_IsChanged_AuditedAndGateInvalidated()
    {
        var existing = new ModuleLicense { ModuleKey = ModuleKeys.Push, Enabled = true };
        var (svc, db, audit, gate, _) = Build([existing]);

        var r = await svc.UpdateModuleAsync(ModuleKeys.Push,
            new(false, null, ModuleLicenseSource.Paid, "  invoice 42  "));

        Assert.True(r.Succeeded);
        Assert.False(existing.Enabled);
        Assert.Equal(ModuleLicenseSource.Paid, existing.Source);
        Assert.Equal("invoice 42", existing.Notes);
        await db.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
        await audit.Received(1).LogAsync("ModuleLicenseUpdated", "ModuleLicense", ModuleKeys.Push, Arg.Any<string?>(), Arg.Any<CancellationToken>());
        gate.Received().Invalidate();
    }

    [Fact]
    public async Task UpdateModule_ExpiredPastGrace_ReportsExpiredAndNotEnabled()
    {
        var (svc, _, _, _, _) = Build();
        var r = await svc.UpdateModuleAsync(ModuleKeys.Analytics,
            new(true, DateTime.UtcNow.AddDays(-30), ModuleLicenseSource.Trial, null));
        Assert.True(r.Succeeded);
        Assert.Equal("expired", r.Data!.State);
        Assert.False(r.Data.Enabled);
    }

    [Fact]
    public void Dependencies_LoyaltyWithoutAnyPos_Warns()
    {
        var enabled = new HashSet<string> { ModuleKeys.Core, ModuleKeys.Loyalty };
        Assert.True(ModuleDependencies.GetWarnings(enabled).ContainsKey(ModuleKeys.Loyalty));
    }

    [Theory]
    [InlineData(ModuleKeys.IikoSync)]
    [InlineData(ModuleKeys.OwnPos)]
    public void Dependencies_LoyaltyWithEitherPos_NoWarning(string pos)
    {
        var enabled = new HashSet<string> { ModuleKeys.Core, ModuleKeys.Loyalty, pos };
        Assert.Empty(ModuleDependencies.GetWarnings(enabled));
    }

    [Fact]
    public async Task Maintenance_MessageTooLong_Rejected()
    {
        var (svc, _, _, _, _) = Build();
        var r = await svc.SetMaintenanceAsync(new MaintenanceDto(true, new string('x', PlatformService.MaxMessageLength + 1)));
        Assert.False(r.Succeeded);
    }
}

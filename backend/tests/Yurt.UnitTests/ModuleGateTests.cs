using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Modules;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Services;

namespace Yurt.UnitTests;

public class ModuleGateTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private static ModuleLicense License(string key, bool enabled = true, DateTime? validUntil = null)
        => new() { ModuleKey = key, Enabled = enabled, ValidUntil = validUntil };

    [Fact]
    public void Evaluate_MissingRow_IsActive()
        => Assert.True(ModuleEvaluator.Evaluate(ModuleKeys.Loyalty, null, Now, 14).IsEnabled);

    [Fact]
    public void Evaluate_NoExpiry_IsActive()
        => Assert.Equal(ModuleState.Active, ModuleEvaluator.Evaluate(ModuleKeys.Loyalty, License(ModuleKeys.Loyalty), Now, 14).State);

    [Fact]
    public void Evaluate_Disabled_IsNotEnabled()
    {
        var s = ModuleEvaluator.Evaluate(ModuleKeys.Loyalty, License(ModuleKeys.Loyalty, enabled: false), Now, 14);
        Assert.Equal(ModuleState.Disabled, s.State);
        Assert.False(s.IsEnabled);
    }

    [Fact]
    public void Evaluate_CoreCannotBeDisabled()
        => Assert.True(ModuleEvaluator.Evaluate(ModuleKeys.Core, License(ModuleKeys.Core, enabled: false), Now, 14).IsEnabled);

    [Fact]
    public void Evaluate_FutureExpiry_IsActive()
        => Assert.Equal(ModuleState.Active,
            ModuleEvaluator.Evaluate(ModuleKeys.Push, License(ModuleKeys.Push, validUntil: Now.AddDays(5)), Now, 14).State);

    [Fact]
    public void Evaluate_ExpiredInsideGrace_StillEnabledWithGraceEnd()
    {
        var s = ModuleEvaluator.Evaluate(ModuleKeys.Push, License(ModuleKeys.Push, validUntil: Now.AddDays(-3)), Now, 14);
        Assert.Equal(ModuleState.Grace, s.State);
        Assert.True(s.IsEnabled);
        Assert.Equal(Now.AddDays(-3).AddDays(14), s.GraceEndsAt);
    }

    [Fact]
    public void Evaluate_ExpiredPastGrace_IsNotEnabled()
    {
        var s = ModuleEvaluator.Evaluate(ModuleKeys.Push, License(ModuleKeys.Push, validUntil: Now.AddDays(-15)), Now, 14);
        Assert.Equal(ModuleState.Expired, s.State);
        Assert.False(s.IsEnabled);
    }

    private static (ModuleGate gate, IApplicationDbContext db, Func<DateTime> setClock) Build(
        IEnumerable<ModuleLicense> rows, int cacheSeconds = 60)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var set = rows.ToList().BuildMockDbSet();
        db.ModuleLicenses.Returns(set);

        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IApplicationDbContext)).Returns(db);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(provider);
        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(scope);

        var now = Now;
        var gate = new ModuleGate(factory, new ModuleOptions { CacheSeconds = cacheSeconds },
            Substitute.For<ILogger<ModuleGate>>(), () => now);
        return (gate, db, () => now);
    }

    [Fact]
    public async Task EmptyTable_EverythingEnabled()
    {
        var (gate, _, _) = Build([]);
        foreach (var key in ModuleKeys.All)
            Assert.True(await gate.IsEnabledAsync(key), key);
    }

    [Fact]
    public async Task DisabledRow_DisablesOnlyThatModule()
    {
        var (gate, _, _) = Build([License(ModuleKeys.Marketing, enabled: false)]);
        Assert.False(await gate.IsEnabledAsync(ModuleKeys.Marketing));
        Assert.True(await gate.IsEnabledAsync(ModuleKeys.Loyalty));
    }

    [Fact]
    public async Task UnknownKey_IsEnabled()
    {
        var (gate, _, _) = Build([]);
        Assert.True(await gate.IsEnabledAsync("does_not_exist"));
    }

    [Fact]
    public async Task LookupFailure_FailsOpen()
    {
        var factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Throws(new InvalidOperationException("db down"));
        var gate = new ModuleGate(factory, new ModuleOptions(), Substitute.For<ILogger<ModuleGate>>());

        Assert.True(await gate.IsEnabledAsync(ModuleKeys.Analytics));
        Assert.All(await gate.GetStatusesAsync(), s => Assert.True(s.IsEnabled));
    }

    [Fact]
    public async Task Result_IsCachedUntilInvalidated()
    {
        var (gate, db, _) = Build([License(ModuleKeys.Push)]);
        await gate.IsEnabledAsync(ModuleKeys.Push);
        await gate.IsEnabledAsync(ModuleKeys.Push);
        _ = db.Received(1).ModuleLicenses;

        gate.Invalidate();
        await gate.IsEnabledAsync(ModuleKeys.Push);
        _ = db.Received(2).ModuleLicenses;
    }
}

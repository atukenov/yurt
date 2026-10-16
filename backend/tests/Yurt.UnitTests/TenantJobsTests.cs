using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Services;

namespace Yurt.UnitTests;

public class TenantJobsTests
{
    private static IServiceScopeFactory Scopes(params Tenant[] tenants)
    {
        var directory = Substitute.For<ITenantDirectory>();
        directory.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(tenants);
        var services = new ServiceCollection();
        services.AddSingleton(directory);
        services.AddScoped<ITenantContext, TenantContext>();
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static Tenant T(string slug) => new() { Slug = slug, Name = slug };

    [Fact]
    public async Task RunsOncePerTenant_WithThatTenantInScope()
    {
        var a = T("a"); var b = T("b");
        var seen = new List<(Guid Id, string Slug)>();

        await TenantJobs.ForEachTenantAsync(Scopes(a, b), NullLogger.Instance, (sp, _) =>
        {
            var ctx = sp.GetRequiredService<ITenantContext>();
            seen.Add((ctx.TenantId, ctx.Slug));
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal([(a.Id, "a"), (b.Id, "b")], seen);
    }

    [Fact]
    public async Task OneFailingTenant_DoesNotStopTheOthers()
    {
        var a = T("a"); var b = T("b");
        var ran = new List<string>();

        await TenantJobs.ForEachTenantAsync(Scopes(a, b), NullLogger.Instance, (sp, _) =>
        {
            var slug = sp.GetRequiredService<ITenantContext>().Slug;
            ran.Add(slug);
            if (slug == "a") throw new InvalidOperationException("boom");
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(["a", "b"], ran);
    }

    [Fact]
    public async Task NoTenants_RunsForTheDefaultTenant()
    {
        Guid? id = null;

        await TenantJobs.ForEachTenantAsync(Scopes(), NullLogger.Instance, (sp, _) =>
        {
            id = sp.GetRequiredService<ITenantContext>().TenantId;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(Tenant.DefaultId, id);
    }

    [Fact]
    public async Task Cancellation_StopsTheLoop()
    {
        using var cts = new CancellationTokenSource();
        var ran = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            TenantJobs.ForEachTenantAsync(Scopes(T("a"), T("b")), NullLogger.Instance, (_, _) =>
            {
                ran++;
                cts.Cancel();
                return Task.CompletedTask;
            }, cts.Token));

        Assert.Equal(1, ran);
    }
}

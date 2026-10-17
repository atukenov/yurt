using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Loyalty;
using Yurt.Application.Features.Platform;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Moving a cafe's bonus balances from iiko to the native wallet, and checking they match (Phase 6).</summary>
[Collection("Integration")]
public class LoyaltyImportTests(YurtWebAppFactory factory)
{
    private static string NewPhone() => "+7701" + Random.Shared.Next(1_000_000, 9_999_999);

    private static LoyaltyImportService ServiceFor(IServiceScope scope, FakeIikoApiClient fake, bool enabled = true)
    {
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(Tenant.DefaultId, "default");
        return new LoyaltyImportService(
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            fake,
            new IikoOptions { Enabled = enabled },
            scope.ServiceProvider.GetRequiredService<ITenantContext>(),
            scope.ServiceProvider.GetRequiredService<IAuditLogService>(),
            NullLogger<LoyaltyImportService>.Instance);
    }

    private static async Task LinkCustomerAsync(IServiceScope scope, string phone)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.CustomerUsers.Add(new CustomerUser
        {
            MobileNumber = phone, PinHash = "x", FirstName = "Aida", LastName = "K",
            IikoCustomerId = FakeIikoApiClient.CustomerId, IikoWalletId = FakeIikoApiClient.WalletId,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<decimal> NativeBalanceAsync(IServiceScope scope, string phone)
        => await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoyaltyEntries
            .Where(e => e.Account.Phone == phone).SumAsync(e => (decimal?)e.Amount) ?? 0m;

    [Fact]
    public async Task Import_CopiesBalances_AndIsSafeToRepeat()
    {
        var fake = new FakeIikoApiClient();
        var rich = NewPhone();
        var empty = NewPhone();
        fake.SetBalance(rich, 120m);
        fake.SetBalance(empty, 0m);

        using var scope = factory.Services.CreateScope();
        await LinkCustomerAsync(scope, rich);
        await LinkCustomerAsync(scope, empty);
        var service = ServiceFor(scope, fake);

        var first = (await service.ImportAsync()).Data!;
        Assert.True(first.Imported >= 2);
        Assert.Equal(120m, await NativeBalanceAsync(scope, rich));
        Assert.Equal(0m, await NativeBalanceAsync(scope, empty));

        var second = (await service.ImportAsync()).Data!;
        Assert.Equal(0, second.Imported);
        Assert.Equal(120m, await NativeBalanceAsync(scope, rich)); // not doubled
    }

    [Fact]
    public async Task Compare_ReportsOnlyTheCustomersThatDiffer()
    {
        var fake = new FakeIikoApiClient();
        var same = NewPhone();
        var drifted = NewPhone();
        fake.SetBalance(same, 40m);
        fake.SetBalance(drifted, 90m);

        using var scope = factory.Services.CreateScope();
        await LinkCustomerAsync(scope, same);
        await LinkCustomerAsync(scope, drifted);
        var service = ServiceFor(scope, fake);
        await service.ImportAsync();

        fake.SetBalance(drifted, 100m); // earned 10 more in iiko after the import

        var report = (await service.CompareAsync()).Data!;
        Assert.Contains(report.Mismatches, m => m.Phone == drifted && m.Iiko == 100m && m.Native == 90m);
        Assert.DoesNotContain(report.Mismatches, m => m.Phone == same);
    }

    [Fact]
    public async Task WithoutIiko_BothActionsAreRefused()
    {
        using var scope = factory.Services.CreateScope();
        var service = ServiceFor(scope, new FakeIikoApiClient(), enabled: false);

        Assert.Equal(400, (await service.ImportAsync()).StatusCode);
        Assert.Equal(400, (await service.CompareAsync()).StatusCode);
    }
}

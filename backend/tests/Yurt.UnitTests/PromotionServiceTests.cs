using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Promotions.DTOs;
using Yurt.Application.Features.Promotions.Services;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

public class PromotionServiceTests
{
    private static (PromotionService svc, IApplicationDbContext db) Build(IEnumerable<Promotion>? promotions = null)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var audit = Substitute.For<IAuditLogService>();

        var set = (promotions ?? []).ToList().BuildMockDbSet();
        db.Promotions.Returns(set);

        return (new PromotionService(db, audit), db);
    }

    [Fact]
    public async Task GetActivePromotionsAsync_ExcludesInactive()
    {
        var active = new Promotion { Title = "Active", IsActive = true };
        var inactive = new Promotion { Title = "Inactive", IsActive = false };
        var (svc, _) = Build([active, inactive]);

        var result = await svc.GetActivePromotionsAsync();

        Assert.Single(result);
        Assert.Equal("Active", result[0].Title);
    }

    [Fact]
    public async Task GetActivePromotionsAsync_ExcludesExpired()
    {
        var expired = new Promotion { Title = "Expired", IsActive = true, ExpiresAt = DateTime.UtcNow.AddDays(-1) };
        var current = new Promotion { Title = "Current", IsActive = true, ExpiresAt = DateTime.UtcNow.AddDays(1) };
        var noExpiry = new Promotion { Title = "Evergreen", IsActive = true, ExpiresAt = null };
        var (svc, _) = Build([expired, current, noExpiry]);

        var result = await svc.GetActivePromotionsAsync();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, p => p.Title == "Expired");
    }

    [Fact]
    public async Task GetAllPromotionsAsync_IncludesInactiveAndExpired()
    {
        var expired = new Promotion { Title = "Expired", IsActive = false, ExpiresAt = DateTime.UtcNow.AddDays(-1) };
        var (svc, _) = Build([expired]);

        var result = await svc.GetAllPromotionsAsync();

        Assert.Single(result);
    }

    [Fact]
    public async Task CreateAsync_AddsPromotionAndSaves()
    {
        var (svc, db) = Build();
        var dto = new CreatePromotionDto("New Promo", null, null, "Details", null, null, null, null, null, null);

        var result = await svc.CreateAsync(dto);

        Assert.True(result.Succeeded);
        Assert.Equal(201, result.StatusCode);
        db.Promotions.Received(1).Add(Arg.Is<Promotion>(p => p.Title == "New Promo"));
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

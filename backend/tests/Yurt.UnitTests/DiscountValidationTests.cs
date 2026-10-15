using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.DiscountCodes.Services;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

/// <summary>
/// Tests for <see cref="DiscountCodeService.ValidateAsync"/> using mocked DB.
/// </summary>
public class DiscountValidationTests
{
    private static readonly Guid Customer = Guid.NewGuid();

    private static DiscountCodeService Build(params DiscountCode[] codes)
        => Build(codes, []);

    private static DiscountCodeService Build(DiscountCode[] codes, Order[] orders)
    {
        var db    = Substitute.For<IApplicationDbContext>();
        var audit = Substitute.For<IAuditLogService>();

        var mockSet = codes.ToList().BuildMockDbSet();
        var ordersMockSet = orders.ToList().BuildMockDbSet();
        db.DiscountCodes.Returns(mockSet);
        db.Orders.Returns(ordersMockSet);

        return new DiscountCodeService(db, audit);
    }

    private static DiscountCode Active(
        string code,
        DiscountType type,
        decimal value,
        int? maxUses          = null,
        int  usedCount        = 0,
        decimal? minAmount    = null,
        DateTime? startsAt    = null,
        DateTime? expiresAt   = null)
        => new()
        {
            Code          = code.ToUpperInvariant(),
            DiscountType  = type,
            DiscountValue = value,
            IsActive      = true,
            MaxUses       = maxUses,
            UsedCount     = usedCount,
            MinOrderAmount = minAmount,
            StartsAt      = startsAt,
            ExpiresAt     = expiresAt,
        };

    [Fact]
    public async Task ValidateAsync_CodeNotFound_ReturnsInvalid()
    {
        var svc    = Build(); // empty DB
        var result = await svc.ValidateAsync("NOCODE", 50m, Customer);

        Assert.False(result.IsValid);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0m, result.DiscountAmount);
    }

    [Fact]
    public async Task ValidateAsync_ExpiredCode_ReturnsInvalid()
    {
        var code = Active("EXPIRED", DiscountType.Percentage, 10, expiresAt: DateTime.UtcNow.AddDays(-1));
        var svc  = Build(code);
        var result = await svc.ValidateAsync("EXPIRED", 50m, Customer);

        Assert.False(result.IsValid);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_NotYetActiveCode_ReturnsInvalid()
    {
        var code = Active("FUTURE", DiscountType.Percentage, 10,
            startsAt: DateTime.UtcNow.AddDays(1), expiresAt: DateTime.UtcNow.AddYears(1));
        var svc  = Build(code);
        var result = await svc.ValidateAsync("FUTURE", 50m, Customer);

        Assert.False(result.IsValid);
        Assert.Contains("not yet active", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_UsageLimitReached_ReturnsInvalid()
    {
        var code = Active("MAXED", DiscountType.Percentage, 10,
            maxUses: 5, usedCount: 5,
            expiresAt: DateTime.UtcNow.AddYears(1));
        var svc  = Build(code);
        var result = await svc.ValidateAsync("MAXED", 50m, Customer);

        Assert.False(result.IsValid);
        Assert.Contains("usage limit", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_SubtotalBelowMinimum_ReturnsInvalid()
    {
        var code = Active("MINAMT", DiscountType.Percentage, 10,
            minAmount: 100m, expiresAt: DateTime.UtcNow.AddYears(1));
        var svc  = Build(code);
        var result = await svc.ValidateAsync("MINAMT", 50m, Customer); // below $100 minimum

        Assert.False(result.IsValid);
        Assert.Contains("minimum", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_ValidPercentageCode_ReturnsCorrectDiscount()
    {
        var code = Active("PCT20", DiscountType.Percentage, 20,
            expiresAt: DateTime.UtcNow.AddYears(1));
        var svc  = Build(code);
        var result = await svc.ValidateAsync("PCT20", 50m, Customer); // 20% of 50 = 10

        Assert.True(result.IsValid);
        Assert.Equal(10m, result.DiscountAmount);
    }

    [Fact]
    public async Task ValidateAsync_ValidFixedCode_ReturnsCorrectDiscount()
    {
        var code = Active("FIXED5", DiscountType.FixedAmount, 5m,
            expiresAt: DateTime.UtcNow.AddYears(1));
        var svc  = Build(code);
        var result = await svc.ValidateAsync("FIXED5", 30m, Customer);

        Assert.True(result.IsValid);
        Assert.Equal(5m, result.DiscountAmount);
    }

    [Fact]
    public async Task ValidateAsync_CodeLookupIsCaseInsensitive()
    {
        var code = Active("SUMMER20", DiscountType.Percentage, 20,
            expiresAt: DateTime.UtcNow.AddYears(1));
        var svc  = Build(code);

        // Call with mixed case — service normalises to upper before querying
        var result = await svc.ValidateAsync("summer20", 100m, Customer);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_AlreadyUsedByThisCustomer_ReturnsInvalid()
    {
        var code = Active("ONCE10", DiscountType.Percentage, 10,
            expiresAt: DateTime.UtcNow.AddYears(1));
        var priorOrder = new Order { CustomerUserId = Customer, DiscountCodeId = code.Id };
        var svc  = Build([code], [priorOrder]);

        var result = await svc.ValidateAsync("ONCE10", 50m, Customer);

        Assert.False(result.IsValid);
        Assert.Contains("already used", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0m, result.DiscountAmount);
    }

    [Fact]
    public async Task ValidateAsync_UsedByAnotherCustomer_StillValidForThisCustomer()
    {
        var code = Active("ONCE10", DiscountType.Percentage, 10,
            expiresAt: DateTime.UtcNow.AddYears(1));
        var otherCustomersOrder = new Order { CustomerUserId = Guid.NewGuid(), DiscountCodeId = code.Id };
        var svc  = Build([code], [otherCustomersOrder]);

        var result = await svc.ValidateAsync("ONCE10", 50m, Customer);

        Assert.True(result.IsValid);
        Assert.Equal(5m, result.DiscountAmount);
    }
}

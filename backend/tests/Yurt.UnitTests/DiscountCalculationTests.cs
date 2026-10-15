using Yurt.Application.Features.DiscountCodes.Services;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

/// <summary>
/// Tests for <see cref="DiscountCodeService.CalculateDiscount"/> — a pure static function.
/// </summary>
public class DiscountCalculationTests
{
    private static DiscountCode Percentage(decimal value) => new()
        { DiscountType = DiscountType.Percentage, DiscountValue = value };

    private static DiscountCode Fixed(decimal value) => new()
        { DiscountType = DiscountType.FixedAmount, DiscountValue = value };

    [Theory]
    [InlineData(10,  100,  10.00)]
    [InlineData(20,  50,   10.00)]
    [InlineData(15,  33.33, 5.00)]   // 33.33 * 15 / 100 = 4.9995 → rounded to 5.00
    [InlineData(33,  1,     0.33)]   // 1 * 33 / 100 = 0.33
    [InlineData(100, 9.99,  9.99)]   // 100% off
    public void Percentage_CalculatesCorrectly(decimal discountPct, decimal subtotal, decimal expected)
    {
        var discount = DiscountCodeService.CalculateDiscount(Percentage(discountPct), subtotal);
        Assert.Equal(expected, discount);
    }

    [Theory]
    [InlineData(5,   20,    5)]    // simple fixed deduction
    [InlineData(5,   4.99,  4.99)] // cap: cannot exceed subtotal
    [InlineData(100, 50,   50)]    // cap: $100 off a $50 order → $50 max
    [InlineData(0.01, 100,  0.01)] // tiny discount
    public void Fixed_CalculatesCorrectlyAndNeverExceedsSubtotal(
        decimal discountAmt, decimal subtotal, decimal expected)
    {
        var discount = DiscountCodeService.CalculateDiscount(Fixed(discountAmt), subtotal);
        Assert.Equal(expected, discount);
    }

    [Fact]
    public void Percentage_ExactlyOneHundred_ReturnsFullSubtotal()
    {
        var discount = DiscountCodeService.CalculateDiscount(Percentage(100), 42.50m);
        Assert.Equal(42.50m, discount);
    }

    [Fact]
    public void Percentage_RoundsToTwoDecimalPlaces()
    {
        // 7% of 99.99 = 6.9993 → rounds to 7.00
        var discount = DiscountCodeService.CalculateDiscount(Percentage(7), 99.99m);
        Assert.Equal(7.00m, discount);
    }

    [Fact]
    public void Fixed_ZeroSubtotal_ReturnsZero()
    {
        var discount = DiscountCodeService.CalculateDiscount(Fixed(10), 0m);
        Assert.Equal(0m, discount);
    }
}

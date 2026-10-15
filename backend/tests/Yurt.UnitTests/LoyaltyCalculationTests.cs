using Yurt.Application.Features.Loyalty.Services;

namespace Yurt.UnitTests;

public class LoyaltyCalculationTests
{
    [Theory]
    [InlineData(1000, 5, 50)]
    [InlineData(9.50, 5, 0.47)]     // 0.475 truncated toward zero — never over-credit
    [InlineData(100, 2.5, 2.50)]
    [InlineData(3.75, 5, 0.18)]
    public void CalculatePoints_ComputesPercentOfTotal(decimal total, decimal percent, decimal expected)
        => Assert.Equal(expected, LoyaltyService.CalculatePoints(total, percent));

    [Theory]
    [InlineData(0, 5)]
    [InlineData(-10, 5)]
    [InlineData(100, 0)]
    [InlineData(100, -1)]
    public void CalculatePoints_ZeroOrNegativeInputs_ReturnZero(decimal total, decimal percent)
        => Assert.Equal(0m, LoyaltyService.CalculatePoints(total, percent));
}

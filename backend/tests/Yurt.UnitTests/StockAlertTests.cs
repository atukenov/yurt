using Yurt.Application.Features.Stock;

namespace Yurt.UnitTests;

public class StockAlertTests
{
    [Theory]
    [InlineData(5, 4, 3, false)]   // still above the warn-at number
    [InlineData(4, 3, 3, true)]    // reaches it
    [InlineData(10, 1, 3, true)]   // jumps past it
    [InlineData(3, 2, 3, false)]   // was already low: no second alert
    [InlineData(2, 0, 3, true)]    // runs out
    [InlineData(0, 0, 3, false)]   // was already out
    [InlineData(2, 0, null, true)] // no warn-at number: only running out alerts
    [InlineData(5, 1, null, false)]
    public void CrossedWarnLevel_OnlyTheCrossingOrderAlerts(int before, int after, int? lowAt, bool expected)
        => Assert.Equal(expected, StockService.CrossedWarnLevel(before, after, lowAt));

    private static StockAlertDto Item(string name, decimal left, string? unit = null)
        => new("ingredient", Guid.NewGuid(), name, Guid.Empty, "Main", left, unit, left <= 0);

    [Fact]
    public void Copy_LowOnly_SaysHowMuchIsLeft()
    {
        var (title, body) = StockAlertPushService.Copy([Item("Milk", 1.5m, "l")]);
        Assert.Equal("Мало на складе", title);
        Assert.Equal("Milk: осталось 1.5 l (Main)", body);
    }

    [Fact]
    public void Copy_SomethingOut_PutsItFirst_AndShortensLongLists()
    {
        var (title, body) = StockAlertPushService.Copy(
            [Item("Apple", 2), Item("Beans", 0), Item("Cups", 3), Item("Donuts", 1), Item("Eggs", 4)]);
        Assert.Equal("Закончилось на складе", title);
        Assert.StartsWith("Beans: закончилось; Apple: осталось 2", body);
        Assert.EndsWith(" и ещё 2 (Main)", body);
    }
}

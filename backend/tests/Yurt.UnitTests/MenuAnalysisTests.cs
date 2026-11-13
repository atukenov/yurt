using Yurt.Application.Features.Profit;

namespace Yurt.UnitTests;

public class MenuAnalysisTests
{
    private static ItemProfitDto Row(Guid id, string name, int sold, decimal revenue, decimal? cost, Guid? variant = null)
        => new(id, name, variant, null, sold, revenue, cost, cost is null ? null : revenue - cost, null, null, 0, false);

    private static MenuAnalysisItemDto Of(MenuAnalysisDto a, string name) => a.Items.Single(i => i.Name == name);

    [Fact]
    public void Sorts_dishes_into_the_four_groups_with_an_action_each()
    {
        // 365 portions and 452 profit: 1.24 per portion on average; popular from 365 / 4 * 0.7 = 63.9 portions.
        var report = new ItemProfitReportDto("2026-11", null,
        [
            Row(Guid.NewGuid(), "Horse", 200, 600, 500),    // popular, 0.50 per portion
            Row(Guid.NewGuid(), "Puzzle", 10, 200, 50),     // rare, 15 per portion
            Row(Guid.NewGuid(), "Dog", 5, 40, 38),          // rare, 0.40 per portion
            Row(Guid.NewGuid(), "Star", 150, 450, 250),     // popular, 1.33 per portion
        ]);
        var a = MenuAnalysis.Build(report);

        Assert.Equal(63.9m, a.PopularFrom);
        Assert.Equal("star", Of(a, "Star").Class);
        Assert.Equal("keep", Of(a, "Star").Action);
        Assert.Equal("plowhorse", Of(a, "Horse").Class);
        Assert.Equal("fix", Of(a, "Horse").Action);
        Assert.Equal("puzzle", Of(a, "Puzzle").Class);
        Assert.Equal("push", Of(a, "Puzzle").Action);
        Assert.Equal("dog", Of(a, "Dog").Class);
        Assert.Equal("drop", Of(a, "Dog").Action);
        Assert.Equal(Math.Round(a.AverageProfitPerItem - 0.5m, 2), Of(a, "Horse").RaisePriceBy);
        Assert.Null(Of(a, "Puzzle").RaisePriceBy);
    }

    [Fact]
    public void A_popular_dish_that_earns_above_average_is_a_star()
    {
        var report = new ItemProfitReportDto("2026-11", null,
        [
            Row(Guid.NewGuid(), "Latte", 100, 1000, 300),   // 7 per portion
            Row(Guid.NewGuid(), "Tea", 100, 300, 200),      // 1 per portion
            Row(Guid.NewGuid(), "Cake", 4, 80, 40),         // 10 per portion, rarely sold
        ]);
        var a = MenuAnalysis.Build(report);
        Assert.Equal("star", Of(a, "Latte").Class);
        Assert.Equal("plowhorse", Of(a, "Tea").Class);
        Assert.Equal("puzzle", Of(a, "Cake").Class);
        Assert.Equal(49.0m, Of(a, "Latte").SoldShare); // 100 of 204 portions
        Assert.Equal(83.3m, Of(a, "Latte").ProfitShare); // 700 of 840 profit
    }

    [Fact]
    public void Sizes_of_one_dish_count_together_and_dishes_without_a_recipe_are_listed_apart()
    {
        var latte = Guid.NewGuid();
        var report = new ItemProfitReportDto("2026-11", null,
        [
            Row(latte, "Latte", 10, 100, 40, Guid.NewGuid()),
            Row(latte, "Latte", 10, 150, 50, Guid.NewGuid()),
            Row(Guid.NewGuid(), "Mystery", 30, 300, null),
        ]);
        var a = MenuAnalysis.Build(report);
        var row = Assert.Single(a.Items);
        Assert.Equal(20, row.Sold);
        Assert.Equal(160m, row.Profit);
        Assert.Equal(8m, row.ProfitPerItem);
        Assert.Equal("Mystery", Assert.Single(a.NoRecipe).Name);
    }

    [Fact]
    public void Abc_gives_A_to_the_dishes_bringing_the_first_80_percent_of_profit()
    {
        var report = new ItemProfitReportDto("2026-11", null,
        [
            Row(Guid.NewGuid(), "Big", 10, 160, 100),     // 60 of 100 profit: starts at 0%
            Row(Guid.NewGuid(), "Mid", 10, 120, 100),     // 20: starts at 60%
            Row(Guid.NewGuid(), "Small", 10, 115, 100),   // 15: starts at 80%
            Row(Guid.NewGuid(), "Tiny", 10, 105, 100),    // 5: starts at 95%
            Row(Guid.NewGuid(), "Loss", 10, 50, 80),      // loses money
        ]);
        var a = MenuAnalysis.Build(report);
        Assert.Equal("A", Of(a, "Big").Abc);
        Assert.Equal("A", Of(a, "Mid").Abc);
        Assert.Equal("B", Of(a, "Small").Abc);
        Assert.Equal("C", Of(a, "Tiny").Abc);
        Assert.Equal("C", Of(a, "Loss").Abc);
    }

    [Fact]
    public void An_empty_month_gives_an_empty_analysis()
    {
        var a = MenuAnalysis.Build(new ItemProfitReportDto("2026-11", null, []));
        Assert.Empty(a.Items);
        Assert.Empty(a.NoRecipe);
    }
}

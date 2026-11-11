using Yurt.Application.Features.Menu.Import;

namespace Yurt.Application.Features.Profit;

/// <summary>The profit reports as Excel files, with the words in the language the admin app is shown in.</summary>
public static class ProfitSpreadsheet
{
    public static string Lang(string? lang) => lang is "ru" or "kk" ? lang : "en";

    private static string T(string lang, string en, string ru, string kk) => lang switch { "ru" => ru, "kk" => kk, _ => en };

    public static byte[] Items(ItemProfitReportDto report, string lang)
    {
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                T(lang, "Dish", "Блюдо", "Тағам"), T(lang, "Size", "Размер", "Өлшем"), T(lang, "Sold", "Продано", "Сатылды"),
                T(lang, "Money taken", "Выручка", "Түсім"), T(lang, "Ingredients cost", "Себестоимость", "Өзіндік құны"),
                T(lang, "Profit", "Прибыль", "Пайда"), T(lang, "Food cost %", "Фудкост %", "Фудкост %"),
                T(lang, "Profit per item", "Прибыль за штуку", "Бір данадан пайда"),
                T(lang, "Priced at today's costs", "По сегодняшним ценам", "Бүгінгі бағамен"),
            },
        };
        foreach (var i in report.Items)
            rows.Add(new object?[]
            {
                i.Name, i.Size, i.Sold, i.Revenue, i.Cost, i.Profit, i.FoodCostPercent, i.ProfitPerItem,
                i.EstimatedCount > 0 ? i.EstimatedCount : null,
            });
        return SpreadsheetWriter.Write(T(lang, "Profit by dish", "Прибыль по блюдам", "Тағам бойынша пайда"), rows);
    }

    public static byte[] Month(ProfitMonthDto m, string lang)
    {
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { m.Month, T(lang, "Amount", "Сумма", "Сома"), T(lang, "Last month", "Прошлый месяц", "Өткен ай") },
            new object?[] { T(lang, "Orders", "Заказы", "Тапсырыстар"), m.Orders, null },
            new object?[] { T(lang, "Sales before discounts", "Продажи до скидок", "Жеңілдікке дейінгі сатылым"), m.Sales, null },
            new object?[] { T(lang, "Discounts", "Скидки", "Жеңілдіктер"), -m.Discounts, null },
            new object?[] { T(lang, "Refunds", "Возвраты", "Қайтарымдар"), -m.Refunds, null },
            new object?[] { T(lang, "Money taken", "Выручка", "Түсім"), m.NetSales, m.Previous.NetSales },
            new object?[] { T(lang, "Ingredients of what was sold", "Себестоимость проданного", "Сатылғанның өзіндік құны"), -m.IngredientCost, -m.Previous.IngredientCost },
            new object?[] { T(lang, "After ingredients", "Валовая прибыль", "Жалпы пайда"), m.GrossProfit, m.Previous.GrossProfit },
        };
        foreach (var w in m.WriteOffsByReason)
            rows.Add(new object?[] { T(lang, "Write-off: ", "Списание: ", "Есептен шығару: ") + w.Reason, -w.Cost, null });
        rows.Add(new object?[] { T(lang, "Stocktake shortage", "Недостача по инвентаризации", "Түгендеу кемшілігі"), -m.StocktakeLoss, null });
        if (m.Payroll is { } pay)
            rows.Add(new object?[] { T(lang, "Wages", "Зарплата", "Жалақы"), -pay, null });
        foreach (var e in m.Expenses)
            rows.Add(new object?[] { e.LocationName is null ? e.Name : $"{e.Name} ({e.LocationName})", -e.Amount, null });
        rows.Add(new object?[] { T(lang, "Profit", "Прибыль", "Пайда"), m.Profit, m.Previous.Profit });
        return SpreadsheetWriter.Write(T(lang, "Profit", "Прибыль", "Пайда"), rows);
    }
}

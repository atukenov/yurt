using System.Globalization;
using Yurt.Application.Features.Menu.Import;

namespace Yurt.Application.Features.Accounting;

/// <summary>
/// The accounting export as one Excel file, one sheet per subject, with headings in the language the admin app is shown
/// in. Payment methods get one column each, so the accountant can match them with the bank and the cash book.
/// </summary>
public static class AccountingSpreadsheet
{
    public static string Lang(string? lang) => lang is "ru" or "kk" ? lang : "en";

    private static string T(string lang, string en, string ru, string kk) => lang switch { "ru" => ru, "kk" => kk, _ => en };

    /// <summary>Payment methods in a fixed order, so the columns do not move from one export to the next.</summary>
    public static readonly IReadOnlyList<string> MethodOrder = ["Cash", "Card", "KaspiBank", "HalykBank", "FreedomBank", "ApplePay", "Other"];

    public static string Method(string method, string lang) => method switch
    {
        "Cash" => T(lang, "Cash", "Наличные", "Қолма-қол"),
        "Card" => T(lang, "Card", "Карта", "Карта"),
        "KaspiBank" => "Kaspi",
        "HalykBank" => "Halyk",
        "FreedomBank" => "Freedom",
        "ApplePay" => "Apple Pay",
        _ => T(lang, "Other", "Другое", "Басқа"),
    };

    private static string Reason(string? reason, string lang) => reason switch
    {
        "spilled" => T(lang, "Spilled", "Пролито", "Төгілді"),
        "expired" => T(lang, "Expired", "Истёк срок", "Мерзімі өтті"),
        "staff" => T(lang, "Staff drink", "Напиток персоналу", "Қызметкерге сусын"),
        "broken" => T(lang, "Broken", "Испорчено", "Бүлінді"),
        null => "",
        _ => T(lang, "Other", "Другое", "Басқа"),
    };

    private static string LossType(string type, string lang) => type switch
    {
        StockLossTypes.WriteOff => T(lang, "Write-off", "Списание", "Есептен шығару"),
        StockLossTypes.StocktakeShortage => T(lang, "Stocktake: missing", "Инвентаризация: недостача", "Түгендеу: кемшілік"),
        _ => T(lang, "Stocktake: found extra", "Инвентаризация: излишек", "Түгендеу: артық"),
    };

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Time(DateTime t) => t.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    private static string YesNo(bool v, string lang) => v ? T(lang, "Yes", "Да", "Иә") : T(lang, "No", "Нет", "Жоқ");

    public static byte[] Write(AccountingExport x, string lang)
    {
        // Only the methods that were used get a column, in the fixed order (unknown ones at the end).
        var used = x.SalesByDay.SelectMany(d => d.ByMethod.Keys).Distinct().ToList();
        var methods = MethodOrder.Where(used.Contains).Concat(used.Where(m => !MethodOrder.Contains(m)).OrderBy(m => m)).ToList();

        return SpreadsheetWriter.Write(
        [
            (T(lang, "Sales by day", "Продажи по дням", "Күн бойынша сатылым"), SalesByDay(x, methods, lang)),
            (T(lang, "Orders", "Заказы", "Тапсырыстар"), Orders(x, lang)),
            (T(lang, "Items sold", "Проданные позиции", "Сатылған позициялар"), Items(x, lang)),
            (T(lang, "Shifts", "Смены", "Ауысымдар"), Shifts(x, lang)),
            (T(lang, "Deliveries", "Поступления", "Түсімдер"), Deliveries(x, lang)),
            (T(lang, "Write-offs", "Списания", "Есептен шығару"), Losses(x, lang)),
        ]);
    }

    private static List<IReadOnlyList<object?>> SalesByDay(AccountingExport x, List<string> methods, string lang)
    {
        var head = new List<object?>
        {
            T(lang, "Date", "Дата", "Күні"), T(lang, "Branch", "Филиал", "Филиал"), T(lang, "Orders", "Заказов", "Тапсырыс"),
            T(lang, "Sales before discounts", "Продажи до скидок", "Жеңілдікке дейінгі сатылым"),
            T(lang, "Discounts", "Скидки", "Жеңілдіктер"), T(lang, "Paid with points", "Оплачено баллами", "Ұпаймен төленді"),
            T(lang, "Paid", "Оплачено", "Төленді"),
        };
        head.AddRange(methods.Select(m => (object?)Method(m, lang)));
        head.Add(T(lang, "Refunds", "Возвраты", "Қайтарымдар"));
        head.Add(T(lang, "Money taken", "Выручка", "Түсім"));
        var rows = new List<IReadOnlyList<object?>> { head };

        foreach (var d in x.SalesByDay)
        {
            var row = new List<object?> { Day(d.Date), d.Branch, d.Orders, d.SalesBeforeDiscounts, d.Discounts, d.Points, d.Paid };
            row.AddRange(methods.Select(m => (object?)(d.ByMethod.TryGetValue(m, out var v) ? v : 0m)));
            row.Add(d.Refunds);
            row.Add(d.MoneyTaken);
            rows.Add(row);
        }

        var total = new List<object?>
        {
            T(lang, "Total", "Итого", "Барлығы"), null, x.SalesByDay.Sum(d => d.Orders),
            x.SalesByDay.Sum(d => d.SalesBeforeDiscounts), x.SalesByDay.Sum(d => d.Discounts),
            x.SalesByDay.Sum(d => d.Points), x.SalesByDay.Sum(d => d.Paid),
        };
        total.AddRange(methods.Select(m => (object?)x.SalesByDay.Sum(d => d.ByMethod.TryGetValue(m, out var v) ? v : 0m)));
        total.Add(x.SalesByDay.Sum(d => d.Refunds));
        total.Add(x.SalesByDay.Sum(d => d.MoneyTaken));
        rows.Add(total);
        return rows;
    }

    private static List<IReadOnlyList<object?>> Orders(AccountingExport x, string lang)
    {
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                T(lang, "Completed", "Завершён", "Аяқталды"), T(lang, "Order", "Заказ", "Тапсырыс"),
                T(lang, "Branch", "Филиал", "Филиал"), T(lang, "Table", "Стол", "Үстел"),
                T(lang, "Sales before discounts", "Сумма до скидки", "Жеңілдікке дейінгі сома"),
                T(lang, "Discount", "Скидка", "Жеңілдік"), T(lang, "Paid with points", "Оплачено баллами", "Ұпаймен төленді"),
                T(lang, "Paid", "Оплачено", "Төленді"), T(lang, "Payment", "Оплата", "Төлем"),
                T(lang, "Refunded", "Возврат", "Қайтарылды"),
                T(lang, "Tax receipt No.", "Номер фискального чека", "Фискалдық чек нөмірі"),
                T(lang, "Tax receipt", "Фискальный чек", "Фискалдық чек"),
            },
        };
        foreach (var o in x.Orders)
            rows.Add(new object?[]
            {
                Time(o.CompletedLocal), o.Number, o.Branch, o.Table, o.SalesBeforeDiscounts, o.Discount, o.Points, o.Paid,
                string.Join("; ", o.Payments.Select(p => $"{Method(p.Method, lang)} {p.Amount.ToString("0.##", CultureInfo.InvariantCulture)}")),
                o.Refunded ? YesNo(true, lang) : null, o.ReceiptNumber,
                o.ReceiptStatus switch
                {
                    "Sent" => T(lang, "Sent", "Отправлен", "Жіберілді"),
                    "Pending" => T(lang, "Not sent yet", "Ещё не отправлен", "Әлі жіберілмеді"),
                    "Skipped" => T(lang, "Not needed", "Не нужен", "Қажет емес"),
                    _ => null,
                },
            });
        return rows;
    }

    private static List<IReadOnlyList<object?>> Items(AccountingExport x, string lang)
    {
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                T(lang, "Dish", "Блюдо", "Тағам"), T(lang, "Size", "Размер", "Өлшем"), T(lang, "Quantity", "Количество", "Саны"),
                T(lang, "Money taken (after discounts)", "Выручка (после скидок)", "Түсім (жеңілдіктен кейін)"),
                T(lang, "Ingredients cost", "Себестоимость", "Өзіндік құны"),
            },
        };
        foreach (var i in x.Items)
            rows.Add(new object?[] { i.Name, i.Size, i.Quantity, i.Amount, i.Cost });
        rows.Add(new object?[]
        {
            T(lang, "Total", "Итого", "Барлығы"), null, x.Items.Sum(i => i.Quantity), x.Items.Sum(i => i.Amount),
            x.Items.All(i => i.Cost is not null) ? x.Items.Sum(i => i.Cost!.Value) : null,
        });
        return rows;
    }

    private static List<IReadOnlyList<object?>> Shifts(AccountingExport x, string lang)
    {
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                T(lang, "Branch", "Филиал", "Филиал"), T(lang, "Opened", "Открыта", "Ашылды"), T(lang, "Opened by", "Открыл", "Ашқан"),
                T(lang, "Closed", "Закрыта", "Жабылды"), T(lang, "Closed by", "Закрыл", "Жапқан"),
                T(lang, "Orders", "Заказов", "Тапсырыс"), T(lang, "Takings", "Выручка", "Түсім"),
                T(lang, "Opening cash", "Наличные на начало", "Бастапқы қолма-қол"),
                T(lang, "Cash sales", "Продажи наличными", "Қолма-қол сатылым"),
                T(lang, "Cash put in", "Внесено", "Салынды"), T(lang, "Cash taken out", "Изъято", "Алынды"),
                T(lang, "Expected cash", "Ожидалось наличных", "Күтілген қолма-қол"),
                T(lang, "Counted cash", "Пересчитано", "Саналды"),
                T(lang, "Difference", "Расхождение", "Айырма"),
                T(lang, "Z report sent", "Z-отчёт отправлен", "Z-есеп жіберілді"),
            },
        };
        foreach (var s in x.Shifts)
            rows.Add(new object?[]
            {
                s.Branch, Time(s.OpenedLocal), s.OpenedBy, s.ClosedLocal is { } c ? Time(c) : T(lang, "Open", "Открыта", "Ашық"),
                s.ClosedBy, s.Orders, s.Takings, s.OpeningCash, s.CashSales, s.CashIn, s.CashOut, s.ExpectedCash,
                s.CountedCash, s.Difference, YesNo(s.ZReportDone, lang),
            });
        return rows;
    }

    private static List<IReadOnlyList<object?>> Deliveries(AccountingExport x, string lang)
    {
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                T(lang, "Date", "Дата", "Күні"), T(lang, "Invoice No.", "Номер накладной", "Жүкқұжат нөмірі"),
                T(lang, "Supplier", "Поставщик", "Жеткізуші"), T(lang, "Supplier BIN", "БИН поставщика", "Жеткізуші БСН"),
                T(lang, "Branch", "Филиал", "Филиал"), T(lang, "Ingredient", "Ингредиент", "Ингредиент"),
                T(lang, "Quantity", "Количество", "Саны"), T(lang, "Unit", "Ед.", "Бірлік"), T(lang, "Sum", "Сумма", "Сома"),
                T(lang, "Invoice total", "Сумма накладной", "Жүкқұжат сомасы"), T(lang, "Paid", "Оплачено", "Төленді"),
                T(lang, "Paid on", "Дата оплаты", "Төленген күні"),
            },
        };
        foreach (var d in x.Deliveries)
            rows.Add(new object?[]
            {
                Day(d.Date), d.Number, d.Supplier, d.SupplierBin, d.Branch, d.Ingredient, d.Quantity, d.Unit, d.Sum,
                d.InvoiceTotal, YesNo(d.Paid, lang), d.PaidLocal is { } p ? Time(p) : null,
            });
        rows.Add(new object?[]
        {
            T(lang, "Total", "Итого", "Барлығы"), null, null, null, null, null, null, null, x.Deliveries.Sum(d => d.Sum ?? 0m),
        });
        return rows;
    }

    private static List<IReadOnlyList<object?>> Losses(AccountingExport x, string lang)
    {
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                T(lang, "When", "Когда", "Қашан"), T(lang, "Branch", "Филиал", "Филиал"), T(lang, "What", "Что", "Не"),
                T(lang, "Item", "Позиция", "Позиция"), T(lang, "Quantity", "Количество", "Саны"), T(lang, "Unit", "Ед.", "Бірлік"),
                T(lang, "Reason", "Причина", "Себебі"), T(lang, "Note", "Комментарий", "Түсініктеме"),
                T(lang, "Cost per unit", "Цена за единицу", "Бірлік бағасы"), T(lang, "Sum", "Сумма", "Сома"),
                T(lang, "By", "Кто", "Кім"),
            },
        };
        foreach (var l in x.StockLosses)
            rows.Add(new object?[]
            {
                Time(l.WhenLocal), l.Branch, LossType(l.Type, lang), l.Item, l.Quantity, l.Unit, Reason(l.Reason, lang), l.Note,
                l.UnitCost, l.Sum, l.By,
            });
        rows.Add(new object?[]
        {
            T(lang, "Total", "Итого", "Барлығы"), null, null, null, null, null, null, null, null, x.StockLosses.Sum(l => l.Sum ?? 0m),
        });
        return rows;
    }
}

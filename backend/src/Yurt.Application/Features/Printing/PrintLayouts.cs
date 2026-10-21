using System.Globalization;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Printing;

/// <summary>The kitchen ticket and the customer receipt as ESC/POS bytes, laid out like the browser-printed versions.</summary>
public static class PrintLayouts
{
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");

    private static readonly Dictionary<string, Dictionary<string, string>> Words = new()
    {
        ["ru"] = new()
        {
            ["subtotal"] = "Подытог", ["discount"] = "Скидка", ["total"] = "Итого", ["bonus"] = "Оплачено бонусами",
            ["paid"] = "К оплате", ["method"] = "Оплата", ["thanks"] = "Спасибо!", ["test"] = "Тестовая печать",
            ["testOk"] = "Принтер работает", ["currency"] = "тг", ["Cash"] = "Наличные", ["Card"] = "Карта", ["Other"] = "Другое",
        },
        ["kk"] = new()
        {
            ["subtotal"] = "Аралық сома", ["discount"] = "Жеңілдік", ["total"] = "Барлығы", ["bonus"] = "Бонуспен төленді",
            ["paid"] = "Төлеуге", ["method"] = "Төлем", ["thanks"] = "Рахмет!", ["test"] = "Сынақ басып шығару",
            ["testOk"] = "Принтер жұмыс істейді", ["currency"] = "тг", ["Cash"] = "Қолма-қол", ["Card"] = "Карта", ["Other"] = "Басқа",
        },
        ["en"] = new()
        {
            ["subtotal"] = "Subtotal", ["discount"] = "Discount", ["total"] = "Total", ["bonus"] = "Paid with bonus",
            ["paid"] = "To pay", ["method"] = "Payment", ["thanks"] = "Thank you!", ["test"] = "Test print",
            ["testOk"] = "The printer works", ["currency"] = "KZT", ["Cash"] = "Cash", ["Card"] = "Card", ["Other"] = "Other",
        },
    };

    public static readonly IReadOnlyCollection<string> Languages = Words.Keys;

    public static string ShortId(Guid orderId) => orderId.ToString("N")[..8].ToUpperInvariant();

    /// <summary>A kitchen ticket: big order number, station and time, then each item with its extras and notes. No prices.</summary>
    public static byte[] Ticket(Printer printer, Order order, IReadOnlyList<OrderItem> items, string? stationName)
    {
        var p = new EscPos(printer.LineWidth, printer.CodeTable);
        p.Center().Big(true).Bold(true).Line($"#{ShortId(order.Id)}", p.Width / 2).Big(false).Bold(false);
        var when = Local(order.AcceptedAt ?? order.CreatedAt).ToString("HH:mm", CultureInfo.InvariantCulture);
        p.Line(string.IsNullOrWhiteSpace(stationName) ? when : $"{stationName} · {when}");
        if (CustomerName(order) is { Length: > 0 } name) p.Line(name);
        p.Left().Rule();
        foreach (var item in items)
        {
            p.Bold(true).Line($"{item.Quantity} x {item.MenuItemName}").Bold(false);
            var extras = Extras(item);
            if (extras.Length > 0) p.Line("  " + extras);
            if (!string.IsNullOrWhiteSpace(item.Notes)) p.Bold(true).Line("! " + item.Notes).Bold(false);
            p.Rule();
        }
        return p.Cut().ToArray();
    }

    /// <summary>A customer receipt: brand, branch, time and number, the lines with prices, totals and how it was paid.</summary>
    public static byte[] Receipt(Printer printer, Order order, string brandName, IReadOnlyList<OrderPayment> split)
    {
        var w = Words.GetValueOrDefault(printer.Language) ?? Words["ru"];
        string Money(decimal n) => $"{Math.Round(n).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ')} {w["currency"]}";

        var p = new EscPos(printer.LineWidth, printer.CodeTable);
        p.Center().Bold(true).Big(true).Line(brandName, p.Width / 2).Big(false).Bold(false);
        p.Line(order.Location?.Name ?? "");
        p.Line(Local(order.CompletedAt ?? order.CreatedAt).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture));
        p.Line($"#{ShortId(order.Id)}");
        p.Left().Rule();
        foreach (var item in order.Items)
        {
            p.Columns($"{item.Quantity} x {item.MenuItemName}", Money(item.LineTotal));
            var extras = Extras(item);
            if (extras.Length > 0) p.Line("  " + extras);
        }
        p.Rule();
        if (order.DiscountAmount > 0)
        {
            p.Columns(w["subtotal"], Money(order.Subtotal));
            p.Columns(order.DiscountCode?.Code is { Length: > 0 } code ? $"{w["discount"]} {code}" : w["discount"], "-" + Money(order.DiscountAmount));
        }
        p.Bold(true).Columns(w["total"], Money(order.Total)).Bold(false);
        var points = order.LoyaltyPointsSpent ?? 0;
        if (points > 0)
        {
            p.Columns(w["bonus"], "-" + Money(points));
            p.Bold(true).Columns(w["paid"], Money(Math.Max(0, order.Total - points))).Bold(false);
        }
        p.Rule();
        if (split.Count > 1)
        {
            foreach (var part in split) p.Columns(MethodName(part.Method, w), Money(part.Amount));
            p.Rule();
        }
        else if (order.PaymentMethod is { } method) p.Center().Line($"{w["method"]}: {MethodName(method, w)}");
        p.Center().Line(w["thanks"]);
        return p.Cut().ToArray();
    }

    /// <summary>A short page that shows the printer is reachable and the Cyrillic letters come out right.</summary>
    public static byte[] Test(Printer printer)
    {
        var w = Words.GetValueOrDefault(printer.Language) ?? Words["ru"];
        var p = new EscPos(printer.LineWidth, printer.CodeTable);
        p.Center().Bold(true).Big(true).Line(w["test"], p.Width / 2).Big(false).Bold(false);
        p.Line(printer.Name).Line(w["testOk"]).Left().Rule();
        p.Line("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ");
        p.Line("абвгдеёжзийклмнопрстуфхцчшщъыьэюя");
        p.Line("Әә Ғғ Ққ Ңң Өө Ұұ Үү Һһ Іі 0123456789");
        p.Rule().Columns("1 x Капучино", "1 200 " + w["currency"]);
        return p.Cut().ToArray();
    }

    private static string Extras(OrderItem item)
        => string.Join(", ", new[] { item.VariantLabel }.Concat(item.Toppings.Select(t => "+ " + t.ToppingName))
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    private static string CustomerName(Order order)
    {
        var c = order.CustomerUser;
        if (c is null) return "";
        return !string.IsNullOrWhiteSpace(c.FirstName) ? $"{c.FirstName} {c.LastName}".Trim() : c.MobileNumber;
    }

    private static string MethodName(PaymentMethod m, Dictionary<string, string> w) => m switch
    {
        PaymentMethod.Cash or PaymentMethod.Card or PaymentMethod.Other => w[m.ToString()],
        PaymentMethod.KaspiBank => "Kaspi",
        PaymentMethod.HalykBank => "Halyk",
        PaymentMethod.FreedomBank => "Freedom",
        PaymentMethod.ApplePay => "Apple Pay",
        _ => m.ToString(),
    };

    private static DateTime Local(DateTime utc)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), BusinessTimeZone);
}

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Yurt.Application.Features.Accounting;

/// <summary>
/// The accounting export as a 1C exchange file in the CommerceML 2 layout (КоммерческаяИнформация, version 2.10), the
/// format 1C uses for exchange with web shops. It holds a catalogue of what was sold and bought, and documents:
/// <list type="bullet">
/// <item>one retail sales report per branch per day (Отпуск товара, the cafe as seller, "Розничный покупатель" as buyer),
/// with the money split by payment method;</item>
/// <item>one return per branch per day for refunded orders (Возврат товара);</item>
/// <item>one incoming invoice per supplier delivery (Отпуск товара with the supplier as seller and the cafe as buyer);</item>
/// <item>one write-off per branch per day (Другое, "Списание товаров"), and one for stock found extra at a stocktake
/// ("Оприходование товаров").</item>
/// </list>
/// Each document's kind and branch are also in ЗначенияРеквизитов, so the accountant can map them in the 1C data
/// processing. The layout is a starting point to be checked with the cafe's accountant.
/// </summary>
public static class OneCExchangeWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private const string RetailBuyerId = "yurt-retail-buyer";

    private static string Money(decimal v) => v.ToString("0.00", Inv);
    private static string Qty(decimal v) => v.ToString("0.###", Inv);
    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", Inv);

    /// <summary>Russian unit name and its OKEI code for the units Yurt uses; anything else is passed through as written.</summary>
    public static (string Name, string? Code, string Full) Unit(string? unit) => unit?.Trim().ToLowerInvariant() switch
    {
        "g" or "г" or "gr" => ("г", "163", "Грамм"),
        "kg" or "кг" => ("кг", "166", "Килограмм"),
        "ml" or "мл" => ("мл", "111", "Миллилитр"),
        "l" or "л" => ("л", "112", "Литр"),
        null or "" or "pcs" or "pc" or "шт" => ("шт", "796", "Штука"),
        var other => (other!, null, other!),
    };

    private static XElement BaseUnit(string? unit)
    {
        var (name, code, full) = Unit(unit);
        var e = new XElement("БазоваяЕдиница", name, new XAttribute("НаименованиеПолное", full));
        if (code is not null) e.Add(new XAttribute("Код", code));
        return e;
    }

    private static XElement Requisite(string name, string value)
        => new("ЗначениеРеквизита", new XElement("Наименование", name), new XElement("Значение", value));

    private static string ProductId(Guid itemId, Guid? variantId) => variantId is { } v ? $"{itemId}#{v}" : itemId.ToString();

    private static string ProductName(string name, string? size) => string.IsNullOrWhiteSpace(size) ? name : $"{name} ({size})";

    private static XElement Line(string id, string name, string? unit, decimal quantity, decimal sum)
        => new("Товар",
            new XElement("Ид", id),
            new XElement("Наименование", name),
            BaseUnit(unit),
            new XElement("ЦенаЗаЕдиницу", Money(quantity != 0 ? Math.Round(sum / quantity, 2) : 0m)),
            new XElement("Количество", Qty(quantity)),
            new XElement("Сумма", Money(sum)));

    private static XElement Party(string id, string name, string role, string? bin = null)
    {
        var e = new XElement("Контрагент", new XElement("Ид", id), new XElement("Наименование", name));
        if (!string.IsNullOrWhiteSpace(bin)) e.Add(new XElement("ИНН", bin));
        e.Add(new XElement("Роль", role));
        return e;
    }

    private static XElement Document(
        string id, string number, DateOnly date, string operation, string role, decimal sum,
        IEnumerable<XElement> parties, string comment, IEnumerable<XElement> lines, IEnumerable<XElement> requisites)
        => new("Документ",
            new XElement("Ид", id),
            new XElement("Номер", number),
            new XElement("Дата", Day(date)),
            new XElement("ХозОперация", operation),
            new XElement("Роль", role),
            new XElement("Валюта", "KZT"),
            new XElement("Курс", "1"),
            new XElement("Сумма", Money(sum)),
            new XElement("Контрагенты", parties),
            new XElement("Время", "23:59:59"),
            new XElement("Комментарий", comment),
            new XElement("Товары", lines),
            new XElement("ЗначенияРеквизитов", requisites));

    public static byte[] Write(AccountingExport x, DateTime createdLocal)
    {
        var cafeId = x.CafeId.ToString();
        var owner = new XElement("Владелец", new XElement("Ид", cafeId), new XElement("Наименование", x.CafeName));
        var classifierId = $"yurt-{x.CafeId:N}";

        // ── catalogue: every dish sold, every ingredient bought or lost ──
        var products = new Dictionary<string, XElement>();
        void AddProduct(string id, string name, string? unit, string group)
        {
            if (products.ContainsKey(id)) return;
            products[id] = new XElement("Товар",
                new XElement("Ид", id),
                new XElement("Наименование", name),
                BaseUnit(unit),
                new XElement("ЗначенияРеквизитов", Requisite("ВидНоменклатуры", group)));
        }
        foreach (var l in x.Orders.SelectMany(o => o.Lines))
            AddProduct(ProductId(l.MenuItemId, l.VariantId), ProductName(l.Name, l.Size), null, "Блюдо");
        foreach (var d in x.Deliveries)
            AddProduct(d.IngredientId.ToString(), d.Ingredient, d.Unit, "Ингредиент");
        foreach (var l in x.StockLosses)
            AddProduct(l.ItemId.ToString(), l.Item, l.Kind == "dish" ? null : l.Unit, l.Kind == "dish" ? "Блюдо" : "Ингредиент");

        var documents = new List<XElement>();
        var cafeSeller = Party(cafeId, x.CafeName, "Продавец");
        var cafeBuyer = Party(cafeId, x.CafeName, "Покупатель");
        var retailBuyer = Party(RetailBuyerId, "Розничный покупатель", "Покупатель");

        // ── sales and returns: one document per branch per day ──
        foreach (var day in x.Orders.GroupBy(o => (Day: DateOnly.FromDateTime(o.CompletedLocal), o.LocationId))
                     .OrderBy(g => g.Key.Day).ThenBy(g => g.First().Branch))
        {
            var branch = day.First().Branch;
            var stamp = day.Key.Day.ToString("yyyyMMdd", Inv);
            var loc = day.Key.LocationId.ToString("N")[..8].ToUpperInvariant();

            static List<XElement> Lines(IEnumerable<OrderRow> orders)
                => orders.SelectMany(o => o.Lines)
                    .GroupBy(l => ProductId(l.MenuItemId, l.VariantId))
                    .Select(g => Line(g.Key, ProductName(g.Last().Name, g.Last().Size), null, g.Sum(l => l.Quantity), g.Sum(l => l.Amount)))
                    .ToList();

            var sold = day.ToList();
            var payments = sold.SelectMany(o => o.Payments).GroupBy(p => p.Method)
                .OrderBy(g => MethodRank(g.Key))
                .Select(g => Requisite("Оплата: " + AccountingSpreadsheet.Method(g.Key, "ru"), Money(g.Sum(p => p.Amount))));
            documents.Add(Document(
                $"yurt-sale-{stamp}-{day.Key.LocationId:N}", $"RS-{stamp}-{loc}", day.Key.Day, "Отпуск товара", "Продавец",
                sold.Sum(o => o.Lines.Sum(l => l.Amount)), [cafeSeller, retailBuyer],
                $"Розничные продажи за {Day(day.Key.Day)}, {branch}. Заказов: {sold.Count}.",
                Lines(sold),
                [
                    Requisite("Вид документа", "Отчет о розничных продажах"),
                    Requisite("Склад", branch),
                    Requisite("Сумма до скидок", Money(sold.Sum(o => o.SalesBeforeDiscounts))),
                    Requisite("Скидки", Money(sold.Sum(o => o.Discount))),
                    Requisite("Оплачено баллами", Money(sold.Sum(o => o.Points))),
                    .. payments,
                ]));

            var refunded = sold.Where(o => o.Refunded).ToList();
            if (refunded.Count == 0) continue;
            var refundPayments = refunded.SelectMany(o => o.Payments).GroupBy(p => p.Method)
                .Select(g => Requisite("Возврат: " + AccountingSpreadsheet.Method(g.Key, "ru"), Money(g.Sum(p => p.Amount))));
            documents.Add(Document(
                $"yurt-return-{stamp}-{day.Key.LocationId:N}", $"RR-{stamp}-{loc}", day.Key.Day, "Возврат товара", "Продавец",
                refunded.Sum(o => o.Lines.Sum(l => l.Amount)), [cafeSeller, retailBuyer],
                $"Возвраты за {Day(day.Key.Day)}, {branch}. Заказы: {string.Join(", ", refunded.Select(o => o.Number))}.",
                Lines(refunded),
                [Requisite("Вид документа", "Возврат товаров от покупателя"), Requisite("Склад", branch), .. refundPayments]));
        }

        // ── supplier deliveries: one document per invoice ──
        foreach (var p in x.Deliveries.GroupBy(d => d.PurchaseId))
        {
            var first = p.First();
            var number = string.IsNullOrWhiteSpace(first.Number) ? $"P-{first.PurchaseId.ToString("N")[..8].ToUpperInvariant()}" : first.Number!;
            var requisites = new List<XElement>
            {
                Requisite("Вид документа", "Поступление товаров"),
                Requisite("Склад", first.Branch),
                Requisite("Оплачено", first.Paid ? "Да" : "Нет"),
            };
            if (first.PaidLocal is { } paid) requisites.Add(Requisite("Дата оплаты", paid.ToString("yyyy-MM-dd", Inv)));
            if (!string.IsNullOrWhiteSpace(first.SupplierBin)) requisites.Add(Requisite("БИН поставщика", first.SupplierBin!));
            documents.Add(Document(
                $"yurt-purchase-{first.PurchaseId}", number, first.Date, "Отпуск товара", "Покупатель",
                p.Sum(d => d.Sum ?? 0m),
                [Party(first.SupplierId.ToString(), first.Supplier, "Продавец", first.SupplierBin), cafeBuyer],
                $"Поступление от поставщика {first.Supplier}, {first.Branch}.",
                p.Select(d => Line(d.IngredientId.ToString(), d.Ingredient, d.Unit, d.Quantity, d.Sum ?? 0m)),
                requisites));
        }

        // ── write-offs and stocktake differences: per branch per day ──
        foreach (var g in x.StockLosses
                     .GroupBy(l => (l.Date, l.LocationId, Surplus: l.Type == StockLossTypes.StocktakeSurplus))
                     .OrderBy(g => g.Key.Date).ThenBy(g => g.First().Branch))
        {
            var branch = g.First().Branch;
            var stamp = g.Key.Date.ToString("yyyyMMdd", Inv);
            var loc = g.Key.LocationId.ToString("N")[..8].ToUpperInvariant();
            var kind = g.Key.Surplus ? "Оприходование товаров" : "Списание товаров";
            var lines = g.GroupBy(l => (l.ItemId, l.Type, l.Reason))
                .Select(i =>
                {
                    var qty = Math.Abs(i.Sum(l => l.Quantity));
                    var sum = Math.Abs(i.Sum(l => l.Sum ?? 0m));
                    var line = Line(i.Key.ItemId.ToString(), i.First().Item, i.First().Kind == "dish" ? null : i.First().Unit, qty, sum);
                    line.Add(new XElement("ЗначенияРеквизитов", Requisite("Причина", LossReason(i.Key.Type, i.Key.Reason))));
                    return line;
                })
                .ToList();
            documents.Add(Document(
                $"yurt-{(g.Key.Surplus ? "surplus" : "writeoff")}-{stamp}-{g.Key.LocationId:N}",
                $"{(g.Key.Surplus ? "SP" : "WO")}-{stamp}-{loc}", g.Key.Date, "Другое", "Продавец",
                Math.Abs(g.Sum(l => l.Sum ?? 0m)), [cafeSeller],
                $"{kind} за {Day(g.Key.Date)}, {branch}.",
                lines,
                [Requisite("Вид документа", kind), Requisite("Склад", branch)]));
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("КоммерческаяИнформация",
                new XAttribute("ВерсияСхемы", "2.10"),
                new XAttribute("ДатаФормирования", createdLocal.ToString("yyyy-MM-ddTHH:mm:ss", Inv)),
                new XElement("Классификатор",
                    new XElement("Ид", classifierId),
                    new XElement("Наименование", $"Yurt: {x.CafeName}"),
                    new XElement(owner)),
                new XElement("Каталог",
                    new XAttribute("СодержитТолькоИзменения", "false"),
                    new XElement("Ид", classifierId),
                    new XElement("ИдКлассификатора", classifierId),
                    new XElement("Наименование", $"Yurt: {x.CafeName}"),
                    new XElement(owner),
                    new XElement("Товары", products.Values)),
                documents));

        using var ms = new MemoryStream();
        using (var w = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
            doc.Save(w);
        return ms.ToArray();
    }

    private static int MethodRank(string method)
    {
        var i = AccountingSpreadsheet.MethodOrder.ToList().IndexOf(method);
        return i < 0 ? 99 : i;
    }

    private static string LossReason(string type, string? reason) => type switch
    {
        StockLossTypes.StocktakeShortage => "Недостача по инвентаризации",
        StockLossTypes.StocktakeSurplus => "Излишек по инвентаризации",
        _ => reason switch
        {
            "spilled" => "Пролито",
            "expired" => "Истёк срок годности",
            "staff" => "Напиток персоналу",
            "broken" => "Испорчено",
            _ => "Другое",
        },
    };
}

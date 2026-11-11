using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>
/// The accountant's export: one Excel file with sales by day (split by payment method), orders, dishes sold, shifts,
/// deliveries and write-offs, and the same period as a 1C exchange file. Days follow the cafe's local time; CEO only.
/// </summary>
[Collection("Integration")]
public class AccountingExportTests(YurtWebAppFactory factory)
{
    private async Task<HttpClient> ClientAsync(AdminRole role)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client, $"acc-{id}", "Acc@123456", role: role));
        return client;
    }

    /// <summary>Local time in the cafe (UTC+5) as UTC.</summary>
    private static DateTime Local(DateOnly day, int hour, int minute = 0)
        => day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc).AddHours(-5);

    private sealed record Seeded(Guid Location, string Branch, DateOnly Day, Guid Latte, Guid Cake, Guid Milk);

    private async Task<Seeded> SeedAsync()
    {
        var day = new DateOnly(2300 + Random.Shared.Next(0, 600), Random.Shared.Next(1, 13), 10);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var branch = $"Acc branch {Guid.NewGuid():N}"[..20];
        var location = new Location { Name = branch, Address = "x", WorkingHours = "", ContactPhone = "", IsActive = true };
        var customer = new CustomerUser { MobileNumber = $"+7{Random.Shared.NextInt64(7000000000, 7999999999)}", PinHash = "x", FirstName = "A", LastName = "B" };
        var latte = Guid.NewGuid();
        var cake = Guid.NewGuid();
        var milk = Guid.NewGuid();
        var category = await db.MenuCategories.FirstAsync();
        db.MenuItems.AddRange(
            new MenuItem { Id = latte, CategoryId = category.Id, Name = "Acc latte", Description = "t", Price = 500m, IsAvailable = true },
            new MenuItem { Id = cake, CategoryId = category.Id, Name = "Acc cake", Description = "t", Price = 1000m, IsAvailable = true });

        // A: cash, at 01:00 local, which is still the day before in UTC.
        var a = new Order
        {
            CustomerUser = customer, Location = location, Status = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid,
            PaymentMethod = PaymentMethod.Cash, Subtotal = 1000m, Total = 1000m, CompletedAt = Local(day, 1),
        };
        a.Items.Add(new OrderItem { MenuItemId = latte, MenuItemName = "Acc latte", Quantity = 2, UnitPrice = 500m, LineTotal = 1000m, UnitCost = 150m });
        // B: 10 % off, 300 in points, the rest split 500 cash + 1000 card.
        var b = new Order
        {
            CustomerUser = customer, Location = location, Status = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid,
            PaymentMethod = PaymentMethod.Cash, Subtotal = 2000m, DiscountAmount = 200m, Total = 1800m, LoyaltyPointsSpent = 300m,
            CompletedAt = Local(day, 12),
        };
        b.Items.Add(new OrderItem { MenuItemId = latte, MenuItemName = "Acc latte", Quantity = 1, UnitPrice = 1000m, LineTotal = 1000m, UnitCost = 150m });
        b.Items.Add(new OrderItem { MenuItemId = cake, MenuItemName = "Acc cake", Quantity = 1, UnitPrice = 1000m, LineTotal = 1000m });
        // C: Kaspi, refunded.
        var c = new Order
        {
            CustomerUser = customer, Location = location, Status = OrderStatus.Completed, PaymentStatus = PaymentStatus.Refunded,
            PaymentMethod = PaymentMethod.KaspiBank, Subtotal = 500m, Total = 500m, CompletedAt = Local(day, 13),
        };
        c.Items.Add(new OrderItem { MenuItemId = latte, MenuItemName = "Acc latte", Quantity = 1, UnitPrice = 500m, LineTotal = 500m, UnitCost = 150m });
        // Declined and next-day orders are not in the export.
        var declined = new Order { CustomerUser = customer, Location = location, Status = OrderStatus.Declined, Subtotal = 9m, Total = 9m, CompletedAt = Local(day, 14) };
        var nextDay = new Order
        {
            CustomerUser = customer, Location = location, Status = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid,
            PaymentMethod = PaymentMethod.Cash, Subtotal = 7m, Total = 7m, CompletedAt = Local(day.AddDays(1), 0, 30),
        };
        db.Orders.AddRange(a, b, c, declined, nextDay);
        db.OrderPayments.AddRange(
            new OrderPayment { Order = b, Method = PaymentMethod.Cash, Amount = 500m },
            new OrderPayment { Order = b, Method = PaymentMethod.Card, Amount = 1000m });

        var shift = new Shift
        {
            Location = location, OpenedAt = Local(day, 8), OpenedBy = "anna", OpeningCash = 10000m,
            ClosedAt = Local(day, 20), ClosedBy = "anna", CountedCash = 11400m,
        };
        shift.Movements.Add(new CashMovement { Amount = 100m, IsOut = true, Reason = "bank", CreatedBy = "anna" });
        db.Shifts.Add(shift);

        var supplier = new Supplier { Name = $"Acc Dairy {Guid.NewGuid():N}"[..20], Bin = "123456789012" };
        var posted = new Purchase
        {
            Supplier = supplier, Location = location, Status = PurchaseStatuses.Posted, Number = "INV-7",
            Date = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), Total = 600m, CreatedBy = "anna",
        };
        posted.Lines.Add(new PurchaseLine { IngredientId = milk, IngredientName = "Acc milk", Unit = "ml", Quantity = 1000m, Total = 600m });
        var cancelled = new Purchase
        {
            Supplier = supplier, Location = location, Status = PurchaseStatuses.Cancelled, Number = "INV-8",
            Date = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), Total = 999m, CreatedBy = "anna",
        };
        cancelled.Lines.Add(new PurchaseLine { IngredientId = milk, IngredientName = "Acc milk", Unit = "ml", Quantity = 5m, Total = 999m });
        db.Purchases.AddRange(posted, cancelled);

        db.StockChanges.AddRange(
            new StockChange
            {
                Location = location, Kind = "ingredient", ItemId = milk, ItemName = "Acc milk", Unit = "ml", OldQuantity = 1000m,
                NewQuantity = 800m, Type = StockChangeTypes.WriteOff, Reason = WriteOffReasons.Spilled, UnitCost = 0.6m,
                ChangedBy = "anna", CreatedAt = Local(day, 15),
            },
            new StockChange
            {
                Location = location, Kind = "ingredient", ItemId = milk, ItemName = "Acc milk", Unit = "ml", OldQuantity = 10m,
                NewQuantity = 12m, Type = StockChangeTypes.Stocktake, UnitCost = 5m, ChangedBy = "anna", CreatedAt = Local(day, 16),
            });
        await db.SaveChangesAsync();
        return new Seeded(location.Id, branch, day, latte, cake, milk);
    }

    /// <summary>Each row of one sheet as cell texts (numbers as written), keyed by column letter.</summary>
    private static List<Dictionary<string, string>> Sheet(ZipArchive zip, int number)
    {
        using var reader = new StreamReader(zip.GetEntry($"xl/worksheets/sheet{number}.xml")!.Open());
        var xml = XDocument.Parse(reader.ReadToEnd());
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return xml.Descendants(ns + "row").Select(r => r.Elements(ns + "c").ToDictionary(
            c => Regex.Replace((string)c.Attribute("r")!, @"\d", ""),
            c => c.Element(ns + "v") is { } v
                // Numbers are compared without trailing zeros (database sums come back as 3500.00).
                ? decimal.Parse(v.Value, CultureInfo.InvariantCulture).ToString("0.###", CultureInfo.InvariantCulture)
                : string.Concat(c.Descendants(ns + "t").Select(t => t.Value)))).ToList();
    }

    [Fact]
    public async Task Excel_HasSalesByDayWithMethods_OrdersItemsShiftsDeliveriesAndWriteOffs()
    {
        var s = await SeedAsync();
        var ceo = await ClientAsync(AdminRole.Ceo);
        var day = s.Day.ToString("yyyy-MM-dd");

        var response = await ceo.GetAsync($"/api/v1/admin/accounting/excel?from={day}&to={day}&locationId={s.Location}&lang=en");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var zip = new ZipArchive(await response.Content.ReadAsStreamAsync());
        var workbook = new StreamReader(zip.GetEntry("xl/workbook.xml")!.Open()).ReadToEnd();
        foreach (var name in new[] { "Sales by day", "Orders", "Items sold", "Shifts", "Deliveries", "Write-offs" })
            Assert.Contains($"name=\"{name}\"", workbook);

        // Sales by day: one row for the day (the 01:00 order counts on this local day; next day and declined left out).
        var sales = Sheet(zip, 1);
        var head = sales[0];
        string Col(string title) => head.Single(h => h.Value == title).Key;
        var row = sales[1];
        Assert.Equal(day, row["A"]);
        Assert.Equal(s.Branch, row["B"]);
        Assert.Equal("3", row[Col("Orders")]);
        Assert.Equal("3500", row[Col("Sales before discounts")]);
        Assert.Equal("200", row[Col("Discounts")]);
        Assert.Equal("300", row[Col("Paid with points")]);
        Assert.Equal("3000", row[Col("Paid")]);
        Assert.Equal("1500", row[Col("Cash")]);
        Assert.Equal("1000", row[Col("Card")]);
        Assert.Equal("500", row[Col("Kaspi")]);
        Assert.Equal("500", row[Col("Refunds")]);
        Assert.Equal("2500", row[Col("Money taken")]);
        Assert.Equal("Total", sales[2]["A"]);
        Assert.Equal(3, sales.Count);

        // Orders: three, with the split payment written out.
        var orders = Sheet(zip, 2);
        Assert.Equal(4, orders.Count);
        Assert.Contains(orders, o => o.GetValueOrDefault("I") == "Cash 500; Card 1000");
        Assert.Contains(orders, o => o.GetValueOrDefault("J") == "Yes");

        // Items sold (refunded order left out): latte 2 + 1 for 1000 + 900; cake has no saved cost.
        var items = Sheet(zip, 3);
        var latte = items.Single(i => i.GetValueOrDefault("A") == "Acc latte");
        Assert.Equal("3", latte["C"]);
        Assert.Equal("1900", latte["D"]);
        Assert.Equal("450", latte["E"]);
        var cake = items.Single(i => i.GetValueOrDefault("A") == "Acc cake");
        Assert.Equal("900", cake["D"]);
        Assert.False(cake.ContainsKey("E"));

        // Shift: cash sales are B's 500 cash part; 10000 + 500 − 100 expected, 11400 counted.
        var shifts = Sheet(zip, 4);
        Assert.Equal(2, shifts.Count);
        Assert.Equal("500", shifts[1]["I"]);
        Assert.Equal("10400", shifts[1]["L"]);
        Assert.Equal("1000", shifts[1]["N"]);

        // Deliveries: the posted invoice only.
        var deliveries = Sheet(zip, 5);
        Assert.Equal(3, deliveries.Count);
        Assert.Equal("INV-7", deliveries[1]["B"]);
        Assert.Equal("123456789012", deliveries[1]["D"]);
        Assert.Equal("600", deliveries[1]["I"]);

        // Write-offs: 200 ml spilled (120) and 2 found extra at a stocktake (−10).
        var losses = Sheet(zip, 6);
        Assert.Equal(4, losses.Count);
        Assert.Equal("200", losses[1]["E"]);
        Assert.Equal("120", losses[1]["J"]);
        Assert.Equal("-2", losses[2]["E"]);
        Assert.Equal("110", losses[3]["J"]);
    }

    [Fact]
    public async Task OneC_HasCatalogueAndSalesReturnDeliveryAndWriteOffDocuments()
    {
        var s = await SeedAsync();
        var ceo = await ClientAsync(AdminRole.Ceo);
        var day = s.Day.ToString("yyyy-MM-dd");

        var response = await ceo.GetAsync($"/api/v1/admin/accounting/1c?from={day}&to={day}&locationId={s.Location}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = xml.Root!;
        Assert.Equal("КоммерческаяИнформация", root.Name.LocalName);
        Assert.Equal("2.10", (string)root.Attribute("ВерсияСхемы")!);

        var products = root.Element("Каталог")!.Element("Товары")!.Elements("Товар").Select(p => (string)p.Element("Ид")!).ToList();
        Assert.Contains(s.Latte.ToString(), products);
        Assert.Contains(s.Cake.ToString(), products);
        Assert.Contains(s.Milk.ToString(), products);

        var docs = root.Elements("Документ").ToList();
        static string Kind(XElement d) => d.Element("ЗначенияРеквизитов")!.Elements("ЗначениеРеквизита")
            .First(r => (string)r.Element("Наименование")! == "Вид документа").Element("Значение")!.Value;
        static string Req(XElement d, string name) => d.Element("ЗначенияРеквизитов")!.Elements("ЗначениеРеквизита")
            .Single(r => (string)r.Element("Наименование")! == name).Element("Значение")!.Value;

        var sale = docs.Single(d => Kind(d) == "Отчет о розничных продажах");
        Assert.Equal(day, (string)sale.Element("Дата")!);
        Assert.Equal("Отпуск товара", (string)sale.Element("ХозОперация")!);
        Assert.Equal("3300.00", (string)sale.Element("Сумма")!);   // 1000 + 1800 + 500, points included
        Assert.Equal("1500.00", Req(sale, "Оплата: Наличные"));
        Assert.Equal("1000.00", Req(sale, "Оплата: Карта"));
        Assert.Equal("500.00", Req(sale, "Оплата: Kaspi"));
        Assert.Equal("300.00", Req(sale, "Оплачено баллами"));
        Assert.Equal(s.Branch, Req(sale, "Склад"));
        var lines = sale.Element("Товары")!.Elements("Товар").ToList();
        Assert.Equal(3300m, lines.Sum(l => decimal.Parse((string)l.Element("Сумма")!, CultureInfo.InvariantCulture)));

        var ret = docs.Single(d => Kind(d) == "Возврат товаров от покупателя");
        Assert.Equal("Возврат товара", (string)ret.Element("ХозОперация")!);
        Assert.Equal("500.00", (string)ret.Element("Сумма")!);

        var delivery = docs.Single(d => Kind(d) == "Поступление товаров");
        Assert.Equal("INV-7", (string)delivery.Element("Номер")!);
        Assert.Equal("Покупатель", (string)delivery.Element("Роль")!);
        Assert.Equal("123456789012", (string)delivery.Descendants("ИНН").Single());
        Assert.Equal("600.00", (string)delivery.Element("Сумма")!);
        Assert.Equal("мл", (string)delivery.Descendants("БазоваяЕдиница").First());

        Assert.Equal("120.00", (string)docs.Single(d => Kind(d) == "Списание товаров").Element("Сумма")!);
        Assert.Equal("10.00", (string)docs.Single(d => Kind(d) == "Оприходование товаров").Element("Сумма")!);
        Assert.Equal(5, docs.Count);
    }

    [Fact]
    public async Task OnlyTheCeo_AndTheRangeIsChecked()
    {
        var admin = await ClientAsync(AdminRole.Admin);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.GetAsync("/api/v1/admin/accounting/excel?from=2026-11-01&to=2026-11-30")).StatusCode);

        var ceo = await ClientAsync(AdminRole.Ceo);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await ceo.GetAsync("/api/v1/admin/accounting/excel?from=2026-11-30&to=2026-11-01")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await ceo.GetAsync("/api/v1/admin/accounting/1c?from=2025-01-01&to=2026-11-01")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await ceo.GetAsync("/api/v1/admin/accounting/excel?from=nov&to=2026-11-01")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await ceo.GetAsync($"/api/v1/admin/accounting/excel?from=2026-11-01&to=2026-11-02&locationId={Guid.NewGuid()}")).StatusCode);

        // An empty period still gives a valid file.
        var empty = await ceo.GetAsync("/api/v1/admin/accounting/excel?from=2199-01-01&to=2199-01-02&lang=ru");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        using var zip = new ZipArchive(await empty.Content.ReadAsStreamAsync());
        Assert.Contains("Продажи по дням", new StreamReader(zip.GetEntry("xl/workbook.xml")!.Open()).ReadToEnd());
    }
}

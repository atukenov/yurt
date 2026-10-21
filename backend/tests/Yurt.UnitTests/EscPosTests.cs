using System.Text;
using Yurt.Application.Features.Printing;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

/// <summary>ESC/POS bytes for network printers (own_pos, Phase 6).</summary>
public class EscPosTests
{
    private static readonly Encoding Cp866;

    static EscPosTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp866 = Encoding.GetEncoding(866);
    }

    [Fact]
    public void StartsWithResetAndCharacterTable_AndEndsWithCut()
    {
        var bytes = new EscPos(32, 17).Line("Привет").Cut().ToArray();
        Assert.Equal(new byte[] { 0x1B, (byte)'@', 0x1B, (byte)'t', 17 }, bytes[..5]);
        Assert.Equal(new byte[] { 0x1D, (byte)'V', 66, 0 }, bytes[^4..]);
        Assert.Contains("Привет", Cp866.GetString(bytes));
    }

    [Fact]
    public void DrawerJob_OnlyOpensTheDrawer_AndTestPageOpensItWhenTicked()
    {
        var kick = new byte[] { 0x1B, (byte)'p', 0, 25, 250, 0x1B, (byte)'p', 1, 25, 250 };
        var printer = new Printer { Name = "Till", Kind = Printer.ReceiptKind, LineWidth = 48, CodeTable = 17 };

        var drawer = PrintLayouts.Drawer(printer);
        Assert.Equal(kick, drawer[5..]); // after reset and character table: no text, no cut, no paper used

        Assert.False(Contains(PrintLayouts.Test(printer), kick));
        printer.CashDrawer = true;
        Assert.True(Contains(PrintLayouts.Test(printer), kick));
    }

    private static bool Contains(byte[] bytes, byte[] part)
    {
        for (var i = 0; i + part.Length <= bytes.Length; i++)
            if (bytes.AsSpan(i, part.Length).SequenceEqual(part)) return true;
        return false;
    }

    [Fact]
    public void KazakhLettersAndTengeSign_BecomeCloseLettersInCodePage866()
    {
        Assert.Equal("Кофе тг, ау кон", EscPos.Clean("Кофе ₸, әү қөң"));
        var text = Cp866.GetString(new EscPos(48, 17).Line("Қымыз ₸").ToArray());
        Assert.Contains("Кымыз тг", text);
        Assert.DoesNotContain("?", text);
    }

    [Fact]
    public void LongTextWraps_AndColumnsKeepTheValueOnTheRight()
    {
        Assert.Equal(["one two", "three"], EscPos.Wrap("one two three", 8));
        Assert.Equal(["abcde", "fgh"], EscPos.Wrap("abcdefgh", 5));

        var lines = Cp866.GetString(new EscPos(20, 17).Columns("2 x Very long cappuccino name", "1 200").ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)[^2..];
        Assert.All(lines, l => Assert.True(l.Length <= 20));
        Assert.EndsWith("1 200", lines[^1]);
        Assert.Equal(20, lines[^1].Length);
    }

    [Fact]
    public void Ticket_HasItemsNotesAndNoPrices_Receipt_HasTotalsAndSplitParts()
    {
        var printer = new Printer { Name = "Kitchen", LineWidth = 32, CodeTable = 17, Language = "ru" };
        var item = new OrderItem
        {
            MenuItemName = "Латте", Quantity = 2, LineTotal = 2400, VariantLabel = "L", Notes = "без сахара",
            Toppings = [new OrderItemTopping { ToppingName = "Сироп" }],
        };
        var order = new Order
        {
            Items = [item], Subtotal = 2400, Total = 2400, CreatedAt = new DateTime(2026, 10, 21, 7, 5, 0, DateTimeKind.Utc),
            PaymentMethod = PaymentMethod.Cash, Location = new Location { Name = "Центр" },
        };

        var ticket = Cp866.GetString(PrintLayouts.Ticket(printer, order, order.Items.ToList(), "Бар"));
        Assert.Contains("2 x Латте", ticket);
        Assert.Contains("L, + Сироп", ticket);
        Assert.Contains("! без сахара", ticket);
        Assert.Contains("Бар", ticket);
        Assert.Contains("12:05", ticket); // Almaty time, UTC+5
        Assert.DoesNotContain("2 400", ticket);

        var receipt = Cp866.GetString(PrintLayouts.Receipt(printer, order, "Yurt", [
            new OrderPayment { Method = PaymentMethod.Cash, Amount = 1000 },
            new OrderPayment { Method = PaymentMethod.Card, Amount = 1400 },
        ]));
        Assert.Contains("Итого", receipt);
        Assert.Contains("2 400 тг", receipt);
        Assert.Contains("Наличные", receipt);
        Assert.Contains("1 400 тг", receipt);
        Assert.Contains("Центр", receipt);
    }

    [Fact]
    public void QrCode_StoresTheLinkWithItsLength_ThenPrintsIt()
    {
        var url = new string('a', 300);
        var bytes = new EscPos(48, 17).QrCode(url, 6).ToArray()[5..];

        Assert.Equal(new byte[] { 0x1D, (byte)'(', (byte)'k', 4, 0, 49, 65, 50, 0 }, bytes[..9]);
        Assert.Equal(new byte[] { 0x1D, (byte)'(', (byte)'k', 3, 0, 49, 67, 6 }, bytes[9..17]);
        // Store: length is the link plus 3, low byte first (303 = 47 + 1 * 256).
        Assert.Equal(new byte[] { 0x1D, (byte)'(', (byte)'k', 47, 1, 49, 80, 48 }, bytes[25..33]);
        Assert.Equal(url, Encoding.ASCII.GetString(bytes[33..(33 + url.Length)]));
        Assert.Equal(new byte[] { 0x1D, (byte)'(', (byte)'k', 3, 0, 49, 81, 48, 0x0A }, bytes[^9..]);
    }

    [Fact]
    public void Receipt_PrintsTheTaxReceiptQr_OnlyWhenItWasSent()
    {
        var printer = new Printer { Name = "Till", LineWidth = 32, CodeTable = 17, Language = "ru" };
        var order = new Order
        {
            Items = [new OrderItem { MenuItemName = "Латте", Quantity = 1, LineTotal = 1200 }], Subtotal = 1200, Total = 1200,
            PaymentMethod = PaymentMethod.Card, Location = new Location { Name = "Центр" },
        };
        byte[] qr = [0x1D, (byte)'(', (byte)'k', 3, 0, 49, 81, 48];

        var plain = PrintLayouts.Receipt(printer, order, "Yurt", []);
        Assert.False(Contains(plain, qr));
        Assert.DoesNotContain("Фискальный чек", Cp866.GetString(plain));
        Assert.Equal(plain, PrintLayouts.Receipt(printer, order, "Yurt", [], new FiscalReceipt { TicketUrl = null }));

        var withQr = PrintLayouts.Receipt(printer, order, "Yurt", [],
            new FiscalReceipt { TicketUrl = "https://ofd.example/t/1", CheckNumber = "777" });
        Assert.True(Contains(withQr, qr));
        var text = Cp866.GetString(withQr);
        Assert.Contains("Фискальный чек", text);
        Assert.Contains("N 777", text);
        Assert.Contains("https://ofd.example/t/1", text);
    }

    [Theory]
    [InlineData("https://ofd.example/t/1", "https://ofd.example/t/1")]
    [InlineData("  http://consumer.oofd.kz?i=1  ", "http://consumer.oofd.kz?i=1")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("ofd.example/t/1", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void FiscalLinks_KeepOnlyWebAddresses(string? url, string? expected)
        => Assert.Equal(expected, Yurt.Application.Features.Fiscal.FiscalLinks.Safe(url));
}

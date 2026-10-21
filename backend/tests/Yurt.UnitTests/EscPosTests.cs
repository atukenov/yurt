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
}

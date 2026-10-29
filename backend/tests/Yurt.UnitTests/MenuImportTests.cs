using System.Text;
using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Menu.Import;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

/// <summary>Menu import from a spreadsheet: reading .xlsx/.csv, checking rows and saving dishes with sizes.</summary>
public class MenuImportTests
{
    private static (MenuImportService svc, IApplicationDbContext db) Build(
        List<MenuCategory>? categories = null, List<MenuItem>? items = null)
    {
        var db = Substitute.For<IApplicationDbContext>();
        // Build the sets first: building one inside Returns() confuses NSubstitute.
        var catSet = (categories ?? []).BuildMockDbSet();
        var itemSet = (items ?? []).BuildMockDbSet();
        var variantSet = new List<MenuItemVariant>().BuildMockDbSet();
        db.MenuCategories.Returns(catSet);
        db.MenuItems.Returns(itemSet);
        db.MenuItemVariants.Returns(variantSet);
        return (new MenuImportService(db, Substitute.For<IAuditLogService>(), Substitute.For<IMenuCacheService>()), db);
    }

    private static Stream Csv(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("kk")]
    public async Task Template_ReadsBackCleanly(string lang)
    {
        var (svc, _) = Build();
        var result = await svc.ImportAsync(new MemoryStream(MenuImportService.Template(lang)), "menu.xlsx", apply: false);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.CategoriesNew);
        Assert.Equal(2, result.ItemsNew);
        Assert.Equal(2, result.Sizes);
        Assert.False(result.Applied);
        var cappuccino = result.Items[0];
        Assert.Equal(1200m, cappuccino.Price);
        Assert.Equal(2, cappuccino.Sizes.Count);
    }

    [Fact]
    public void Reader_KeepsExcelRowNumbersAndColumns()
    {
        var bytes = SpreadsheetWriter.Write("S", [["a", null, "c"], [], ["x", 5m]]);
        var rows = SpreadsheetReader.Read(new MemoryStream(bytes), "f.xlsx");
        Assert.Equal(["a", "", "c"], rows[0]);
        Assert.Equal(["x", "5"], rows[2]);
    }

    [Fact]
    public async Task RussianCsvWithSemicolons_AndOnlyRussianNames_IsRead()
    {
        var (svc, _) = Build();
        var csv = "Категория;Название;Цена\nКофе;Латте;\"1 200 ₸\"\nЧай;Зелёный чай;800,50\n";
        var result = await svc.ImportAsync(Csv(csv), "menu.csv", apply: false);

        Assert.Empty(result.Errors);
        Assert.Equal(1200m, result.Items[0].Price);
        Assert.Equal(800.50m, result.Items[1].Price);
        Assert.Equal("Латте", result.Items[0].Name);
    }

    [Fact]
    public async Task MissingPriceColumn_IsReportedForTheWholeFile()
    {
        var (svc, _) = Build();
        var result = await svc.ImportAsync(Csv("Category,Name\nCoffee,Latte\n"), "menu.csv", apply: true);

        var error = Assert.Single(result.Errors);
        Assert.Equal(("missingColumns", "price", 0), (error.Code, error.Value, error.Row));
        Assert.False(result.Applied);
    }

    [Fact]
    public async Task RowProblems_AreReportedByRow_AndNothingIsSaved()
    {
        var (svc, db) = Build();
        var csv = "Category,Name,Price,Size\n" +
                  "Coffee,Latte,abc,\n" +            // row 2: bad price
                  "Coffee,,500,\n" +                  // row 3: no name
                  "Coffee,Mocha,900,\n" +
                  "Coffee,Mocha,1000,Large\n" +       // row 5: sizes on some rows only
                  "Tea,Green,700,\n";
        var result = await svc.ImportAsync(Csv(csv), "menu.csv", apply: true);

        Assert.Equal([(2, "badPrice"), (3, "noName"), (5, "mixedSizes")], result.Errors.Select(e => (e.Row, e.Code)));
        Assert.False(result.Applied);
        db.MenuItems.DidNotReceiveWithAnyArgs().Add(default!);
        await db.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Apply_CreatesCategoriesDishesAndSizes()
    {
        var (svc, db) = Build();
        var csv = "Category,Name,Price,Size,Name RU\nCoffee,Latte,1000,Small,Латте\nCoffee,Latte,1300,Large,Латте\nTea,Green,700,,Зелёный\n";
        var result = await svc.ImportAsync(Csv(csv), "menu.csv", apply: true);

        Assert.True(result.Applied);
        db.MenuCategories.Received(2).Add(Arg.Any<MenuCategory>());
        db.MenuItems.Received(1).Add(Arg.Is<MenuItem>(i => i.Name == "Latte" && i.NameRu == "Латте" && i.Price == 1000m));
        db.MenuItems.Received(1).Add(Arg.Is<MenuItem>(i => i.Name == "Green" && i.Price == 700m));
        db.MenuItemVariants.Received(1).Add(Arg.Is<MenuItemVariant>(v => v.Label == "Small" && v.IsDefault && v.Price == 1000m));
        db.MenuItemVariants.Received(1).Add(Arg.Is<MenuItemVariant>(v => v.Label == "Large" && !v.IsDefault && v.Price == 1300m));
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_UpdatesAnExistingDish_InsteadOfAddingASecond()
    {
        var cat = new MenuCategory { Name = "Coffee" };
        var latte = new MenuItem { CategoryId = cat.Id, Category = cat, Name = "Latte", Price = 900m };
        var (svc, db) = Build([cat], [latte]);

        var result = await svc.ImportAsync(Csv("Category,Name,Price\ncoffee,LATTE,1100\n"), "menu.csv", apply: true);

        Assert.Equal((0, 1, 0, 1), (result.CategoriesNew, result.CategoriesExisting, result.ItemsNew, result.ItemsUpdated));
        Assert.Equal(1100m, latte.Price);
        db.MenuItems.DidNotReceiveWithAnyArgs().Add(default!);
        db.MenuCategories.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Theory]
    [InlineData("menu.xls", "oldXls")]
    [InlineData("menu.pdf", "wrongType")]
    [InlineData("menu.xlsx", "badFile")]
    public async Task UnreadableFiles_GetAClearCode(string name, string code)
    {
        var (svc, _) = Build();
        var result = await svc.ImportAsync(Csv("not a spreadsheet"), name, apply: false);
        Assert.Equal(code, Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData("1 200", 1200)]
    [InlineData("1200,5", 1200.5)]
    [InlineData("990 тг", 990)]
    [InlineData("₸1500", 1500)]
    public void ParsePrice_AcceptsHowPeopleTypePrices(string text, double expected)
        => Assert.Equal((decimal)expected, SpreadsheetReader.ParsePrice(text));

    [Theory]
    [InlineData("1.200.50")]
    [InlineData("free")]
    [InlineData("12/5")]
    public void ParsePrice_RejectsNonNumbers(string text)
    {
        // "free" has letters only, which are stripped, leaving nothing.
        Assert.Null(SpreadsheetReader.ParsePrice(text));
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Persistence;

/// <summary>
/// Fills one cafe with believable demo data so every admin and customer screen has something to show: branches, a
/// translated menu with sizes, stations, QR tables, ingredients with recipes and stock, suppliers and delivery invoices,
/// staff with salaries and a closed payroll month, customers with favourites and loyalty points, about two and a half
/// months of orders (paid, split, refunded, declined, rated, table and pickup orders, a few live ones right now), cash
/// shifts, stock history, monthly costs, camera-watch minutes and alerts, reports and audit entries.
///
/// It only ADDS rows and never changes or deletes what is already there, apart from filling empty translations and an
/// empty station on the seeded menu. Every part checks for its own demo rows first, so running it again adds nothing.
/// Runs on the cafe the database context is set to (the default Yurt cafe at start-up).
///
/// Turned on with SEED_DEMO_DATA=true (local docker-compose does this), or once by hand with the
/// <c>seed-demo [cafe-slug]</c> command. Never turn it on for a real cafe: the orders and customers are made up.
/// </summary>
public static class DemoDataSeeder
{
    /// <summary>Password of every demo staff account (they do not have to change it).</summary>
    public const string StaffPassword = "demo12345";

    /// <summary>PIN of every demo customer; their phones are +7 701 555 00 01 ... 30.</summary>
    public const string CustomerPin = "1234";

    public const string PhonePrefix = "+7701555";
    private const string OrderKeyPrefix = "demo-";
    private const string Seeder = "demo-seed";
    private const int HistoryDays = 75;

    private static readonly TimeZoneInfo CafeTime = TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static bool IsEnabled()
        => Environment.GetEnvironmentVariable("SEED_DEMO_DATA")?.Trim().ToLowerInvariant() is "true" or "1" or "yes";

    // ── demo menu (also the source of the ru/kk names for the menu that DbSeeder adds) ──

    private sealed record DemoCategory(string Name, string Ru, string Kk, int Sort, string Station);

    private sealed record DemoItem(
        string Category, string Name, string Ru, string Kk, string Description, string DescriptionRu, decimal Price,
        int Popularity, string Image, bool Sizes = false);

    private sealed record DemoTopping(string Name, string Ru, decimal Price, string? Group, string[] Categories);

    private static readonly DemoCategory[] Categories =
    [
        new("Coffee", "Кофе", "Кофе", 1, "Bar"),
        new("Cold Drinks", "Холодные напитки", "Салқын сусындар", 2, "Bar"),
        new("Food & Pastries", "Еда и выпечка", "Тағам және тоқаштар", 3, "Kitchen"),
    ];

    private static readonly DemoItem[] Items =
    [
        new("Coffee", "Espresso", "Эспрессо", "Эспрессо", "Rich, concentrated single-shot espresso.", "Насыщенный эспрессо, одна порция.", 2.50m, 5, "photo-1510707577719-ae7c14805e3a"),
        new("Coffee", "Americano", "Американо", "Американо", "Espresso diluted with hot water for a smooth, bold cup.", "Эспрессо с горячей водой: мягкий и крепкий.", 3.00m, 12, "photo-1551030173-122aabc4489c", Sizes: true),
        new("Coffee", "Cappuccino", "Капучино", "Капучино", "Equal parts espresso, steamed milk, and thick foam.", "Эспрессо, горячее молоко и густая пенка.", 3.75m, 14, "photo-1553361371-9b22f78e8b1d", Sizes: true),
        new("Coffee", "Flat White", "Флэт уайт", "Флэт уайт", "Double ristretto with velvety microfoam milk.", "Двойной ристретто и бархатное молоко.", 4.00m, 6, "photo-1577968897966-3d4325b36b02"),
        new("Coffee", "Latte", "Латте", "Латте", "Smooth espresso with lots of steamed milk and a small layer of foam.", "Эспрессо с большим количеством молока и лёгкой пенкой.", 4.25m, 16, "photo-1561882468-9110e03e0f78", Sizes: true),
        new("Coffee", "Mocha", "Мокко", "Мокко", "Espresso, chocolate sauce, steamed milk, and whipped cream.", "Эспрессо, шоколад, молоко и сливки.", 4.75m, 5, "photo-1572442388796-11668a67e53d"),
        new("Cold Drinks", "Iced Latte", "Айс латте", "Мұзды латте", "Chilled espresso over ice with cold milk.", "Эспрессо со льдом и холодным молоком.", 4.50m, 8, "photo-1461023058943-07fcbe16d735"),
        new("Cold Drinks", "Cold Brew", "Колд брю", "Колд брю", "Slow-steeped 18-hour cold brew concentrate.", "Кофе холодного заваривания, 18 часов.", 4.75m, 5, "photo-1517701604599-bb29b565090b"),
        new("Cold Drinks", "Matcha Latte", "Матча латте", "Матча латте", "Ceremonial-grade matcha with oat milk over ice.", "Матча с овсяным молоком и льдом.", 5.00m, 6, "photo-1536256263959-770b48d82b0a"),
        new("Cold Drinks", "Fresh Lemonade", "Лимонад", "Лимонад", "Hand-squeezed lemonade with a hint of mint.", "Свежевыжатый лимонад с мятой.", 3.50m, 5, "photo-1613478223719-2ab802602423"),
        new("Food & Pastries", "Butter Croissant", "Круассан", "Круассан", "Flaky, buttery, golden-baked classic French croissant.", "Классический слоёный круассан на сливочном масле.", 3.25m, 10, "photo-1555507036-ab1f4038808a"),
        new("Food & Pastries", "Avocado Toast", "Тост с авокадо", "Авокадо тосты", "Sourdough topped with smashed avocado, sea salt, and chilli flakes.", "Хлеб на закваске с авокадо, солью и перцем.", 6.50m, 5, "photo-1541519227354-08fa5d50c820"),
        new("Food & Pastries", "Banana Bread", "Банановый хлеб", "Банан наны", "Moist, house-baked banana bread with walnut crunch.", "Домашний банановый хлеб с грецким орехом.", 4.00m, 6, "photo-1588195538326-c5b1e9f80a1b"),
    ];

    private static readonly DemoTopping[] Toppings =
    [
        new("Extra Espresso Shot", "Доп. шот эспрессо", 0.75m, null, ["Coffee", "Cold Drinks"]),
        new("Vanilla Syrup", "Ванильный сироп", 0.50m, "syrup", ["Coffee", "Cold Drinks"]),
        new("Caramel Syrup", "Карамельный сироп", 0.50m, "syrup", ["Coffee", "Cold Drinks"]),
        new("Hazelnut Syrup", "Ореховый сироп", 0.50m, "syrup", ["Coffee", "Cold Drinks"]),
        new("Oat Milk", "Овсяное молоко", 0.75m, "milk", ["Coffee", "Cold Drinks"]),
        new("Almond Milk", "Миндальное молоко", 0.75m, "milk", ["Coffee", "Cold Drinks"]),
        new("Whipped Cream", "Взбитые сливки", 0.50m, null, ["Coffee", "Cold Drinks"]),
        new("Cinnamon", "Корица", 0.00m, null, ["Coffee"]),
        new("Extra Ice", "Больше льда", 0.00m, null, ["Cold Drinks"]),
        new("Strawberry Jam", "Клубничный джем", 0.50m, null, ["Food & Pastries"]),
        new("Poached Egg", "Яйцо пашот", 1.50m, null, ["Food & Pastries"]),
        new("Honey", "Мёд", 0.50m, null, ["Food & Pastries"]),
    ];

    private static readonly Dictionary<string, string> ToppingRu = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Soy Milk"] = "Соевое молоко", ["Chocolate Drizzle"] = "Шоколадный топпинг", ["Brown Sugar Syrup"] = "Сироп из тростникового сахара",
        ["Extra Butter"] = "Больше масла", ["Extra Avocado"] = "Больше авокадо", ["Cream Cheese"] = "Сливочный сыр",
    };

    // ── ingredients, recipes and suppliers ──

    private sealed record DemoIngredient(string Name, string Unit, decimal Cost, decimal LowAt, decimal UpTo, string Supplier, decimal Pack, string PackName);

    private static readonly DemoIngredient[] Ingredients =
    [
        new("Coffee beans", "g", 0.030m, 2000, 6000, "Arabica Trade", 1000, "1 kg bag"),
        new("Milk 3.2%", "ml", 0.0012m, 10000, 30000, "Milk Valley", 1000, "1 l carton"),
        new("Oat milk", "ml", 0.0040m, 3000, 8000, "Milk Valley", 1000, "1 l carton"),
        new("Almond milk", "ml", 0.0050m, 2000, 5000, "Milk Valley", 1000, "1 l carton"),
        new("Soy milk", "ml", 0.0035m, 1000, 4000, "Milk Valley", 1000, "1 l carton"),
        new("Whipping cream", "g", 0.0080m, 500, 2000, "Milk Valley", 500, "500 g pack"),
        new("Butter", "g", 0.0100m, 500, 2000, "Milk Valley", 500, "500 g pack"),
        new("Cream cheese", "g", 0.0120m, 400, 1500, "Milk Valley", 300, "300 g tub"),
        new("Eggs", "pcs", 0.20m, 20, 60, "Green Market", 10, "10 eggs"),
        new("Chocolate sauce", "ml", 0.0100m, 500, 2000, "Barista Supply", 1000, "1 l bottle"),
        new("Vanilla syrup", "ml", 0.0120m, 500, 2000, "Barista Supply", 1000, "1 l bottle"),
        new("Caramel syrup", "ml", 0.0120m, 500, 2000, "Barista Supply", 1000, "1 l bottle"),
        new("Hazelnut syrup", "ml", 0.0120m, 500, 2000, "Barista Supply", 1000, "1 l bottle"),
        new("Brown sugar syrup", "ml", 0.0110m, 500, 2000, "Barista Supply", 1000, "1 l bottle"),
        new("Matcha powder", "g", 0.15m, 100, 400, "Barista Supply", 100, "100 g tin"),
        new("Sugar", "g", 0.0010m, 1000, 5000, "Barista Supply", 1000, "1 kg bag"),
        new("Cinnamon", "g", 0.0200m, 50, 200, "Barista Supply", 100, "100 g jar"),
        new("Honey", "g", 0.0150m, 300, 1000, "Green Market", 500, "500 g jar"),
        new("Strawberry jam", "g", 0.0100m, 300, 1000, "Green Market", 500, "500 g jar"),
        new("Lemons", "pcs", 0.25m, 15, 50, "Green Market", 10, "1 kg net"),
        new("Mint", "g", 0.0500m, 30, 150, "Green Market", 50, "bunch 50 g"),
        new("Avocado", "pcs", 0.90m, 6, 24, "Green Market", 6, "box of 6"),
        new("Ice", "g", 0.0002m, 5000, 20000, "Green Market", 5000, "5 kg bag"),
        new("Croissant dough", "pcs", 0.60m, 20, 80, "Bakery Partner", 20, "box of 20"),
        new("Sourdough slice", "pcs", 0.30m, 20, 60, "Bakery Partner", 20, "loaf, 20 slices"),
        new("Banana bread slice", "pcs", 0.80m, 10, 40, "Bakery Partner", 10, "loaf, 10 slices"),
        new("Cup 350 ml with lid", "pcs", 0.08m, 200, 1000, "Barista Supply", 100, "sleeve of 100"),
    ];

    private static readonly Dictionary<string, (string Ingredient, decimal Qty)[]> Recipes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Espresso"] = [("Coffee beans", 9)],
        ["Americano"] = [("Coffee beans", 18), ("Cup 350 ml with lid", 1)],
        ["Cappuccino"] = [("Coffee beans", 18), ("Milk 3.2%", 150), ("Cup 350 ml with lid", 1)],
        ["Flat White"] = [("Coffee beans", 18), ("Milk 3.2%", 120), ("Cup 350 ml with lid", 1)],
        ["Latte"] = [("Coffee beans", 18), ("Milk 3.2%", 250), ("Cup 350 ml with lid", 1)],
        ["Mocha"] = [("Coffee beans", 18), ("Milk 3.2%", 200), ("Chocolate sauce", 30), ("Whipping cream", 15), ("Cup 350 ml with lid", 1)],
        ["Iced Latte"] = [("Coffee beans", 18), ("Milk 3.2%", 200), ("Ice", 150), ("Cup 350 ml with lid", 1)],
        ["Cold Brew"] = [("Coffee beans", 25), ("Ice", 150), ("Cup 350 ml with lid", 1)],
        ["Matcha Latte"] = [("Matcha powder", 4), ("Oat milk", 250), ("Ice", 150), ("Cup 350 ml with lid", 1)],
        ["Fresh Lemonade"] = [("Lemons", 1.5m), ("Sugar", 25), ("Mint", 3), ("Ice", 150), ("Cup 350 ml with lid", 1)],
        ["Butter Croissant"] = [("Croissant dough", 1)],
        ["Avocado Toast"] = [("Sourdough slice", 2), ("Avocado", 0.5m)],
        ["Banana Bread"] = [("Banana bread slice", 1)],
    };

    private static readonly Dictionary<string, (string Ingredient, decimal Qty)[]> ToppingUses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Extra Espresso Shot"] = [("Coffee beans", 9)],
        ["Vanilla Syrup"] = [("Vanilla syrup", 20)],
        ["Caramel Syrup"] = [("Caramel syrup", 20)],
        ["Hazelnut Syrup"] = [("Hazelnut syrup", 20)],
        ["Brown Sugar Syrup"] = [("Brown sugar syrup", 20)],
        ["Oat Milk"] = [("Oat milk", 200)],
        ["Almond Milk"] = [("Almond milk", 200)],
        ["Soy Milk"] = [("Soy milk", 200)],
        ["Whipped Cream"] = [("Whipping cream", 15)],
        ["Chocolate Drizzle"] = [("Chocolate sauce", 10)],
        ["Cinnamon"] = [("Cinnamon", 1)],
        ["Extra Ice"] = [("Ice", 100)],
        ["Extra Butter"] = [("Butter", 10)],
        ["Strawberry Jam"] = [("Strawberry jam", 20)],
        ["Extra Avocado"] = [("Avocado", 0.5m)],
        ["Poached Egg"] = [("Eggs", 1)],
        ["Cream Cheese"] = [("Cream cheese", 30)],
        ["Honey"] = [("Honey", 15)],
    };

    private sealed record DemoSupplier(string Name, string Contact, string Phone, string Bin, string Terms, int? PayInDays, string Note);

    private static readonly DemoSupplier[] Suppliers =
    [
        new("Arabica Trade", "Ruslan", "+77015551101", "180240012345", SupplierPaymentTerms.Transfer, null, "Roasts on Mondays, delivers Tue/Fri."),
        new("Milk Valley", "Saule", "+77015551102", "150540067890", SupplierPaymentTerms.Later, 14, "Order before 18:00 for next-morning delivery."),
        new("Bakery Partner", "Oleg", "+77015551103", "200140054321", SupplierPaymentTerms.Cash, null, "Fresh dough every morning except Sunday."),
        new("Green Market", "Aida", "+77015551104", "190840098765", SupplierPaymentTerms.Cash, null, "Fruit and herbs from Zelyony Bazaar."),
        new("Barista Supply", "Timur", "+77015551105", "170340011223", SupplierPaymentTerms.Transfer, null, "Syrups, cups, matcha. Free delivery from 100."),
    ];

    // ── people ──

    private sealed record DemoStaff(string Key, AdminRole Role, decimal Salary, string Name, string Station);

    private static readonly DemoStaff[] Staff =
    [
        new("manager", AdminRole.Admin, 1200m, "Asel", "bar"),
        new("aigerim", AdminRole.Worker, 800m, "Aigerim", "bar"),
        new("daniyar", AdminRole.Worker, 800m, "Daniyar", "bar"),
        new("madina", AdminRole.Worker, 750m, "Madina", "kitchen"),
    ];

    private static readonly (string First, string Last)[] CustomerNames =
    [
        ("Aruzhan", "Nurlanova"), ("Dias", "Seitkali"), ("Kamila", "Abenova"), ("Yerlan", "Zhaksybekov"),
        ("Alina", "Kim"), ("Nursultan", "Omarov"), ("Dana", "Tulegenova"), ("Arman", "Iskakov"),
        ("Zarina", "Mukanova"), ("Timur", "Bekov"), ("Aliya", "Sadykova"), ("Maksat", "Ospanov"),
        ("Sofia", "Petrova"), ("Ilyas", "Karimov"), ("Asem", "Baimukhanova"), ("Bekzat", "Nurpeisov"),
        ("Madina", "Alieva"), ("Ruslan", "Tokhtarov"), ("Ainur", "Kassymova"), ("Daulet", "Zhumabayev"),
        ("Elena", "Sokolova"), ("Askar", "Mamyrov"), ("Togzhan", "Yesenova"), ("Alibek", "Shaikenov"),
        ("Saltanat", "Rakhimova"), ("Nurlan", "Abdrakhmanov"), ("Diana", "Lee"), ("Olzhas", "Kenzhebekov"),
        ("Gulnara", "Saparova"), ("Viktor", "Pak"),
    ];

    /// <summary>The first guests are regulars from before the order history; the last ones joined in the last weeks.</summary>
    private const int Regulars = 20;

    private static readonly string[] RatingComments =
    [
        "Best flat white in town!", "Fast and friendly, thank you", "Очень вкусно, спасибо!", "Croissant was perfect",
        "A bit slow today but tasty", "Керемет кофе!", "Could be hotter", "Love the oat latte",
    ];

    private static readonly string[] DeclineReasons =
    [
        "Out of oat milk, sorry!", "We are closing in 10 minutes", "Coffee machine is being cleaned", "Ran out of croissants",
    ];

    // ── entry point ──

    private sealed class Demo(ApplicationDbContext db, Tenant cafe, ILogger logger)
    {
        public ApplicationDbContext Db { get; } = db;
        public Tenant Cafe { get; } = cafe;
        public ILogger Logger { get; } = logger;
        public Random Rng { get; } = new(20261111);
        public DateTime Now { get; } = DateTime.UtcNow;
        public DateOnly Today { get; } = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CafeTime));
        public string Prefix => Cafe.Slug == Tenant.DefaultSlug ? "demo." : Cafe.Slug + ".";
        public string User(string key) => Prefix + key;
        public List<Location> Branches { get; set; } = [];
        public string? PasswordHash { get; set; }

        /// <summary>A local cafe time (Asia/Almaty) on a day, in UTC.</summary>
        public DateTime At(DateOnly day, double hours)
            => TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue).AddSeconds(Math.Round(hours * 3600)), DateTimeKind.Unspecified), CafeTime);

        public double LocalHourNow => TimeZoneInfo.ConvertTimeFromUtc(Now, CafeTime).TimeOfDay.TotalHours;
        public string Month(int monthsBack) => Today.AddMonths(-monthsBack).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        public T Pick<T>(IReadOnlyList<T> list) => list[Rng.Next(list.Count)];
        public bool Chance(double p) => Rng.NextDouble() < p;
        public decimal Money(decimal v) => Math.Round(v, 2);
    }

    /// <summary>Seeds the cafe the context is currently set to. Safe to run again: it only adds what is missing.</summary>
    public static async Task SeedAsync(ApplicationDbContext db, Tenant cafe, ILogger logger, CancellationToken ct = default)
    {
        var d = new Demo(db, cafe, logger);
        logger.LogInformation("Seeding demo data for cafe '{Slug}'...", cafe.Slug);

        await BranchesAndMenuAsync(d, ct);
        await StationsAndTablesAsync(d, ct);
        await IngredientsAsync(d, ct);
        await SuppliersAndPurchasesAsync(d, ct);
        await StaffAndPayrollAsync(d, ct);
        await CustomersAsync(d, ct);
        await MarketingAsync(d, ct);
        await PrintersAsync(d, ct);
        await OrdersAndShiftsAsync(d, ct);
        await StockHistoryAsync(d, ct);
        await ExpensesAsync(d, ct);
        await StaffWatchAsync(d, ct);
        await ExtrasAsync(d, ct);

        logger.LogInformation(
            "Demo data ready for cafe '{Slug}'. Staff sign in as {Users} with password '{Password}'; customers use phones {First}..{Last} with PIN {Pin}.",
            cafe.Slug, string.Join(", ", Staff.Select(s => d.User(s.Key))), StaffPassword, PhonePrefix + "0001",
            $"{PhonePrefix}{CustomerNames.Length:0000}", CustomerPin);
    }

    // ── branches and menu ──

    private static async Task BranchesAndMenuAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        if (!await db.Locations.AnyAsync(ct))
        {
            db.Locations.AddRange(
                new Location { Name = "Demo Downtown", Address = "Abay Ave 52, Almaty", WorkingHours = "Daily 08:00–22:00", ContactPhone = "+77015550100", IsActive = true },
                new Location { Name = "Demo Riverside", Address = "Dostyk Ave 180, Almaty", WorkingHours = "Daily 08:00–21:00", ContactPhone = "+77015550200", IsActive = true });
            await db.SaveChangesAsync(ct);
        }
        d.Branches = await db.Locations.Where(l => l.IsActive).OrderBy(l => l.CreatedAt).ThenBy(l => l.Name).ToListAsync(ct);
        if (d.Branches.Count == 0)
            d.Branches = await db.Locations.OrderBy(l => l.CreatedAt).Take(1).ToListAsync(ct);

        // A map link where the branch has none.
        foreach (var b in d.Branches) b.TwoGisUrl ??= "https://2gis.kz/almaty";

        // Categories and items: add the demo ones that are missing (by name), translate the ones that have no translation.
        var categories = await db.MenuCategories.ToListAsync(ct);
        foreach (var c in Categories)
        {
            var row = categories.FirstOrDefault(x => x.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                row = new MenuCategory { Name = c.Name, SortOrder = c.Sort };
                db.MenuCategories.Add(row);
                categories.Add(row);
            }
            row.NameRu ??= c.Ru;
            row.NameKk ??= c.Kk;
        }

        var items = await db.MenuItems.Include(i => i.Variants).ToListAsync(ct);
        var sort = 0;
        foreach (var i in Items)
        {
            var row = items.FirstOrDefault(x => x.Name.Equals(i.Name, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                row = new MenuItem
                {
                    Category = categories.First(c => c.Name.Equals(i.Category, StringComparison.OrdinalIgnoreCase)),
                    Name = i.Name, Description = i.Description, Price = i.Price, IsAvailable = true, SortOrder = sort,
                    ImageUrl = $"https://images.unsplash.com/{i.Image}?w=400", IsNew = i.Name == "Matcha Latte",
                };
                db.MenuItems.Add(row);
                items.Add(row);
            }
            sort++;
            row.NameRu ??= i.Ru;
            row.NameKk ??= i.Kk;
            row.DescriptionRu ??= i.DescriptionRu;
            if (i.Sizes && row.Variants.Count == 0)
            {
                db.MenuItemVariants.AddRange(
                    new MenuItemVariant { MenuItem = row, Label = "Small 250 ml", LabelRu = "Маленький 250 мл", LabelKk = "Кішкентай 250 мл", Price = row.Price, SortOrder = 0, IsDefault = true },
                    new MenuItemVariant { MenuItem = row, Label = "Medium 350 ml", LabelRu = "Средний 350 мл", LabelKk = "Орташа 350 мл", Price = row.Price + 0.50m, SortOrder = 1 },
                    new MenuItemVariant { MenuItem = row, Label = "Large 450 ml", LabelRu = "Большой 450 мл", LabelKk = "Үлкен 450 мл", Price = row.Price + 1.00m, SortOrder = 2 });
            }
        }

        var toppings = await db.MenuToppings.Include(t => t.ToppingCategories).ToListAsync(ct);
        foreach (var t in Toppings)
        {
            if (toppings.Any(x => x.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase))) continue;
            var row = new MenuTopping { Name = t.Name, NameRu = t.Ru, Price = t.Price, Group = t.Group, IsAvailable = true };
            db.MenuToppings.Add(row);
            foreach (var cat in t.Categories)
                db.MenuToppingCategories.Add(new MenuToppingCategory
                {
                    Topping = row, Category = categories.First(c => c.Name.Equals(cat, StringComparison.OrdinalIgnoreCase)),
                });
            toppings.Add(row);
        }
        foreach (var t in toppings)
            t.NameRu ??= Toppings.FirstOrDefault(x => x.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase))?.Ru
                         ?? ToppingRu.GetValueOrDefault(t.Name);

        // Avocado toast is only made at the first branch (shows the "available at" setting).
        var toast = items.FirstOrDefault(i => i.Name == "Avocado Toast");
        if (toast is not null && d.Branches.Count > 1 && !await db.MenuItemLocations.AnyAsync(l => l.MenuItemId == toast.Id, ct))
            db.MenuItemLocations.Add(new MenuItemLocation { MenuItem = toast, LocationId = d.Branches[0].Id });

        await db.SaveChangesAsync(ct);
    }

    // ── stations and QR tables ──

    private static async Task StationsAndTablesAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        var stations = await db.Stations.ToListAsync(ct);
        foreach (var (name, order) in new[] { ("Bar", 1), ("Kitchen", 2) })
        {
            if (stations.Any(s => s.Name == name)) continue;
            var s = new Station { Name = name, SortOrder = order };
            db.Stations.Add(s);
            stations.Add(s);
        }
        foreach (var c in await db.MenuCategories.Where(c => c.StationId == null).ToListAsync(ct))
        {
            var demo = Categories.FirstOrDefault(x => x.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase));
            if (demo is not null) c.StationId = stations.First(s => s.Name == demo.Station).Id;
        }

        const string alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        for (var b = 0; b < d.Branches.Count; b++)
        {
            var branch = d.Branches[b];
            if (await db.DiningTables.AnyAsync(t => t.LocationId == branch.Id, ct)) continue;
            var count = b == 0 ? 8 : 5;
            for (var n = 1; n <= count; n++)
                db.DiningTables.Add(new DiningTable
                {
                    LocationId = branch.Id, Label = n <= count - 2 ? n.ToString(CultureInfo.InvariantCulture) : $"Terrace {n - count + 2}",
                    Code = RandomNumberGenerator.GetString(alphabet, 10), SortOrder = n,
                });
        }
        await db.SaveChangesAsync(ct);
    }

    // ── ingredients, stock, recipes ──

    private static async Task IngredientsAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        var existing = await db.Ingredients.ToListAsync(ct);
        foreach (var i in Ingredients)
        {
            var row = existing.FirstOrDefault(x => x.Name.Equals(i.Name, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                row = new Ingredient { Name = i.Name, Unit = i.Unit, CostPerUnit = i.Cost };
                db.Ingredients.Add(row);
                existing.Add(row);
            }
        }
        await db.SaveChangesAsync(ct);

        var byName = existing.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        var stocks = await db.IngredientStocks.ToListAsync(ct);
        foreach (var branch in d.Branches)
        foreach (var i in Ingredients)
        {
            var ing = byName[i.Name];
            if (stocks.Any(s => s.IngredientId == ing.Id && s.LocationId == branch.Id)) continue;
            // Most items comfortably stocked, a few running low so "what to order" has something to suggest.
            var low = d.Chance(0.2);
            var qty = low ? i.LowAt * (decimal)(0.3 + d.Rng.NextDouble() * 0.6) : i.LowAt + (i.UpTo - i.LowAt) * (decimal)(0.3 + d.Rng.NextDouble() * 0.7);
            db.IngredientStocks.Add(new IngredientStock
            {
                IngredientId = ing.Id, LocationId = branch.Id, Quantity = Math.Round(qty, i.Unit == "pcs" ? 0 : 1),
                LowStockAt = i.LowAt, OrderUpTo = i.UpTo,
            });
        }

        var items = await db.MenuItems.ToListAsync(ct);
        var withRecipe = (await db.RecipeLines.Select(r => r.MenuItemId).Distinct().ToListAsync(ct)).ToHashSet();
        foreach (var item in items)
        {
            if (withRecipe.Contains(item.Id) || !Recipes.TryGetValue(item.Name, out var lines)) continue;
            foreach (var (ingredient, qty) in lines)
                db.RecipeLines.Add(new RecipeLine { MenuItemId = item.Id, IngredientId = byName[ingredient].Id, Quantity = qty });
        }

        var toppings = await db.MenuToppings.ToListAsync(ct);
        var withUse = (await db.ToppingUses.Select(u => u.ToppingId).Distinct().ToListAsync(ct)).ToHashSet();
        foreach (var t in toppings)
        {
            if (withUse.Contains(t.Id) || !ToppingUses.TryGetValue(t.Name, out var uses)) continue;
            foreach (var (ingredient, qty) in uses)
                db.ToppingUses.Add(new ToppingUse { ToppingId = t.Id, IngredientId = byName[ingredient].Id, Quantity = qty });
        }

        // Pastries are also counted as ready pieces on the shelf.
        foreach (var name in new[] { "Butter Croissant", "Banana Bread" })
        {
            var item = items.FirstOrDefault(i => i.Name == name);
            if (item is null) continue;
            foreach (var branch in d.Branches)
                if (!await db.MenuItemStocks.AnyAsync(s => s.MenuItemId == item.Id && s.LocationId == branch.Id, ct))
                    db.MenuItemStocks.Add(new MenuItemStock { MenuItemId = item.Id, LocationId = branch.Id, Quantity = d.Rng.Next(3, 15), LowStockAt = 5 });
        }
        await db.SaveChangesAsync(ct);
    }

    // ── suppliers and delivery invoices ──

    private static async Task SuppliersAndPurchasesAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        var suppliers = await db.Suppliers.ToListAsync(ct);
        foreach (var s in Suppliers)
        {
            if (suppliers.Any(x => x.Name == s.Name)) continue;
            var row = new Supplier
            {
                Name = s.Name, ContactName = s.Contact, Phone = s.Phone, Bin = s.Bin, PaymentTerms = s.Terms,
                PayInDays = s.PayInDays, Note = s.Note,
            };
            db.Suppliers.Add(row);
            suppliers.Add(row);
        }
        await db.SaveChangesAsync(ct);

        var ingredients = (await db.Ingredients.ToListAsync(ct)).ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        var products = await db.SupplierProducts.ToListAsync(ct);
        foreach (var i in Ingredients)
        {
            var supplier = suppliers.First(s => s.Name == i.Supplier);
            var ing = ingredients[i.Name];
            if (products.Any(p => p.SupplierId == supplier.Id && p.IngredientId == ing.Id)) continue;
            db.SupplierProducts.Add(new SupplierProduct
            {
                SupplierId = supplier.Id, IngredientId = ing.Id, PackSize = i.Pack, PackName = i.PackName, PackPrice = d.Money(i.Pack * i.Cost),
            });
        }
        await db.SaveChangesAsync(ct);

        if (await db.Purchases.AnyAsync(p => p.Number != null && p.Number.StartsWith("DEMO-"), ct)) return;

        var branch = d.Branches[0];
        var stock = (await db.IngredientStocks.Where(s => s.LocationId == branch.Id).ToListAsync(ct)).ToDictionary(s => s.IngredientId);
        var number = 1;
        var manager = d.User("manager");

        Purchase NewPurchase(string supplierName, int daysAgo, string status)
        {
            var supplier = suppliers.First(s => s.Name == supplierName);
            var date = d.Today.AddDays(-daysAgo);
            var p = new Purchase
            {
                SupplierId = supplier.Id, LocationId = branch.Id, Status = status, Number = $"DEMO-{number++:000}",
                Date = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), CreatedBy = manager, CreatedAt = d.At(date, 9.5),
            };
            var lines = Ingredients.Where(i => i.Supplier == supplierName).ToList();
            var order = 0;
            foreach (var i in lines.Where(_ => d.Chance(0.8)).DefaultIfEmpty(lines[0]))
            {
                var packs = d.Rng.Next(2, 7);
                var qty = i.Pack * packs;
                var price = i.Cost * (decimal)(0.95 + d.Rng.NextDouble() * 0.1);
                p.Lines.Add(new PurchaseLine
                {
                    IngredientId = ingredients[i.Name].Id, IngredientName = i.Name, Unit = i.Unit, Quantity = qty,
                    Total = status == PurchaseStatuses.Ordered ? null : d.Money(qty * price), CostBefore = i.Cost, SortOrder = order++,
                });
            }
            p.Total = p.Lines.Sum(l => l.Total ?? 0m);
            if (status == PurchaseStatuses.Posted)
            {
                p.PostedBy = manager;
                p.PostedAt = d.At(date, 10);
                var terms = Suppliers.First(s => s.Name == supplierName);
                if (terms.Terms == SupplierPaymentTerms.Later)
                {
                    p.DueDate = date.AddDays(terms.PayInDays ?? 14).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                    p.IsPaid = daysAgo > 14;
                }
                else p.IsPaid = true;
                if (p.IsPaid) p.PaidAt = d.At(date, 11);

                // The stock history rows a posted delivery leaves.
                foreach (var l in p.Lines)
                {
                    var before = stock.TryGetValue(l.IngredientId, out var s) ? s.Quantity : 0m;
                    db.StockChanges.Add(new StockChange
                    {
                        LocationId = branch.Id, Kind = "ingredient", ItemId = l.IngredientId, ItemName = l.IngredientName, Unit = l.Unit,
                        OldQuantity = Math.Max(0, before - l.Quantity), NewQuantity = before, ChangedBy = manager,
                        Type = StockChangeTypes.Restock, UnitCost = l.Total is { } t ? Math.Round(t / l.Quantity, 4) : null,
                        Note = $"Delivery {p.Number}", PurchaseId = p.Id, CreatedAt = p.PostedAt.Value,
                    });
                }
            }
            if (status == PurchaseStatuses.Cancelled)
            {
                p.CancelledBy = manager;
                p.CancelledAt = d.At(date, 12);
                p.Note = "Supplier could not deliver this week.";
            }
            if (status == PurchaseStatuses.Draft) p.Note = "Photo of the paper invoice to be added.";
            db.Purchases.Add(p);
            return p;
        }

        // Regular deliveries over the last two months, plus one of each other state.
        for (var w = 8; w >= 1; w--)
        {
            NewPurchase("Milk Valley", w * 7 + 1, PurchaseStatuses.Posted);
            NewPurchase("Bakery Partner", w * 7 + 3, PurchaseStatuses.Posted);
            if (w % 2 == 0) NewPurchase("Arabica Trade", w * 7 + 2, PurchaseStatuses.Posted);
            if (w % 3 == 0) NewPurchase("Barista Supply", w * 7 + 4, PurchaseStatuses.Posted);
            if (w % 2 == 1) NewPurchase("Green Market", w * 7, PurchaseStatuses.Posted);
        }
        NewPurchase("Milk Valley", 0, PurchaseStatuses.Ordered);
        NewPurchase("Green Market", 1, PurchaseStatuses.Draft);
        NewPurchase("Arabica Trade", 5, PurchaseStatuses.Cancelled);
        await db.SaveChangesAsync(ct);
    }

    // ── staff, salaries, payroll ──

    private static async Task StaffAndPayrollAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        d.PasswordHash ??= BCrypt.Net.BCrypt.HashPassword(StaffPassword);
        var users = new List<AdminUser>();
        foreach (var s in Staff)
        {
            var username = d.User(s.Key);
            var row = await db.AdminUsers.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Username == username, ct);
            if (row is not null && row.TenantId != d.Cafe.Id) continue; // the name belongs to another cafe: leave it
            if (row is null)
            {
                row = new AdminUser { Username = username, PasswordHash = d.PasswordHash, Role = s.Role, IsActive = true, CreatedAt = d.Now.AddDays(-120) };
                db.AdminUsers.Add(row);
            }
            users.Add(row);
        }
        // A former employee, so the staff list shows a switched-off account too.
        var former = d.User("former");
        if (!await db.AdminUsers.IgnoreQueryFilters().AnyAsync(u => u.Username == former, ct))
            db.AdminUsers.Add(new AdminUser { Username = former, PasswordHash = d.PasswordHash, Role = AdminRole.Worker, IsActive = false, CreatedAt = d.Now.AddDays(-200) });
        await db.SaveChangesAsync(ct);

        var salaried = (await db.StaffSalaries.Select(s => s.AdminUserId).ToListAsync(ct)).ToHashSet();
        foreach (var u in users.Where(u => !salaried.Contains(u.Id)))
            db.StaffSalaries.Add(new StaffSalary { AdminUserId = u.Id, MonthlySalary = Staff.First(s => d.User(s.Key) == u.Username).Salary });

        // Bonuses and deductions for the last two months; last month is closed (paid), this month is still open.
        if (!await db.PayAdjustments.AnyAsync(a => a.CreatedBy == Seeder, ct) && users.Count > 0)
        {
            foreach (var m in new[] { 1, 0 })
            {
                var month = d.Month(m);
                db.PayAdjustments.Add(new PayAdjustment { AdminUserId = users[d.Rng.Next(users.Count)].Id, Month = month, Amount = 100, Reason = "Best reviews of the month", CreatedBy = Seeder });
                db.PayAdjustments.Add(new PayAdjustment { AdminUserId = users[d.Rng.Next(users.Count)].Id, Month = month, Amount = 50, Reason = "Covered an extra shift", CreatedBy = Seeder });
                db.PayAdjustments.Add(new PayAdjustment { AdminUserId = users[^1].Id, Month = month, Amount = 20, IsDeduction = true, Reason = "Late three times", CreatedBy = Seeder });
            }
            await db.SaveChangesAsync(ct);

            var last = d.Month(1);
            if (!await db.PayrollMonths.AnyAsync(p => p.Month == last, ct))
            {
                var adjustments = await db.PayAdjustments.Where(a => a.Month == last).ToListAsync(ct);
                var salaries = await db.StaffSalaries.ToListAsync(ct);
                var salaryIds = salaries.Select(s => s.AdminUserId).ToList();
                var accounts = await db.AdminUsers.Where(u => salaryIds.Contains(u.Id)).ToListAsync(ct);
                var closed = new PayrollMonth { Month = last, ClosedAt = d.At(d.Today.AddDays(-d.Today.Day + 1), 12), ClosedBy = d.User("manager") };
                foreach (var s in salaries)
                {
                    var account = accounts.FirstOrDefault(a => a.Id == s.AdminUserId);
                    if (account is null) continue;
                    var bonuses = adjustments.Where(a => a.AdminUserId == s.AdminUserId && !a.IsDeduction).Sum(a => a.Amount);
                    var deductions = adjustments.Where(a => a.AdminUserId == s.AdminUserId && a.IsDeduction).Sum(a => a.Amount);
                    closed.Lines.Add(new PayrollLine
                    {
                        AdminUserId = s.AdminUserId, Username = account.Username, Role = account.Role.ToString(),
                        Salary = s.MonthlySalary, Bonuses = bonuses, Deductions = deductions, Total = s.MonthlySalary + bonuses - deductions,
                    });
                }
                if (closed.Lines.Count > 0) db.PayrollMonths.Add(closed);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    // ── customers, favourites, devices, loyalty ──

    private static async Task CustomersAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        var pin = BCrypt.Net.BCrypt.HashPassword(CustomerPin);
        var items = await db.MenuItems.ToListAsync(ct);
        for (var n = 0; n < CustomerNames.Length; n++)
        {
            var phone = $"{PhonePrefix}{n + 1:0000}";
            if (await db.CustomerUsers.AnyAsync(c => c.MobileNumber == phone, ct)) continue;
            var (first, last) = CustomerNames[n];
            var customer = new CustomerUser
            {
                MobileNumber = phone, PinHash = pin, FirstName = first, LastName = last, IsActive = n != CustomerNames.Length - 1,
                DateOfBirth = n % 3 == 0 ? new DateOnly(1975 + n, (n % 12) + 1, 1 + n % 28) : null,
                CreatedAt = n < Regulars ? d.Now.AddDays(-HistoryDays - 30 + n) : d.Now.AddDays(-45 + (n - Regulars) * 4),
            };
            db.CustomerUsers.Add(customer);
            foreach (var item in items.OrderBy(_ => d.Rng.Next()).Take(d.Rng.Next(0, 4)))
                db.Favorites.Add(new Favorite { CustomerUser = customer, MenuItemId = item.Id });
            if (n % 2 == 0)
                db.CustomerDeviceTokens.Add(new CustomerDeviceToken
                {
                    Customer = customer, Platform = n % 4 == 0 ? DevicePlatform.Ios : DevicePlatform.Android,
                    Token = $"demo-push-token-{Guid.NewGuid():N}", LastSeenAt = d.Now.AddHours(-d.Rng.Next(1, 72)),
                });

            // A bonus wallet for most guests (used when the cafe keeps points in Yurt instead of iiko).
            if (n % 4 != 3 && !await db.LoyaltyAccounts.AnyAsync(a => a.Phone == phone, ct))
            {
                var account = new LoyaltyAccount { Phone = phone, Name = first, Surname = last, WalletId = Guid.NewGuid() };
                var balance = 0m;
                for (var e = 0; e < d.Rng.Next(2, 6); e++)
                {
                    var earned = d.Money((decimal)(0.2 + d.Rng.NextDouble() * 0.8));
                    balance += earned;
                    account.Entries.Add(new LoyaltyEntry { Kind = LoyaltyEntryKind.Topup, Amount = earned, Comment = "Bonus for an order", CreatedAt = d.Now.AddDays(-d.Rng.Next(1, 60)) });
                }
                if (balance > 1)
                    account.Entries.Add(new LoyaltyEntry { Kind = LoyaltyEntryKind.Chargeoff, Amount = -1, Comment = "Paid with points", CreatedAt = d.Now.AddDays(-1) });
                db.LoyaltyAccounts.Add(account);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    // ── discount codes and promotions ──

    private static async Task MarketingAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        var codes = new[]
        {
            new DiscountCode { Code = "WELCOME10", Title = "10% off your first order", DiscountType = DiscountType.Percentage, DiscountValue = 10, IsActive = true },
            new DiscountCode { Code = "MORNING1", Title = "1 off before 10:00", DiscountType = DiscountType.FixedAmount, DiscountValue = 1, MinOrderAmount = 5, MaxUses = 500, IsActive = true },
            new DiscountCode { Code = "SUMMER20", Title = "Summer sale 20%", DiscountType = DiscountType.Percentage, DiscountValue = 20, ExpiresAt = d.Now.AddDays(-40), IsActive = false },
        };
        foreach (var c in codes)
            if (!await db.DiscountCodes.AnyAsync(x => x.Code == c.Code, ct)) db.DiscountCodes.Add(c);

        var promos = new[]
        {
            new Promotion
            {
                Title = "Autumn pumpkin latte", TitleRu = "Тыквенный латте", TitleKk = "Асқабақ латте",
                Description = "Our seasonal latte with pumpkin spice is back until the end of November.",
                DescriptionRu = "Сезонный латте с тыквенными специями — до конца ноября.",
                ImageUrl = "https://images.unsplash.com/photo-1572286258217-215cf8e8d6b5?w=800", IsActive = true,
                ExpiresAt = d.Now.AddDays(20),
            },
            new Promotion
            {
                Title = "Every 6th coffee is free", TitleRu = "Каждый 6-й кофе бесплатно", TitleKk = "Әр 6-шы кофе тегін",
                Description = "Collect points with every order and pay for coffee with them.",
                DescriptionRu = "Копите баллы с каждого заказа и оплачивайте ими кофе.",
                ImageUrl = "https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=800", IsActive = true,
                ButtonLabel = "Order now", ButtonUrl = "/menu",
            },
            new Promotion
            {
                Title = "Summer cold brew week", Description = "Cold brew for 3.50 all week.", IsActive = false,
                ExpiresAt = d.Now.AddDays(-60),
            },
        };
        foreach (var p in promos)
            if (!await db.Promotions.AnyAsync(x => x.Title == p.Title, ct)) db.Promotions.Add(p);
        await db.SaveChangesAsync(ct);
        await GuestMessagesAsync(d, ct);
    }

    /// <summary>The three automatic guest messages, switched on, plus one one-off message already sent to a few guests.</summary>
    private static async Task GuestMessagesAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        if (await db.MarketingCampaigns.AnyAsync(ct)) return;
        db.MarketingCampaigns.AddRange(
            new MarketingCampaign
            {
                Kind = CampaignKind.ComeBack, Name = "We miss you", Days = 30, IsActive = true,
                Title = "We miss you, {name}!", TitleRu = "Мы скучаем, {name}!", TitleKk = "Сізді сағындық, {name}!",
                Body = "Come back for coffee: {discount} off with code {code} for {days} days.",
                BodyRu = "Заходите на кофе: скидка {discount} по коду {code}, действует {days} дней.",
                BodyKk = "Кофеге келіңіз: {code} коды бойынша {discount} жеңілдік, {days} күн жарамды.",
                DiscountType = DiscountType.Percentage, DiscountValue = 15, CodeValidDays = 7,
            },
            new MarketingCampaign
            {
                Kind = CampaignKind.Birthday, Name = "Birthday gift", Days = 3, IsActive = true,
                Title = "Happy birthday, {name}!", TitleRu = "С днём рождения, {name}!", TitleKk = "Туған күніңізбен, {name}!",
                Body = "A gift from us: {discount} off with code {code}.",
                BodyRu = "Подарок от нас: скидка {discount} по коду {code}.",
                BodyKk = "Бізден сыйлық: {code} коды бойынша {discount} жеңілдік.",
                DiscountType = DiscountType.Percentage, DiscountValue = 20, CodeValidDays = 10,
            },
            new MarketingCampaign
            {
                Kind = CampaignKind.FirstOrderThanks, Name = "Thanks for the first order", Days = 1, IsActive = true,
                Title = "Thank you, {name}!", TitleRu = "Спасибо, {name}!", TitleKk = "Рахмет, {name}!",
                Body = "Glad you tried us. Your next coffee is {discount} cheaper with code {code}.",
                BodyRu = "Рады, что вы с нами. Следующий кофе дешевле на {discount} по коду {code}.",
                BodyKk = "Бізбен болғаныңызға қуаныштымыз. Келесі кофе {code} коды бойынша {discount} арзан.",
                DiscountType = DiscountType.Percentage, DiscountValue = 10, CodeValidDays = 14,
            });
        var oneOff = new MarketingCampaign
        {
            Kind = CampaignKind.OneOff, Name = "Pumpkin latte is back", Segment = "all", IsActive = false,
            SentAt = d.Now.AddDays(-5),
            Title = "Pumpkin latte is back", TitleRu = "Тыквенный латте вернулся", TitleKk = "Асқабақ латте қайта оралды",
            Body = "Our autumn favourite is on the menu until the end of November.",
            BodyRu = "Осенний хит снова в меню до конца ноября.",
            BodyKk = "Күзгі хит қараша соңына дейін мәзірде.",
        };
        db.MarketingCampaigns.Add(oneOff);
        var guests = await db.CustomerUsers.Where(c => c.DeviceTokens.Any()).OrderBy(c => c.MobileNumber).Take(8)
            .Select(c => c.Id).ToListAsync(ct);
        foreach (var guest in guests)
            db.MarketingMessages.Add(new MarketingMessage { Campaign = oneOff, CustomerUserId = guest, CreatedAt = oneOff.SentAt.Value });
        await db.SaveChangesAsync(ct);
    }

    // ── printers ──

    private static async Task PrintersAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        var bar = await db.Stations.FirstOrDefaultAsync(s => s.Name == "Bar", ct);
        var kitchen = await db.Stations.FirstOrDefaultAsync(s => s.Name == "Kitchen", ct);
        for (var b = 0; b < d.Branches.Count; b++)
        {
            var branch = d.Branches[b];
            if (await db.Printers.AnyAsync(p => p.LocationId == branch.Id, ct)) continue;
            // Made-up network addresses, so automatic printing is off: nothing waits on a printer that does not exist.
            db.Printers.AddRange(
                new Printer { LocationId = branch.Id, Name = "Receipt (demo)", Kind = Printer.ReceiptKind, Host = $"192.168.{b + 1}.50", AutoPrint = false, CashDrawer = true },
                new Printer { LocationId = branch.Id, Name = "Bar tickets (demo)", Kind = Printer.KitchenKind, StationId = bar?.Id, Host = $"192.168.{b + 1}.51", AutoPrint = false, LineWidth = 32 },
                new Printer { LocationId = branch.Id, Name = "Kitchen tickets (demo)", Kind = Printer.KitchenKind, StationId = kitchen?.Id, Host = $"192.168.{b + 1}.52", AutoPrint = false });
            if (b == 0 && !await db.PrintAgents.AnyAsync(a => a.LocationId == branch.Id, ct))
                db.PrintAgents.Add(new PrintAgent { LocationId = branch.Id, KeyHash = Sha256($"demo-agent-{Guid.NewGuid()}"), LastSeenAt = d.Now.AddMinutes(-3) });
        }
        await db.SaveChangesAsync(ct);
    }

    // ── orders, payments, shifts ──

    private static readonly double[] HourWeights = [6, 9, 8, 6, 7, 8, 6, 5, 6, 7, 6, 4, 2]; // 08:00 ... 20:00

    private sealed record Line(MenuItem Item, MenuItemVariant? Variant, List<MenuTopping> Toppings, int Quantity);

    /// <summary>An order with the rows that hang off it by id.</summary>
    private sealed class Sale(Order order)
    {
        public Order Order { get; } = order;
        public List<OrderPayment> Splits { get; } = [];
        public List<Payment> Online { get; } = [];
        public List<FiscalReceipt> Receipts { get; } = [];
        public List<PrintJob> Jobs { get; } = [];
    }

    private static async Task OrdersAndShiftsAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        if (await db.Orders.AnyAsync(o => o.IdempotencyKey != null && o.IdempotencyKey.StartsWith(OrderKeyPrefix), ct)) return;

        var customers = await db.CustomerUsers.Where(c => c.MobileNumber.StartsWith(PhonePrefix) && c.IsActive)
            .OrderBy(c => c.MobileNumber).ToListAsync(ct);
        var items = await db.MenuItems.Include(i => i.Variants).Include(i => i.MenuItemLocations).Where(i => i.IsAvailable).ToListAsync(ct);
        if (customers.Count == 0 || items.Count == 0) return;

        var links = await db.MenuToppingCategories.Include(l => l.Topping).ToListAsync(ct);
        var toppingsByCategory = links.Where(l => l.Topping.IsAvailable).GroupBy(l => l.CategoryId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.Topping).ToList());
        var recipeCost = (await db.RecipeLines.Select(r => new { r.MenuItemId, r.Quantity, r.Ingredient.CostPerUnit }).ToListAsync(ct))
            .GroupBy(r => r.MenuItemId).ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity * (r.CostPerUnit ?? 0m)));
        var toppingCost = (await db.ToppingUses.Select(u => new { u.ToppingId, u.Quantity, u.Ingredient.CostPerUnit }).ToListAsync(ct))
            .GroupBy(u => u.ToppingId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity * (u.CostPerUnit ?? 0m)));
        var tables = (await db.DiningTables.ToListAsync(ct)).GroupBy(t => t.LocationId).ToDictionary(g => g.Key, g => g.ToList());
        var stations = await db.Stations.ToListAsync(ct);
        var categories = await db.MenuCategories.ToListAsync(ct);
        var codes = await db.DiscountCodes.Where(c => c.Code == "WELCOME10" || c.Code == "MORNING1").ToListAsync(ct);
        var printers = await db.Printers.Where(p => p.Kind == Printer.KitchenKind).ToListAsync(ct);
        var weights = items.Select(i => (double)(Items.FirstOrDefault(x => x.Name == i.Name)?.Popularity ?? 4)).ToArray();

        MenuItem PickItem(Guid locationId)
        {
            while (true)
            {
                var r = d.Rng.NextDouble() * weights.Sum();
                for (var k = 0; k < items.Count; k++)
                {
                    r -= weights[k];
                    if (r > 0) continue;
                    var item = items[k];
                    if (item.MenuItemLocations.Count == 0 || item.MenuItemLocations.Any(l => l.LocationId == locationId)) return item;
                    break;
                }
            }
        }

        double PickHour()
        {
            var r = d.Rng.NextDouble() * HourWeights.Sum();
            for (var h = 0; h < HourWeights.Length; h++)
            {
                r -= HourWeights[h];
                if (r <= 0) return 8 + h + d.Rng.NextDouble();
            }
            return 20.5;
        }

        // Regulars order often, newer guests now and then, and nobody orders before signing up.
        CustomerUser PickCustomer(DateTime at)
        {
            var known = customers.Where(c => c.CreatedAt <= at).ToList();
            if (known.Count == 0) return customers[0];
            return d.Chance(0.6) ? d.Pick(known.Take(8).ToList()) : d.Pick(known);
        }

        Order Build(Location branch, DateTime createdAt, OrderStatus status, int seq)
        {
            var customer = PickCustomer(createdAt);
            var lines = new List<Line>();
            var count = d.Chance(0.55) ? 1 : d.Chance(0.6) ? 2 : d.Rng.Next(3, 5);
            for (var k = 0; k < count; k++)
            {
                var item = PickItem(branch.Id);
                var variant = item.Variants.Count == 0 ? null
                    : d.Chance(0.5) ? item.Variants.FirstOrDefault(v => v.IsDefault) ?? item.Variants.First()
                    : d.Pick(item.Variants.ToList());
                var pool = toppingsByCategory.GetValueOrDefault(item.CategoryId) ?? [];
                var tops = pool.Count > 0 && d.Chance(0.3) ? pool.OrderBy(_ => d.Rng.Next()).Take(d.Rng.Next(1, 3)).ToList() : [];
                lines.Add(new Line(item, variant, tops, d.Chance(0.85) ? 1 : 2));
            }

            var order = new Order
            {
                CustomerUserId = customer.Id, LocationId = branch.Id, Status = status, CreatedAt = createdAt,
                IdempotencyKey = $"{OrderKeyPrefix}{seq}",
            };
            foreach (var l in lines)
            {
                var basePrice = l.Variant?.Price ?? l.Item.Price;
                var toppingTotal = l.Toppings.Sum(t => t.Price);
                decimal? cost = recipeCost.TryGetValue(l.Item.Id, out var c) ? Math.Round(c + l.Toppings.Sum(t => toppingCost.GetValueOrDefault(t.Id)), 4) : null;
                var oi = new OrderItem
                {
                    MenuItemId = l.Item.Id, MenuItemName = l.Item.Name, VariantId = l.Variant?.Id, VariantLabel = l.Variant?.Label,
                    Quantity = l.Quantity, UnitPrice = basePrice + toppingTotal, LineTotal = (basePrice + toppingTotal) * l.Quantity,
                    UnitCost = cost, Notes = d.Chance(0.05) ? "Extra hot please" : null, CreatedAt = createdAt,
                };
                foreach (var t in l.Toppings)
                    oi.Toppings.Add(new OrderItemTopping { ToppingId = t.Id, ToppingName = t.Name, Price = t.Price, CreatedAt = createdAt });
                order.Items.Add(oi);
            }
            order.Subtotal = order.Items.Sum(i => i.LineTotal);

            var code = codes.Count > 0 && d.Chance(0.06) ? d.Pick(codes) : null;
            if (code is not null && order.Subtotal >= (code.MinOrderAmount ?? 0))
            {
                order.DiscountCodeId = code.Id;
                order.DiscountAmount = code.DiscountType == DiscountType.Percentage
                    ? d.Money(order.Subtotal * code.DiscountValue / 100m) : Math.Min(code.DiscountValue, order.Subtotal);
                code.UsedCount++;
            }
            order.Total = order.Subtotal - order.DiscountAmount;

            if (tables.TryGetValue(branch.Id, out var branchTables) && d.Chance(0.15))
            {
                var table = d.Pick(branchTables);
                order.DiningTableId = table.Id;
                order.TableLabel = table.Label;
            }
            else if (d.Chance(0.1)) order.PickupAt = createdAt.AddMinutes(30);

            order.PaymentMethod = d.Rng.NextDouble() switch
            {
                < 0.30 => PaymentMethod.Cash, < 0.55 => PaymentMethod.Card, < 0.90 => PaymentMethod.KaspiBank, _ => PaymentMethod.ApplePay,
            };
            if (status != OrderStatus.Declined && status != OrderStatus.Created)
            {
                order.AcceptedAt = createdAt.AddMinutes(d.Rng.Next(1, 4));
                order.EtaMinutes = d.Rng.Next(1, 4) * 5;
            }
            return order;
        }

        var all = new List<Sale>();
        var seq = 0;
        var startDay = d.Today.AddDays(-HistoryDays);

        for (var day = startDay; day <= d.Today; day = day.AddDays(1))
        {
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            for (var b = 0; b < d.Branches.Count; b++)
            {
                var branch = d.Branches[b];
                var perDay = (b == 0 ? 16 : 9) * (weekend ? 1.3 : 1.0) * (0.7 + d.Rng.NextDouble() * 0.6);
                for (var n = 0; n < (int)perDay; n++)
                {
                    var hour = PickHour();
                    var createdAt = d.At(day, hour);
                    if (createdAt > d.Now.AddMinutes(-45)) continue; // the last minutes get live orders below
                    var declined = d.Chance(0.04);
                    var order = Build(branch, createdAt, declined ? OrderStatus.Declined : OrderStatus.Completed, ++seq);
                    if (declined)
                    {
                        order.DeclineReason = d.Pick(DeclineReasons);
                        order.UpdatedAt = createdAt.AddMinutes(2);
                        order.PaymentMethod = null;
                    }
                    else
                    {
                        order.CompletedAt = order.AcceptedAt!.Value.AddMinutes(d.Rng.Next(4, 16));
                        order.UpdatedAt = order.CompletedAt;
                        order.PaymentStatus = d.Chance(0.01) ? PaymentStatus.Refunded : PaymentStatus.Paid;
                        if (d.Chance(0.25))
                        {
                            order.Rating = d.Rng.NextDouble() switch { < 0.6 => 5, < 0.88 => 4, < 0.96 => 3, _ => 2 };
                            order.RatingComment = d.Chance(0.4) ? d.Pick(RatingComments) : null;
                            order.RatedAt = order.CompletedAt.Value.AddMinutes(d.Rng.Next(10, 180));
                        }
                    }
                    all.Add(new Sale(order));
                }
            }
        }

        var bar = stations.FirstOrDefault(s => s.Name == "Bar");
        var barCategories = categories.Where(c => c.StationId == bar?.Id).Select(c => c.Id).ToHashSet();
        var itemCategory = items.ToDictionary(i => i.Id, i => i.CategoryId);
        int Drinks(Order o) => o.Items.Count(i => barCategories.Contains(itemCategory.GetValueOrDefault(i.MenuItemId)));

        // Live orders right now at the first branch: new, accepted, being made (drinks among them), ready.
        var liveStatuses = new[] { OrderStatus.Created, OrderStatus.Created, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Preparing, OrderStatus.Ready };
        for (var k = 0; k < liveStatuses.Length; k++)
        {
            var order = Build(d.Branches[0], d.Now.AddMinutes(-(liveStatuses.Length - k) * 6), liveStatuses[k], ++seq);
            for (var tries = 0; liveStatuses[k] == OrderStatus.Preparing && Drinks(order) == 0 && tries < 50; tries++)
                order = Build(d.Branches[0], order.CreatedAt, liveStatuses[k], seq);
            order.PaymentStatus = order.PaymentMethod is PaymentMethod.ApplePay or PaymentMethod.KaspiBank ? PaymentStatus.Paid : PaymentStatus.Unpaid;
            if (order.Status == OrderStatus.Ready) order.UpdatedAt = d.Now.AddMinutes(-1);
            all.Add(new Sale(order));
        }

        // Child rows: split payments, online payments, tax receipts, printed tickets, station "ready" marks.
        var recent = d.Now.AddDays(-3);
        foreach (var sale in all)
        {
            var o = sale.Order;
            if (o.Status == OrderStatus.Completed && o.PaymentMethod == PaymentMethod.Cash && o.Total > 6 && d.Chance(0.15))
            {
                var cash = Math.Floor(o.Total / 2);
                sale.Splits.Add(new OrderPayment { OrderId = o.Id, Method = PaymentMethod.Cash, Amount = cash, CreatedAt = o.CompletedAt!.Value });
                sale.Splits.Add(new OrderPayment { OrderId = o.Id, Method = PaymentMethod.Card, Amount = o.Total - cash, CreatedAt = o.CompletedAt!.Value });
            }
            if (o.PaymentMethod == PaymentMethod.ApplePay && o.Status != OrderStatus.Declined)
            {
                var status = o.PaymentStatus switch
                {
                    PaymentStatus.Paid => PaymentRecordStatus.Paid, PaymentStatus.Refunded => PaymentRecordStatus.Refunded, _ => PaymentRecordStatus.Pending,
                };
                sale.Online.Add(new Payment
                {
                    OrderId = o.Id,
                    Provider = PaymentProvider.Stripe, InvoiceId = $"cs_test_demo_{Guid.NewGuid():N}", Amount = o.Total, Currency = Currency.USD,
                    Status = status, PaymentUrl = "https://checkout.stripe.com/c/pay/demo", QrCode = string.Empty,
                    ProviderPaymentId = status == PaymentRecordStatus.Pending ? null : $"pi_demo_{Guid.NewGuid():N}"[..27],
                    ExpiresAt = o.CreatedAt.AddMinutes(30), CreatedAt = o.CreatedAt,
                });
            }
            if (o.Status == OrderStatus.Completed && o.CreatedAt > recent && o.PaymentMethod != PaymentMethod.Cash)
            {
                var sentAt = o.CompletedAt!.Value.AddSeconds(20);
                sale.Receipts.Add(new FiscalReceipt
                {
                    OrderId = o.Id, LocationId = o.LocationId, Kind = FiscalReceiptKind.Sale, Status = FiscalReceiptStatus.Sent, Amount = o.Total,
                    Provider = "webkassa", Attempts = 1, NextAttemptAt = sentAt, SentAt = sentAt, RegisterNumber = "DEMO-KKM-0001",
                    CheckNumber = $"{d.Rng.Next(100000, 999999)}", TicketUrl = "https://consumer.oofd.kz/demo", CreatedAt = o.CompletedAt!.Value,
                });
                if (o.PaymentStatus == PaymentStatus.Refunded)
                    sale.Receipts.Add(new FiscalReceipt
                    {
                        OrderId = o.Id, LocationId = o.LocationId, Kind = FiscalReceiptKind.Return, Status = FiscalReceiptStatus.Sent, Amount = o.Total,
                        Provider = "webkassa", Attempts = 1, NextAttemptAt = sentAt, SentAt = sentAt.AddHours(1), RegisterNumber = "DEMO-KKM-0001",
                        CheckNumber = $"{d.Rng.Next(100000, 999999)}", CreatedAt = sentAt.AddHours(1),
                    });
            }
            if (o.Status is not OrderStatus.Declined and not OrderStatus.Created && o.CreatedAt > recent)
            {
                var printer = printers.FirstOrDefault(p => p.LocationId == o.LocationId);
                if (printer is not null)
                    sale.Jobs.Add(new PrintJob
                    {
                        OrderId = o.Id, PrinterId = printer.Id, Kind = PrintJob.TicketKind, Data = Encoding.UTF8.GetBytes($"Demo ticket {o.IdempotencyKey}\n"),
                        Status = PrintJob.Printed, Attempts = 1, PrintedAt = o.AcceptedAt, CreatedAt = o.AcceptedAt ?? o.CreatedAt,
                    });
            }
            // The bar has marked its drinks ready while the order is still being made.
            if (o.Status == OrderStatus.Preparing && bar is not null && Drinks(o) > 0)
                o.ReadyStations.Add(new OrderStationReady { StationId = bar.Id, CreatedAt = d.Now.AddMinutes(-2) });
        }

        foreach (var batch in all.Chunk(250))
        {
            foreach (var sale in batch)
            {
                db.Orders.Add(sale.Order);
                db.OrderPayments.AddRange(sale.Splits);
                db.Payments.AddRange(sale.Online);
                db.FiscalReceipts.AddRange(sale.Receipts);
                db.PrintJobs.AddRange(sale.Jobs);
            }
            await db.SaveChangesAsync(ct);
            // Stop tracking what is saved (the objects stay in memory for the shift totals), so each batch saves quickly.
            db.ChangeTracker.Clear();
        }
        d.Logger.LogInformation("Demo: {Count} orders added.", all.Count);

        await ShiftsAsync(d, all, ct);
        await GroupOrderAndWebhooksAsync(d, customers, items, ct);
    }

    private static async Task ShiftsAsync(Demo d, List<Sale> sales, CancellationToken ct)
    {
        var db = d.Db;
        var opener = d.User("aigerim");
        if (await db.Shifts.AnyAsync(s => s.OpenedBy == opener || s.OpenedBy == d.User("daniyar"), ct)) return;

        for (var back = 14; back >= 0; back--)
        {
            var day = d.Today.AddDays(-back);
            for (var b = 0; b < d.Branches.Count; b++)
            {
                var branch = d.Branches[b];
                var openedAt = d.At(day, 7.75);
                if (openedAt > d.Now) continue;
                var closing = back > 0 || d.LocalHourNow >= 21.5;
                if (!closing && await db.Shifts.AnyAsync(s => s.LocationId == branch.Id && s.ClosedAt == null, ct)) continue;

                var who = b == 0 ? opener : d.User("daniyar");
                var shift = new Shift { LocationId = branch.Id, OpenedAt = openedAt, OpenedBy = who, OpeningCash = 100, CreatedAt = openedAt };
                if (d.Chance(0.4))
                    shift.Movements.Add(new CashMovement { Amount = d.Money(5 + (decimal)d.Rng.NextDouble() * 20), IsOut = true, Reason = "Bought milk at the shop", CreatedBy = who, CreatedAt = d.At(day, 12) });
                if (d.Chance(0.2))
                    shift.Movements.Add(new CashMovement { Amount = 50, IsOut = false, Reason = "Change from the safe", CreatedBy = d.User("manager"), CreatedAt = d.At(day, 9) });

                if (closing)
                {
                    var closedAt = d.At(day, 21.5);
                    var cashSales = sales
                        .Where(s => s.Order.LocationId == branch.Id && s.Order.Status == OrderStatus.Completed
                            && s.Order.CompletedAt >= openedAt && s.Order.CompletedAt <= closedAt)
                        .Sum(s => s.Splits.Count > 0
                            ? s.Splits.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount)
                            : s.Order.PaymentMethod == PaymentMethod.Cash ? s.Order.Total : 0m);
                    var expected = shift.OpeningCash + cashSales
                        + shift.Movements.Where(m => !m.IsOut).Sum(m => m.Amount) - shift.Movements.Where(m => m.IsOut).Sum(m => m.Amount);
                    var diff = d.Chance(0.7) ? 0m : d.Money((decimal)(d.Rng.NextDouble() * 6 - 4));
                    shift.ClosedAt = closedAt;
                    shift.ClosedBy = who;
                    shift.CountedCash = expected + diff;
                    shift.Note = diff < 0 ? "Short at closing, will check the camera." : null;
                }
                db.Shifts.Add(shift);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task GroupOrderAndWebhooksAsync(Demo d, List<CustomerUser> customers, List<MenuItem> items, CancellationToken ct)
    {
        var db = d.Db;
        if (!await db.GroupCarts.AnyAsync(g => g.Code == "DEMO42", ct) && customers.Count >= 3)
        {
            var host = customers[0];
            var cart = new GroupCart
            {
                Code = "DEMO42", LocationId = d.Branches[0].Id, CreatedByUserId = host.Id, Status = GroupCartStatus.Finalized,
                ExpiresAt = d.Now.AddDays(-1).AddHours(1), CreatedAt = d.Now.AddDays(-1),
            };
            foreach (var c in customers.Take(3))
            {
                cart.Members.Add(new GroupCartMember { CustomerUserId = c.Id, DisplayName = c.FirstName });
                var item = d.Pick(items);
                cart.Items.Add(new GroupCartItem
                {
                    AddedByUserId = c.Id, AddedByName = c.FirstName, MenuItemId = item.Id, MenuItemName = item.Name,
                    UnitPrice = item.Price, Quantity = 1,
                });
            }
            db.GroupCarts.Add(cart);
        }

        if (!await db.PaymentWebhookLogs.AnyAsync(w => w.Headers.Contains(Seeder), ct))
        {
            var invoices = await db.Payments.Where(p => p.InvoiceId.StartsWith("cs_test_demo_")).OrderByDescending(p => p.CreatedAt).Take(3).ToListAsync(ct);
            foreach (var p in invoices)
                db.PaymentWebhookLogs.Add(new PaymentWebhookLog
                {
                    Provider = PaymentProvider.Stripe, Processed = true, CreatedAt = p.CreatedAt.AddMinutes(1),
                    Headers = JsonSerializer.Serialize(new { source = Seeder }),
                    Payload = JsonSerializer.Serialize(new { type = "checkout.session.completed", data = new { @object = new { id = p.InvoiceId, payment_status = "paid" } } }),
                });
        }
        await db.SaveChangesAsync(ct);
    }

    // ── stock history: write-offs, stocktake, transfer ──

    private static async Task StockHistoryAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        if (await db.StockChanges.AnyAsync(c => c.Note != null && c.Note.StartsWith("[demo]"), ct)) return;
        var ingredients = (await db.Ingredients.ToListAsync(ct)).ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        var branch = d.Branches[0];
        var worker = d.User("aigerim");

        StockChange Change(string name, string type, decimal oldQty, decimal newQty, int daysAgo, string? reason, string note, Location? at = null)
        {
            var ing = ingredients[name];
            return new StockChange
            {
                LocationId = (at ?? branch).Id, Kind = "ingredient", ItemId = ing.Id, ItemName = ing.Name, Unit = ing.Unit,
                OldQuantity = oldQty, NewQuantity = newQty, ChangedBy = worker, Type = type, Reason = reason, Note = "[demo] " + note,
                UnitCost = ing.CostPerUnit, CreatedAt = d.At(d.Today.AddDays(-daysAgo), 10 + d.Rng.Next(0, 9)),
            };
        }

        for (var w = 0; w < 9; w++)
        {
            db.StockChanges.Add(Change("Milk 3.2%", StockChangeTypes.WriteOff, 12000, 11500, w * 7 + 2, WriteOffReasons.Spilled, "Jug knocked over"));
            db.StockChanges.Add(Change("Croissant dough", StockChangeTypes.WriteOff, 30, 26, w * 7 + 4, WriteOffReasons.Expired, "Left from yesterday"));
            db.StockChanges.Add(Change("Coffee beans", StockChangeTypes.WriteOff, 4000, 3964, w * 7 + 1, WriteOffReasons.StaffDrink, "Staff coffee"));
            if (w % 3 == 0)
                db.StockChanges.Add(Change("Cup 350 ml with lid", StockChangeTypes.WriteOff, 600, 580, w * 7 + 5, WriteOffReasons.Broken, "Crushed sleeve"));
        }
        // Monthly stocktake: most counts match, a few short.
        foreach (var m in new[] { 3, 33, 63 })
        {
            db.StockChanges.Add(Change("Coffee beans", StockChangeTypes.Stocktake, 3500, 3380, m, null, "Monthly count"));
            db.StockChanges.Add(Change("Oat milk", StockChangeTypes.Stocktake, 5000, 4750, m, null, "Monthly count"));
            db.StockChanges.Add(Change("Sugar", StockChangeTypes.Stocktake, 3000, 3000, m, null, "Monthly count"));
        }
        if (d.Branches.Count > 1)
        {
            var other = d.Branches[1];
            var outRow = Change("Oat milk", StockChangeTypes.TransferOut, 6000, 4000, 6, null, "Riverside ran out", branch);
            outRow.OtherLocationId = other.Id;
            outRow.OtherLocationName = other.Name;
            var inRow = Change("Oat milk", StockChangeTypes.TransferIn, 500, 2500, 6, null, "From the first branch", other);
            inRow.OtherLocationId = branch.Id;
            inRow.OtherLocationName = branch.Name;
            inRow.CreatedAt = outRow.CreatedAt;
            db.StockChanges.AddRange(outRow, inRow);
        }
        // Ready pieces on the shelf counted by hand.
        var croissant = await db.MenuItems.FirstOrDefaultAsync(i => i.Name == "Butter Croissant", ct);
        if (croissant is not null)
            db.StockChanges.Add(new StockChange
            {
                LocationId = branch.Id, Kind = "dish", ItemId = croissant.Id, ItemName = croissant.Name, OldQuantity = 4, NewQuantity = 20,
                ChangedBy = worker, Type = StockChangeTypes.Set, Note = "[demo] Morning bake", CreatedAt = d.At(d.Today, 8),
            });
        await db.SaveChangesAsync(ct);
    }

    // ── monthly costs ──

    private static async Task ExpensesAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        if (await db.CafeExpenses.AnyAsync(e => e.CreatedBy == Seeder, ct)) return;
        foreach (var m in new[] { 2, 1, 0 })
        {
            var month = d.Month(m);
            for (var b = 0; b < d.Branches.Count; b++)
            {
                var branch = d.Branches[b];
                db.CafeExpenses.Add(new CafeExpense { Month = month, LocationId = branch.Id, Category = CafeExpenseCategories.Rent, Name = "Rent", Amount = b == 0 ? 2500 : 1600, CreatedBy = Seeder });
                db.CafeExpenses.Add(new CafeExpense { Month = month, LocationId = branch.Id, Category = CafeExpenseCategories.Utilities, Name = "Electricity and water", Amount = d.Money(180 + (decimal)d.Rng.NextDouble() * 60), CreatedBy = Seeder });
            }
            db.CafeExpenses.Add(new CafeExpense { Month = month, Category = CafeExpenseCategories.Marketing, Name = "Instagram ads", Amount = 300, CreatedBy = Seeder });
            db.CafeExpenses.Add(new CafeExpense { Month = month, Category = CafeExpenseCategories.Taxes, Name = "Simplified tax 3%", Amount = d.Money(400 + (decimal)d.Rng.NextDouble() * 100), CreatedBy = Seeder });
            if (m == 1)
                db.CafeExpenses.Add(new CafeExpense { Month = month, LocationId = d.Branches[0].Id, Category = CafeExpenseCategories.Repairs, Name = "Coffee machine service", Amount = 220, CreatedBy = Seeder });
        }
        await db.SaveChangesAsync(ct);
    }

    // ── camera watch ──

    private static async Task StaffWatchAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        var branch = d.Branches[0];
        var yesterday = d.Today.AddDays(-1);
        if (await db.WatchCameras.AnyAsync(c => c.LocationId == branch.Id, ct)
            || await db.WatchMinutes.AnyAsync(m => m.LocationId == branch.Id, ct)) return;

        if (!await db.WatchSettings.AnyAsync(s => s.LocationId == branch.Id, ct))
        db.WatchSettings.Add(new WatchSettings
        {
            LocationId = branch.Id, Open = "08:00", Close = "22:00",
            SettingsJson = JsonSerializer.Serialize(new { alerts = new { emptyWhileQueueMinutes = 3, queueMinGuests = 2, emptyOnShiftMinutes = 10, emptyMinutes = 15, lateGraceMinutes = 10, phoneMinutes = 5, stillMinutes = 10 }, detect = new { phones = true, still = true } }, Json),
        });
        db.WatchCameras.Add(new WatchCamera
        {
            LocationId = branch.Id, Name = "counter",
            ZonesJson = JsonSerializer.Serialize(new object[]
            {
                new { name = "bar", label = "Bar", kind = "station", points = new[] { new[] { 10.0, 40 }, new[] { 55.0, 40 }, new[] { 55.0, 95 }, new[] { 10.0, 95 } }, queue = "queue" },
                new { name = "queue", label = "Queue", kind = "queue", points = new[] { new[] { 60.0, 30 }, new[] { 95.0, 30 }, new[] { 95.0, 95 }, new[] { 60.0, 95 } } },
            }, Json),
        });
        db.WatchCameras.Add(new WatchCamera
        {
            LocationId = branch.Id, Name = "entrance",
            ZonesJson = JsonSerializer.Serialize(new object[]
            {
                new { name = "door", label = "Front door", kind = "door", points = new[] { new[] { 20.0, 70 }, new[] { 80.0, 70 } }, inside = new[] { 50.0, 90 } },
                new { name = "kitchen", label = "Kitchen", kind = "station", points = new[] { new[] { 5.0, 5 }, new[] { 40.0, 5 }, new[] { 40.0, 50 }, new[] { 5.0, 50 } } },
            }, Json),
        });
        if (!await db.WatchBoxes.AnyAsync(b => b.LocationId == branch.Id, ct))
        db.WatchBoxes.Add(new WatchBox { LocationId = branch.Id, KeyHash = Sha256($"demo-box-{Guid.NewGuid()}"), LastSeenAt = d.Now.AddMinutes(-1), CameraNames = "counter,entrance", Version = "0.3.0" });
        var shifts = new[]
        {
            ("Aigerim", "bar", "08:00", "15:00"), ("Daniyar", "bar", "15:00", "22:00"), ("Madina", "kitchen", "08:00", "16:00"),
        };
        if (!await db.WatchShifts.AnyAsync(s => s.LocationId == branch.Id, ct))
        for (var k = 0; k < shifts.Length; k++)
            db.WatchShifts.Add(new WatchShift { LocationId = branch.Id, Day = "daily", Name = shifts[k].Item1, Station = shifts[k].Item2, Start = shifts[k].Item3, End = shifts[k].Item4, SortOrder = k });

        // Minutes for yesterday (whole day) and today up to now, every 5th minute to keep it small.
        foreach (var day in new[] { d.Today.AddDays(-1), d.Today })
        {
            for (var minute = 8 * 60; minute < 22 * 60; minute += 5)
            {
                var at = d.At(day, minute / 60.0);
                if (at > d.Now) break;
                var busy = HourWeights[Math.Min(HourWeights.Length - 1, minute / 60 - 8)] / 9.0;
                foreach (var zone in new[] { "bar", "kitchen", "queue", "door" })
                {
                    var occupied = zone == "door" ? 0 : (int)(60 * Math.Min(1, 0.6 + d.Rng.NextDouble() * 0.4));
                    db.WatchMinutes.Add(new WatchMinute
                    {
                        LocationId = branch.Id, Minute = at, Zone = zone, Seconds = 60, Occupied = occupied,
                        PeopleSum = zone == "queue" ? (int)(busy * 120 * d.Rng.NextDouble()) : occupied, PeopleMax = zone == "queue" ? (int)(busy * 4) + 1 : 1,
                        Phone = zone == "bar" && d.Chance(0.1) ? d.Rng.Next(10, 40) : 0,
                        Still = zone == "kitchen" && d.Chance(0.1) ? d.Rng.Next(20, 50) : 0,
                        Entered = zone == "door" ? (int)Math.Round(busy * 3 * d.Rng.NextDouble()) : 0,
                        Left = zone == "door" ? (int)Math.Round(busy * 3 * d.Rng.NextDouble()) : 0,
                    });
                }
            }
        }

        foreach (var (day, hour, kind, zone, text) in new[]
        {
            (d.Today.AddDays(-1), 9.2, "late", "bar", "Aigerim is 12 min late for the bar shift (08:00)."),
            (d.Today.AddDays(-1), 13.6, "phone", "bar", "Bar: on the phone for 6 min while on duty (Aigerim)."),
            (d.Today.AddDays(-1), 17.4, "unattended_queue", "bar", "🚨 Bar: guests waiting with nobody at the station for 95 s (3 in the queue)."),
            (d.Today.AddDays(-1), 19.1, "still", "kitchen", "Kitchen: nobody moved for 11 min (Madina)."),
            (d.Today, 8.5, "camera_down", "entrance", "Camera \"entrance\" sends no picture for 5 min."),
            (d.Today, 8.6, "camera_back", "entrance", "Camera \"entrance\" is back."),
        })
        {
            var at = d.At(day, hour);
            if (at <= d.Now) db.WatchAlerts.Add(new WatchAlert { LocationId = branch.Id, At = at, Kind = kind, Zone = zone, Text = text, SentToTelegram = true });
        }
        if (!await db.WatchLive.AnyAsync(l => l.LocationId == branch.Id, ct))
        db.WatchLive.Add(new WatchLive
        {
            LocationId = branch.Id, At = d.Now.AddSeconds(-20),
            StateJson = JsonSerializer.Serialize(new
            {
                stations = new object[]
                {
                    new { zone = "bar", label = "Bar", known = true, present = true, awaySince = (string?)null, phone = false, still = false, unattendedSince = (string?)null },
                    new { zone = "kitchen", label = "Kitchen", known = true, present = true, awaySince = (string?)null, phone = false, still = false, unattendedSince = (string?)null },
                },
                queues = new object[] { new { zone = "queue", label = "Queue", known = true, people = 2, waitingSince = (string?)null } },
                visitorsIn = 37, visitorsOut = 31,
            }, Json),
        });
        if (!await db.WatchReports.AnyAsync(r => r.LocationId == branch.Id && r.Day == yesterday, ct))
        db.WatchReports.Add(new WatchReport
        {
            LocationId = branch.Id, Day = d.Today.AddDays(-1),
            Text = "Yesterday: 412 visitors, bar staffed 96% of opening hours, kitchen 91%. 1 late arrival, 1 long phone break, the bar was empty once while guests waited.",
            DataJson = JsonSerializer.Serialize(new { visitors = 412, coverage = new { bar = 96, kitchen = 91 }, alerts = 4 }, Json),
        });
        await db.SaveChangesAsync(ct);
    }

    // ── reports, audit log, app release ──

    private static async Task ExtrasAsync(Demo d, CancellationToken ct)
    {
        var db = d.Db;
        var customers = await db.CustomerUsers.Where(c => c.MobileNumber.StartsWith(PhonePrefix)).OrderBy(c => c.MobileNumber).ToListAsync(ct);
        var customerIds = customers.Select(c => c.Id).ToList();
        if (customers.Count >= 3 && !await db.UserReports.AnyAsync(r => customerIds.Contains(r.CustomerId), ct))
        {
            db.UserReports.Add(new UserReport { CustomerId = customers[1].Id, Text = "My order showed 'ready' but the barista said it was not made yet.", CreatedAt = d.Now.AddDays(-2) });
            db.UserReports.Add(new UserReport { CustomerId = customers[2].Id, Text = "Приложение не принимает промокод WELCOME10.", IsResolved = true, ResolvedAt = d.Now.AddDays(-5), CreatedAt = d.Now.AddDays(-6) });
            db.UserReports.Add(new UserReport { CustomerId = customers[0].Id, Text = "Please add oat milk to the cold brew options!", CreatedAt = d.Now.AddHours(-5) });
        }

        if (!await db.AuditLogs.AnyAsync(a => a.Details != null && a.Details.StartsWith("[demo]"), ct))
        {
            var manager = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == d.User("manager"), ct);
            var entries = new[]
            {
                ("MenuItemUpdated", "MenuItem", "Latte price 4.00 → 4.25", 40), ("DiscountCodeCreated", "DiscountCode", "WELCOME10", 35),
                ("ShiftClosed", "Shift", "Downtown, counted 642.50", 1), ("PurchasePosted", "Purchase", "DEMO-012 Milk Valley", 8),
                ("WorkerCreated", "AdminUser", d.User("madina"), 60), ("StocktakeSaved", "StockChange", "Monthly count, 3 lines", 3),
            };
            foreach (var (action, entity, details, daysAgo) in entries)
                db.AuditLogs.Add(new AuditLog
                {
                    Action = action, EntityType = entity, PerformedByAdminId = manager?.Id, PerformedByUsername = manager?.Username ?? d.User("manager"),
                    Details = "[demo] " + details, IpAddress = "127.0.0.1", CreatedAt = d.Now.AddDays(-daysAgo).AddHours(-d.Rng.Next(0, 8)),
                });
        }

        if (!await db.AppReleases.AnyAsync(r => r.Version == "1.0.0", ct))
            db.AppReleases.Add(new AppRelease
            {
                Version = "1.0.0", IsMandatory = false, CreatedAt = d.Now.AddDays(-30),
                NotesEn = "Order ahead, pay in the app and collect bonus points.",
                NotesRu = "Заказывайте заранее, оплачивайте в приложении и копите баллы.",
                NotesKk = "Алдын ала тапсырыс беріп, қосымшада төлеп, бонус жинаңыз.",
            });
        await db.SaveChangesAsync(ct);
    }

    private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}

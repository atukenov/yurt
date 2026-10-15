using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using BCrypt.Net;

namespace Yurt.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        await db.Database.MigrateAsync();

        await SeedLocationsAsync(db, logger);
        await SeedMenuAsync(db, logger);
        await SeedToppingsAsync(db, logger);
        await SeedAdminsAsync(db, logger);
    }

    private static async Task SeedLocationsAsync(ApplicationDbContext db, ILogger logger)
    {
        if (await db.Locations.AnyAsync()) return;

        db.Locations.AddRange(
            new Location
            {
                Id = Guid.Parse("11111111-0000-0000-0000-000000000001"),
                Name = "Yurt Downtown",
                Address = "123 Main Street, Downtown, City 10001",
                WorkingHours = "Mon–Fri 07:00–22:00 | Sat–Sun 08:00–23:00",
                ContactPhone = "+15551001001",
                IsActive = true
            },
            new Location
            {
                Id = Guid.Parse("11111111-0000-0000-0000-000000000002"),
                Name = "Yurt Midtown",
                Address = "456 Park Avenue, Midtown, City 10022",
                WorkingHours = "Daily 08:00–21:00",
                ContactPhone = "+15551001002",
                IsActive = true
            },
            new Location
            {
                Id = Guid.Parse("11111111-0000-0000-0000-000000000003"),
                Name = "Yurt Airport Terminal 2",
                Address = "Terminal 2, City International Airport",
                WorkingHours = "Daily 05:00–23:00",
                ContactPhone = "+15551001003",
                IsActive = false
            }
        );
        await db.SaveChangesAsync();
        logger.LogInformation("Locations seeded.");
    }

    private static async Task SeedMenuAsync(ApplicationDbContext db, ILogger logger)
    {
        if (await db.MenuCategories.AnyAsync()) return;

        var catCoffee = new MenuCategory { Id = Guid.Parse("22222222-0000-0000-0000-000000000001"), Name = "Coffee", SortOrder = 1 };
        var catCold = new MenuCategory { Id = Guid.Parse("22222222-0000-0000-0000-000000000002"), Name = "Cold Drinks", SortOrder = 2 };
        var catFood = new MenuCategory { Id = Guid.Parse("22222222-0000-0000-0000-000000000003"), Name = "Food & Pastries", SortOrder = 3 };
        db.MenuCategories.AddRange(catCoffee, catCold, catFood);

        db.MenuItems.AddRange(
            // Coffee
            new MenuItem { CategoryId = catCoffee.Id, Name = "Espresso", Description = "Rich, concentrated single-shot espresso.", Price = 2.50m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1510707577719-ae7c14805e3a?w=400" },
            new MenuItem { CategoryId = catCoffee.Id, Name = "Americano", Description = "Espresso diluted with hot water for a smooth, bold cup.", Price = 3.00m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1551030173-122aabc4489c?w=400" },
            new MenuItem { CategoryId = catCoffee.Id, Name = "Cappuccino", Description = "Equal parts espresso, steamed milk, and thick foam.", Price = 3.75m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1553361371-9b22f78e8b1d?w=400" },
            new MenuItem { CategoryId = catCoffee.Id, Name = "Flat White", Description = "Double ristretto with velvety microfoam milk.", Price = 4.00m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1577968897966-3d4325b36b02?w=400" },
            new MenuItem { CategoryId = catCoffee.Id, Name = "Latte", Description = "Smooth espresso with lots of steamed milk and a small layer of foam.", Price = 4.25m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1561882468-9110e03e0f78?w=400" },
            new MenuItem { CategoryId = catCoffee.Id, Name = "Mocha", Description = "Espresso, chocolate sauce, steamed milk, and whipped cream.", Price = 4.75m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1572442388796-11668a67e53d?w=400" },
            // Cold
            new MenuItem { CategoryId = catCold.Id, Name = "Iced Latte", Description = "Chilled espresso over ice with cold milk.", Price = 4.50m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1461023058943-07fcbe16d735?w=400" },
            new MenuItem { CategoryId = catCold.Id, Name = "Cold Brew", Description = "Slow-steeped 18-hour cold brew concentrate.", Price = 4.75m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1517701604599-bb29b565090b?w=400" },
            new MenuItem { CategoryId = catCold.Id, Name = "Matcha Latte", Description = "Ceremonial-grade matcha with oat milk over ice.", Price = 5.00m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1536256263959-770b48d82b0a?w=400" },
            new MenuItem { CategoryId = catCold.Id, Name = "Fresh Lemonade", Description = "Hand-squeezed lemonade with a hint of mint.", Price = 3.50m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1613478223719-2ab802602423?w=400" },
            // Food
            new MenuItem { CategoryId = catFood.Id, Name = "Butter Croissant", Description = "Flaky, buttery, golden-baked classic French croissant.", Price = 3.25m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1555507036-ab1f4038808a?w=400" },
            new MenuItem { CategoryId = catFood.Id, Name = "Avocado Toast", Description = "Sourdough topped with smashed avocado, sea salt, and chilli flakes.", Price = 6.50m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1541519227354-08fa5d50c820?w=400" },
            new MenuItem { CategoryId = catFood.Id, Name = "Banana Bread", Description = "Moist, house-baked banana bread with walnut crunch.", Price = 4.00m, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1588195538326-c5b1e9f80a1b?w=400" }
        );

        await db.SaveChangesAsync();
        logger.LogInformation("Menu seeded.");
    }

    private static async Task SeedToppingsAsync(ApplicationDbContext db, ILogger logger)
    {
        if (await db.MenuToppings.AnyAsync())
        {
            // Backfill Group for toppings seeded before this field was added.
            string[] milkNames  = ["Oat Milk", "Almond Milk", "Soy Milk"];
            string[] syrupNames = ["Vanilla Syrup", "Caramel Syrup", "Hazelnut Syrup", "Brown Sugar Syrup"];

            var patched = 0;
            patched += await db.MenuToppings
                .Where(t => milkNames.Contains(t.Name) && t.Group == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Group, "milk"));
            patched += await db.MenuToppings
                .Where(t => syrupNames.Contains(t.Name) && t.Group == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Group, "syrup"));

            if (patched > 0)
                logger.LogInformation("Backfilled Group for {Count} toppings.", patched);

            return;
        }

        var catCoffeeId = Guid.Parse("22222222-0000-0000-0000-000000000001");
        var catColdId   = Guid.Parse("22222222-0000-0000-0000-000000000002");
        var catFoodId   = Guid.Parse("22222222-0000-0000-0000-000000000003");

        // Shared — Coffee + Cold Drinks
        var extraShot      = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000001"), Name = "Extra Espresso Shot", Price = 0.75m, IsAvailable = true };
        var vanillaSyrup   = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000002"), Name = "Vanilla Syrup",       Price = 0.50m, IsAvailable = true, Group = "syrup" };
        var caramelSyrup   = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000003"), Name = "Caramel Syrup",       Price = 0.50m, IsAvailable = true, Group = "syrup" };
        var hazelnutSyrup  = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000004"), Name = "Hazelnut Syrup",      Price = 0.50m, IsAvailable = true, Group = "syrup" };
        var oatMilk        = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000005"), Name = "Oat Milk",            Price = 0.75m, IsAvailable = true, Group = "milk" };
        var almondMilk     = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000006"), Name = "Almond Milk",         Price = 0.75m, IsAvailable = true, Group = "milk" };
        var soyMilk        = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000007"), Name = "Soy Milk",            Price = 0.75m, IsAvailable = true, Group = "milk" };
        var whippedCream   = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000008"), Name = "Whipped Cream",       Price = 0.50m, IsAvailable = true };

        // Coffee only
        var chocolateDrizzle = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000009"), Name = "Chocolate Drizzle", Price = 0.25m, IsAvailable = true };
        var cinnamon         = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000010"), Name = "Cinnamon",          Price = 0.00m, IsAvailable = true };

        // Cold Drinks only
        var extraIce   = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000011"), Name = "Extra Ice",         Price = 0.00m, IsAvailable = true };
        var brownSugar = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000012"), Name = "Brown Sugar Syrup", Price = 0.50m, IsAvailable = true, Group = "syrup" };

        // Food & Pastries only
        var extraButter  = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000013"), Name = "Extra Butter",    Price = 0.00m, IsAvailable = true };
        var strawberryJam = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000014"), Name = "Strawberry Jam", Price = 0.50m, IsAvailable = true };
        var extraAvocado  = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000015"), Name = "Extra Avocado",  Price = 1.50m, IsAvailable = true };
        var poachedEgg    = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000016"), Name = "Poached Egg",    Price = 1.50m, IsAvailable = true };
        var creamCheese   = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000017"), Name = "Cream Cheese",   Price = 0.75m, IsAvailable = true };
        var honey         = new MenuTopping { Id = Guid.Parse("33333333-0000-0000-0000-000000000018"), Name = "Honey",          Price = 0.50m, IsAvailable = true };

        db.MenuToppings.AddRange(
            extraShot, vanillaSyrup, caramelSyrup, hazelnutSyrup, oatMilk, almondMilk, soyMilk, whippedCream,
            chocolateDrizzle, cinnamon,
            extraIce, brownSugar,
            extraButter, strawberryJam, extraAvocado, poachedEgg, creamCheese, honey
        );

        db.MenuToppingCategories.AddRange(
            // Shared: Coffee + Cold Drinks
            new MenuToppingCategory { ToppingId = extraShot.Id,     CategoryId = catCoffeeId },
            new MenuToppingCategory { ToppingId = extraShot.Id,     CategoryId = catColdId },
            new MenuToppingCategory { ToppingId = vanillaSyrup.Id,  CategoryId = catCoffeeId },
            new MenuToppingCategory { ToppingId = vanillaSyrup.Id,  CategoryId = catColdId },
            new MenuToppingCategory { ToppingId = caramelSyrup.Id,  CategoryId = catCoffeeId },
            new MenuToppingCategory { ToppingId = caramelSyrup.Id,  CategoryId = catColdId },
            new MenuToppingCategory { ToppingId = hazelnutSyrup.Id, CategoryId = catCoffeeId },
            new MenuToppingCategory { ToppingId = hazelnutSyrup.Id, CategoryId = catColdId },
            new MenuToppingCategory { ToppingId = oatMilk.Id,       CategoryId = catCoffeeId },
            new MenuToppingCategory { ToppingId = oatMilk.Id,       CategoryId = catColdId },
            new MenuToppingCategory { ToppingId = almondMilk.Id,    CategoryId = catCoffeeId },
            new MenuToppingCategory { ToppingId = almondMilk.Id,    CategoryId = catColdId },
            new MenuToppingCategory { ToppingId = soyMilk.Id,       CategoryId = catCoffeeId },
            new MenuToppingCategory { ToppingId = soyMilk.Id,       CategoryId = catColdId },
            new MenuToppingCategory { ToppingId = whippedCream.Id,  CategoryId = catCoffeeId },
            new MenuToppingCategory { ToppingId = whippedCream.Id,  CategoryId = catColdId },
            // Coffee only
            new MenuToppingCategory { ToppingId = chocolateDrizzle.Id, CategoryId = catCoffeeId },
            new MenuToppingCategory { ToppingId = cinnamon.Id,         CategoryId = catCoffeeId },
            // Cold Drinks only
            new MenuToppingCategory { ToppingId = extraIce.Id,   CategoryId = catColdId },
            new MenuToppingCategory { ToppingId = brownSugar.Id, CategoryId = catColdId },
            // Food & Pastries only
            new MenuToppingCategory { ToppingId = extraButter.Id,   CategoryId = catFoodId },
            new MenuToppingCategory { ToppingId = strawberryJam.Id, CategoryId = catFoodId },
            new MenuToppingCategory { ToppingId = extraAvocado.Id,  CategoryId = catFoodId },
            new MenuToppingCategory { ToppingId = poachedEgg.Id,    CategoryId = catFoodId },
            new MenuToppingCategory { ToppingId = creamCheese.Id,   CategoryId = catFoodId },
            new MenuToppingCategory { ToppingId = honey.Id,         CategoryId = catFoodId }
        );

        await db.SaveChangesAsync();
        logger.LogInformation("Toppings seeded.");
    }

    private static async Task SeedAdminsAsync(ApplicationDbContext db, ILogger logger)
    {
        if (await db.AdminUsers.AnyAsync()) return;

        var username = Environment.GetEnvironmentVariable("SEED_ADMIN_USERNAME");
        var password = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "SEED_ADMIN_USERNAME or SEED_ADMIN_PASSWORD env vars are not set. " +
                "Skipping admin seed — set these variables and restart to create the initial admin account.");
            return;
        }

        if (password.Length < 8)
        {
            logger.LogWarning("SEED_ADMIN_PASSWORD must be at least 8 characters. Skipping admin seed.");
            return;
        }

        db.AdminUsers.Add(new AdminUser
        {
            Username = username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = AdminRole.Admin,
            IsActive = true,
            MustChangePassword = true
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Admin user '{Username}' seeded (MustChangePassword=true).", username);
    }
}

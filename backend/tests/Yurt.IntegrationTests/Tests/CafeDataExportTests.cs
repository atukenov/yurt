using System.IO.Compression;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Common;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Data export for a cafe that leaves Yurt: only that cafe's rows, never passwords, keys or operators.</summary>
[Collection("Integration")]
public class CafeDataExportTests(YurtWebAppFactory factory)
{
    private async Task<HttpClient> ClientAsync(AdminRole role)
    {
        var client = factory.CreateClient();
        ApiHelpers.Authorize(client, await ApiHelpers.CreateAdminTokenAsync(factory.Services, client,
            username: role == AdminRole.SuperAdmin ? "platform-owner" : $"exp-adm-{Guid.NewGuid():N}"[..20],
            password: "PlatformOwner@123", role: role));
        return client;
    }

    private sealed record Seeded(Guid TenantId, string Slug, string ItemName, string Phone, string Worker);

    /// <summary>A cafe with a menu item, a customer with an order, a staff account and a stored secret.</summary>
    private async Task<Seeded> SeedCafeAsync(string marker)
    {
        var slug = $"exp-{Guid.NewGuid():N}"[..12];
        var tenant = new Tenant { Slug = slug, Name = $"Export {marker}" };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }
        factory.Services.GetRequiredService<ITenantDirectory>().Invalidate();

        using var cafeScope = factory.Services.CreateScope();
        cafeScope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant.Id, slug);
        var cafeDb = cafeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var phone = $"+7{Random.Shared.NextInt64(7000000000, 7999999999)}";
        var worker = $"exp-w-{Guid.NewGuid():N}"[..16];
        var location = new Location { Name = $"Branch {marker}", Address = "x", WorkingHours = "x", ContactPhone = "x" };
        var category = new MenuCategory { Name = $"Drinks {marker}" };
        var item = new MenuItem { Category = category, Name = $"Latte, \"{marker}\"", Price = 1250.5m };
        var customer = new CustomerUser { MobileNumber = phone, PinHash = "secret-pin-hash", FirstName = "=HYPERLINK(1)", LastName = marker };
        var order = new Order { CustomerUser = customer, Location = location, Subtotal = 1250.5m, Total = 1250.5m };
        cafeDb.AddRange(location, category, item, customer, order);
        cafeDb.OrderItems.Add(new OrderItem { Order = order, MenuItem = item, Quantity = 1, UnitPrice = 1250.5m });
        cafeDb.AdminUsers.Add(new AdminUser { Username = worker, PasswordHash = "secret-password-hash", Role = AdminRole.Worker, IsActive = true });
        cafeDb.TenantSecrets.Add(new TenantSecret { Key = "stripe.secret-key", Ciphertext = "secret-ciphertext" });
        await cafeDb.SaveChangesAsync();
        return new Seeded(tenant.Id, slug, item.Name, phone, worker);
    }

    private static async Task<Dictionary<string, string>> ReadZipAsync(HttpResponseMessage res)
    {
        await using var stream = await res.Content.ReadAsStreamAsync();
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var files = new Dictionary<string, string>();
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            files[entry.FullName] = await reader.ReadToEndAsync();
        }
        return files;
    }

    [Fact]
    public async Task Export_HasOnlyThatCafesRows_AndNoSecrets()
    {
        var mine = await SeedCafeAsync($"mine{Guid.NewGuid():N}"[..10]);
        var other = await SeedCafeAsync($"other{Guid.NewGuid():N}"[..11]);
        var op = await ClientAsync(AdminRole.SuperAdmin);

        var res = await op.GetAsync($"/api/v1/platform/cafes/{mine.TenantId}/export");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/zip", res.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith($"{mine.Slug}-data-", res.Content.Headers.ContentDisposition?.FileNameStar ?? res.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        var files = await ReadZipAsync(res);
        Assert.Contains("README.txt", files.Keys);
        Assert.Contains(mine.Phone, files["tables/CustomerUsers.csv"]);
        Assert.Contains("\"Latte, \"\"", files["tables/MenuItems.csv"]); // comma and quotes are CSV-quoted
        Assert.Contains("1250.5", files["tables/Orders.csv"]);
        Assert.Contains("'=HYPERLINK(1)", files["tables/CustomerUsers.csv"]); // formulas are defused
        Assert.Contains(mine.Worker, files["tables/AdminUsers.csv"]);

        var all = string.Concat(files.Values);
        Assert.DoesNotContain(other.Phone, all);
        Assert.DoesNotContain(other.Worker, all);
        Assert.DoesNotContain("platform-owner", all);
        Assert.DoesNotContain("secret-", all);
        Assert.DoesNotContain(files, f => f.Key.EndsWith(".csv") && f.Value.Contains("PasswordHash"));
        Assert.DoesNotContain("tables/TenantSecrets.csv", files.Keys);

        using var manifest = JsonDocument.Parse(files["cafe.json"]);
        Assert.Equal(mine.Slug, manifest.RootElement.GetProperty("cafe").GetProperty("slug").GetString());
        var orders = manifest.RootElement.GetProperty("tables").EnumerateArray().Single(t => t.GetProperty("file").GetString() == "tables/Orders.csv");
        Assert.Equal(1, orders.GetProperty("rows").GetInt32());
    }

    [Fact]
    public async Task ExportOfDefaultCafe_LeavesOutPlatformOperators()
    {
        var op = await ClientAsync(AdminRole.SuperAdmin);
        var files = await ReadZipAsync(await op.GetAsync($"/api/v1/platform/cafes/{Tenant.DefaultId}/export"));
        Assert.DoesNotContain("platform-owner", files["tables/AdminUsers.csv"]);
    }

    [Fact]
    public async Task OnlyOperators_CanExport_AndUnknownCafeIs404()
    {
        var mine = await SeedCafeAsync("auth");
        var admin = await ClientAsync(AdminRole.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/v1/platform/cafes/{mine.TenantId}/export")).StatusCode);

        var op = await ClientAsync(AdminRole.SuperAdmin);
        Assert.Equal(HttpStatusCode.NotFound, (await op.GetAsync($"/api/v1/platform/cafes/{Guid.NewGuid()}/export")).StatusCode);
    }

    [Fact]
    public async Task Export_IsAudited()
    {
        var mine = await SeedCafeAsync("audit");
        var op = await ClientAsync(AdminRole.SuperAdmin);
        (await op.GetAsync($"/api/v1/platform/cafes/{mine.TenantId}/export")).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.Action == "CafeDataExported" && a.EntityId == mine.TenantId.ToString()));
    }

    /// <summary>A new column that holds a hash, key or token must be added to the left-out list on purpose.</summary>
    [Fact]
    public void NoSecretLookingColumn_IsExported()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string[] words = ["Hash", "Secret", "Cipher", "Token", "Password"];
        var risky = CafeDataExporter.ExportedTables(db.Model)
            .SelectMany(t => t.GetProperties().Select(p => (Table: t.ClrType.Name, Column: p.Name)))
            .Where(c => words.Any(w => c.Column.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Where(c => !CafeDataExporter.SkippedColumns.Contains(c.Column) && c.Column != nameof(AdminUser.MustChangePassword))
            .ToList();
        Assert.True(risky.Count == 0, "Check these columns: " + string.Join(", ", risky.Select(c => $"{c.Table}.{c.Column}")));

        var everyTable = db.Model.GetEntityTypes().Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType) && !t.IsOwned()).Count();
        Assert.Equal(everyTable, CafeDataExporter.ExportedTables(db.Model).Count() + CafeDataExporter.SkippedTables.Count);
    }
}

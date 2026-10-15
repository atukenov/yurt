using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.IntegrationTests.Helpers;

internal static class ApiHelpers
{
    internal static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Register a customer and log them in; returns (accessToken, userId).
    internal static async Task<(string Token, Guid UserId)> CreateCustomerAsync(
        HttpClient client, string phone, string pin = "1234")
    {
        var start = await client.PostAsJsonAsync("/api/v1/auth/register/start", new
        {
            mobileNumber = phone, pin4 = pin, firstName = "Test", lastName = "User"
        });
        start.EnsureSuccessStatusCode();
        var startBody = await start.Content.ReadFromJsonAsync<RegisterStartResult>(JsonOpts);

        var verify = await client.PostAsJsonAsync("/api/v1/auth/register/verify", new
        {
            mobileNumber = phone, code = startBody!.DevCode
        });
        verify.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            mobileNumber = phone, pin4 = pin
        });
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<AuthResult>(JsonOpts);
        return (body!.AccessToken, body.UserId);
    }

    // Insert an admin directly in DB (bypasses seeder's MustChangePassword=true),
    // then log in via the API.  Returns the admin's access token.
    internal static async Task<string> CreateAdminTokenAsync(
        IServiceProvider services, HttpClient client,
        string username = "testadmin", string password = "TestAdmin@123",
        bool mustChangePassword = false)
    {
        await using var scope = services.CreateAsyncScope();
        var db     = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (!await db.AdminUsers.AnyAsync(a => a.Username == username))
        {
            db.AdminUsers.Add(new AdminUser
            {
                Username           = username,
                PasswordHash       = hasher.Hash(password),
                Role               = AdminRole.Admin,
                IsActive           = true,
                MustChangePassword = mustChangePassword
            });
            await db.SaveChangesAsync();
        }

        var resp = await client.PostAsJsonAsync("/api/v1/admin/auth/login", new { username, password });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AuthResult>(JsonOpts);
        return body!.AccessToken;
    }

    // Fetch a seeded menu item by its English name.
    internal static async Task<(Guid Id, decimal Price, string Name)> GetMenuItemAsync(
        IServiceProvider services, string name)
    {
        await using var scope = services.CreateAsyncScope();
        var db   = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var item = await db.MenuItems.FirstAsync(m => m.Name == name);
        return (item.Id, item.Price, item.Name);
    }

    // Add a variant to a menu item and return the variant Id.
    internal static async Task<Guid> AddVariantAsync(
        IServiceProvider services, Guid menuItemId, string label, decimal price)
    {
        await using var scope = services.CreateAsyncScope();
        var db      = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var variant = new MenuItemVariant { MenuItemId = menuItemId, Label = label, Price = price, SortOrder = 99 };
        db.MenuItemVariants.Add(variant);
        await db.SaveChangesAsync();
        return variant.Id;
    }

    // Seed a discount code directly in the DB (avoids timezone conversion ambiguity).
    internal static async Task<DiscountCode> SeedDiscountCodeAsync(
        IServiceProvider services,
        string code,
        DiscountType type,
        decimal value,
        bool isActive                = true,
        int?      maxUses            = null,
        decimal?  minOrderAmount     = null,
        DateTime? startsAt           = null,
        DateTime? expiresAt          = null)
    {
        await using var scope = services.CreateAsyncScope();
        var db   = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var entity = new DiscountCode
        {
            Code            = code.ToUpperInvariant(),
            Title           = $"Test {code}",
            DiscountType    = type,
            DiscountValue   = value,
            IsActive        = isActive,
            MaxUses         = maxUses,
            UsedCount       = 0,
            MinOrderAmount  = minOrderAmount,
            StartsAt        = startsAt,
            ExpiresAt       = expiresAt,
        };
        db.DiscountCodes.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    internal static void Authorize(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization =
               new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    internal static void ClearAuth(HttpClient client)
        => client.DefaultRequestHeaders.Authorization = null;

    // ── Local response shapes ────────────────────────────────────────────────

    internal record RegisterStartResult(string MobileNumber, DateTime ExpiresAt, string? DevCode);

    internal record AuthResult(
        string AccessToken,
        string RefreshToken,
        Guid   UserId,
        string DisplayName,
        bool   MustChangePassword = false);

    internal record OrderResult(
        Guid    Id,
        decimal Subtotal,
        decimal DiscountAmount,
        decimal Total,
        string  Status,
        string  PaymentStatus);

    internal record PaymentResult(
        Guid    PaymentId,
        string  InvoiceId,
        string  PaymentUrl,
        decimal Amount,
        string  Status);

    internal record DiscountValidResult(bool IsValid, string Message, decimal DiscountAmount);

    internal record GroupCartResult(Guid Id, string Code, bool IsCreator, List<GroupCartItemResult> Items);
    internal record GroupCartItemResult(Guid Id, Guid MenuItemId, decimal UnitPrice, int Quantity);
}

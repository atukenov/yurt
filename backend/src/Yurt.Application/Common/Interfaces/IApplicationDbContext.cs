using Microsoft.EntityFrameworkCore;
using Yurt.Domain.Entities;

namespace Yurt.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<CustomerUser> CustomerUsers { get; }
    DbSet<PhoneVerification> PhoneVerifications { get; }
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<Location> Locations { get; }
    DbSet<MenuCategory> MenuCategories { get; }
    DbSet<MenuItem> MenuItems { get; }
    DbSet<MenuTopping> MenuToppings { get; }
    DbSet<MenuToppingCategory> MenuToppingCategories { get; }
    DbSet<OrderItemTopping> OrderItemToppings { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PaymentWebhookLog> PaymentWebhookLogs { get; }
    DbSet<Promotion> Promotions { get; }
    DbSet<GroupCart> GroupCarts { get; }
    DbSet<GroupCartItem> GroupCartItems { get; }
    DbSet<GroupCartMember> GroupCartMembers { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<DiscountCode> DiscountCodes { get; }
    DbSet<MenuItemLocation> MenuItemLocations { get; }
    DbSet<MenuItemStock> MenuItemStocks { get; }
    DbSet<Ingredient> Ingredients { get; }
    DbSet<IngredientStock> IngredientStocks { get; }
    DbSet<RecipeLine> RecipeLines { get; }
    DbSet<MenuItemVariant> MenuItemVariants { get; }
    DbSet<UserReport> UserReports { get; }
    DbSet<AppRelease> AppReleases { get; }
    DbSet<CustomerDeviceToken> CustomerDeviceTokens { get; }
    DbSet<ModuleLicense> ModuleLicenses { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<PlatformSetting> PlatformSettings { get; }
    DbSet<TenantSecret> TenantSecrets { get; }
    DbSet<LoyaltyAccount> LoyaltyAccounts { get; }
    DbSet<LoyaltyEntry> LoyaltyEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

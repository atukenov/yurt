using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;

namespace Yurt.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<CustomerUser> CustomerUsers => Set<CustomerUser>();
    public DbSet<PhoneVerification> PhoneVerifications => Set<PhoneVerification>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuTopping> MenuToppings => Set<MenuTopping>();
    public DbSet<MenuToppingCategory> MenuToppingCategories => Set<MenuToppingCategory>();
    public DbSet<OrderItemTopping> OrderItemToppings => Set<OrderItemTopping>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentWebhookLog> PaymentWebhookLogs => Set<PaymentWebhookLog>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<GroupCart> GroupCarts => Set<GroupCart>();
    public DbSet<GroupCartItem> GroupCartItems => Set<GroupCartItem>();
    public DbSet<GroupCartMember> GroupCartMembers => Set<GroupCartMember>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
    public DbSet<MenuItemLocation> MenuItemLocations => Set<MenuItemLocation>();
    public DbSet<MenuItemVariant> MenuItemVariants => Set<MenuItemVariant>();
    public DbSet<UserReport> UserReports => Set<UserReport>();
    public DbSet<AppRelease> AppReleases => Set<AppRelease>();
    public DbSet<CustomerDeviceToken> CustomerDeviceTokens => Set<CustomerDeviceToken>();
    public DbSet<ModuleLicense> ModuleLicenses => Set<ModuleLicense>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}

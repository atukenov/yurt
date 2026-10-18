using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Common;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ITenantContext _tenant;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenant)
        : base(options) => _tenant = tenant;

    /// <summary>Read by the tenant query filters; EF evaluates it per context instance, so each request sees its own cafe.</summary>
    private Guid CurrentTenantId => _tenant.TenantId;

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
    public DbSet<MenuItemStock> MenuItemStocks => Set<MenuItemStock>();
    public DbSet<MenuItemVariant> MenuItemVariants => Set<MenuItemVariant>();
    public DbSet<UserReport> UserReports => Set<UserReport>();
    public DbSet<AppRelease> AppReleases => Set<AppRelease>();
    public DbSet<CustomerDeviceToken> CustomerDeviceTokens => Set<CustomerDeviceToken>();
    public DbSet<ModuleLicense> ModuleLicenses => Set<ModuleLicense>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<TenantSecret> TenantSecrets => Set<TenantSecret>();
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<LoyaltyEntry> LoyaltyEntries => Set<LoyaltyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType)).ToList())
        {
            var entity = modelBuilder.Entity(entityType.ClrType);
            // Rows that existed before the column was added belong to the default tenant.
            entity.Property(nameof(ITenantOwned.TenantId)).HasDefaultValue(Tenant.DefaultId);
            entity.HasIndex(nameof(ITenantOwned.TenantId));
            entity.HasOne(typeof(Tenant)).WithMany().HasForeignKey(nameof(ITenantOwned.TenantId)).OnDelete(DeleteBehavior.Restrict);
            if (entityType.ClrType == typeof(AdminUser)) continue; // own filter below
            typeof(ApplicationDbContext)
                .GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder]);
        }
        ConfigureAdminUserFilter(modelBuilder);
    }

    private void ConfigureAdminUserFilter(ModelBuilder modelBuilder)
        // Platform operators belong to no cafe: they must be able to sign in from any cafe's host.
        => modelBuilder.Entity<AdminUser>().HasQueryFilter(e => e.TenantId == CurrentTenantId || e.Role == AdminRole.SuperAdmin);

    private void ApplyTenantFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantOwned
        => modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                entry.Entity.TenantId = _tenant.TenantId;
        }
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common;
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
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();
    public DbSet<FiscalReceipt> FiscalReceipts => Set<FiscalReceipt>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<OrderStationReady> OrderStationReadies => Set<OrderStationReady>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    public DbSet<PrintAgent> PrintAgents => Set<PrintAgent>();
    public DbSet<ToppingUse> ToppingUses => Set<ToppingUse>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientStock> IngredientStocks => Set<IngredientStock>();
    public DbSet<StockChange> StockChanges => Set<StockChange>();
    public DbSet<RecipeLine> RecipeLines => Set<RecipeLine>();
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

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                entry.Entity.TenantId = _tenant.TenantId;
        }
        await GuardCafeLinksAsync(cancellationToken);
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The query filters stop one cafe from reading another cafe's rows, but a save could still point a row at an id
    /// from another cafe (say a menu item moved into another cafe's category). This refuses such saves, and refuses
    /// moving a row to another cafe.
    /// </summary>
    private async Task GuardCafeLinksAsync(CancellationToken ct)
    {
        var entries = ChangeTracker.Entries<ITenantOwned>().ToList();
        var tracked = new Dictionary<(Type, object), ITenantOwned>();
        foreach (var e in entries)
        {
            if (e.Metadata.FindPrimaryKey() is { Properties.Count: 1 } pk && e.Property(pk.Properties[0].Name).CurrentValue is { } key)
                tracked.TryAdd((e.Metadata.GetRootType().ClrType, key), e.Entity);
        }

        var toCheck = new Dictionary<(Type Principal, string Key, Guid Owner), HashSet<Guid>>();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Modified)
            {
                var cafe = entry.Property(nameof(ITenantOwned.TenantId));
                if (cafe.IsModified && !Equals(cafe.OriginalValue, cafe.CurrentValue))
                    throw new CrossTenantReferenceException($"A {entry.Metadata.ClrType.Name} cannot move to another cafe.");
            }
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            var owner = entry.Entity.TenantId;
            foreach (var fk in entry.Metadata.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType;
                if (!typeof(ITenantOwned).IsAssignableFrom(principal.ClrType) || fk.Properties.Count != 1) continue;
                var link = entry.Property(fk.Properties[0].Name);
                if (entry.State == EntityState.Modified && !link.IsModified) continue;
                if (link.CurrentValue is not Guid id) continue;

                if (fk.PrincipalKey.IsPrimaryKey() && tracked.TryGetValue((principal.GetRootType().ClrType, id), out var target))
                {
                    if (target.TenantId != owner)
                        throw Refused(entry.Metadata.ClrType, principal.ClrType);
                    continue;
                }
                var group = (principal.GetRootType().ClrType, fk.PrincipalKey.Properties[0].Name, owner);
                if (!toCheck.TryGetValue(group, out var ids)) toCheck[group] = ids = [];
                ids.Add(id);
            }
        }

        foreach (var ((principal, key, owner), ids) in toCheck)
        {
            var other = await (Task<bool>)typeof(ApplicationDbContext)
                .GetMethod(nameof(AnyInOtherCafeAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(principal)
                .Invoke(this, [key, ids.ToList(), owner, ct])!;
            if (other) throw Refused(null, principal);
        }
    }

    private Task<bool> AnyInOtherCafeAsync<T>(string key, List<Guid> ids, Guid owner, CancellationToken ct) where T : class, ITenantOwned
        => Set<T>().IgnoreQueryFilters().AnyAsync(e => ids.Contains(EF.Property<Guid>(e, key)) && e.TenantId != owner, ct);

    private static CrossTenantReferenceException Refused(Type? row, Type target)
        => new($"{row?.Name ?? "A row"} cannot point to a {target.Name} of another cafe.");
}

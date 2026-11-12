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
    DbSet<MenuOptionGroup> MenuOptionGroups { get; }
    DbSet<MenuItemTopping> MenuItemToppings { get; }
    DbSet<MenuToppingLocation> MenuToppingLocations { get; }
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
    DbSet<MarketingCampaign> MarketingCampaigns { get; }
    DbSet<MarketingMessage> MarketingMessages { get; }
    DbSet<MenuItemLocation> MenuItemLocations { get; }
    DbSet<MenuItemStock> MenuItemStocks { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<OrderPayment> OrderPayments { get; }
    DbSet<FiscalReceipt> FiscalReceipts { get; }
    DbSet<Station> Stations { get; }
    DbSet<DiningTable> DiningTables { get; }
    DbSet<OrderStationReady> OrderStationReadies { get; }
    DbSet<Printer> Printers { get; }
    DbSet<PrintJob> PrintJobs { get; }
    DbSet<PrintAgent> PrintAgents { get; }
    DbSet<WatchBox> WatchBoxes { get; }
    DbSet<WatchCamera> WatchCameras { get; }
    DbSet<WatchSettings> WatchSettings { get; }
    DbSet<WatchShift> WatchShifts { get; }
    DbSet<WatchMinute> WatchMinutes { get; }
    DbSet<WatchAlert> WatchAlerts { get; }
    DbSet<WatchLive> WatchLive { get; }
    DbSet<WatchReport> WatchReports { get; }
    DbSet<Picture> Pictures { get; }
    DbSet<StaffSalary> StaffSalaries { get; }
    DbSet<PayAdjustment> PayAdjustments { get; }
    DbSet<PayrollMonth> PayrollMonths { get; }
    DbSet<PayrollLine> PayrollLines { get; }
    DbSet<CafeExpense> CafeExpenses { get; }
    DbSet<ToppingUse> ToppingUses { get; }
    DbSet<CashMovement> CashMovements { get; }
    DbSet<Ingredient> Ingredients { get; }
    DbSet<IngredientStock> IngredientStocks { get; }
    DbSet<StockChange> StockChanges { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierProduct> SupplierProducts { get; }
    DbSet<Purchase> Purchases { get; }
    DbSet<PurchaseLine> PurchaseLines { get; }
    DbSet<RecipeLine> RecipeLines { get; }
    DbSet<MenuItemVariant> MenuItemVariants { get; }
    DbSet<UserReport> UserReports { get; }
    DbSet<AppRelease> AppReleases { get; }
    DbSet<CustomerDeviceToken> CustomerDeviceTokens { get; }
    DbSet<ModuleLicense> ModuleLicenses { get; }
    DbSet<CafeBilling> CafeBillings { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<PlatformSetting> PlatformSettings { get; }
    DbSet<TenantSecret> TenantSecrets { get; }
    DbSet<LoyaltyAccount> LoyaltyAccounts { get; }
    DbSet<LoyaltyEntry> LoyaltyEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

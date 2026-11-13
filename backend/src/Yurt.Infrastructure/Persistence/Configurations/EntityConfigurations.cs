using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Infrastructure.Persistence.Configurations;

public class CustomerUserConfiguration : IEntityTypeConfiguration<CustomerUser>
{
    public void Configure(EntityTypeBuilder<CustomerUser> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.TenantId, e.MobileNumber }).IsUnique();
        builder.Property(e => e.MobileNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PinHash).HasMaxLength(200).IsRequired();
    }
}

public class PhoneVerificationConfiguration : IEntityTypeConfiguration<PhoneVerification>
{
    public void Configure(EntityTypeBuilder<PhoneVerification> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.TenantId, e.MobileNumber, e.Purpose }).IsUnique();
        builder.Property(e => e.MobileNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CodeHash).HasMaxLength(200).IsRequired();
        builder.Property(e => e.PinHash).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FirstName).HasMaxLength(100);
        builder.Property(e => e.LastName).HasMaxLength(100);
    }
}

public class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Username).IsUnique();
        builder.Property(e => e.Username).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ManagerPinHash).HasMaxLength(200);
        builder.Property(e => e.StaffPinHash).HasMaxLength(200);
    }
}

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(500).IsRequired();
        builder.Property(e => e.WorkingHours).HasColumnType("text").IsRequired();
        builder.Property(e => e.ContactPhone).HasMaxLength(30).IsRequired();
        builder.Property(e => e.TwoGisUrl).HasMaxLength(1000);
        builder.Property(e => e.PaymentMethods).HasMaxLength(200);
        builder.Property(e => e.FiscalRegisterNumber).HasMaxLength(50);
        builder.Property(e => e.TimeZone).HasMaxLength(64);
    }
}

public class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.NameRu).HasMaxLength(100);
        builder.Property(e => e.NameKk).HasMaxLength(100);
        // Deleting a station only un-routes its categories.
        builder.HasOne<Station>().WithMany().HasForeignKey(e => e.StationId).OnDelete(DeleteBehavior.SetNull);
        // Deleting a schedule puts its categories back on the menu all day.
        builder.HasOne<MenuSchedule>().WithMany().HasForeignKey(e => e.ScheduleId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class StationConfiguration : IEntityTypeConfiguration<Station>
{
    public void Configure(EntityTypeBuilder<Station> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
    }
}

public class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
{
    public void Configure(EntityTypeBuilder<DiningTable> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Label).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(16).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => new { e.LocationId, e.Label }).IsUnique();
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameRu).HasMaxLength(200);
        builder.Property(e => e.NameKk).HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.DescriptionRu).HasMaxLength(1000);
        builder.Property(e => e.DescriptionKk).HasMaxLength(1000);
        builder.Property(e => e.Price).HasPrecision(10, 2);
        builder.Property(e => e.ImageUrl).HasMaxLength(500);
        builder.Property(e => e.Allergens).HasMaxLength(200);
        builder.Property(e => e.Protein).HasPrecision(7, 1);
        builder.Property(e => e.Fat).HasPrecision(7, 1);
        builder.Property(e => e.Carbs).HasPrecision(7, 1);
        builder.HasOne<MenuSchedule>().WithMany().HasForeignKey(e => e.ScheduleId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.Category).WithMany(c => c.Items)
            .HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.CustomerUserId, e.MenuItemId }).IsUnique();
        builder.HasOne(e => e.CustomerUser).WithMany(c => c.Favorites)
            .HasForeignKey(e => e.CustomerUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.MenuItem).WithMany(m => m.Favorites)
            .HasForeignKey(e => e.MenuItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.PaymentStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.DeclineReason).HasMaxLength(500);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(e => new { e.CustomerUserId, e.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        // A counter sale has no guest account, so its key is unique per branch instead (a till that resends a sale
        // after a lost answer, or after coming back online, never makes a second order).
        builder.HasIndex(e => new { e.LocationId, e.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("IX_Orders_LocationId_IdempotencyKey_Counter")
            .HasFilter("\"IdempotencyKey\" IS NOT NULL AND \"CustomerUserId\" IS NULL");
        // Kept on purpose: the filtered index above is no use for ordinary branch queries.
        builder.HasIndex(e => e.LocationId);
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(20).HasDefaultValue(OrderSource.App);
        builder.Property(e => e.OrderType).HasConversion<string>().HasMaxLength(20).HasDefaultValue(OrderType.TakeAway);
        builder.Property(e => e.Subtotal).HasPrecision(10, 2);
        builder.Property(e => e.DiscountAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.TipAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.TipMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Total).HasPrecision(10, 2);
        builder.HasOne(e => e.CustomerUser).WithMany(c => c.Orders)
            .HasForeignKey(e => e.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Location).WithMany(l => l.Orders)
            .HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DiscountCode).WithMany(d => d.Orders)
            .HasForeignKey(e => e.DiscountCodeId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(e => e.TableLabel).HasMaxLength(30);
        // Deleting a table keeps its orders; they still show the label.
        builder.HasOne<DiningTable>().WithMany().HasForeignKey(e => e.DiningTableId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class DiscountCodeConfiguration : IEntityTypeConfiguration<DiscountCode>
{
    public void Configure(EntityTypeBuilder<DiscountCode> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.DiscountType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.DiscountValue).HasPrecision(10, 2);
        builder.Property(e => e.MinOrderAmount).HasPrecision(10, 2);
        builder.Property(e => e.UsedCount).HasDefaultValue(0);
        // Personal codes from guest messages: deleting the guest or the message removes / unlinks them.
        builder.HasOne<CustomerUser>().WithMany().HasForeignKey(e => e.CustomerUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MarketingCampaign>().WithMany().HasForeignKey(e => e.CampaignId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class MarketingCampaignConfiguration : IEntityTypeConfiguration<MarketingCampaign>
{
    public void Configure(EntityTypeBuilder<MarketingCampaign> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Segment).HasMaxLength(40);
        builder.Property(e => e.Title).HasMaxLength(120);
        builder.Property(e => e.TitleRu).HasMaxLength(120);
        builder.Property(e => e.TitleKk).HasMaxLength(120);
        builder.Property(e => e.Body).HasMaxLength(500);
        builder.Property(e => e.BodyRu).HasMaxLength(500);
        builder.Property(e => e.BodyKk).HasMaxLength(500);
        builder.Property(e => e.DiscountType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.DiscountValue).HasPrecision(10, 2);
        builder.Property(e => e.MinOrderAmount).HasPrecision(10, 2);
    }
}

public class MarketingMessageConfiguration : IEntityTypeConfiguration<MarketingMessage>
{
    public void Configure(EntityTypeBuilder<MarketingMessage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne(e => e.Campaign).WithMany(c => c.Messages).HasForeignKey(e => e.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CustomerUser>().WithMany().HasForeignKey(e => e.CustomerUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DiscountCode>().WithMany().HasForeignKey(e => e.DiscountCodeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(e => new { e.CustomerUserId, e.CreatedAt });
        builder.HasIndex(e => new { e.CampaignId, e.CustomerUserId });
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MenuItemName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.UnitPrice).HasPrecision(10, 2);
        builder.Property(e => e.LineTotal).HasPrecision(10, 2);
        builder.Property(e => e.UnitCost).HasPrecision(12, 4);
        builder.HasOne(e => e.Order).WithMany(o => o.Items)
            .HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.MenuItem).WithMany(m => m.OrderItems)
            .HasForeignKey(e => e.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        // What a combo line contained is read with the line (kitchen ticket, stock, order screens).
        builder.Navigation(e => e.ComboParts).AutoInclude();
    }
}

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.TitleRu).HasMaxLength(200);
        builder.Property(e => e.TitleKk).HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.DescriptionRu).HasMaxLength(2000);
        builder.Property(e => e.DescriptionKk).HasMaxLength(2000);
        builder.Property(e => e.ImageUrl).HasMaxLength(500);
    }
}

public class MenuToppingConfiguration : IEntityTypeConfiguration<MenuTopping>
{
    public void Configure(EntityTypeBuilder<MenuTopping> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameRu).HasMaxLength(200);
        builder.Property(e => e.NameKk).HasMaxLength(200);
        builder.Property(e => e.Price).HasPrecision(10, 2);
    }
}

public class MenuToppingCategoryConfiguration : IEntityTypeConfiguration<MenuToppingCategory>
{
    public void Configure(EntityTypeBuilder<MenuToppingCategory> builder)
    {
        builder.HasKey(e => new { e.ToppingId, e.CategoryId });
        builder.HasOne(e => e.Topping).WithMany(t => t.ToppingCategories)
            .HasForeignKey(e => e.ToppingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Category).WithMany(c => c.ToppingLinks)
            .HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MenuOptionGroupConfiguration : IEntityTypeConfiguration<MenuOptionGroup>
{
    public void Configure(EntityTypeBuilder<MenuOptionGroup> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Key).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(100);
        builder.Property(e => e.TitleRu).HasMaxLength(100);
        builder.Property(e => e.TitleKk).HasMaxLength(100);
        builder.HasIndex(e => new { e.TenantId, e.Key }).IsUnique();
    }
}

public class MenuScheduleConfiguration : IEntityTypeConfiguration<MenuSchedule>
{
    public void Configure(EntityTypeBuilder<MenuSchedule> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasOne<Location>().WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MenuTimePriceConfiguration : IEntityTypeConfiguration<MenuTimePrice>
{
    public void Configure(EntityTypeBuilder<MenuTimePrice> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Price).HasPrecision(10, 2);
        builder.Property(e => e.PercentOff).HasPrecision(5, 2);
        builder.HasOne(e => e.Schedule).WithMany(s => s.Prices).HasForeignKey(e => e.ScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MenuItem>().WithMany().HasForeignKey(e => e.MenuItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MenuCategory>().WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MenuItemToppingConfiguration : IEntityTypeConfiguration<MenuItemTopping>
{
    public void Configure(EntityTypeBuilder<MenuItemTopping> builder)
    {
        builder.HasKey(e => new { e.MenuItemId, e.ToppingId });
        builder.HasOne(e => e.MenuItem).WithMany(m => m.ToppingLinks)
            .HasForeignKey(e => e.MenuItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Topping).WithMany()
            .HasForeignKey(e => e.ToppingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ComboPartConfiguration : IEntityTypeConfiguration<ComboPart>
{
    public void Configure(EntityTypeBuilder<ComboPart> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne(e => e.Combo).WithMany(m => m.ComboParts)
            .HasForeignKey(e => e.ComboId).OnDelete(DeleteBehavior.Cascade);
        // A dish that sits inside a combo cannot be deleted until the combo drops it.
        builder.HasOne(e => e.Item).WithMany()
            .HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.ComboId);
        builder.HasIndex(e => e.ItemId);
    }
}

public class OrderItemComboPartConfiguration : IEntityTypeConfiguration<OrderItemComboPart>
{
    public void Configure(EntityTypeBuilder<OrderItemComboPart> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.VariantLabel).HasMaxLength(100);
        builder.Property(e => e.RegularPrice).HasPrecision(10, 2);
        builder.HasOne(e => e.OrderItem).WithMany(oi => oi.ComboParts)
            .HasForeignKey(e => e.OrderItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.OrderItemId);
    }
}

public class MenuToppingLocationConfiguration : IEntityTypeConfiguration<MenuToppingLocation>
{
    public void Configure(EntityTypeBuilder<MenuToppingLocation> builder)
    {
        builder.HasKey(e => new { e.ToppingId, e.LocationId });
        builder.Property(e => e.Price).HasPrecision(10, 2);
        builder.HasOne(e => e.Topping).WithMany(t => t.LocationPrices)
            .HasForeignKey(e => e.ToppingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Location).WithMany()
            .HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderItemToppingConfiguration : IEntityTypeConfiguration<OrderItemTopping>
{
    public void Configure(EntityTypeBuilder<OrderItemTopping> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ToppingName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Price).HasPrecision(10, 2);
        builder.HasOne(e => e.OrderItem).WithMany(oi => oi.Toppings)
            .HasForeignKey(e => e.OrderItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.InvoiceId).IsUnique();
        builder.Property(e => e.Provider).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Currency).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        // Stripe Checkout links can be longer than 500 characters.
        builder.Property(e => e.PaymentUrl).HasColumnType("text").IsRequired();
        builder.Property(e => e.ProviderPaymentId).HasMaxLength(255);
        builder.HasIndex(e => e.ProviderPaymentId);
        builder.Property(e => e.QrCode).HasColumnType("text").IsRequired();
        builder.Property(e => e.RawResponse).HasColumnType("text");
        builder.Property(e => e.Amount).HasPrecision(10, 2);
        builder.HasOne(e => e.Order).WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentWebhookLogConfiguration : IEntityTypeConfiguration<PaymentWebhookLog>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookLog> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Payload).HasColumnType("text").IsRequired();
        builder.Property(e => e.Headers).HasColumnType("text").IsRequired();
        builder.Property(e => e.Processed).HasDefaultValue(false);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Token).IsUnique();
        builder.Property(e => e.Token).HasMaxLength(128).IsRequired();
        builder.Property(e => e.UserType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ReplacedByToken).HasMaxLength(128);
        builder.Property(e => e.CreatedByIp).HasMaxLength(50);
    }
}

public class MenuItemLocationConfiguration : IEntityTypeConfiguration<MenuItemLocation>
{
    public void Configure(EntityTypeBuilder<MenuItemLocation> builder)
    {
        builder.HasKey(e => new { e.MenuItemId, e.LocationId });
        builder.Property(e => e.Price).HasPrecision(10, 2);
        builder.HasOne(e => e.MenuItem).WithMany(m => m.MenuItemLocations)
            .HasForeignKey(e => e.MenuItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Location).WithMany()
            .HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MenuItemStockConfiguration : IEntityTypeConfiguration<MenuItemStock>
{
    public void Configure(EntityTypeBuilder<MenuItemStock> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne(e => e.MenuItem).WithMany().HasForeignKey(e => e.MenuItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.MenuItemId, e.LocationId }).IsUnique();
    }
}

public class ToppingUseConfiguration : IEntityTypeConfiguration<ToppingUse>
{
    public void Configure(EntityTypeBuilder<ToppingUse> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Quantity).HasPrecision(18, 3);
        builder.HasOne(e => e.Topping).WithMany().HasForeignKey(e => e.ToppingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Ingredient).WithMany().HasForeignKey(e => e.IngredientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.ToppingId, e.IngredientId }).IsUnique();
    }
}

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.OpenedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ClosedBy).HasMaxLength(100);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.Property(e => e.FiscalZReportError).HasMaxLength(500);
        builder.Property(e => e.OpeningCash).HasPrecision(18, 2);
        builder.Property(e => e.CountedCash).HasPrecision(18, 2);
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        // One open shift per branch, enforced by the database too.
        builder.HasIndex(e => new { e.TenantId, e.LocationId }).IsUnique().HasFilter("\"ClosedAt\" IS NULL");
        builder.HasIndex(e => new { e.LocationId, e.OpenedAt });
    }
}

public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Reason).HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.HasOne(e => e.Shift).WithMany(s => s.Movements).HasForeignKey(e => e.ShiftId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderPaymentConfiguration : IEntityTypeConfiguration<OrderPayment>
{
    public void Configure(EntityTypeBuilder<OrderPayment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.HasOne(e => e.Order).WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.OrderId);
    }
}

public class FiscalReceiptConfiguration : IEntityTypeConfiguration<FiscalReceipt>
{
    public void Configure(EntityTypeBuilder<FiscalReceipt> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Provider).HasMaxLength(20).IsRequired();
        builder.Property(e => e.LastError).HasMaxLength(500);
        builder.Property(e => e.RegisterNumber).HasMaxLength(50);
        builder.Property(e => e.CheckNumber).HasMaxLength(100);
        builder.Property(e => e.TicketUrl).HasMaxLength(1000);
        builder.HasOne(e => e.Order).WithMany().HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
        // At most one sale and one return receipt per order, enforced by the database too.
        builder.HasIndex(e => new { e.OrderId, e.Kind }).IsUnique();
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt });
        builder.HasIndex(e => new { e.LocationId, e.Status });
    }
}

public class OrderStationReadyConfiguration : IEntityTypeConfiguration<OrderStationReady>
{
    public void Configure(EntityTypeBuilder<OrderStationReady> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne(e => e.Order).WithMany(o => o.ReadyStations).HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Station>().WithMany().HasForeignKey(e => e.StationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.OrderId, e.StationId }).IsUnique();
    }
}

public class PrinterConfiguration : IEntityTypeConfiguration<Printer>
{
    public void Configure(EntityTypeBuilder<Printer> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Kind).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Host).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Language).HasMaxLength(5).IsRequired();
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        // Deleting a station turns its printer into an every-item printer rather than losing the printer.
        builder.HasOne<Station>().WithMany().HasForeignKey(e => e.StationId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
    public void Configure(EntityTypeBuilder<PrintJob> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.LastError).HasMaxLength(300);
        builder.HasOne(e => e.Printer).WithMany().HasForeignKey(e => e.PrinterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.Status, e.CreatedAt });
    }
}

public class PrintAgentConfiguration : IEntityTypeConfiguration<PrintAgent>
{
    public void Configure(EntityTypeBuilder<PrintAgent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.KeyHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(e => e.KeyHash).IsUnique();
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.TenantId, e.LocationId }).IsUnique();
    }
}

public class PictureConfiguration : IEntityTypeConfiguration<Picture>
{
    public void Configure(EntityTypeBuilder<Picture> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ContentType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Data).IsRequired();
    }
}

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Unit).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CostPerUnit).HasPrecision(18, 4);
        builder.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
    }
}

public class IngredientStockConfiguration : IEntityTypeConfiguration<IngredientStock>
{
    public void Configure(EntityTypeBuilder<IngredientStock> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Quantity).HasPrecision(18, 3);
        builder.Property(e => e.LowStockAt).HasPrecision(18, 3);
        builder.Property(e => e.OrderUpTo).HasPrecision(18, 3);
        builder.HasOne(e => e.Ingredient).WithMany().HasForeignKey(e => e.IngredientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.IngredientId, e.LocationId }).IsUnique();
    }
}

public class StockChangeConfiguration : IEntityTypeConfiguration<StockChange>
{
    public void Configure(EntityTypeBuilder<StockChange> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Unit).HasMaxLength(20);
        builder.Property(e => e.ChangedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.OldQuantity).HasPrecision(18, 3);
        builder.Property(e => e.NewQuantity).HasPrecision(18, 3);
        builder.Property(e => e.Type).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(20);
        builder.Property(e => e.Note).HasMaxLength(300);
        builder.Property(e => e.UnitCost).HasPrecision(18, 4);
        builder.Property(e => e.OtherLocationName).HasMaxLength(200);
        builder.HasIndex(e => e.PurchaseId);
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.LocationId, e.CreatedAt });
    }
}

public class RecipeLineConfiguration : IEntityTypeConfiguration<RecipeLine>
{
    public void Configure(EntityTypeBuilder<RecipeLine> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Quantity).HasPrecision(18, 3);
        builder.HasOne(e => e.MenuItem).WithMany().HasForeignKey(e => e.MenuItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Variant).WithMany().HasForeignKey(e => e.VariantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Ingredient).WithMany().HasForeignKey(e => e.IngredientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.MenuItemId);
        builder.HasIndex(e => e.IngredientId);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Action).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityId).HasMaxLength(100);
        builder.Property(e => e.PerformedByUsername).HasMaxLength(200);
        builder.Property(e => e.Details).HasMaxLength(2000);
        builder.Property(e => e.IpAddress).HasMaxLength(50);
    }
}

public class MenuItemVariantConfiguration : IEntityTypeConfiguration<MenuItemVariant>
{
    public void Configure(EntityTypeBuilder<MenuItemVariant> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Label).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LabelRu).HasMaxLength(100);
        builder.Property(e => e.LabelKk).HasMaxLength(100);
        builder.Property(e => e.Price).HasPrecision(10, 2);
        builder.HasOne(e => e.MenuItem).WithMany(m => m.Variants)
            .HasForeignKey(e => e.MenuItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CustomerDeviceTokenConfiguration : IEntityTypeConfiguration<CustomerDeviceToken>
{
    public void Configure(EntityTypeBuilder<CustomerDeviceToken> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Token).IsUnique();
        builder.Property(e => e.Token).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Platform).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Language).HasMaxLength(5);
        builder.HasOne(e => e.Customer).WithMany(c => c.DeviceTokens)
            .HasForeignKey(e => e.CustomerUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserReportConfiguration : IEntityTypeConfiguration<UserReport>
{
    public void Configure(EntityTypeBuilder<UserReport> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Text).HasMaxLength(2000).IsRequired();
        builder.HasOne(e => e.Customer)
               .WithMany()
               .HasForeignKey(e => e.CustomerId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppReleaseConfiguration : IEntityTypeConfiguration<AppRelease>
{
    public void Configure(EntityTypeBuilder<AppRelease> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Version).HasMaxLength(20).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Version }).IsUnique();
        builder.Property(e => e.NotesEn).HasColumnType("text").IsRequired();
        builder.Property(e => e.NotesRu).HasColumnType("text").IsRequired();
        builder.Property(e => e.NotesKk).HasColumnType("text").IsRequired();
    }
}

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Slug).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.Slug).IsUnique();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Domain).HasMaxLength(255);
        builder.Property(e => e.LoyaltyLedger).HasMaxLength(20);
        builder.Property(e => e.FiscalProvider).HasMaxLength(20);
        builder.Property(e => e.PaymentProvider).HasMaxLength(20);
        builder.Property(e => e.FiscalSkipMethods).HasMaxLength(200);
        builder.HasIndex(e => e.Domain).IsUnique();
    }
}

public class ModuleLicenseConfiguration : IEntityTypeConfiguration<ModuleLicense>
{
    public void Configure(EntityTypeBuilder<ModuleLicense> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ModuleKey).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.ModuleKey }).IsUnique();
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Notes).HasMaxLength(1000);
    }
}

public class CafeBillingConfiguration : IEntityTypeConfiguration<CafeBilling>
{
    public void Configure(EntityTypeBuilder<CafeBilling> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.TenantId).IsUnique();
        builder.Property(e => e.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(e => e.LastPaidAmount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.ModuleKeys).HasMaxLength(500);
        builder.Property(e => e.ModulePrices).HasMaxLength(2000);
        builder.Property(e => e.StripeCustomerId).HasMaxLength(100);
        builder.Property(e => e.StripeSubscriptionId).HasMaxLength(100);
        builder.HasIndex(e => e.StripeSubscriptionId);
        builder.Property(e => e.Status).HasMaxLength(30);
        builder.Property(e => e.LastInvoiceId).HasMaxLength(100);
    }
}

public class PlatformSettingConfiguration : IEntityTypeConfiguration<PlatformSetting>
{
    public void Configure(EntityTypeBuilder<PlatformSetting> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Key }).IsUnique();
        builder.Property(e => e.Value).HasMaxLength(2000).IsRequired();
    }
}

public class TenantSecretConfiguration : IEntityTypeConfiguration<TenantSecret>
{
    public void Configure(EntityTypeBuilder<TenantSecret> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Key }).IsUnique();
        builder.Property(e => e.Ciphertext).IsRequired();
    }
}

public class LoyaltyAccountConfiguration : IEntityTypeConfiguration<LoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccount> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Phone).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100);
        builder.Property(e => e.Surname).HasMaxLength(100);
        builder.Property(e => e.Version).IsRowVersion();
        builder.HasIndex(e => new { e.TenantId, e.Phone }).IsUnique();
    }
}

public class LoyaltyEntryConfiguration : IEntityTypeConfiguration<LoyaltyEntry>
{
    public void Configure(EntityTypeBuilder<LoyaltyEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Comment).HasMaxLength(500);
        builder.HasOne(e => e.Account).WithMany(a => a.Entries).HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.AccountId, e.CreatedAt });
    }
}

// ── Staff watch (staff_watch) ───────────────────────────────────────────────

public class WatchBoxConfiguration : IEntityTypeConfiguration<WatchBox>
{
    public void Configure(EntityTypeBuilder<WatchBox> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.KeyHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(e => e.KeyHash).IsUnique();
        builder.Property(e => e.CameraNames).HasMaxLength(500);
        builder.Property(e => e.Version).HasMaxLength(20);
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.TenantId, e.LocationId }).IsUnique();
    }
}

public class WatchCameraConfiguration : IEntityTypeConfiguration<WatchCamera>
{
    public void Configure(EntityTypeBuilder<WatchCamera> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(40).IsRequired();
        builder.Property(e => e.ZonesJson).IsRequired();
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.LocationId, e.Name }).IsUnique();
    }
}

public class WatchSettingsConfiguration : IEntityTypeConfiguration<WatchSettings>
{
    public void Configure(EntityTypeBuilder<WatchSettings> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Open).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Close).HasMaxLength(5).IsRequired();
        builder.Property(e => e.SettingsJson).IsRequired();
        builder.Property(e => e.TelegramChatId).HasMaxLength(40);
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.LocationId).IsUnique();
    }
}

public class WatchShiftConfiguration : IEntityTypeConfiguration<WatchShift>
{
    public void Configure(EntityTypeBuilder<WatchShift> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Day).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Station).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Start).HasMaxLength(5).IsRequired();
        builder.Property(e => e.End).HasMaxLength(5).IsRequired();
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.LocationId, e.SortOrder });
    }
}

public class WatchMinuteConfiguration : IEntityTypeConfiguration<WatchMinute>
{
    public void Configure(EntityTypeBuilder<WatchMinute> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Zone).HasMaxLength(40).IsRequired();
        builder.HasOne<Location>().WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.LocationId, e.Minute, e.Zone }).IsUnique();
    }
}

public class WatchAlertConfiguration : IEntityTypeConfiguration<WatchAlert>
{
    public void Configure(EntityTypeBuilder<WatchAlert> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Zone).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Text).HasMaxLength(1000).IsRequired();
        builder.HasOne<Location>().WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.LocationId, e.At });
        // the box may send an alert again when an answer got lost; the same time, kind and zone is stored once
        builder.HasIndex(e => new { e.LocationId, e.At, e.Kind, e.Zone }).IsUnique();
    }
}

public class WatchLiveConfiguration : IEntityTypeConfiguration<WatchLive>
{
    public void Configure(EntityTypeBuilder<WatchLive> builder)
    {
        builder.ToTable("WatchLive");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.StateJson).IsRequired();
        builder.HasOne<Location>().WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.LocationId).IsUnique();
    }
}

public class WatchReportConfiguration : IEntityTypeConfiguration<WatchReport>
{
    public void Configure(EntityTypeBuilder<WatchReport> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Text).HasMaxLength(8000).IsRequired();
        builder.Property(e => e.DataJson).IsRequired();
        builder.HasOne<Location>().WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.LocationId, e.Day }).IsUnique();
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ContactName).HasMaxLength(100);
        builder.Property(e => e.Phone).HasMaxLength(30);
        builder.Property(e => e.Bin).HasMaxLength(20);
        builder.Property(e => e.PaymentTerms).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
    }
}

public class SupplierProductConfiguration : IEntityTypeConfiguration<SupplierProduct>
{
    public void Configure(EntityTypeBuilder<SupplierProduct> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PackSize).HasPrecision(18, 3);
        builder.Property(e => e.PackName).HasMaxLength(100);
        builder.Property(e => e.PackPrice).HasPrecision(18, 2);
        builder.HasOne(e => e.Supplier).WithMany(s => s.Products).HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Ingredient).WithMany().HasForeignKey(e => e.IngredientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.SupplierId, e.IngredientId }).IsUnique();
    }
}

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Number).HasMaxLength(50);
        builder.Property(e => e.Note).HasMaxLength(300);
        builder.Property(e => e.PhotoUrl).HasMaxLength(500);
        builder.Property(e => e.Total).HasPrecision(18, 2);
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PostedBy).HasMaxLength(100);
        builder.Property(e => e.CancelledBy).HasMaxLength(100);
        // Suppliers with invoices are switched off, never deleted.
        builder.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.LocationId, e.Date });
        builder.HasIndex(e => new { e.SupplierId, e.Status });
    }
}

public class PurchaseLineConfiguration : IEntityTypeConfiguration<PurchaseLine>
{
    public void Configure(EntityTypeBuilder<PurchaseLine> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.IngredientName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Unit).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Quantity).HasPrecision(18, 3);
        builder.Property(e => e.Total).HasPrecision(18, 2);
        builder.Property(e => e.CostBefore).HasPrecision(18, 4);
        builder.HasOne(e => e.Purchase).WithMany(p => p.Lines).HasForeignKey(e => e.PurchaseId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StaffSalaryConfiguration : IEntityTypeConfiguration<StaffSalary>
{
    public void Configure(EntityTypeBuilder<StaffSalary> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MonthlySalary).HasPrecision(12, 2);
        builder.Property(e => e.HourlyRate).HasPrecision(12, 2);
        builder.HasIndex(e => e.AdminUserId).IsUnique();
    }
}

public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EditedBy).HasMaxLength(100);
        builder.Property(e => e.EditNote).HasMaxLength(200);
        builder.HasIndex(e => new { e.AdminUserId, e.ClockInAt });
    }
}

public class PayAdjustmentConfiguration : IEntityTypeConfiguration<PayAdjustment>
{
    public void Configure(EntityTypeBuilder<PayAdjustment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Month).HasMaxLength(7).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(12, 2);
        builder.Property(e => e.Reason).HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => new { e.Month, e.AdminUserId });
    }
}

public class PayrollMonthConfiguration : IEntityTypeConfiguration<PayrollMonth>
{
    public void Configure(EntityTypeBuilder<PayrollMonth> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Month).HasMaxLength(7).IsRequired();
        builder.Property(e => e.ClosedBy).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.Month }).IsUnique();
    }
}

public class PayrollLineConfiguration : IEntityTypeConfiguration<PayrollLine>
{
    public void Configure(EntityTypeBuilder<PayrollLine> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Username).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Role).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Salary).HasPrecision(12, 2);
        builder.Property(e => e.Hours).HasPrecision(9, 2);
        builder.Property(e => e.HourlyRate).HasPrecision(12, 2);
        builder.Property(e => e.HourlyPay).HasPrecision(12, 2);
        builder.Property(e => e.Bonuses).HasPrecision(12, 2);
        builder.Property(e => e.Deductions).HasPrecision(12, 2);
        builder.Property(e => e.Total).HasPrecision(12, 2);
        builder.HasOne(e => e.PayrollMonth).WithMany(m => m.Lines).HasForeignKey(e => e.PayrollMonthId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CafeExpenseConfiguration : IEntityTypeConfiguration<CafeExpense>
{
    public void Configure(EntityTypeBuilder<CafeExpense> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Month).HasMaxLength(7).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(14, 2);
        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.HasOne(e => e.Location).WithMany()
            .HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.Month);
    }
}

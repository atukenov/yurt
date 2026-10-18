using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yurt.Domain.Entities;

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
        builder.Property(e => e.Subtotal).HasPrecision(10, 2);
        builder.Property(e => e.DiscountAmount).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.Total).HasPrecision(10, 2);
        builder.HasOne(e => e.CustomerUser).WithMany(c => c.Orders)
            .HasForeignKey(e => e.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Location).WithMany(l => l.Orders)
            .HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DiscountCode).WithMany(d => d.Orders)
            .HasForeignKey(e => e.DiscountCodeId).OnDelete(DeleteBehavior.SetNull);
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
        builder.HasOne(e => e.Order).WithMany(o => o.Items)
            .HasForeignKey(e => e.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.MenuItem).WithMany(m => m.OrderItems)
            .HasForeignKey(e => e.MenuItemId).OnDelete(DeleteBehavior.Restrict);
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
        builder.Property(e => e.PaymentUrl).HasMaxLength(500).IsRequired();
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

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.OpenedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ClosedBy).HasMaxLength(100);
        builder.Property(e => e.Note).HasMaxLength(500);
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

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Unit).HasMaxLength(20).IsRequired();
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
        builder.HasOne(e => e.Ingredient).WithMany().HasForeignKey(e => e.IngredientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Location).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.IngredientId, e.LocationId }).IsUnique();
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

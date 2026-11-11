using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>Someone the cafe buys ingredients from (own_pos purchasing).</summary>
public class Supplier : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }

    /// <summary>Phone or WhatsApp number, used for the "send order" link.</summary>
    public string? Phone { get; set; }

    /// <summary>Kazakh business id (BIN/IIN) of the supplier, for paperwork.</summary>
    public string? Bin { get; set; }

    /// <summary>How the cafe pays: see <see cref="SupplierPaymentTerms"/>.</summary>
    public string PaymentTerms { get; set; } = SupplierPaymentTerms.Cash;

    /// <summary>For "later": how many days after the delivery the invoice must be paid.</summary>
    public int? PayInDays { get; set; }

    public string? Note { get; set; }

    /// <summary>A supplier with invoices is never deleted, only switched off, so old invoices keep their supplier.</summary>
    public bool IsActive { get; set; } = true;

    public List<SupplierProduct> Products { get; set; } = [];
}

public static class SupplierPaymentTerms
{
    public const string Cash = "cash";
    public const string Transfer = "transfer";
    public const string Later = "later";

    public static readonly IReadOnlyList<string> All = [Cash, Transfer, Later];
}

/// <summary>
/// An ingredient a supplier sells, in what pack and at what usual price. For example milk sold in boxes of 12 L
/// (<c>PackSize</c> 12000 when the ingredient is counted in ml) for 6240.
/// </summary>
public class SupplierProduct : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid SupplierId { get; set; }
    public Guid IngredientId { get; set; }

    /// <summary>How many of the ingredient's units one pack holds (at least a little more than zero).</summary>
    public decimal PackSize { get; set; } = 1m;

    /// <summary>Free text for the pack, for example "box of 12 × 1 L".</summary>
    public string? PackName { get; set; }

    /// <summary>The usual price of one pack. Null when not known.</summary>
    public decimal? PackPrice { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}

/// <summary>
/// An order to a supplier or a delivery (purchase invoice) at one branch. An order becomes the invoice when the goods
/// arrive: the same document is filled in and posted, which puts everything on the shelf in one go.
/// </summary>
public class Purchase : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    /// <summary>See <see cref="PurchaseStatuses"/>.</summary>
    public string Status { get; set; } = PurchaseStatuses.Draft;

    /// <summary>The supplier's invoice number, as printed on the paper.</summary>
    public string? Number { get; set; }

    /// <summary>Day of the delivery (or of the order), UTC midnight.</summary>
    public DateTime Date { get; set; } = DateTime.UtcNow.Date;

    public string? Note { get; set; }

    /// <summary>Link to a photo of the paper invoice (an uploaded picture).</summary>
    public string? PhotoUrl { get; set; }

    /// <summary>Sum of the line totals that are known.</summary>
    public decimal Total { get; set; }

    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }

    /// <summary>When an unpaid posted invoice must be paid (from the supplier's "pay in N days").</summary>
    public DateTime? DueDate { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public string? PostedBy { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }

    public List<PurchaseLine> Lines { get; set; } = [];
}

public static class PurchaseStatuses
{
    /// <summary>Sent (or to be sent) to the supplier; nothing is on the shelf yet.</summary>
    public const string Ordered = "ordered";
    /// <summary>A delivery being typed in; not on the shelf yet.</summary>
    public const string Draft = "draft";
    /// <summary>On the shelf: counts and costs were updated.</summary>
    public const string Posted = "posted";
    /// <summary>Posted by mistake and taken back off the shelf.</summary>
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlyList<string> All = [Ordered, Draft, Posted, Cancelled];
}

/// <summary>
/// One ingredient on an order or invoice. Kept with the ingredient's name and unit (no foreign key), so the invoice
/// still reads after the ingredient is deleted.
/// </summary>
public class PurchaseLine : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid PurchaseId { get; set; }
    public Purchase Purchase { get; set; } = null!;

    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    /// <summary>Amount in the ingredient's unit (g, ml, pcs).</summary>
    public decimal Quantity { get; set; }

    /// <summary>What this line cost in total. Null when not known (an order without prices).</summary>
    public decimal? Total { get; set; }

    /// <summary>The ingredient's cost per unit just before posting, so cancelling can put it back.</summary>
    public decimal? CostBefore { get; set; }

    public int SortOrder { get; set; }
}

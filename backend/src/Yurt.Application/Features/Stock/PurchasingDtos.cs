namespace Yurt.Application.Features.Stock;

// ── Suppliers ──

/// <summary>An ingredient a supplier sells: pack size in the ingredient's unit, pack name and usual pack price.</summary>
public record SupplierProductDto(
    Guid IngredientId, string IngredientName, string Unit, decimal PackSize, string? PackName, decimal? PackPrice);

/// <summary>
/// A supplier with what it sells. <c>Owed</c> is the total of posted invoices not paid yet; <c>Overdue</c> the part
/// of it past its due date. Both are only filled for cafe admins.
/// </summary>
public record SupplierDto(
    Guid Id, string Name, string? ContactName, string? Phone, string? Bin, string PaymentTerms, int? PayInDays,
    string? Note, bool IsActive, List<SupplierProductDto> Products, decimal? Owed = null, decimal? Overdue = null,
    int Invoices = 0);

public record SaveSupplierProductDto(Guid IngredientId, decimal PackSize, string? PackName, decimal? PackPrice);

public record SaveSupplierDto(
    string Name, string? ContactName, string? Phone, string? Bin, string? PaymentTerms, int? PayInDays, string? Note,
    bool? IsActive, List<SaveSupplierProductDto>? Products);

// ── Orders and invoices ──

/// <summary>
/// One ingredient on an order or invoice. <c>Total</c> is what the whole line cost (null when not known yet).
/// <c>Left</c> is what the branch has now (null when it does not count the ingredient); only filled for one document.
/// </summary>
public record PurchaseLineDto(
    Guid Id, Guid IngredientId, string IngredientName, string Unit, decimal Quantity, decimal? Total,
    decimal? UnitPrice);

/// <summary>An order to a supplier or a delivery invoice. <c>Status</c> is ordered, draft, posted or cancelled.</summary>
public record PurchaseDto(
    Guid Id, Guid SupplierId, string SupplierName, string? SupplierPhone, Guid LocationId, string LocationName,
    string Status, string? Number, DateTime Date, string? Note, string? PhotoUrl, decimal Total, bool IsPaid,
    DateTime? PaidAt, DateTime? DueDate, bool IsOverdue, string CreatedBy, DateTime CreatedAt, string? PostedBy,
    DateTime? PostedAt, string? CancelledBy, DateTime? CancelledAt, int LineCount, List<PurchaseLineDto> Lines);

public record SavePurchaseLineDto(Guid IngredientId, decimal Quantity, decimal? Total);

/// <summary>
/// Creates or changes an order or invoice that is not posted yet. <c>Status</c> is "ordered" (an order to the
/// supplier) or "draft" (a delivery being typed in). <c>Date</c> defaults to today.
/// </summary>
public record SavePurchaseDto(
    Guid SupplierId, Guid LocationId, string? Status, string? Number, DateTime? Date, string? Note, string? PhotoUrl,
    List<SavePurchaseLineDto>? Lines);

public record SetPurchasePaidDto(bool Paid);

/// <summary>Result of posting: the invoice and how much of each ingredient the branch has now.</summary>
public record PostPurchaseResultDto(PurchaseDto Purchase, List<StockMoveResultDto> Stock);

// ── What to order ──

/// <summary>
/// An ingredient at or under its "warn at" number at a branch, with a suggested amount to order.
/// <c>Suggested</c> fills the shelf up to <c>OrderUpTo</c> (or twice the warn-at number), less what is already on
/// open orders, rounded up to whole packs of the chosen supplier. <c>Packs</c> is null when the pack is not known.
/// </summary>
public record ToOrderLineDto(
    Guid IngredientId, string Name, string Unit, decimal Left, decimal LowStockAt, decimal? OrderUpTo,
    decimal AlreadyOrdered, decimal Suggested, decimal? Packs, decimal? PackSize, string? PackName,
    decimal? EstimatedCost);

/// <summary>Lines grouped by the supplier to order from. A null supplier means no supplier sells it yet.</summary>
public record ToOrderGroupDto(
    Guid? SupplierId, string? SupplierName, string? SupplierPhone, List<ToOrderLineDto> Lines, decimal? EstimatedTotal);

public record SetOrderUpToDto(decimal? OrderUpTo);

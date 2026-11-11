namespace Yurt.Application.Features.Accounting;

/// <summary>
/// Everything the accountant gets for a period, worked out once and then written out as Excel or as a 1C exchange file.
/// Days and times are the cafe's local time (Asia/Almaty). Money is in tenge.
/// </summary>
public record AccountingExport(
    string CafeName,
    Guid CafeId,
    DateOnly From,
    DateOnly To,
    Guid? LocationId,
    string? LocationName,
    List<SalesDayRow> SalesByDay,
    List<OrderRow> Orders,
    List<ItemSoldRow> Items,
    List<ShiftRow> Shifts,
    List<DeliveryRow> Deliveries,
    List<StockLossRow> StockLosses);

/// <summary>
/// One branch on one day. <c>Paid</c> is what guests paid (after discounts, without loyalty points), split by method in
/// <c>ByMethod</c>; <c>Refunds</c> is the money of refunded orders; <c>MoneyTaken</c> = Paid − Refunds.
/// </summary>
public record SalesDayRow(
    DateOnly Date, Guid LocationId, string Branch, int Orders,
    decimal SalesBeforeDiscounts, decimal Discounts, decimal Points, decimal Paid, decimal Refunds, decimal MoneyTaken,
    Dictionary<string, decimal> ByMethod);

/// <summary>One completed order. <c>Payments</c> is each method with what it paid (two or more for a split payment).</summary>
public record OrderRow(
    Guid Id, string Number, DateTime CompletedLocal, Guid LocationId, string Branch, string? Table,
    decimal SalesBeforeDiscounts, decimal Discount, decimal Points, decimal Paid, bool Refunded,
    List<(string Method, decimal Amount)> Payments,
    string? ReceiptNumber, string? ReceiptStatus,
    List<OrderLineRow> Lines);

/// <summary>One line of an order: <c>Amount</c> is the line's share of what the order cost after its discount code.</summary>
public record OrderLineRow(Guid MenuItemId, Guid? VariantId, string Name, string? Size, int Quantity, decimal Amount, decimal? UnitCost);

/// <summary>Every dish and size sold (refunded orders left out). <c>Cost</c> is null when a sale has no saved cost.</summary>
public record ItemSoldRow(Guid MenuItemId, Guid? VariantId, string Name, string? Size, int Quantity, decimal Amount, decimal? Cost);

public record ShiftRow(
    string Branch, DateTime OpenedLocal, string OpenedBy, DateTime? ClosedLocal, string? ClosedBy,
    int Orders, decimal Takings, decimal OpeningCash, decimal CashSales, decimal CashIn, decimal CashOut,
    decimal ExpectedCash, decimal? CountedCash, decimal? Difference, bool ZReportDone);

/// <summary>One line of a supplier invoice that was put on the shelf (posted, not cancelled).</summary>
public record DeliveryRow(
    Guid PurchaseId, DateOnly Date, string? Number, Guid SupplierId, string Supplier, string? SupplierBin,
    Guid LocationId, string Branch, Guid IngredientId, string Ingredient, string Unit, decimal Quantity, decimal? Sum,
    decimal InvoiceTotal, bool Paid, DateTime? PaidLocal);

/// <summary>A write-off, or a difference found by a stocktake. <c>Quantity</c> lost is positive; extra found is negative.</summary>
public record StockLossRow(
    DateOnly Date, DateTime WhenLocal, Guid LocationId, string Branch, string Kind, Guid ItemId, string Item, string? Unit,
    string Type, string? Reason, string? Note, decimal Quantity, decimal? UnitCost, decimal? Sum, string By);

public static class StockLossTypes
{
    public const string WriteOff = "writeoff";
    public const string StocktakeShortage = "stocktake_shortage";
    public const string StocktakeSurplus = "stocktake_surplus";
}

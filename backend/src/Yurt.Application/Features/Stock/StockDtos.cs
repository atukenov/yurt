namespace Yurt.Application.Features.Stock;

/// <summary>One menu item of a branch with its count. <c>Quantity</c> is null when the item is not counted.</summary>
public record StockItemDto(
    Guid MenuItemId, string Name, string CategoryName, int? Quantity, int? LowStockAt, bool IsLow, bool IsSoldOut);

/// <summary>A null <c>Quantity</c> stops counting the item (it becomes unlimited again).</summary>
public record SetStockDto(int? Quantity, int? LowStockAt);

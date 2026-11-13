namespace Yurt.Application.Features.Combos;

/// <summary>One dish inside a combo, as the admin sees it. <c>RegularPrice</c> = one piece bought separately.</summary>
public record ComboPartDto(Guid ItemId, string ItemName, Guid? VariantId, string? VariantLabel, int Quantity, decimal RegularPrice);

public record ComboDto(
    Guid Id, string Name, string? NameRu, string? NameKk,
    string Description, string? DescriptionRu, string? DescriptionKk,
    Guid CategoryId, string CategoryName, decimal Price, bool IsAvailable, string? ImageUrl,
    List<Guid> LocationIds, List<ComboPartDto> Parts,
    // What the parts cost bought separately (at their usual prices) and what the guest saves by taking the combo.
    decimal RegularTotal, decimal Saving,
    // True when the combo is off the guest menu right now because a dish inside it is on the stop-list or sold out.
    bool Blocked, int TimesSold);

public record SaveComboPartDto(Guid ItemId, Guid? VariantId, int Quantity);

public record SaveComboDto
{
    public string Name { get; init; } = "";
    public string? NameRu { get; init; }
    public string? NameKk { get; init; }
    public string? Description { get; init; }
    public string? DescriptionRu { get; init; }
    public string? DescriptionKk { get; init; }
    public Guid CategoryId { get; init; }
    public decimal Price { get; init; }
    public bool IsAvailable { get; init; } = true;
    public string? ImageUrl { get; init; }
    // Empty = every branch.
    public List<Guid>? LocationIds { get; init; }
    public List<SaveComboPartDto> Parts { get; init; } = [];
}

/// <summary>One combo over a month. Money is after the order's discount is shared out over its lines.</summary>
public record ComboReportItemDto(
    Guid ComboId, string Name, int Sold,
    decimal Revenue,
    // What the same dishes would have cost bought separately, and what the guests saved.
    decimal RegularValue, decimal Saved,
    // Null when a dish inside has no recipe, so the cost is unknown.
    decimal? Cost, decimal? Profit);

public record ComboReportTotalsDto(
    int Sold, decimal Revenue, decimal RegularValue, decimal Saved, decimal? Cost, decimal? Profit,
    // Combos' share of all the month's net sales, in percent.
    decimal ShareOfSales, int OrdersWithCombo);

public record ComboReportDto(string Month, Guid? LocationId, ComboReportTotalsDto Totals, List<ComboReportItemDto> Items);

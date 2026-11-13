using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// One dish inside a combo (a <see cref="MenuItem"/> with <c>IsCombo</c>). A sold combo takes <c>Quantity</c> of this dish
/// (in this size, if one is set) off the stock for each combo sold.
/// </summary>
public class ComboPart : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid ComboId { get; set; }
    public Guid ItemId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; } = 1;
    public int SortOrder { get; set; }

    public MenuItem Combo { get; set; } = null!;
    public MenuItem Item { get; set; } = null!;
}

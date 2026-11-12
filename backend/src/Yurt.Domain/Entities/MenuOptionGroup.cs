using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// Rule for a group of options (the <see cref="MenuTopping.Group"/> key, e.g. "milk"): must the guest choose,
/// and how many. A group without a row keeps the old behaviour: pick at most one, may pick none.
/// </summary>
public class MenuOptionGroup : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    /// <summary>Same text as <see cref="MenuTopping.Group"/>.</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>Shown above the choices. Falls back to the key.</summary>
    public string? Title { get; set; }
    public string? TitleRu { get; set; }
    public string? TitleKk { get; set; }
    /// <summary>The guest has to choose at least <see cref="MinSelect"/> (at least 1 when this is on).</summary>
    public bool Required { get; set; }
    public int MinSelect { get; set; }
    /// <summary>Null = no limit.</summary>
    public int? MaxSelect { get; set; }
}

using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// A table in a branch with its own QR code (<c>table_ordering</c>). A guest who scans the code orders to this table:
/// staff see the table name on the order and bring it over, instead of calling the guest to the counter.
/// </summary>
public class DiningTable : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid LocationId { get; set; }

    /// <summary>What staff and the guest see, for example "5" or "Terrace 2".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The random part of the QR link (/t/{code}). A new code makes old printed stickers stop working.</summary>
    public string Code { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public Location Location { get; set; } = null!;
}

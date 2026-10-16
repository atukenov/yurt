namespace Yurt.Domain.Common;

/// <summary>
/// Data that belongs to one cafe. The database context filters these by the current tenant and stamps new rows with it
/// (Phase 5). Existing rows were given the default tenant when the column was added.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}

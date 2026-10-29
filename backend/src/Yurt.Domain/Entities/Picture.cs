using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// A picture a cafe uploaded (logo, icon, splash, menu or promotion photo). Kept in the database, so a deployment needs
/// no separate file storage. A picture never changes: a new upload is a new row with a new id, so the public link can
/// be cached for good.
/// </summary>
public class Picture : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    /// <summary>image/png, image/jpeg or image/webp; checked against the file's first bytes, not the browser's claim.</summary>
    public string ContentType { get; set; } = string.Empty;

    public byte[] Data { get; set; } = [];

    /// <summary>Length of <see cref="Data"/>, so the cafe's total can be summed without loading the files.</summary>
    public int Size { get; set; }
}

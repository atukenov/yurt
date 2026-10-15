using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

public class Promotion : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? TitleRu { get; set; }
    public string? TitleKk { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? DescriptionRu { get; set; }
    public string? DescriptionKk { get; set; }
    public string? ImageUrl { get; set; }
    public string? ButtonLabel { get; set; }
    public string? ButtonUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
}

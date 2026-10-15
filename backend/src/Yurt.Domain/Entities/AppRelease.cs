using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// A published app version with its "what's new" notes. The newest release drives the
/// update popup; a mandatory release raises the minimum version older apps are blocked below.
/// </summary>
public class AppRelease : BaseEntity
{
    public string Version { get; set; } = string.Empty;

    // One bullet per line.
    public string NotesEn { get; set; } = string.Empty;
    public string NotesRu { get; set; } = string.Empty;
    public string NotesKk { get; set; } = string.Empty;

    public bool IsMandatory { get; set; }
}

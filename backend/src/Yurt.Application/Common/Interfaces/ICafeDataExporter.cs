namespace Yurt.Application.Common.Interfaces;

/// <summary>
/// Builds a ZIP with everything one cafe owns (menu, orders, customers, stock, settings...) so a cafe that leaves
/// Yurt can take its data with it. One CSV per table plus a cafe.json summary and a README. Passwords, keys and
/// login codes are never included.
/// </summary>
public interface ICafeDataExporter
{
    /// <summary>Writes the export to a temporary file and returns it opened for reading (deleted when closed),
    /// or null when the cafe does not exist.</summary>
    Task<CafeDataExport?> ExportAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed record CafeDataExport(Stream Content, string FileName);

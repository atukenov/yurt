namespace Yurt.Application.Features.Tables;

/// <param name="Link">The address the QR code opens; null while the cafe has no customer app address.</param>
/// <param name="QrSvg">The QR code of <paramref name="Link"/> as an SVG data link, ready for an img tag; null with no link.</param>
public record TableDto(Guid Id, Guid LocationId, string Label, string Code, string? Link, string? QrSvg);

/// <summary>Adds tables to a branch, one per label (for example "1" to "12").</summary>
public record CreateTablesDto(Guid LocationId, List<string> Labels);

public record RenameTableDto(string Label);

/// <summary>What the customer app needs after a QR scan: which branch and table the guest is at.</summary>
public record TableLookupDto(string Code, string Label, Guid LocationId, string LocationName);

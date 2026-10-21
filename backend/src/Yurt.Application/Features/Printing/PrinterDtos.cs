namespace Yurt.Application.Features.Printing;

public record PrinterDto(
    Guid Id, Guid LocationId, string Name, string Kind, Guid? StationId, string Host, int Port,
    int LineWidth, int CodeTable, string Language, bool AutoPrint);

public record SavePrinterDto(
    Guid LocationId, string Name, string Kind, Guid? StationId, string Host, int Port = 9100,
    int LineWidth = 48, int CodeTable = 17, string Language = "ru", bool AutoPrint = true);

public record PrintJobDto(
    Guid Id, Guid PrinterId, string PrinterName, Guid? OrderId, string Kind, string Status, int Attempts,
    string? LastError, DateTime CreatedAt, DateTime? PrintedAt);

/// <summary>How many printers got the job. 0 means the branch has no printer for it, so the screen prints in the browser.</summary>
public record PrintQueuedDto(int Printers);

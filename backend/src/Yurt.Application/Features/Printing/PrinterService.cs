using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Printing;

/// <summary>Setting up the network printers of each branch, a test print, and the list of recent print jobs (own_pos).</summary>
public partial class PrinterService(IApplicationDbContext db, IPrinterTransport transport, IAuditLogService audit)
{
    private const int MaxName = 50, MaxPrinters = 30;

    /// <summary>
    /// Raw printing uses ports 9100 to 9109. Only these are allowed, so a printer address can never be used to make the
    /// server talk to its own database or other services.
    /// </summary>
    public const int MinPort = 9100, MaxPort = 9109;

    private static readonly int[] LineWidths = [32, 42, 48];

    [GeneratedRegex(@"^[A-Za-z0-9]([A-Za-z0-9.-]{0,98}[A-Za-z0-9])?$")]
    private static partial Regex HostPattern();

    public async Task<List<PrinterDto>> ListAsync(CancellationToken ct = default)
        => await db.Printers.OrderBy(p => p.Name).Select(p => ToDto(p)).ToListAsync(ct);

    public async Task<Result<PrinterDto>> CreateAsync(SavePrinterDto dto, CancellationToken ct = default)
    {
        if (await db.Printers.CountAsync(ct) >= MaxPrinters) return Result<PrinterDto>.Failure("Too many printers.", 400);
        var printer = new Printer();
        var error = await ApplyAsync(printer, dto, ct);
        if (error is not null) return error;
        db.Printers.Add(printer);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("PrinterCreated", "Printer", printer.Id.ToString(), $"{printer.Name} {printer.Host}:{printer.Port}", ct);
        return Result<PrinterDto>.Success(ToDto(printer), 201);
    }

    public async Task<Result<PrinterDto>> UpdateAsync(Guid id, SavePrinterDto dto, CancellationToken ct = default)
    {
        var printer = await db.Printers.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (printer is null) return Result<PrinterDto>.NotFound("Printer not found.");
        var error = await ApplyAsync(printer, dto, ct);
        if (error is not null) return error;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("PrinterUpdated", "Printer", id.ToString(), $"{printer.Name} {printer.Host}:{printer.Port}", ct);
        return Result<PrinterDto>.Success(ToDto(printer));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var printer = await db.Printers.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (printer is null) return Result<bool>.NotFound("Printer not found.");
        db.Printers.Remove(printer);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("PrinterDeleted", "Printer", id.ToString(), printer.Name, ct);
        return Result<bool>.Success(true);
    }

    /// <summary>Prints a test page right away and says whether it worked, so staff can check the address while setting up.</summary>
    public async Task<Result<bool>> TestAsync(Guid id, CancellationToken ct = default)
    {
        var printer = await db.Printers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (printer is null) return Result<bool>.NotFound("Printer not found.");
        try
        {
            await transport.SendAsync(printer.Host, printer.Port, PrintLayouts.Test(printer), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return Result<bool>.Failure($"Could not reach the printer at {printer.Host}:{printer.Port}. {PrintService.Short(ex)}", 502);
        }
        return Result<bool>.Success(true);
    }

    /// <summary>The newest print jobs, so staff can see what printed and what did not.</summary>
    public async Task<List<PrintJobDto>> RecentJobsAsync(int limit, CancellationToken ct = default)
        => await db.PrintJobs.OrderByDescending(j => j.CreatedAt).Take(Math.Clamp(limit, 1, 100))
            .Select(j => new PrintJobDto(j.Id, j.PrinterId, j.Printer.Name, j.OrderId, j.Kind, j.Status, j.Attempts,
                j.LastError, j.CreatedAt, j.PrintedAt))
            .ToListAsync(ct);

    private async Task<Result<PrinterDto>?> ApplyAsync(Printer printer, SavePrinterDto dto, CancellationToken ct)
    {
        var name = dto.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > MaxName) return Result<PrinterDto>.Failure($"Name is required (up to {MaxName} characters).", 400);
        if (dto.Kind is not (Printer.KitchenKind or Printer.ReceiptKind)) return Result<PrinterDto>.Failure("Kind must be Kitchen or Receipt.", 400);
        var host = dto.Host?.Trim() ?? "";
        if (!HostPattern().IsMatch(host)) return Result<PrinterDto>.Failure("Enter the printer's network address, such as 192.168.1.50.", 400);
        if (dto.Port is < MinPort or > MaxPort) return Result<PrinterDto>.Failure($"Port must be between {MinPort} and {MaxPort} (usually 9100).", 400);
        if (!LineWidths.Contains(dto.LineWidth)) return Result<PrinterDto>.Failure("Line width must be 32 (58 mm paper), 42 or 48 (80 mm paper).", 400);
        if (dto.CodeTable is < 0 or > 255) return Result<PrinterDto>.Failure("Character table must be between 0 and 255.", 400);
        if (!PrintLayouts.Languages.Contains(dto.Language)) return Result<PrinterDto>.Failure("Language must be ru, kk or en.", 400);
        if (!await db.Locations.AnyAsync(l => l.Id == dto.LocationId, ct)) return Result<PrinterDto>.Failure("Branch not found.", 404);
        var stationId = dto.Kind == Printer.KitchenKind ? dto.StationId : null;
        if (stationId is { } sid && !await db.Stations.AnyAsync(s => s.Id == sid, ct)) return Result<PrinterDto>.Failure("Station not found.", 404);

        printer.LocationId = dto.LocationId;
        printer.Name = name;
        printer.Kind = dto.Kind;
        printer.StationId = stationId;
        printer.Host = host;
        printer.Port = dto.Port;
        printer.LineWidth = dto.LineWidth;
        printer.CodeTable = dto.CodeTable;
        printer.Language = dto.Language;
        printer.AutoPrint = dto.AutoPrint;
        return null;
    }

    private static PrinterDto ToDto(Printer p)
        => new(p.Id, p.LocationId, p.Name, p.Kind, p.StationId, p.Host, p.Port, p.LineWidth, p.CodeTable, p.Language, p.AutoPrint);
}

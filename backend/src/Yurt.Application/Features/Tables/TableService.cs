using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Payments.Services;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Tables;

/// <summary>
/// Tables with QR codes (<c>table_ordering</c>). Each table has a random code; its QR code opens the customer app at
/// <c>/t/{code}</c>, which picks the branch and sends the next orders to that table.
/// </summary>
public class TableService(
    IApplicationDbContext db, ITenantContext tenant, PaymentReturnOptions appAddress, IQrCodeImages qr, IAuditLogService audit)
{
    private const int MaxLabel = 30, MaxTablesPerBranch = 200;

    // No 0/O or 1/I/L, so a code read out loud or typed from a sticker is not mistaken.
    private const string CodeAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int CodeLength = 10;

    public async Task<Result<List<TableDto>>> ListAsync(Guid locationId, CancellationToken ct = default)
    {
        var baseUrl = await AppBaseAsync(ct);
        var tables = await db.DiningTables.AsNoTracking().Where(t => t.LocationId == locationId)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Label).ToListAsync(ct);
        return Result<List<TableDto>>.Success(tables.Select(t => ToDto(t, baseUrl)).ToList());
    }

    public async Task<Result<List<TableDto>>> CreateAsync(CreateTablesDto dto, CancellationToken ct = default)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == dto.LocationId, ct)) return Result<List<TableDto>>.Failure("Location not found.", 404);
        var labels = (dto.Labels ?? []).Select(l => l?.Trim() ?? "").ToList();
        if (labels.Count == 0 || labels.Any(l => l.Length == 0 || l.Length > MaxLabel))
            return Result<List<TableDto>>.Failure($"Each table needs a name (up to {MaxLabel} characters).", 400);
        if (labels.Distinct(StringComparer.OrdinalIgnoreCase).Count() != labels.Count)
            return Result<List<TableDto>>.Failure("The same table name is given twice.", 400);

        var existing = await db.DiningTables.Where(t => t.LocationId == dto.LocationId).ToListAsync(ct);
        if (existing.Count + labels.Count > MaxTablesPerBranch)
            return Result<List<TableDto>>.Failure($"A branch can have up to {MaxTablesPerBranch} tables.", 400);
        if (labels.FirstOrDefault(l => existing.Any(t => string.Equals(t.Label, l, StringComparison.OrdinalIgnoreCase))) is { } taken)
            return Result<List<TableDto>>.Failure($"Table \"{taken}\" already exists at this branch.", 409);

        var order = existing.Count == 0 ? 0 : existing.Max(t => t.SortOrder) + 1;
        var created = new List<DiningTable>();
        foreach (var label in labels)
        {
            var table = new DiningTable { LocationId = dto.LocationId, Label = label, Code = await NewCodeAsync(ct), SortOrder = order++ };
            db.DiningTables.Add(table);
            created.Add(table);
        }
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("TablesCreated", "Location", dto.LocationId.ToString(), string.Join(", ", labels), ct);

        var baseUrl = await AppBaseAsync(ct);
        return Result<List<TableDto>>.Success(created.Select(t => ToDto(t, baseUrl)).ToList(), 201);
    }

    public async Task<Result<TableDto>> RenameAsync(Guid id, RenameTableDto dto, CancellationToken ct = default)
    {
        var table = await db.DiningTables.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (table is null) return Result<TableDto>.Failure("Table not found.", 404);
        var label = dto.Label?.Trim() ?? "";
        if (label.Length == 0 || label.Length > MaxLabel) return Result<TableDto>.Failure($"Name is required (up to {MaxLabel} characters).", 400);
        var clash = await db.DiningTables.Where(t => t.LocationId == table.LocationId && t.Id != id).Select(t => t.Label).ToListAsync(ct);
        if (clash.Any(l => string.Equals(l, label, StringComparison.OrdinalIgnoreCase)))
            return Result<TableDto>.Failure($"Table \"{label}\" already exists at this branch.", 409);

        var old = table.Label;
        table.Label = label;
        table.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("TableRenamed", "DiningTable", id.ToString(), $"{old} -> {label}", ct);
        return Result<TableDto>.Success(ToDto(table, await AppBaseAsync(ct)));
    }

    /// <summary>Gives the table a new code. The old QR code stops working, for example after a sticker was copied.</summary>
    public async Task<Result<TableDto>> NewCodeAsync(Guid id, CancellationToken ct = default)
    {
        var table = await db.DiningTables.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (table is null) return Result<TableDto>.Failure("Table not found.", 404);
        table.Code = await NewCodeAsync(ct);
        table.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("TableNewCode", "DiningTable", id.ToString(), table.Label, ct);
        return Result<TableDto>.Success(ToDto(table, await AppBaseAsync(ct)));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var table = await db.DiningTables.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (table is null) return Result<bool>.Failure("Table not found.", 404);
        db.DiningTables.Remove(table);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("TableDeleted", "DiningTable", id.ToString(), table.Label, ct);
        return Result<bool>.Success(true);
    }

    /// <summary>The table behind a scanned code, at an open branch of this cafe.</summary>
    public async Task<Result<TableLookupDto>> LookupAsync(string code, CancellationToken ct = default)
    {
        var wanted = code?.Trim().ToUpperInvariant() ?? "";
        var found = await db.DiningTables.AsNoTracking()
            .Where(t => t.Code == wanted && t.Location.IsActive)
            .Select(t => new TableLookupDto(t.Code, t.Label, t.LocationId, t.Location.Name))
            .FirstOrDefaultAsync(ct);
        return found is null ? Result<TableLookupDto>.Failure("Table not found.", 404) : Result<TableLookupDto>.Success(found);
    }

    private TableDto ToDto(DiningTable t, string? baseUrl)
    {
        var link = baseUrl is null ? null : $"{baseUrl}/t/{t.Code}";
        return new TableDto(t.Id, t.LocationId, t.Label, t.Code, link, link is null ? null : qr.SvgDataUri(link));
    }

    /// <summary>The customer app of this cafe: its own web address, else the server-wide one; null when neither is set.</summary>
    private async Task<string?> AppBaseAsync(CancellationToken ct)
    {
        var domain = await db.Tenants.AsNoTracking().Where(t => t.Id == tenant.TenantId).Select(t => t.Domain).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(domain)) return $"https://{domain.Trim().TrimEnd('/')}";
        return string.IsNullOrWhiteSpace(appAddress.CustomerAppUrl) ? null : appAddress.CustomerAppUrl.Trim().TrimEnd('/');
    }

    private async Task<string> NewCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);
            // Codes are unique across all cafes, so the check ignores the cafe filter.
            if (!await db.DiningTables.IgnoreQueryFilters().AnyAsync(t => t.Code == code, ct)
                && !db.DiningTables.Local.Any(t => t.Code == code))
                return code;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Reports;

public class ReportService
{
    private readonly IApplicationDbContext _db;

    public ReportService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<bool>> CreateReportAsync(Guid customerId, string text, CancellationToken ct = default)
    {
        var report = new UserReport
        {
            CustomerId = customerId,
            Text = text.Trim(),
        };
        _db.UserReports.Add(report);
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }

    public async Task<List<ReportDto>> GetReportsAsync(bool resolved, CancellationToken ct = default)
    {
        return await _db.UserReports
            .Include(r => r.Customer)
            .Where(r => r.IsResolved == resolved)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReportDto(
                r.Id,
                r.CustomerId,
                $"{r.Customer.FirstName} {r.Customer.LastName}".Trim(),
                r.Customer.MobileNumber,
                r.Text,
                r.IsResolved,
                r.ResolvedAt,
                r.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<Result<bool>> MarkResolvedAsync(Guid id, CancellationToken ct = default)
    {
        var report = await _db.UserReports.FindAsync([id], ct);
        if (report is null) return Result<bool>.Failure("Report not found.");
        report.IsResolved = true;
        report.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

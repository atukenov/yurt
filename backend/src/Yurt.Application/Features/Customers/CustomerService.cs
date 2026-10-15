using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Customers;

public class CustomerService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditLogService _audit;

    public CustomerService(IApplicationDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<(int Total, List<CustomerSummaryDto> Items)> GetCustomersAsync(
        string? phone, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.CustomerUsers
            .Include(u => u.Orders)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(phone))
            query = query.Where(u => u.MobileNumber.Contains(phone));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new CustomerSummaryDto(
                u.Id,
                u.MobileNumber,
                (u.FirstName + " " + u.LastName).Trim(),
                u.CreatedAt,
                u.Orders.Count(o => o.Status == OrderStatus.Completed),
                u.Orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.Total)))
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<Result<CustomerDetailDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var user = await _db.CustomerUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Result<CustomerDetailDto>.NotFound();

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var action = isActive ? "CustomerActivated" : "CustomerDeactivated";
        await _audit.LogAsync(action, "CustomerUser", id.ToString(), user.MobileNumber, ct);

        var detail = await GetCustomerDetailAsync(id, ct);
        return Result<CustomerDetailDto>.Success(detail!);
    }

    public async Task<CustomerDetailDto?> GetCustomerDetailAsync(Guid id, CancellationToken ct = default)
        => await _db.CustomerUsers
            .Where(c => c.Id == id)
            .Select(c => new CustomerDetailDto(
                c.Id,
                c.MobileNumber,
                (c.FirstName + " " + c.LastName).Trim(),
                c.CreatedAt,
                c.IsActive,
                c.Orders.Count(o => !o.IsArchived),
                c.Orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => (decimal?)o.Total) ?? 0,
                c.Orders
                    .Where(o => !o.IsArchived)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(50)
                    .Select(o => new CustomerOrderSummaryDto(
                        o.Id,
                        o.CreatedAt,
                        o.Status,
                        o.Total,
                        o.Location.Name,
                        o.Items.Count,
                        o.LoyaltyPointsEarned,
                        o.LoyaltyPointsSpent))
                    .ToList()))
            .FirstOrDefaultAsync(ct);
}

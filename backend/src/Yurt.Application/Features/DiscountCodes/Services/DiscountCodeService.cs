using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.DiscountCodes.DTOs;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.DiscountCodes.Services;

public class DiscountCodeService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditLogService _audit;

    public DiscountCodeService(IApplicationDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<DiscountCodeDto>> GetAllAsync(CancellationToken ct = default)
        => await _db.DiscountCodes
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => MapToDto(d))
            .ToListAsync(ct);

    public async Task<Result<DiscountCodeDto>> CreateAsync(
        CreateDiscountCodeDto dto, CancellationToken ct = default)
    {
        if (!Enum.TryParse<DiscountType>(dto.DiscountType, true, out var type))
            return Result<DiscountCodeDto>.Failure("Invalid discount type. Use 'Percentage' or 'FixedAmount'.", 400);

        if (dto.DiscountValue <= 0)
            return Result<DiscountCodeDto>.Failure("Discount value must be greater than zero.", 400);

        if (type == DiscountType.Percentage && dto.DiscountValue > 100)
            return Result<DiscountCodeDto>.Failure("Percentage discount cannot exceed 100.", 400);

        var normalizedCode = dto.Code.Trim().ToUpperInvariant();
        var exists = await _db.DiscountCodes.AnyAsync(d => d.Code == normalizedCode, ct);
        if (exists)
            return Result<DiscountCodeDto>.Failure("A discount code with this value already exists.", 409);

        var code = new DiscountCode
        {
            Code = normalizedCode,
            Title = dto.Title,
            DiscountType = type,
            DiscountValue = dto.DiscountValue,
            MaxUses = dto.MaxUses,
            MinOrderAmount = dto.MinOrderAmount,
            StartsAt = ToUtc(dto.StartsAt),
            ExpiresAt = ToUtc(dto.ExpiresAt),
            IsActive = dto.IsActive
        };
        _db.DiscountCodes.Add(code);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("DiscountCodeCreated", "DiscountCode", code.Id.ToString(), code.Code, ct);
        return Result<DiscountCodeDto>.Success(MapToDto(code), 201);
    }

    public async Task<Result<DiscountCodeDto>> UpdateAsync(
        Guid id, UpdateDiscountCodeDto dto, CancellationToken ct = default)
    {
        var code = await _db.DiscountCodes.FindAsync([id], ct);
        if (code == null) return Result<DiscountCodeDto>.NotFound();

        if (!Enum.TryParse<DiscountType>(dto.DiscountType, true, out var type))
            return Result<DiscountCodeDto>.Failure("Invalid discount type. Use 'Percentage' or 'FixedAmount'.", 400);

        if (dto.DiscountValue <= 0)
            return Result<DiscountCodeDto>.Failure("Discount value must be greater than zero.", 400);

        if (type == DiscountType.Percentage && dto.DiscountValue > 100)
            return Result<DiscountCodeDto>.Failure("Percentage discount cannot exceed 100.", 400);

        code.Title = dto.Title;
        code.DiscountType = type;
        code.DiscountValue = dto.DiscountValue;
        code.MaxUses = dto.MaxUses;
        code.MinOrderAmount = dto.MinOrderAmount;
        code.StartsAt = ToUtc(dto.StartsAt);
        code.ExpiresAt = ToUtc(dto.ExpiresAt);
        code.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("DiscountCodeUpdated", "DiscountCode", id.ToString(), code.Code, ct);
        return Result<DiscountCodeDto>.Success(MapToDto(code));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var code = await _db.DiscountCodes.FindAsync([id], ct);
        if (code == null) return Result<bool>.NotFound();
        _db.DiscountCodes.Remove(code);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("DiscountCodeDeleted", "DiscountCode", id.ToString(), code.Code, ct);
        return Result<bool>.Success(true);
    }

    public async Task<ValidateDiscountCodeResponseDto> ValidateAsync(
        string codeStr, decimal subtotal, Guid customerId, CancellationToken ct = default)
    {
        var normalizedCode = codeStr.Trim().ToUpperInvariant();
        var code = await _db.DiscountCodes
            .FirstOrDefaultAsync(d => d.Code == normalizedCode && d.IsActive, ct);

        if (code == null)
            return new(false, "Discount code not found.", 0, "");

        var now = DateTime.UtcNow;
        if (code.StartsAt.HasValue && now < code.StartsAt.Value)
            return new(false, "Discount code is not yet active.", 0, "");

        if (code.ExpiresAt.HasValue && now > code.ExpiresAt.Value)
            return new(false, "Discount code has expired.", 0, "");

        if (code.MaxUses.HasValue && code.UsedCount >= code.MaxUses.Value)
            return new(false, "Discount code has reached its usage limit.", 0, "");

        if (code.MinOrderAmount.HasValue && subtotal < code.MinOrderAmount.Value)
            return new(false, $"Minimum order amount is {code.MinOrderAmount.Value:N0} ₸.", 0, "");

        // Each customer may redeem a given code at most once, regardless of the code's global usage limit.
        var alreadyUsedByCustomer = await _db.Orders
            .AnyAsync(o => o.CustomerUserId == customerId && o.DiscountCodeId == code.Id, ct);
        if (alreadyUsedByCustomer)
            return new(false, "You have already used this discount code.", 0, "");

        var discountAmount = CalculateDiscount(code, subtotal);
        var description = code.DiscountType == DiscountType.Percentage
            ? $"{code.DiscountValue}% off"
            : $"{code.DiscountValue:N0} ₸ off";

        return new(true, "Code applied successfully.", discountAmount, description);
    }

    public static decimal CalculateDiscount(DiscountCode code, decimal subtotal)
    {
        if (code.DiscountType == DiscountType.Percentage)
            return Math.Round(subtotal * code.DiscountValue / 100, 2);
        return Math.Min(code.DiscountValue, subtotal);
    }

    private static readonly TimeZoneInfo BusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Almaty");

    // Admin sends a plain wall-clock value (datetime-local input, no offset).
    // It represents business-local time, so convert it to true UTC rather than
    // just relabeling the Kind (which would leave it ~5 hours off).
    private static DateTime? ToUtc(DateTime? dt)
    {
        if (!dt.HasValue) return null;
        var unspecified = DateTime.SpecifyKind(dt.Value, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, BusinessTimeZone);
    }

    private static DiscountCodeDto MapToDto(DiscountCode d) => new(
        d.Id, d.Code, d.Title,
        d.DiscountType.ToString(), d.DiscountValue,
        d.MaxUses, d.UsedCount, d.MinOrderAmount,
        d.StartsAt, d.ExpiresAt, d.IsActive, d.CreatedAt);
}

namespace Yurt.Application.Features.DiscountCodes.DTOs;

public record DiscountCodeDto(
    Guid Id,
    string Code,
    string Title,
    string DiscountType,
    decimal DiscountValue,
    int? MaxUses,
    int UsedCount,
    decimal? MinOrderAmount,
    DateTime? StartsAt,
    DateTime? ExpiresAt,
    bool IsActive,
    DateTime CreatedAt);

public record CreateDiscountCodeDto(
    string Code,
    string Title,
    string DiscountType,
    decimal DiscountValue,
    int? MaxUses,
    decimal? MinOrderAmount,
    DateTime? StartsAt,
    DateTime? ExpiresAt,
    bool IsActive = true);

public record UpdateDiscountCodeDto(
    string Title,
    string DiscountType,
    decimal DiscountValue,
    int? MaxUses,
    decimal? MinOrderAmount,
    DateTime? StartsAt,
    DateTime? ExpiresAt,
    bool IsActive);

public record ValidateDiscountCodeDto(string Code, decimal Subtotal);

public record ValidateDiscountCodeResponseDto(
    bool IsValid,
    string Message,
    decimal DiscountAmount,
    string Description);

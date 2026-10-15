namespace Yurt.Application.Features.Promotions.DTOs;

// Customer-facing (localized title/description)
public record PromotionDto(
    Guid Id,
    string Title,
    string Description,
    string? ImageUrl,
    string? ButtonLabel,
    string? ButtonUrl,
    bool IsActive,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

// Admin-facing (all language fields)
public record AdminPromotionDto(
    Guid Id,
    string Title,
    string? TitleRu,
    string? TitleKk,
    string Description,
    string? DescriptionRu,
    string? DescriptionKk,
    string? ImageUrl,
    string? ButtonLabel,
    string? ButtonUrl,
    bool IsActive,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

public record CreatePromotionDto(
    string Title,
    string? TitleRu,
    string? TitleKk,
    string Description,
    string? DescriptionRu,
    string? DescriptionKk,
    string? ImageUrl,
    string? ButtonLabel,
    string? ButtonUrl,
    DateTime? ExpiresAt);

public record UpdatePromotionDto(
    string Title,
    string? TitleRu,
    string? TitleKk,
    string Description,
    string? DescriptionRu,
    string? DescriptionKk,
    string? ImageUrl,
    string? ButtonLabel,
    string? ButtonUrl,
    bool IsActive,
    DateTime? ExpiresAt);

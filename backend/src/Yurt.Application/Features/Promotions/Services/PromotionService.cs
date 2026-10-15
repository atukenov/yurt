using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Helpers;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Promotions.DTOs;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Promotions.Services;

public class PromotionService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditLogService _audit;

    public PromotionService(IApplicationDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<PromotionDto>> GetActivePromotionsAsync(string lang = "ru", CancellationToken ct = default)
        => await _db.Promotions
            .Where(p => p.IsActive && (p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PromotionDto(
                p.Id,
                LocalizationHelper.Localize(p.Title, p.TitleRu, p.TitleKk, lang),
                LocalizationHelper.Localize(p.Description, p.DescriptionRu, p.DescriptionKk, lang),
                p.ImageUrl, p.ButtonLabel, p.ButtonUrl, p.IsActive, p.ExpiresAt, p.CreatedAt))
            .ToListAsync(ct);

    public async Task<List<AdminPromotionDto>> GetAllPromotionsAsync(CancellationToken ct = default)
        => await _db.Promotions
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AdminPromotionDto(
                p.Id, p.Title, p.TitleRu, p.TitleKk,
                p.Description, p.DescriptionRu, p.DescriptionKk,
                p.ImageUrl, p.ButtonLabel, p.ButtonUrl, p.IsActive, p.ExpiresAt, p.CreatedAt))
            .ToListAsync(ct);

    public async Task<Result<AdminPromotionDto>> CreateAsync(CreatePromotionDto dto, CancellationToken ct = default)
    {
        var promo = new Promotion
        {
            Title = dto.Title,
            TitleRu = dto.TitleRu,
            TitleKk = dto.TitleKk,
            Description = dto.Description,
            DescriptionRu = dto.DescriptionRu,
            DescriptionKk = dto.DescriptionKk,
            ImageUrl = dto.ImageUrl,
            ButtonLabel = dto.ButtonLabel,
            ButtonUrl = dto.ButtonUrl,
            ExpiresAt = dto.ExpiresAt
        };
        _db.Promotions.Add(promo);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PromotionCreated", "Promotion", promo.Id.ToString(), promo.Title, ct);
        return Result<AdminPromotionDto>.Success(MapToAdminDto(promo), 201);
    }

    public async Task<Result<AdminPromotionDto>> UpdateAsync(Guid id, UpdatePromotionDto dto, CancellationToken ct = default)
    {
        var promo = await _db.Promotions.FindAsync([id], ct);
        if (promo == null) return Result<AdminPromotionDto>.NotFound();

        promo.Title = dto.Title;
        promo.TitleRu = dto.TitleRu;
        promo.TitleKk = dto.TitleKk;
        promo.Description = dto.Description;
        promo.DescriptionRu = dto.DescriptionRu;
        promo.DescriptionKk = dto.DescriptionKk;
        promo.ImageUrl = dto.ImageUrl;
        promo.ButtonLabel = dto.ButtonLabel;
        promo.ButtonUrl = dto.ButtonUrl;
        promo.IsActive = dto.IsActive;
        promo.ExpiresAt = dto.ExpiresAt;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PromotionUpdated", "Promotion", id.ToString(), promo.Title, ct);
        return Result<AdminPromotionDto>.Success(MapToAdminDto(promo));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var promo = await _db.Promotions.FindAsync([id], ct);
        if (promo == null) return Result<bool>.NotFound();
        var title = promo.Title;
        _db.Promotions.Remove(promo);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PromotionDeleted", "Promotion", id.ToString(), title, ct);
        return Result<bool>.Success(true);
    }

    private static AdminPromotionDto MapToAdminDto(Promotion p)
        => new(p.Id, p.Title, p.TitleRu, p.TitleKk,
               p.Description, p.DescriptionRu, p.DescriptionKk,
               p.ImageUrl, p.ButtonLabel, p.ButtonUrl, p.IsActive, p.ExpiresAt, p.CreatedAt);
}

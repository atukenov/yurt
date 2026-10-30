using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Fiscal;

/// <summary>
/// Operator tool for a cafe's official tax receipts (Phase 6): which cloud cash register, which payment methods to
/// skip, the VAT rate, and the register number of each branch. The provider login goes in the cafe's encrypted
/// secrets (Fiscal:Login, Fiscal:Password, Fiscal:ApiKey; ReKassa: Fiscal:Password and Fiscal:Pin). Turn it on only on the day the cafe leaves iiko, so no
/// sale gets two receipts.
/// </summary>
public class FiscalSettingsService(
    IApplicationDbContext db,
    ITenantContext tenant,
    ITenantDirectory directory,
    ITenantSecrets secrets,
    IAuditLogService audit)
{
    public async Task<Result<FiscalSettingsDto>> GetAsync(CancellationToken ct = default)
    {
        var cafe = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.TenantId, ct);
        return cafe is null
            ? Result<FiscalSettingsDto>.Failure("Cafe not found.", 404)
            : Result<FiscalSettingsDto>.Success(await ToDtoAsync(cafe, ct));
    }

    public async Task<Result<FiscalSettingsDto>> SaveAsync(SaveFiscalSettingsDto dto, CancellationToken ct = default)
    {
        var provider = string.IsNullOrWhiteSpace(dto.Provider) || dto.Provider.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)
            ? null
            : dto.Provider.Trim().ToLowerInvariant();
        if (provider is not null && !FiscalProviders.All.Contains(provider))
            return Result<FiscalSettingsDto>.Failure($"Provider must be one of: none, {string.Join(", ", FiscalProviders.All)}.", 400);
        if (dto.VatPercent is < 0 or > 100)
            return Result<FiscalSettingsDto>.Failure("VAT must be between 0 and 100, or empty when the cafe does not pay VAT.", 400);

        var skip = new List<PaymentMethod>();
        foreach (var name in dto.SkipMethods ?? [])
        {
            if (!Enum.TryParse<PaymentMethod>(name, true, out var m) || !Enum.IsDefined(m))
                return Result<FiscalSettingsDto>.Failure($"Unknown payment method '{name}'.", 400);
            if (!skip.Contains(m)) skip.Add(m);
        }

        var cafe = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.TenantId, ct);
        if (cafe is null) return Result<FiscalSettingsDto>.Failure("Cafe not found.", 404);

        if (dto.Branches is not null)
        {
            var locations = await db.Locations.ToListAsync(ct);
            foreach (var b in dto.Branches)
            {
                var location = locations.FirstOrDefault(l => l.Id == b.LocationId);
                if (location is null) return Result<FiscalSettingsDto>.Failure("Branch not found.", 404);
                var number = string.IsNullOrWhiteSpace(b.RegisterNumber) ? null : b.RegisterNumber.Trim();
                if (number is { Length: > 50 })
                    return Result<FiscalSettingsDto>.Failure("A cash register number is at most 50 characters.", 400);
                location.FiscalRegisterNumber = number;
            }
        }

        var turnedOn = cafe.FiscalProvider is null && provider is not null;
        // Only orders from now on get receipts: sales made while it was off were covered by the old register.
        if (turnedOn) cafe.FiscalSince = DateTime.UtcNow;
        cafe.FiscalProvider = provider;
        cafe.FiscalSkipMethods = skip.Count == 0 ? null : string.Join(",", skip);
        cafe.FiscalVatPercent = dto.VatPercent;
        cafe.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        directory.Invalidate();

        await audit.LogPlatformAsync("FiscalSettingsSaved", "Tenant", cafe.Id.ToString(),
            $"provider {provider ?? "none"}, skip {cafe.FiscalSkipMethods ?? "none"}, VAT {dto.VatPercent?.ToString() ?? "none"}", ct);
        return Result<FiscalSettingsDto>.Success(await ToDtoAsync(cafe, ct));
    }

    private async Task<FiscalSettingsDto> ToDtoAsync(Tenant cafe, CancellationToken ct)
    {
        var branches = await db.Locations.AsNoTracking().OrderBy(l => l.Name)
            .Select(l => new FiscalBranchDto(l.Id, l.Name, l.FiscalRegisterNumber)).ToListAsync(ct);
        var keys = await secrets.ConfiguredKeysAsync(cafe.Id, ct);
        return new FiscalSettingsDto(
            cafe.FiscalProvider, cafe.FiscalSince,
            FiscalReceiptService.ParseMethods(cafe.FiscalSkipMethods).Select(m => m.ToString()).ToList(),
            cafe.FiscalVatPercent, branches, FiscalProviders.All.ToList(),
            (!FiscalProviders.NeedsLogin(cafe.FiscalProvider) || keys.Contains(FiscalProviders.LoginKey))
                && keys.Contains(FiscalProviders.PasswordKey));
    }
}

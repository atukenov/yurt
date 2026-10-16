using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Platform;

/// <summary>Reads the saved brand from the platform settings; the Yurt default when none is saved or it is unreadable.</summary>
public static class BrandReader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<BrandDto> ReadAsync(IApplicationDbContext db, CancellationToken ct = default)
    {
        var raw = await db.PlatformSettings.AsNoTracking()
            .Where(s => s.Key == PlatformSettingKeys.Brand)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(raw)) return BrandDto.Default;
        try
        {
            var brand = JsonSerializer.Deserialize<BrandDto>(raw, Json);
            return brand is not null && PlatformService.ValidateBrand(brand) is null ? brand : BrandDto.Default;
        }
        catch (JsonException)
        {
            return BrandDto.Default;
        }
    }
}

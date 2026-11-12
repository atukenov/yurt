using Yurt.Application.Features.Orders.DTOs;

namespace Yurt.Application.Features.Orders.Services;

/// <summary>Works out how much a hand-given discount takes off.</summary>
public static class ManualDiscount
{
    public static (decimal Value, string? Error) Compute(decimal basis, DiscountInputDto? dto)
    {
        if (dto is null) return (0, "Give an amount or a percent.");
        var hasAmount = dto.Amount is > 0;
        var hasPercent = dto.Percent is > 0;
        if (hasAmount == hasPercent) return (0, "Give either an amount or a percent, not both.");
        if (hasPercent)
        {
            if (dto.Percent > 100) return (0, "A percent discount is between 1 and 100.");
            return (Math.Round(basis * dto.Percent!.Value / 100m, 2, MidpointRounding.AwayFromZero), null);
        }
        if (dto.Amount > basis) return (0, "The discount can't be more than the bill.");
        return (Math.Round(dto.Amount!.Value, 2, MidpointRounding.AwayFromZero), null);
    }

    public static string Describe(DiscountInputDto dto) => dto.Percent is > 0 ? $"{dto.Percent:0.##}%" : "fixed amount";
}

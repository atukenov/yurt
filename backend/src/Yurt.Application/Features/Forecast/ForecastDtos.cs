namespace Yurt.Application.Features.Forecast;

/// <summary>One dish and size expected on the day, with how many to have ready for each part of the day.</summary>
public record ForecastDishDto(
    Guid MenuItemId, Guid? VariantId, string Name, string? Size,
    int Expected, int Morning, int Lunch, int Afternoon, int Evening,
    decimal UsualPerDay, int DaysSold);

/// <summary>What the day's sales will use of one ingredient, next to what the branch has now.</summary>
public record ForecastIngredientDto(
    Guid IngredientId, string Name, string Unit, decimal Needed, decimal? InStock, decimal Missing);

/// <summary>One past day the forecast learned from.</summary>
public record ForecastDayDto(string Date, int Items);

/// <summary>The sales forecast and prep plan of one branch for one day.</summary>
public record ForecastDto(
    string Date, int Weekday, Guid LocationId,
    int DaysUsed, string Confidence, int AdjustPercent, int BufferPercent,
    int TotalItems, int[] Hourly, int? PeakHour,
    List<ForecastDishDto> Dishes, List<ForecastIngredientDto> Ingredients, List<ForecastDayDto> History);

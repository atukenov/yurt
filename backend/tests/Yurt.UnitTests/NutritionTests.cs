using Yurt.Application.Features.Menu;
using Yurt.Application.Features.Menu.DTOs;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

/// <summary>Allergens and nutrition on a dish: cleaning what the admin sends and what the guest menu shows.</summary>
public class NutritionTests
{
    [Fact]
    public void CleanAllergens_DropsUnknownAndRepeats_AndUsesStandardOrder()
    {
        var clean = Nutrition.CleanAllergens(["Milk", " gluten ", "milk", "kryptonite", ""]);
        Assert.Equal(["gluten", "milk"], clean);
    }

    [Fact]
    public void Apply_StoresAllergensAsList_AndRoundTrips()
    {
        var item = new MenuItem();
        Nutrition.Apply(item, new NutritionDto(310, 6m, 17m, 34m, 80, ["eggs", "gluten"]));

        Assert.Equal("gluten,eggs", item.Allergens);
        var dto = Nutrition.ToDto(item)!;
        Assert.Equal(310, dto.Calories);
        Assert.Equal(80, dto.WeightGrams);
        Assert.Equal(["gluten", "eggs"], dto.Allergens);
    }

    [Fact]
    public void Apply_NegativeNumbers_AreTreatedAsNotGiven()
    {
        var item = new MenuItem();
        Nutrition.Apply(item, new NutritionDto(-5, -1m, 2m, null, -10, []));

        Assert.Null(item.Calories);
        Assert.Null(item.Protein);
        Assert.Equal(2m, item.Fat);
        Assert.Null(item.WeightGrams);
        Assert.Null(item.Allergens);
    }

    [Fact]
    public void ToDto_NothingFilledIn_IsNull()
    {
        Assert.Null(Nutrition.ToDto(new MenuItem()));
    }

    [Fact]
    public void Apply_Null_ClearsEverything()
    {
        var item = new MenuItem { Allergens = "milk", Calories = 100 };
        Nutrition.Apply(item, null);
        Assert.Null(Nutrition.ToDto(item));
    }

    [Fact]
    public void AllergenCodes_AreTheFourteenEuAllergens()
    {
        Assert.Equal(14, Nutrition.AllergenCodes.Count);
        Assert.Equal(14, Nutrition.AllergenCodes.Distinct().Count());
    }
}

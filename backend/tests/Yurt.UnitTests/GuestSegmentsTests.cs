using Yurt.Application.Features.Marketing;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

public class GuestSegmentsTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 11);

    private static GuestStats Guest(int orders = 0, int last30 = 0, int? lastDaysAgo = null, decimal spent = 0, DateOnly? dob = null)
        => new(Guid.NewGuid(), "Test", Now.AddDays(-100), dob, false, true, orders, last30,
            lastDaysAgo is { } d ? Now.AddDays(-d - 10) : null, lastDaysAgo is { } l ? Now.AddDays(-l) : null, spent);

    [Fact]
    public void Groups_FollowOrdersAndDates()
    {
        var never = Guest();
        var fresh = Guest(orders: 1, last30: 1, lastDaysAgo: 3, spent: 1000);
        var regular = Guest(orders: 20, last30: 6, lastDaysAgo: 1, spent: 50000);
        var sleeping = Guest(orders: 4, lastDaysAgo: 45, spent: 6000);
        var gone = Guest(orders: 2, lastDaysAgo: 120, spent: 3000);
        var bday = Guest(dob: new DateOnly(1990, 11, 15));
        var all = new[] { never, fresh, regular, sleeping, gone, bday };

        List<GuestStats> In(string key) => GuestSegments.Filter(key, all, Now, Today).ToList();
        Assert.Equal(6, In(GuestSegments.All).Count);
        Assert.Equal([never, bday], In(GuestSegments.NeverOrdered));
        Assert.Equal([fresh], In(GuestSegments.New));
        Assert.Equal([regular], In(GuestSegments.Regulars));
        Assert.Equal([sleeping, gone], In(GuestSegments.Sleeping30));
        Assert.Equal([gone], In(GuestSegments.Sleeping90));
        Assert.Equal([bday], In(GuestSegments.BirthdayWeek));
        Assert.Equal([regular], In(GuestSegments.BigSpenders)); // top 10% of 4 paying guests = 1
        Assert.Empty(In("unknown"));
    }

    [Theory]
    [InlineData(11, 11, 0)]
    [InlineData(11, 12, 1)]
    [InlineData(11, 10, 364)]
    [InlineData(1, 1, 51)]
    public void DaysToBirthday_WrapsToNextYear(int month, int day, int expected)
        => Assert.Equal(expected, GuestSegments.DaysToBirthday(new DateOnly(1990, month, day), Today));

    [Fact]
    public void LeapDayBirthday_CountsAsFeb28()
        => Assert.Equal(0, GuestSegments.DaysToBirthday(new DateOnly(1996, 2, 29), new DateOnly(2027, 2, 28)));

    private static MarketingCampaign Campaign() => new()
    {
        Kind = CampaignKind.ComeBack, Name = "Back", CodeValidDays = 7,
        Title = "We miss you, {name}", Body = "{discount} off for {days} days",
        TitleRu = "Скучаем, {name}", BodyRu = "Скидка {discount} по коду {code}",
        DiscountType = DiscountType.Percentage, DiscountValue = 15,
    };

    [Fact]
    public void Render_PicksLanguage_FillsPlaceholders_AddsCodeWhenMissing()
    {
        var c = Campaign();
        Assert.Equal(("Скучаем, Aru", "Скидка 15% по коду BACK-X"), MarketingService.Render(c, "ru", "Aru", "BACK-X"));
        Assert.Equal(("Скучаем, Aru", "Скидка 15% по коду BACK-X"), MarketingService.Render(c, null, "Aru", "BACK-X"));
        Assert.Equal(("We miss you, Aru", "15% off for 7 days\nYour promo code: BACK-X"), MarketingService.Render(c, "en", "Aru", "BACK-X"));
        // No Kazakh text: Russian is used.
        Assert.Equal("Скучаем, Aru", MarketingService.Render(c, "kk", "Aru", "BACK-X").Title);
        // No name: the name is left out cleanly.
        Assert.Equal("We miss you", MarketingService.Render(c, "en", "", null).Title);
        c.DiscountType = DiscountType.FixedAmount; c.DiscountValue = 500;
        Assert.StartsWith("500 ₸ off", MarketingService.Render(c, "en", "A", null).Body);
    }

    [Fact]
    public void Apply_ChecksTheForm()
    {
        SaveCampaignDto Dto(string kind = "ComeBack", int days = 30, string? segment = null, string? type = null, decimal? value = null, string? title = "t")
            => new(kind, "Name", true, segment, days, title, null, null, "b", null, null, type, value, null, null);
        var c = new MarketingCampaign();
        Assert.Null(MarketingService.Apply(c, Dto(), isNew: true));
        Assert.Equal(CampaignKind.ComeBack, c.Kind);
        Assert.NotNull(MarketingService.Apply(new MarketingCampaign(), Dto(days: 3), true));
        Assert.NotNull(MarketingService.Apply(new MarketingCampaign(), Dto(kind: "OneOff"), true));
        Assert.Null(MarketingService.Apply(new MarketingCampaign(), Dto(kind: "OneOff", segment: "sleeping_30"), true));
        Assert.NotNull(MarketingService.Apply(new MarketingCampaign(), Dto(type: "Percentage", value: 0), true));
        Assert.NotNull(MarketingService.Apply(new MarketingCampaign(), Dto(title: null), true));
        Assert.NotNull(MarketingService.Apply(c, Dto(kind: "Birthday", days: 3), isNew: false)); // kind can't change
    }
}

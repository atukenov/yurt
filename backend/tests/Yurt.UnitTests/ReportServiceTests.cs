using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Reports;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

public class ReportServiceTests
{
    private static (ReportService svc, IApplicationDbContext db) Build(IEnumerable<UserReport>? reports = null)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var set = (reports ?? []).ToList().BuildMockDbSet();
        db.UserReports.Returns(set);
        return (new ReportService(db), db);
    }

    private static UserReport MakeReport(bool resolved, DateTime createdAt)
    {
        var customer = new CustomerUser { FirstName = "Jane", LastName = "Doe", MobileNumber = "+77011234567" };
        return new UserReport
        {
            CustomerId = customer.Id,
            Customer = customer,
            Text = "Something's wrong",
            IsResolved = resolved,
            CreatedAt = createdAt,
        };
    }

    [Fact]
    public async Task GetReportsAsync_FiltersByResolvedFlag()
    {
        var open = MakeReport(resolved: false, DateTime.UtcNow);
        var resolved = MakeReport(resolved: true, DateTime.UtcNow);
        var (svc, _) = Build([open, resolved]);

        var openResults = await svc.GetReportsAsync(resolved: false);
        var resolvedResults = await svc.GetReportsAsync(resolved: true);

        Assert.Single(openResults);
        Assert.Single(resolvedResults);
        Assert.False(openResults[0].IsResolved);
        Assert.True(resolvedResults[0].IsResolved);
    }

    [Fact]
    public async Task GetReportsAsync_OrdersByNewestFirst()
    {
        var older = MakeReport(false, DateTime.UtcNow.AddDays(-2));
        var newer = MakeReport(false, DateTime.UtcNow);
        var (svc, _) = Build([older, newer]);

        var result = await svc.GetReportsAsync(resolved: false);

        Assert.Equal(newer.Id, result[0].Id);
        Assert.Equal(older.Id, result[1].Id);
    }

    [Fact]
    public async Task GetReportsAsync_IncludesCustomerNameAndPhone()
    {
        var report = MakeReport(false, DateTime.UtcNow);
        var (svc, _) = Build([report]);

        var result = await svc.GetReportsAsync(resolved: false);

        Assert.Equal("Jane Doe", result[0].CustomerName);
        Assert.Equal("+77011234567", result[0].CustomerPhone);
    }

    [Fact]
    public async Task CreateReportAsync_TrimsTextAndSaves()
    {
        var (svc, db) = Build();
        var customerId = Guid.NewGuid();

        var result = await svc.CreateReportAsync(customerId, "  needs trimming  ");

        Assert.True(result.Succeeded);
        db.UserReports.Received(1).Add(Arg.Is<UserReport>(r =>
            r.CustomerId == customerId && r.Text == "needs trimming"));
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Locations.DTOs;
using Yurt.Application.Features.Locations.Services;
using Yurt.Domain.Entities;

namespace Yurt.UnitTests;

public class LocationServiceTests
{
    private static (LocationService svc, IApplicationDbContext db) Build(IEnumerable<Location>? locations = null)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var audit = Substitute.For<IAuditLogService>();

        var set = (locations ?? []).ToList().BuildMockDbSet();
        db.Locations.Returns(set);

        return (new LocationService(db, audit), db);
    }

    [Fact]
    public async Task GetActiveLocationsAsync_ExcludesInactiveAndSortsByName()
    {
        var active1 = new Location { Name = "Zebra Branch", IsActive = true };
        var active2 = new Location { Name = "Apple Branch", IsActive = true };
        var inactive = new Location { Name = "Closed Branch", IsActive = false };
        var (svc, _) = Build([active1, active2, inactive]);

        var result = await svc.GetActiveLocationsAsync();

        Assert.Equal(["Apple Branch", "Zebra Branch"], result.Select(r => r.Name));
    }

    [Fact]
    public async Task GetAllLocationsAsync_IncludesInactiveLocations()
    {
        var active = new Location { Name = "Active", IsActive = true };
        var inactive = new Location { Name = "Inactive", IsActive = false };
        var (svc, _) = Build([active, inactive]);

        var result = await svc.GetAllLocationsAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task CreateAsync_AddsLocationAndSaves()
    {
        var (svc, db) = Build();
        var dto = new CreateLocationDto
        {
            Name = "New Branch",
            Address = "Main St 1",
            WorkingHours = "09:00-21:00",
            ContactPhone = "+7000000000",
        };

        var result = await svc.CreateAsync(dto);

        Assert.True(result.Succeeded);
        Assert.Equal(201, result.StatusCode);
        db.Locations.Received(1).Add(Arg.Is<Location>(l => l.Name == "New Branch" && l.Address == "Main St 1"));
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Customers;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

/// <summary>
/// Tests for <see cref="CustomerService.GetCustomersAsync"/> — admin customer
/// list pagination and phone search, previously untested.
/// </summary>
public class CustomerServiceTests
{
    private static (CustomerService svc, IApplicationDbContext db) Build(IEnumerable<CustomerUser>? customers = null)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var audit = Substitute.For<IAuditLogService>();

        var set = (customers ?? []).ToList().BuildMockDbSet();
        db.CustomerUsers.Returns(set);

        return (new CustomerService(db, audit), db);
    }

    private static CustomerUser MakeCustomer(string phone, DateTime createdAt, params Order[] orders)
    {
        var c = new CustomerUser { MobileNumber = phone, FirstName = "F", LastName = "L", CreatedAt = createdAt };
        foreach (var o in orders) c.Orders.Add(o);
        return c;
    }

    [Fact]
    public async Task GetCustomersAsync_FiltersByPhoneSubstring()
    {
        var match = MakeCustomer("+77011234567", DateTime.UtcNow);
        var noMatch = MakeCustomer("+77029999999", DateTime.UtcNow);
        var (svc, _) = Build([match, noMatch]);

        var (total, items) = await svc.GetCustomersAsync("1234567", 1, 20);

        Assert.Equal(1, total);
        Assert.Equal("+77011234567", items[0].Phone);
    }

    [Fact]
    public async Task GetCustomersAsync_NoPhoneFilter_ReturnsAll()
    {
        var a = MakeCustomer("+77011111111", DateTime.UtcNow);
        var b = MakeCustomer("+77022222222", DateTime.UtcNow);
        var (svc, _) = Build([a, b]);

        var (total, items) = await svc.GetCustomersAsync(null, 1, 20);

        Assert.Equal(2, total);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task GetCustomersAsync_OrdersByNewestFirst()
    {
        var older = MakeCustomer("+77011111111", DateTime.UtcNow.AddDays(-5));
        var newer = MakeCustomer("+77022222222", DateTime.UtcNow);
        var (svc, _) = Build([older, newer]);

        var (_, items) = await svc.GetCustomersAsync(null, 1, 20);

        Assert.Equal("+77022222222", items[0].Phone);
        Assert.Equal("+77011111111", items[1].Phone);
    }

    [Fact]
    public async Task GetCustomersAsync_PaginatesResults()
    {
        var customers = Enumerable.Range(0, 5)
            .Select(i => MakeCustomer($"+7701000000{i}", DateTime.UtcNow.AddMinutes(-i)))
            .ToArray();
        var (svc, _) = Build(customers);

        var (total, page1) = await svc.GetCustomersAsync(null, 1, 2);
        var (_, page2) = await svc.GetCustomersAsync(null, 2, 2);

        Assert.Equal(5, total);
        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.NotEqual(page1[0].Id, page2[0].Id);
    }

    [Fact]
    public async Task GetCustomersAsync_OnlyCountsCompletedOrdersTowardSpendAndOrderCount()
    {
        var completed = new Order { Status = OrderStatus.Completed, Total = 1000, LocationId = Guid.NewGuid() };
        var declined = new Order { Status = OrderStatus.Declined, Total = 5000, LocationId = Guid.NewGuid() };
        var customer = MakeCustomer("+77011111111", DateTime.UtcNow, completed, declined);
        var (svc, _) = Build([customer]);

        var (_, items) = await svc.GetCustomersAsync(null, 1, 20);

        Assert.Equal(1, items[0].OrderCount);
        Assert.Equal(1000, items[0].TotalSpent);
    }

    [Fact]
    public async Task SetActiveAsync_UnknownId_ReturnsNotFound()
    {
        var (svc, _) = Build();
        var result = await svc.SetActiveAsync(Guid.NewGuid(), false);
        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }
}

using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.IikoIntegration.Services;
using Yurt.Application.Features.Loyalty;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

// Every test creates its own dedicated Location + MenuItem rather than mutating the
// shared seeded "Cappuccino" item / seeded LocationId that other test files (and other
// tests in this file) also rely on — those writes are otherwise never rolled back and
// leak across tests sharing the same collection-fixture database, e.g. one test's
// IikoTerminalGroupId/IikoProductId assignment silently "unblocking" a later test that
// deliberately wants an unmapped item or location.
[Collection("Integration")]
public class IikoOrderSyncTests(YurtWebAppFactory factory)
{
    private record PlacedOrder(Guid Id, string CustomerToken);

    // ── Push on accept ───────────────────────────────────────────────────────

    [Fact]
    public async Task MappedItem_OnAccept_PushesOrderToIikoAndRecordsId()
    {
        var fake = new FakeIikoApiClient();
        var terminalGroupId = Guid.NewGuid();
        var iikoProductId = Guid.NewGuid();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(enabledFactory.Services, terminalGroupId);
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, iikoProductId);

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000801", locationId, menuItemId);

        var call = Assert.Single(fake.PushCalls);
        Assert.Equal(terminalGroupId, call.TerminalGroupId);
        Assert.Equal(iikoProductId, call.Items.Single().ProductId);

        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == placed.Id);
        Assert.Equal(IikoOrderSyncStatus.Pushed, order.IikoOrderSyncStatus);
        Assert.Equal(fake.NextIikoOrderId, order.IikoDeliveryOrderId);
        Assert.Equal(IikoOrderSyncPendingAction.None, order.IikoOrderSyncPendingAction);
    }

    [Fact]
    public async Task PushOrdersDisabled_OnAccept_NeverCallsIiko()
    {
        var fake = new FakeIikoApiClient();
        var options = EnabledOptions();
        options.PushOrdersEnabled = false; // loyalty on, order-push off
        using var factoryWithLoyaltyOnly = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(options);
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(factoryWithLoyaltyOnly.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(factoryWithLoyaltyOnly.Services, Guid.NewGuid());

        var client = factoryWithLoyaltyOnly.CreateClient();
        await PlaceAndAcceptOrderAsync(factoryWithLoyaltyOnly, client, "+77002000802", locationId, menuItemId);

        Assert.Empty(fake.PushCalls);
    }

    [Fact]
    public async Task UnmappedMenuItem_OnAccept_SkipsPushAndLogsAudit()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        // Terminal group configured, but the menu item is left unmapped (no IikoProductId).
        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, iikoProductId: null);

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000803", locationId, menuItemId);

        Assert.Empty(fake.PushCalls);

        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == placed.Id);
        Assert.Equal(IikoOrderSyncStatus.SkippedUnmapped, order.IikoOrderSyncStatus);
        Assert.Equal(IikoOrderSyncPendingAction.None, order.IikoOrderSyncPendingAction);
        Assert.True(await db.AuditLogs.AnyAsync(a =>
            a.Action == "IikoOrderSkippedUnmapped" && a.EntityId == placed.Id.ToString()));
    }

    [Fact]
    public async Task LocationWithNoTerminalGroup_OnAccept_SkipsPush()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        // Menu item mapped, but the location is deliberately left with no terminal group.
        var locationId = await CreateTestLocationAsync(enabledFactory.Services, terminalGroupId: null);
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, Guid.NewGuid());

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000804", locationId, menuItemId);

        Assert.Empty(fake.PushCalls);
        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == placed.Id);
        Assert.Equal(IikoOrderSyncStatus.SkippedUnmapped, order.IikoOrderSyncStatus);
    }

    // ── Retry ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PushFails_MarksPendingPush_RetrySweepRecoversIt()
    {
        var fake = new FakeIikoApiClient { FailPush = true };
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, Guid.NewGuid());

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000805", locationId, menuItemId);

        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var order = await db.Orders.FirstAsync(o => o.Id == placed.Id);
            Assert.Equal(IikoOrderSyncStatus.NotPushed, order.IikoOrderSyncStatus);
            Assert.Equal(IikoOrderSyncPendingAction.Push, order.IikoOrderSyncPendingAction);
        }

        fake.FailPush = false;
        await using (var scope = enabledFactory.Services.CreateAsyncScope())
        {
            var sync = scope.ServiceProvider.GetRequiredService<IikoOrderSyncService>();
            await sync.RetryPendingAsync();
        }

        Assert.Single(fake.PushCalls);
        await using var verifyScope = enabledFactory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var verified = await verifyDb.Orders.FirstAsync(o => o.Id == placed.Id);
        Assert.Equal(IikoOrderSyncStatus.Pushed, verified.IikoOrderSyncStatus);
        Assert.Equal(IikoOrderSyncPendingAction.None, verified.IikoOrderSyncPendingAction);
    }

    // ── Close on completion ──────────────────────────────────────────────────

    [Fact]
    public async Task CompletedOrder_PreviouslyPushed_ClosesIikoOrder()
    {
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, Guid.NewGuid());

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000806", locationId, menuItemId);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(enabledFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);
        foreach (var status in new[] { "Preparing", "Ready", "Completed" })
            (await client.PostAsJsonAsync($"/api/v1/admin/orders/{placed.Id}/status", new { status }))
                .EnsureSuccessStatusCode();

        Assert.Equal(fake.NextIikoOrderId, Assert.Single(fake.CloseCalls));

        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == placed.Id);
        Assert.Equal(IikoOrderSyncStatus.Closed, order.IikoOrderSyncStatus);
    }

    [Fact]
    public async Task CompletedOrder_NeverPushed_DoesNotCallClose()
    {
        // Unmapped item — never pushed at accept time — must not attempt a close either.
        var fake = new FakeIikoApiClient();
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(EnabledOptions());
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, iikoProductId: null);

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000807", locationId, menuItemId);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(enabledFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);
        foreach (var status in new[] { "Preparing", "Ready", "Completed" })
            (await client.PostAsJsonAsync($"/api/v1/admin/orders/{placed.Id}/status", new { status }))
                .EnsureSuccessStatusCode();

        Assert.Empty(fake.CloseCalls);
    }

    // ── Webhook ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_DeliveryOrderErrorEvent_FlagsOrderFailed()
    {
        var fake = new FakeIikoApiClient();
        var options = EnabledOptions();
        options.WebhookAuthToken = "test-webhook-token";
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(options);
                services.AddSingleton<IIikoApiClient>(fake);
            }));

        var locationId = await CreateTestLocationAsync(enabledFactory.Services, Guid.NewGuid());
        var menuItemId = await CreateTestMenuItemAsync(enabledFactory.Services, Guid.NewGuid());

        var client = enabledFactory.CreateClient();
        var placed = await PlaceAndAcceptOrderAsync(enabledFactory, client, "+77002000808", locationId, menuItemId);
        var iikoOrderId = fake.PushCalls.Count > 0 ? fake.NextIikoOrderId : Guid.Empty;
        Assert.NotEqual(Guid.Empty, iikoOrderId);

        var payload = new[]
        {
            new
            {
                eventType = "DeliveryOrderError",
                eventInfo = new { id = iikoOrderId, order = (object?)null }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/iiko")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("Authorization", "test-webhook-token");
        var resp = await client.SendAsync(request);
        resp.EnsureSuccessStatusCode();

        await using var scope = enabledFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == placed.Id);
        Assert.Equal(IikoOrderSyncStatus.Failed, order.IikoOrderSyncStatus);
    }

    [Fact]
    public async Task Webhook_WrongAuthToken_ReturnsUnauthorized()
    {
        var options = EnabledOptions();
        options.WebhookAuthToken = "correct-token";
        using var enabledFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(options);
                services.AddSingleton<IIikoApiClient>(new FakeIikoApiClient());
            }));

        var client = enabledFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/iiko")
        {
            Content = JsonContent.Create(Array.Empty<object>())
        };
        request.Headers.Add("Authorization", "wrong-token");
        var resp = await client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IikoOptions EnabledOptions() => new()
    {
        Enabled          = true,
        PushOrdersEnabled = true,
        EarnPercent      = 0m, // isolate order-sync assertions from loyalty topup side effects
        OrganizationId   = Guid.NewGuid(),
        ProgramId        = Guid.NewGuid(),
        PaymentTypeId    = Guid.NewGuid(),
    };

    // Dedicated Location per test — never reuse/mutate the shared seeded location, since
    // that write would leak into every other test (in this file and others) that reads it.
    private static async Task<Guid> CreateTestLocationAsync(IServiceProvider services, Guid? terminalGroupId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var location = new Location
        {
            Name = $"iiko sync test location {Guid.NewGuid():N}",
            Address = "Test address",
            WorkingHours = "",
            ContactPhone = "",
            IsActive = true,
            IikoTerminalGroupId = terminalGroupId,
        };
        db.Locations.Add(location);
        await db.SaveChangesAsync();
        return location.Id;
    }

    // Dedicated MenuItem per test — never reuse/mutate the shared seeded "Cappuccino"
    // item, for the same reason as CreateTestLocationAsync above.
    private static async Task<Guid> CreateTestMenuItemAsync(IServiceProvider services, Guid? iikoProductId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var category = await db.MenuCategories.FirstAsync();
        var item = new MenuItem
        {
            CategoryId = category.Id,
            Name = $"iiko sync test item {Guid.NewGuid():N}",
            Description = "",
            Price = 500,
            IsAvailable = true,
            IikoProductId = iikoProductId,
        };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private static async Task<PlacedOrder> PlaceAndAcceptOrderAsync(
        WebApplicationFactory<Program> appFactory, HttpClient client, string phone,
        Guid locationId, Guid menuItemId)
    {
        var (customerToken, _) = await ApiHelpers.CreateCustomerAsync(client, phone);
        ApiHelpers.Authorize(client, customerToken);

        var resp = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId,
            paymentMethod = "Cash",
            items         = new[] { new { menuItemId, quantity = 1 } }
        });
        resp.EnsureSuccessStatusCode();
        var order = await resp.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts);

        var adminToken = await ApiHelpers.CreateAdminTokenAsync(appFactory.Services, client);
        ApiHelpers.Authorize(client, adminToken);

        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order!.Id}/payment",
                new { paymentStatus = "Paid", paymentMethod = "Cash" }))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/v1/admin/orders/{order.Id}/accept", new { etaMinutes = 10 }))
            .EnsureSuccessStatusCode();

        ApiHelpers.ClearAuth(client);
        return new PlacedOrder(order.Id, customerToken);
    }
}

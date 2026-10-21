using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Yurt.Domain.Entities;
using Yurt.Infrastructure.Persistence;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Network printers per branch: setup, test print, and tickets/receipts sent by the server (own_pos, Phase 6).</summary>
[Collection("Integration")]
public class PrinterTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private const int PrinterPort = 9107, ClosedPort = 9108;

    static PrinterTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Fact]
    public async Task Printers_AreValidated_TestPrint_AndAutoPrintOnAccept()
    {
        using var printer = new FakePrinter(PrinterPort);
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));

        var bar = await CreateStationAsync(admin, "Bar");
        var other = await CreateStationAsync(admin, "Grill");
        var drink = await CreateItemAsync(bar, "Printed latte");
        var food = await CreateItemAsync(other, "Printed burger");
        var printerIds = new List<Guid>();

        try
        {
            object Body(string name, string kind, Guid? stationId, string host = "127.0.0.1", int port = PrinterPort, bool autoPrint = true)
                => new { locationId = LocationId, name, kind, stationId, host, port, lineWidth = 48, codeTable = 17, language = "ru", autoPrint };

            // Only raw-printing ports and plain addresses are accepted, so the server never talks to anything else.
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/printers", Body("P", "Kitchen", bar, port: 5432))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/printers", Body("P", "Kitchen", bar, host: "http://x/y"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/printers", Body("P", "Fax", bar))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/v1/admin/printers", Body("P", "Kitchen", Guid.NewGuid()))).StatusCode);

            var barPrinter = await CreatePrinterAsync(admin, Body("Bar printer", "Kitchen", bar));
            var till = await CreatePrinterAsync(admin, Body("Till", "Receipt", bar, autoPrint: false));
            printerIds.AddRange([barPrinter, till]);
            var list = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/printers");
            // A receipt printer has no station.
            var tillJson = list.EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == till);
            Assert.True(!tillJson.TryGetProperty("stationId", out var sid) || sid.ValueKind == JsonValueKind.Null);

            // Test print goes out right away; a printer that does not answer gives a clear error.
            (await admin.PostAsync($"/api/v1/admin/printers/{barPrinter}/test", null)).EnsureSuccessStatusCode();
            Assert.Contains("Тестовая печать", await printer.NextAsync());
            var offline = await CreatePrinterAsync(admin, Body("Offline", "Kitchen", other, port: ClosedPort, autoPrint: false));
            printerIds.Add(offline);
            Assert.Equal(HttpStatusCode.BadGateway, (await admin.PostAsync($"/api/v1/admin/printers/{offline}/test", null)).StatusCode);

            // Accepting an order sends the bar ticket by itself: only the bar's item. The receipt printer waits for a press.
            var order = await PlaceAcceptedOrderAsync(admin, "+77002990501", drink, food);
            var ticket = await printer.NextAsync();
            Assert.Contains("1 x Printed latte", ticket);
            Assert.DoesNotContain("Printed burger", ticket);
            await WaitForJobAsync(admin, order, "Ticket", "Printed");

            var res = await admin.PostAsync($"/api/v1/admin/orders/{order}/print/receipt", null);
            Assert.Equal(1, (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("printers").GetInt32());
            var receipt = await printer.NextAsync();
            Assert.Contains("Итого", receipt);
            Assert.Contains("Printed burger", receipt);

            // A station with no printer here gets 0, so the screen falls back to printing in the browser.
            res = await admin.PostAsync($"/api/v1/admin/orders/{order}/print/ticket?stationId={Guid.NewGuid()}", null);
            Assert.Equal(0, (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("printers").GetInt32());

            // The offline grill printer: the job is tried again, then shows as failed with the reason.
            res = await admin.PostAsync($"/api/v1/admin/orders/{order}/print/ticket?stationId={other}", null);
            Assert.Equal(1, (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("printers").GetInt32());
            var failed = await WaitForJobAsync(admin, order, "Ticket", "Failed", offline, TimeSpan.FromSeconds(60));
            Assert.Equal(3, failed.GetProperty("attempts").GetInt32());
            Assert.False(string.IsNullOrEmpty(failed.GetProperty("lastError").GetString()));
            Assert.False(failed.TryGetProperty("printedAt", out var printedAt) && printedAt.ValueKind != JsonValueKind.Null);

            Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/admin/printers/{offline}")).StatusCode);
            printerIds.Remove(offline);
        }
        finally
        {
            foreach (var id in printerIds) await admin.DeleteAsync($"/api/v1/admin/printers/{id}");
            foreach (var s in new[] { bar, other }) await admin.DeleteAsync($"/api/v1/admin/stations/{s}");
        }
    }

    [Fact]
    public async Task OnlyAnAdmin_CanSetUpPrinters()
    {
        var staff = factory.CreateClient();
        staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, staff, username: "printworker", role: Domain.Enums.AdminRole.Worker));
        var body = new { locationId = LocationId, name = "P", kind = "Kitchen", host = "127.0.0.1", port = 9100 };
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/admin/printers", body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/admin/printers")).StatusCode);
    }

    private static async Task<JsonElement> WaitForJobAsync(
        HttpClient admin, Guid order, string kind, string status, Guid? printerId = null, TimeSpan? within = null)
    {
        var until = DateTime.UtcNow + (within ?? TimeSpan.FromSeconds(15));
        while (true)
        {
            var jobs = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/printers/jobs?limit=100");
            var job = jobs.EnumerateArray().FirstOrDefault(j =>
                j.TryGetProperty("orderId", out var o) && o.ValueKind != JsonValueKind.Null && o.GetGuid() == order
                && j.GetProperty("kind").GetString() == kind && j.GetProperty("status").GetString() == status
                && (printerId is null || j.GetProperty("printerId").GetGuid() == printerId));
            if (job.ValueKind != JsonValueKind.Undefined) return job;
            if (DateTime.UtcNow > until) throw new TimeoutException($"No {status} {kind} job for order {order}.");
            await Task.Delay(250);
        }
    }

    private static async Task<Guid> CreatePrinterAsync(HttpClient admin, object body)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/admin/printers", body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateStationAsync(HttpClient admin, string name)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/admin/stations", new { name = $"{name} {Guid.NewGuid():N}"[..20] });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateItemAsync(Guid stationId, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new MenuCategory { Name = $"Print cat {Guid.NewGuid():N}"[..18], StationId = stationId };
        db.MenuCategories.Add(category);
        await db.SaveChangesAsync();
        var item = new MenuItem { CategoryId = category.Id, Name = name, Description = "t", Price = 4m, IsAvailable = true };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<Yurt.Application.Common.Interfaces.IMenuCacheService>().InvalidateAllMenuAsync();
        return item.Id;
    }

    private async Task<Guid> PlaceAcceptedOrderAsync(HttpClient admin, string phone, params Guid[] items)
    {
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, phone);
        ApiHelpers.Authorize(customer, token);
        var placed = await customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod = "Cash",
            items = items.Select(i => new { menuItemId = i, quantity = 1, toppings = Array.Empty<object>() }).ToArray(),
        });
        placed.EnsureSuccessStatusCode();
        var order = (await placed.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts))!.Id;
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order}/payment", new { paymentStatus = "Paid" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{order}/accept", new { etaMinutes = 5 })).EnsureSuccessStatusCode();
        return order;
    }

    /// <summary>A pretend network printer: accepts connections and hands back what each one sent, read as code page 866.</summary>
    private sealed class FakePrinter : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly BlockingCollection<string> _received = new();
        private readonly CancellationTokenSource _stop = new();

        public FakePrinter(int port)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _ = Task.Run(AcceptLoopAsync);
        }

        public Task<string> NextAsync() => Task.Run(() =>
            _received.TryTake(out var text, TimeSpan.FromSeconds(15)) ? text : throw new TimeoutException("Nothing printed."));

        private async Task AcceptLoopAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    using var buffer = new MemoryStream();
                    await client.GetStream().CopyToAsync(buffer, _stop.Token);
                    _received.Add(Encoding.GetEncoding(866).GetString(buffer.ToArray()));
                }
                catch (OperationCanceledException) { return; }
                catch (SocketException) { return; }
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
        }
    }
}

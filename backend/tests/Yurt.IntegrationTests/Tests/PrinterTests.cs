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

/// <summary>Printers per branch: setup, test print, and tickets/receipts sent by the server, the print agent or a Bluetooth tablet (own_pos, Phase 6).</summary>
[Collection("Integration")]
public class PrinterTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private const int PrinterPort = 9107, ClosedPort = 9108, DrawerPort = 9106;

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
    public async Task PrintAgent_FetchesItsBranchJobs_AndReportsBack()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var station = await CreateStationAsync(admin, "Agent bar");
        var drink = await CreateItemAsync(station, "Agent latte");
        Guid printerId = default;

        try
        {
            // An address only the cafe's own network can reach: the server never connects to it, the agent does.
            printerId = await CreatePrinterAsync(admin, new
            {
                locationId = LocationId, name = "Agent printer", kind = "Kitchen", stationId = station,
                host = "192.168.77.10", port = 9100, lineWidth = 32, codeTable = 17, language = "en", autoPrint = true, viaAgent = true,
            });

            var agent = factory.CreateClient();
            HttpRequestMessage Claim(string key)
            {
                var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/print-agent/jobs/claim");
                req.Headers.Add("X-Print-Agent-Key", key);
                req.Headers.Add("X-Tenant", key.Split('.')[0]);
                return req;
            }
            async Task<HttpResponseMessage> Report(string key, Guid job, bool ok)
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/print-agent/jobs/{job}/result")
                    { Content = JsonContent.Create(new { ok, error = ok ? null : "Paper out" }) };
                req.Headers.Add("X-Print-Agent-Key", key);
                req.Headers.Add("X-Tenant", key.Split('.')[0]);
                return await agent.SendAsync(req);
            }

            Assert.Equal(HttpStatusCode.Unauthorized, (await agent.SendAsync(Claim("default.not-a-key"))).StatusCode);

            var created = await admin.PostAsync($"/api/v1/admin/printers/agents/{LocationId}", null);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var key = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("key").GetString()!;
            Assert.StartsWith("default.", key);

            // The order's ticket waits for the agent: the server's own sender leaves it alone.
            var order = await PlaceAcceptedOrderAsync(admin, "+77002990502", drink);
            await Task.Delay(1000);
            var jobs = await ClaimedAsync(await agent.SendAsync(Claim(key)));
            var ticket = jobs.Single(j => j.GetProperty("host").GetString() == "192.168.77.10");
            Assert.Equal(9100, ticket.GetProperty("port").GetInt32());
            Assert.Contains("1 x Agent latte", Encoding.GetEncoding(866).GetString(ticket.GetProperty("data").GetBytesFromBase64()));
            Assert.Empty(await ClaimedAsync(await agent.SendAsync(Claim(key)))); // Already taken.

            (await Report(key, ticket.GetProperty("id").GetGuid(), true)).EnsureSuccessStatusCode();
            await WaitForJobAsync(admin, order, "Ticket", "Printed", printerId);
            Assert.Equal(HttpStatusCode.Conflict, (await Report(key, ticket.GetProperty("id").GetGuid(), true)).StatusCode);

            var agents = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/printers/agents");
            Assert.True(agents.EnumerateArray().Single(a => a.GetProperty("locationId").GetGuid() == LocationId)
                .TryGetProperty("lastSeenAt", out _));

            // A test print for an agent printer is queued; three failed tries mark it not printed.
            var test = await (await admin.PostAsync($"/api/v1/admin/printers/{printerId}/test", null)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(test.GetProperty("printed").GetBoolean());
            for (var i = 0; i < 3; i++)
            {
                var job = Assert.Single(await ClaimedAsync(await agent.SendAsync(Claim(key))));
                (await Report(key, job.GetProperty("id").GetGuid(), false)).EnsureSuccessStatusCode();
            }
            Assert.Empty(await ClaimedAsync(await agent.SendAsync(Claim(key))));
            var jobsList = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/printers/jobs?limit=100");
            var failed = jobsList.EnumerateArray().First(j => j.GetProperty("kind").GetString() == "Test" && j.GetProperty("printerId").GetGuid() == printerId);
            Assert.Equal("Failed", failed.GetProperty("status").GetString());
            Assert.Equal("Paper out", failed.GetProperty("lastError").GetString());

            // A new key replaces the old one; removing it locks the agent out.
            var newKey = (await (await admin.PostAsync($"/api/v1/admin/printers/agents/{LocationId}", null)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("key").GetString()!;
            Assert.Equal(HttpStatusCode.Unauthorized, (await agent.SendAsync(Claim(key))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await agent.SendAsync(Claim(newKey))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/admin/printers/agents/{LocationId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await agent.SendAsync(Claim(newKey))).StatusCode);
        }
        finally
        {
            if (printerId != default) await admin.DeleteAsync($"/api/v1/admin/printers/{printerId}");
            await admin.DeleteAsync($"/api/v1/admin/printers/agents/{LocationId}");
            await admin.DeleteAsync($"/api/v1/admin/stations/{station}");
        }
    }

    [Fact]
    public async Task BluetoothPrinter_TabletFetchesItsJobs_AndReportsBack()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        // The kitchen tablet is often signed in as a worker: that is enough to print.
        var tablet = factory.CreateClient();
        tablet.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, tablet, username: "btworker", role: Domain.Enums.AdminRole.Worker));
        var station = await CreateStationAsync(admin, "BT bar");
        var drink = await CreateItemAsync(station, "BT latte");
        Guid printerId = default;

        try
        {
            // No address: only the tablet paired with it can reach a Bluetooth printer. The agent tick is ignored.
            var created = await admin.PostAsJsonAsync("/api/v1/admin/printers", new
            {
                locationId = LocationId, name = "BT printer", kind = "Kitchen", stationId = station,
                lineWidth = 32, codeTable = 17, language = "en", autoPrint = true, viaAgent = true, viaBluetooth = true,
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var saved = await created.Content.ReadFromJsonAsync<JsonElement>();
            printerId = saved.GetProperty("id").GetGuid();
            Assert.True(saved.GetProperty("viaBluetooth").GetBoolean());
            Assert.False(saved.GetProperty("viaAgent").GetBoolean());
            Assert.Equal("", saved.GetProperty("host").GetString());

            Task<HttpResponseMessage> Claim(params Guid[] printers)
                => tablet.PostAsJsonAsync("/api/v1/admin/printers/bluetooth/claim", new { printerIds = printers });
            Task<HttpResponseMessage> Report(Guid job, bool ok)
                => tablet.PostAsJsonAsync($"/api/v1/admin/printers/bluetooth/jobs/{job}/result", new { ok, error = ok ? null : "Printer is off" });

            // The ticket waits for the tablet: the server's own sender leaves it alone.
            var order = await PlaceAcceptedOrderAsync(admin, "+77002991503", drink);
            await Task.Delay(1000);
            await WaitForJobAsync(admin, order, "Ticket", "Pending", printerId);

            // A tablet only gets jobs for the printers it says it is connected to.
            Assert.Empty(await ClaimedAsync(await Claim()));
            Assert.Empty(await ClaimedAsync(await Claim(Guid.NewGuid())));
            var ticket = Assert.Single(await ClaimedAsync(await Claim(printerId)));
            Assert.Equal(printerId, ticket.GetProperty("printerId").GetGuid());
            Assert.Equal("Ticket", ticket.GetProperty("kind").GetString());
            Assert.Contains("1 x BT latte", Encoding.GetEncoding(866).GetString(ticket.GetProperty("data").GetBytesFromBase64()));
            Assert.Empty(await ClaimedAsync(await Claim(printerId))); // Already taken.

            (await Report(ticket.GetProperty("id").GetGuid(), true)).EnsureSuccessStatusCode();
            await WaitForJobAsync(admin, order, "Ticket", "Printed", printerId);
            Assert.Equal(HttpStatusCode.Conflict, (await Report(ticket.GetProperty("id").GetGuid(), true)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Report(Guid.NewGuid(), true)).StatusCode);

            // A test print is queued for the tablet; three failed tries mark it not printed.
            var test = await (await admin.PostAsync($"/api/v1/admin/printers/{printerId}/test", null)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(test.GetProperty("printed").GetBoolean());
            for (var i = 0; i < 3; i++)
            {
                var job = Assert.Single(await ClaimedAsync(await Claim(printerId)));
                (await Report(job.GetProperty("id").GetGuid(), false)).EnsureSuccessStatusCode();
            }
            Assert.Empty(await ClaimedAsync(await Claim(printerId)));
            var jobsList = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/printers/jobs?limit=100");
            var failed = jobsList.EnumerateArray().First(j => j.GetProperty("kind").GetString() == "Test" && j.GetProperty("printerId").GetGuid() == printerId);
            Assert.Equal("Failed", failed.GetProperty("status").GetString());
            Assert.Equal("Printer is off", failed.GetProperty("lastError").GetString());

            Assert.Equal(HttpStatusCode.BadRequest, (await Claim(Enumerable.Range(0, 31).Select(_ => Guid.NewGuid()).ToArray())).StatusCode);
        }
        finally
        {
            if (printerId != default) await admin.DeleteAsync($"/api/v1/admin/printers/{printerId}");
            await admin.DeleteAsync($"/api/v1/admin/stations/{station}");
        }
    }

    private static async Task<List<JsonElement>> ClaimedAsync(HttpResponseMessage res)
    {
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
    }

    [Fact]
    public async Task CashDrawer_OpensOnCashPayment_AndOnButton()
    {
        using var printer = new FakePrinter(DrawerPort);
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        var station = await CreateStationAsync(admin, "Till");
        var item = await CreateItemAsync(station, "Drawer latte");
        var printerIds = new List<Guid>();
        try
        {
            object Body(string name, string kind) => new
            {
                locationId = LocationId, name, kind, host = "127.0.0.1", port = DrawerPort, lineWidth = 48, codeTable = 17,
                language = "ru", autoPrint = false, cashDrawer = true,
            };
            var till = await CreatePrinterAsync(admin, Body("Till with drawer", "Receipt"));
            var kitchen = await CreatePrinterAsync(admin, Body("Kitchen", "Kitchen"));
            printerIds.AddRange([till, kitchen]);

            // Only a receipt printer can have a cash drawer.
            var list = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/printers")).EnumerateArray().ToList();
            Assert.True(list.Single(p => p.GetProperty("id").GetGuid() == till).GetProperty("cashDrawer").GetBoolean());
            Assert.False(list.Single(p => p.GetProperty("id").GetGuid() == kitchen).GetProperty("cashDrawer").GetBoolean());

            // Cash marked paid at the till opens the drawer, and nothing is printed.
            var cash = await PlaceOrderAsync("+77002990503", "Cash", item);
            (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{cash}/payment", new { paymentStatus = "Paid" })).EnsureSuccessStatusCode();
            var kick = await printer.NextAsync();
            Assert.Contains("\u001Bp\u0000", kick);
            Assert.DoesNotContain("Итого", kick);
            await WaitForJobAsync(admin, cash, "Drawer", "Printed", till);

            // A card payment leaves the drawer shut.
            var card = await PlaceOrderAsync("+77002990504", "Card", item);
            (await admin.PostAsJsonAsync($"/api/v1/admin/orders/{card}/payment", new { paymentStatus = "Paid" })).EnsureSuccessStatusCode();
            await Task.Delay(1000);
            var jobs = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/printers/jobs?limit=100");
            Assert.DoesNotContain(jobs.EnumerateArray(), j =>
                j.TryGetProperty("orderId", out var o) && o.ValueKind != JsonValueKind.Null && o.GetGuid() == card);

            // Any staff member can press Open drawer; it is not tied to an order.
            var staff = factory.CreateClient();
            staff.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                await ApiHelpers.CreateAdminTokenAsync(factory.Services, staff, username: "drawerworker", role: Domain.Enums.AdminRole.Worker));
            var res = await staff.PostAsync($"/api/v1/admin/cash-drawer/{LocationId}/open", null);
            Assert.Equal(1, (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("printers").GetInt32());
            Assert.Contains("\u001Bp\u0000", await printer.NextAsync());
            Assert.Equal(HttpStatusCode.NotFound, (await staff.PostAsync($"/api/v1/admin/cash-drawer/{Guid.NewGuid()}/open", null)).StatusCode);
        }
        finally
        {
            foreach (var id in printerIds) await admin.DeleteAsync($"/api/v1/admin/printers/{id}");
            await admin.DeleteAsync($"/api/v1/admin/stations/{station}");
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

    private async Task<Guid> PlaceOrderAsync(string phone, string paymentMethod, params Guid[] items)
    {
        var customer = factory.CreateClient();
        var (token, _) = await ApiHelpers.CreateCustomerAsync(customer, phone);
        ApiHelpers.Authorize(customer, token);
        var placed = await customer.PostAsJsonAsync("/api/v1/orders", new
        {
            locationId = LocationId, paymentMethod,
            items = items.Select(i => new { menuItemId = i, quantity = 1, toppings = Array.Empty<object>() }).ToArray(),
        });
        placed.EnsureSuccessStatusCode();
        return (await placed.Content.ReadFromJsonAsync<ApiHelpers.OrderResult>(ApiHelpers.JsonOpts))!.Id;
    }

    private async Task<Guid> PlaceAcceptedOrderAsync(HttpClient admin, string phone, params Guid[] items)
    {
        var order = await PlaceOrderAsync(phone, "Cash", items);
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

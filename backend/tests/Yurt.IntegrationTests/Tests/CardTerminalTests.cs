using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Yurt.IntegrationTests.Helpers;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Bank card terminals (own_pos): setup, safe payment states, refunds, and the branch agent path.</summary>
[Collection("Integration")]
public class CardTerminalTests(YurtWebAppFactory factory)
{
    private static readonly Guid LocationId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private const string Base = "/api/v1/admin/card-terminals";

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            await ApiHelpers.CreateAdminTokenAsync(factory.Services, admin));
        return admin;
    }

    private static async Task<Guid> CreateAsync(HttpClient admin, string provider, bool viaAgent, string host = "")
    {
        var res = await admin.PostAsJsonAsync(Base, new { locationId = LocationId, name = "T " + Guid.NewGuid().ToString("N")[..6], provider, host, port = 8080, viaAgent, active = true });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> PayAsync(HttpClient admin, Guid terminal, decimal amount, HttpStatusCode expected = HttpStatusCode.Created)
    {
        var res = await admin.PostAsJsonAsync($"{Base}/payments", new { terminalId = terminal, amount });
        Assert.Equal(expected, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Setup_IsValidated()
    {
        var admin = await AdminAsync();
        object Body(string provider, bool viaAgent, string host) => new { locationId = LocationId, name = "T", provider, host, port = 8080, viaAgent };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Base, Body("nope", true, ""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Base, Body("halyk", true, ""))).StatusCode);           // address needed
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Base, Body("halyk", true, "http://x/y"))).StatusCode); // plain host only
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Base, Body("halyk", false, "192.168.1.60"))).StatusCode); // no server adapter
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Base, Body("halyk", true, "192.168.1.60"))).StatusCode);
    }

    [Fact]
    public async Task ServerSideTerminal_ApprovesDeclinesAndNeverGuesses()
    {
        var admin = await AdminAsync();
        var terminal = await CreateAsync(admin, "simulator", viaAgent: false);

        Assert.Equal("Approved", (await PayAsync(admin, terminal, 2300)).GetProperty("status").GetString() is "Approved" ? "Approved" : "x");

        // Whole tenge only, and above zero.
        await PayAsync(admin, terminal, 10.5m, HttpStatusCode.BadRequest);
        await PayAsync(admin, terminal, 0, HttpStatusCode.BadRequest);

        Assert.Equal("Declined", (await PayAsync(admin, terminal, 1113)).GetProperty("status").GetString());
        Assert.Equal("Failed", (await PayAsync(admin, terminal, 1177)).GetProperty("status").GetString());

        // No answer = Unknown, and it blocks a new charge until staff resolve it.
        var unknown = await PayAsync(admin, terminal, 1199);
        Assert.Equal("Unknown", unknown.GetProperty("status").GetString());
        await PayAsync(admin, terminal, 500, HttpStatusCode.Conflict);
        var resolved = await admin.PostAsJsonAsync($"{Base}/payments/{unknown.GetProperty("id").GetGuid()}/resolve", new { charged = false });
        Assert.Equal("Failed", (await resolved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal("Approved", (await PayAsync(admin, terminal, 500)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Refund_OnlyOnce_AndOnlyForApproved()
    {
        var admin = await AdminAsync();
        var terminal = await CreateAsync(admin, "simulator", viaAgent: false);
        var sale = await PayAsync(admin, terminal, 3000);
        var declined = await PayAsync(admin, terminal, 1013);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"{Base}/payments/{declined.GetProperty("id").GetGuid()}/refund", null)).StatusCode);

        var id = sale.GetProperty("id").GetGuid();
        var refund = await admin.PostAsync($"{Base}/payments/{id}/refund", null);
        Assert.Equal(HttpStatusCode.Created, refund.StatusCode);
        var json = await refund.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", json.GetProperty("status").GetString());
        Assert.Equal("Refund", json.GetProperty("kind").GetString() is "Refund" or "1" ? "Refund" : "x");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"{Base}/payments/{id}/refund", null)).StatusCode);
    }

    [Fact]
    public async Task AgentTerminal_TakesRequest_AndReportsResult()
    {
        var admin = await AdminAsync();
        var terminal = await CreateAsync(admin, "halyk", viaAgent: true, host: "192.168.1.61");
        var key = (await (await admin.PostAsync($"/api/v1/admin/printers/agents/{LocationId}", null)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("key").GetString()!;
        try
        {
            HttpRequestMessage Agent(string url, object? body = null)
            {
                var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
                req.Headers.Add("X-Print-Agent-Key", key);
                req.Headers.Add("X-Tenant", key.Split('.')[0]);
                return req;
            }

            var pay = await PayAsync(admin, terminal, 4200);
            var id = pay.GetProperty("id").GetGuid();
            Assert.Equal("Pending", pay.GetProperty("status").GetString());

            // A wrong key gets nothing.
            var bad = new HttpRequestMessage(HttpMethod.Post, "/api/v1/print-agent/terminal/claim");
            bad.Headers.Add("X-Print-Agent-Key", "nope"); bad.Headers.Add("X-Tenant", key.Split('.')[0]);
            Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().SendAsync(bad)).StatusCode);

            var agent = factory.CreateClient();
            var claimed = await (await agent.SendAsync(Agent("/api/v1/print-agent/terminal/claim"))).Content.ReadFromJsonAsync<JsonElement>();
            var job = claimed.EnumerateArray().Single(j => j.GetProperty("id").GetGuid() == id);
            Assert.Equal("192.168.1.61", job.GetProperty("host").GetString());
            // Taken once only.
            var again = await (await agent.SendAsync(Agent("/api/v1/print-agent/terminal/claim"))).Content.ReadFromJsonAsync<JsonElement>();
            Assert.DoesNotContain(again.EnumerateArray(), j => j.GetProperty("id").GetGuid() == id);
            // Cancelling is no longer possible once the terminal has it.
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"{Base}/payments/{id}/cancel", null)).StatusCode);

            (await agent.SendAsync(Agent($"/api/v1/print-agent/terminal/{id}/result",
                new { outcome = "approved", approvalCode = "ZZ1234", transactionRef = "RRN42", cardLast4 = "1111" }))).EnsureSuccessStatusCode();
            var done = await admin.GetFromJsonAsync<JsonElement>($"{Base}/payments/{id}");
            Assert.Equal("Approved", done.GetProperty("status").GetString());
            Assert.Equal("1111", done.GetProperty("cardLast4").GetString());

            // A late second report cannot change a finished payment.
            await agent.SendAsync(Agent($"/api/v1/print-agent/terminal/{id}/result", new { outcome = "declined", error = "late" }));
            Assert.Equal("Approved", (await admin.GetFromJsonAsync<JsonElement>($"{Base}/payments/{id}")).GetProperty("status").GetString());

            // A request nobody takes can be cancelled.
            var waiting = await PayAsync(admin, terminal, 800);
            var cancelled = await admin.PostAsync($"{Base}/payments/{waiting.GetProperty("id").GetGuid()}/cancel", null);
            Assert.Equal("Cancelled", (await cancelled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        }
        finally { await admin.DeleteAsync($"/api/v1/admin/printers/agents/{LocationId}"); }
    }
}

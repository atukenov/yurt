using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Fiscal;
using Yurt.Application.Features.Orders.Services;
using Yurt.Application.Features.Printing;
using Yurt.Application.Features.Shifts;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Persistence;

namespace Yurt.IntegrationTests.Tests;

/// <summary>Official tax receipts: off by default, queued for paid orders, retried, skipped per method, returned on refund (own_pos, Phase 6).</summary>
[Collection("Integration")]
public class FiscalReceiptTests(YurtWebAppFactory factory)
{
    private sealed class FakeRegistrar : IFiscalRegistrar
    {
        public bool Fail { get; set; }
        public List<FiscalReceiptRequest> Sent { get; } = [];
        public List<string> ShiftsClosed { get; } = [];
        public string Provider => FiscalProviders.Webkassa;

        public Task<FiscalResult> SendReceiptAsync(FiscalCredentials credentials, FiscalReceiptRequest receipt, CancellationToken ct = default)
        {
            if (Fail) return Task.FromResult(FiscalResult.Fail("Webkassa is down"));
            Sent.Add(receipt);
            return Task.FromResult(FiscalResult.Ok($"CHK{Sent.Count}", "https://ofd.example/t/1"));
        }

        public Task<FiscalResult> CloseShiftAsync(FiscalCredentials credentials, string registerNumber, CancellationToken ct = default)
        {
            if (Fail) return Task.FromResult(FiscalResult.Fail("Webkassa is down"));
            ShiftsClosed.Add(registerNumber);
            return Task.FromResult(FiscalResult.Ok("1", null));
        }
    }

    private sealed record Cafe(Guid Id, string Slug, Guid LocationId, Guid CustomerId, Guid MenuItemId);

    private IServiceScope ScopeFor(Cafe cafe)
    {
        var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(cafe.Id, cafe.Slug);
        return scope;
    }

    private static FiscalReceiptService Fiscal(IServiceScope scope, FakeRegistrar registrar)
    {
        var sp = scope.ServiceProvider;
        return new FiscalReceiptService(
            sp.GetRequiredService<ApplicationDbContext>(), sp.GetRequiredService<IModuleGate>(),
            sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<ITenantSecrets>(),
            [registrar], sp.GetRequiredService<PrintService>(), sp.GetRequiredService<IQrCodeImages>(),
            NullLogger<FiscalReceiptService>.Instance);
    }

    private static FiscalSettingsService Settings(IServiceScope scope)
    {
        var sp = scope.ServiceProvider;
        return new FiscalSettingsService(
            sp.GetRequiredService<ApplicationDbContext>(), sp.GetRequiredService<ITenantContext>(),
            sp.GetRequiredService<ITenantDirectory>(), sp.GetRequiredService<ITenantSecrets>(),
            sp.GetRequiredService<IAuditLogService>());
    }

    private async Task<Cafe> NewCafeAsync()
    {
        var slug = $"f-{Guid.NewGuid():N}"[..12];
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Tenants.Add(new Tenant { Slug = slug, Name = slug });
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<ITenantDirectory>().Invalidate();
        }
        var id = await IdOf(slug);
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(id, slug);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var location = new Location { Name = "Main", Address = "x", WorkingHours = "x", ContactPhone = "x" };
            var customer = new CustomerUser { MobileNumber = $"+7{Random.Shared.NextInt64(7000000000, 7999999999)}", PinHash = "x", FirstName = "A", LastName = "B" };
            var category = new MenuCategory { Name = "Coffee" };
            var item = new MenuItem { Category = category, Name = "Latte", Description = "t", Price = 1000m, IsAvailable = true };
            db.AddRange(location, customer, category, item);
            await db.SaveChangesAsync();
            return new Cafe(id, slug, location.Id, customer.Id, item.Id);
        }
    }

    private async Task<Guid> IdOf(string slug)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Tenants.Where(t => t.Slug == slug).Select(t => t.Id).FirstAsync();
    }

    /// <summary>Turns receipts on with a login and the branch's register number.</summary>
    private async Task TurnOnAsync(Cafe cafe, List<string>? skip = null, string? register = "SWK00001")
    {
        using var scope = ScopeFor(cafe);
        var secrets = scope.ServiceProvider.GetRequiredService<ITenantSecrets>();
        await secrets.SetAsync(cafe.Id, FiscalProviders.LoginKey, "cashier@cafe.kz");
        await secrets.SetAsync(cafe.Id, FiscalProviders.PasswordKey, "pw");
        var saved = await Settings(scope).SaveAsync(new SaveFiscalSettingsDto(
            "webkassa", skip, null, [new FiscalBranchInputDto(cafe.LocationId, register)]));
        Assert.True(saved.Succeeded, saved.Error);
        Assert.True(saved.Data!.CredentialsSet);
    }

    private async Task<Guid> AddOrderAsync(Cafe cafe, OrderStatus status = OrderStatus.Accepted,
        PaymentMethod method = PaymentMethod.Card, decimal discount = 0m, decimal points = 0m, DateTime? createdAt = null)
    {
        using var scope = ScopeFor(cafe);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = new Order
        {
            CustomerUserId = cafe.CustomerId, LocationId = cafe.LocationId, Status = status,
            PaymentStatus = PaymentStatus.Paid, PaymentMethod = method,
            Subtotal = 3000m, DiscountAmount = discount, Total = 3000m - discount, LoyaltyPointsSpent = points == 0 ? null : points,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
        order.Items.Add(new OrderItem { MenuItemId = cafe.MenuItemId, MenuItemName = "Latte", Quantity = 2, UnitPrice = 1000m, LineTotal = 2000m });
        order.Items.Add(new OrderItem { MenuItemId = cafe.MenuItemId, MenuItemName = "Latte", VariantLabel = "L", Quantity = 1, UnitPrice = 1000m, LineTotal = 1000m });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    private async Task<List<FiscalReceipt>> ReceiptsAsync(Cafe cafe, Guid orderId)
    {
        using var scope = ScopeFor(cafe);
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().FiscalReceipts.AsNoTracking()
            .Where(r => r.OrderId == orderId).OrderBy(r => r.CreatedAt).ToListAsync();
    }

    private async Task RunAsync(Cafe cafe, FakeRegistrar registrar)
    {
        using var scope = ScopeFor(cafe);
        await Fiscal(scope, registrar).RunAsync();
    }

    [Fact]
    public async Task Off_ByDefault_NothingIsQueued()
    {
        var cafe = await NewCafeAsync();
        var orderId = await AddOrderAsync(cafe);
        var registrar = new FakeRegistrar();

        await RunAsync(cafe, registrar);

        Assert.Empty(await ReceiptsAsync(cafe, orderId));
        Assert.Empty(registrar.Sent);
    }

    [Fact]
    public async Task PaidOrder_GetsOneReceipt_WithDiscountAndPointsSpreadOverTheLines()
    {
        var cafe = await NewCafeAsync();
        var before = await AddOrderAsync(cafe, createdAt: DateTime.UtcNow.AddHours(-1)); // sold before receipts were on
        await TurnOnAsync(cafe);
        var orderId = await AddOrderAsync(cafe, discount: 300m, points: 200m);
        var waiting = await AddOrderAsync(cafe, status: OrderStatus.Created); // may still be declined
        var registrar = new FakeRegistrar();

        await RunAsync(cafe, registrar);
        await RunAsync(cafe, registrar); // a second run sends nothing twice

        var sale = Assert.Single(await ReceiptsAsync(cafe, orderId));
        Assert.Equal(FiscalReceiptStatus.Sent, sale.Status);
        Assert.Equal(2500m, sale.Amount);
        Assert.Equal("CHK1", sale.CheckNumber);
        Assert.Equal("SWK00001", sale.RegisterNumber);
        var sent = Assert.Single(registrar.Sent);
        Assert.Equal(FiscalReceiptKind.Sale, sent.Kind);
        Assert.Equal($"{orderId:N}-s", sent.ExternalId);
        Assert.Equal(2500m, sent.Lines.Sum(l => l.UnitPrice * l.Quantity - l.Discount));
        Assert.Contains(sent.Lines, l => l.Name == "Latte (L)");
        Assert.Equal(new FiscalPayment(PaymentMethod.Card, 2500m), Assert.Single(sent.Payments));

        Assert.Empty(await ReceiptsAsync(cafe, before));
        Assert.Empty(await ReceiptsAsync(cafe, waiting));
    }

    [Fact]
    public async Task SentReceipt_LinkShowsForTheGuest_AndAsQrOnThePrintedReceipt()
    {
        var cafe = await NewCafeAsync();
        await TurnOnAsync(cafe);
        var orderId = await AddOrderAsync(cafe);
        Guid printerId;
        using (var scope = ScopeFor(cafe))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var printer = new Printer { LocationId = cafe.LocationId, Name = "Till", Kind = Printer.ReceiptKind, Host = "10.0.0.9" };
            db.Printers.Add(printer);
            await db.SaveChangesAsync();
            printerId = printer.Id;
        }

        // Not sent yet: nothing changes for the guest or on paper.
        Assert.Null((await GuestViewAsync(cafe, orderId)).FiscalReceiptUrl);
        Assert.False(HasQr(await PrintReceiptAsync(cafe, orderId, printerId)));

        await RunAsync(cafe, new FakeRegistrar());

        var dto = await GuestViewAsync(cafe, orderId);
        Assert.Equal("https://ofd.example/t/1", dto.FiscalReceiptUrl);
        Assert.Null(dto.FiscalReturnUrl);
        var printed = await PrintReceiptAsync(cafe, orderId, printerId);
        Assert.True(HasQr(printed));
        Assert.Contains("https://ofd.example/t/1", System.Text.Encoding.UTF8.GetString(printed));

        // A link that is not a web address is never shown or printed.
        using (var scope = ScopeFor(cafe))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.FiscalReceipts.Where(r => r.OrderId == orderId)
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.TicketUrl, "javascript:alert(1)"));
        }
        Assert.Null((await GuestViewAsync(cafe, orderId)).FiscalReceiptUrl);
        Assert.False(HasQr(await PrintReceiptAsync(cafe, orderId, printerId)));
    }

    [Fact]
    public async Task SentReceipt_PrintsAgainWithQr_OnlyOnPrintersWithTheTick_AndOnlyWhileFresh()
    {
        var cafe = await NewCafeAsync();
        await TurnOnAsync(cafe);
        Guid reprinter, plain;
        using (var scope = ScopeFor(cafe))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Behind a print agent, so the jobs stay in the list instead of the server trying to reach these addresses.
            var a = new Printer { LocationId = cafe.LocationId, Name = "Till", Kind = Printer.ReceiptKind, Host = "10.0.0.9", ViaAgent = true, ReprintWithTaxQr = true };
            var b = new Printer { LocationId = cafe.LocationId, Name = "Bar", Kind = Printer.ReceiptKind, Host = "10.0.0.10", ViaAgent = true };
            db.Printers.AddRange(a, b);
            await db.SaveChangesAsync();
            (reprinter, plain) = (a.Id, b.Id);
        }

        var fresh = await AddOrderAsync(cafe);
        await RunAsync(cafe, new FakeRegistrar());

        var jobs = await JobsAsync(cafe, fresh);
        var job = Assert.Single(jobs);
        Assert.Equal(reprinter, job.PrinterId);
        Assert.Equal(PrintJob.ReceiptKind, job.Kind);
        Assert.True(HasQr(job.Data));

        // The browser receipt gets the same link as a QR picture.
        using (var scope = ScopeFor(cafe))
        {
            var list = (await Fiscal(scope, new FakeRegistrar()).ListForOrderAsync(fresh)).Data!;
            Assert.StartsWith("data:image/svg+xml;base64,", Assert.Single(list).TicketQr);
        }

        // A receipt that only went through long after the order (service was down) does not print by itself.
        var late = await AddOrderAsync(cafe);
        var registrar = new FakeRegistrar { Fail = true };
        await RunAsync(cafe, registrar);
        using (var scope = ScopeFor(cafe))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.FiscalReceipts.Where(r => r.OrderId == late).ExecuteUpdateAsync(x => x
                .SetProperty(r => r.CreatedAt, DateTime.UtcNow.AddHours(-1)).SetProperty(r => r.NextAttemptAt, DateTime.UtcNow.AddMinutes(-1)));
        }
        registrar.Fail = false;
        await RunAsync(cafe, registrar);
        Assert.Equal(FiscalReceiptStatus.Sent, Assert.Single(await ReceiptsAsync(cafe, late)).Status);
        Assert.Empty(await JobsAsync(cafe, late));
        Assert.DoesNotContain(await JobsAsync(cafe, fresh), j => j.PrinterId == plain);
    }

    private async Task<List<PrintJob>> JobsAsync(Cafe cafe, Guid orderId)
    {
        using var scope = ScopeFor(cafe);
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PrintJobs.AsNoTracking()
            .Where(j => j.OrderId == orderId).ToListAsync();
    }

    private async Task<Yurt.Application.Features.Orders.DTOs.OrderDto> GuestViewAsync(Cafe cafe, Guid orderId)
    {
        using var scope = ScopeFor(cafe);
        var result = await scope.ServiceProvider.GetRequiredService<OrderService>().GetOrderAsync(orderId, cafe.CustomerId);
        Assert.True(result.Succeeded, result.Error);
        return result.Data!;
    }

    private async Task<byte[]> PrintReceiptAsync(Cafe cafe, Guid orderId, Guid printerId)
    {
        using var scope = ScopeFor(cafe);
        var queued = await scope.ServiceProvider.GetRequiredService<PrintService>().PrintReceiptAsync(orderId);
        Assert.Equal(1, queued.Data!.Printers);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.PrintJobs.AsNoTracking().Where(j => j.PrinterId == printerId)
            .OrderByDescending(j => j.CreatedAt).Select(j => j.Data).FirstAsync();
    }

    /// <summary>The "print the stored QR code" command.</summary>
    private static bool HasQr(byte[] bytes)
    {
        byte[] print = [0x1D, (byte)'(', (byte)'k', 3, 0, 49, 81, 48];
        for (var i = 0; i + print.Length <= bytes.Length; i++)
            if (bytes.AsSpan(i, print.Length).SequenceEqual(print)) return true;
        return false;
    }

    [Fact]
    public async Task SkippedMethod_AndPointsOnly_AreSkipped_NotSent()
    {
        var cafe = await NewCafeAsync();
        await TurnOnAsync(cafe, skip: ["KaspiBank"]);
        var kaspi = await AddOrderAsync(cafe, method: PaymentMethod.KaspiBank);
        var points = await AddOrderAsync(cafe, points: 3000m);
        var registrar = new FakeRegistrar();

        await RunAsync(cafe, registrar);

        Assert.Equal(FiscalReceiptStatus.Skipped, Assert.Single(await ReceiptsAsync(cafe, kaspi)).Status);
        Assert.Equal(FiscalReceiptStatus.Skipped, Assert.Single(await ReceiptsAsync(cafe, points)).Status);
        Assert.Empty(registrar.Sent);
    }

    [Fact]
    public async Task ProviderDown_ReceiptWaits_ShiftCloseAsks_ThenRetrySends()
    {
        var cafe = await NewCafeAsync();
        await TurnOnAsync(cafe);
        var orderId = await AddOrderAsync(cafe);
        var registrar = new FakeRegistrar { Fail = true };

        await RunAsync(cafe, registrar);

        var row = Assert.Single(await ReceiptsAsync(cafe, orderId));
        Assert.Equal(FiscalReceiptStatus.Pending, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.Equal("Webkassa is down", row.LastError);
        Assert.True(row.NextAttemptAt > DateTime.UtcNow);

        using (var scope = ScopeFor(cafe))
        {
            var fiscal = Fiscal(scope, registrar);
            var waiting = await fiscal.ListWaitingAsync(cafe.LocationId);
            Assert.Equal(orderId, Assert.Single(waiting.Data!).OrderId);

            // The cashier cannot close the shift by accident while a receipt waits.
            var sp = scope.ServiceProvider;
            var shifts = new ShiftService(sp.GetRequiredService<ApplicationDbContext>(), sp.GetRequiredService<ICurrentUserService>(),
                sp.GetRequiredService<IAuditLogService>(), fiscal);
            var shift = (await shifts.OpenAsync(new OpenShiftDto(cafe.LocationId, 0m))).Data!;
            Assert.Equal(409, (await shifts.CloseAsync(shift.Id, new CloseShiftDto(0m, null))).StatusCode);

            // Closing anyway works; the fiscal Z report failed too and is saved on the shift.
            var closed = await shifts.CloseAsync(shift.Id, new CloseShiftDto(0m, null, CloseWithWaitingReceipts: true));
            Assert.True(closed.Succeeded, closed.Error);
            Assert.Null(closed.Data!.Shift.FiscalZReportAt);
            Assert.Equal("Webkassa is down", closed.Data.Shift.FiscalZReportError);

            registrar.Fail = false;
            var retried = await fiscal.RetryAsync(row.Id);
            Assert.Equal("Sent", retried.Data!.Status);
            var z = await shifts.RetryFiscalZReportAsync(shift.Id);
            Assert.NotNull(z.Data!.FiscalZReportAt);
            Assert.Null(z.Data.FiscalZReportError);
            Assert.Equal(["SWK00001"], registrar.ShiftsClosed);
        }
    }

    [Fact]
    public async Task BranchWithoutRegister_Waits_WithAPlainReason()
    {
        var cafe = await NewCafeAsync();
        await TurnOnAsync(cafe, register: null);
        var orderId = await AddOrderAsync(cafe);
        var registrar = new FakeRegistrar();

        await RunAsync(cafe, registrar);

        var row = Assert.Single(await ReceiptsAsync(cafe, orderId));
        Assert.Equal(FiscalReceiptStatus.Pending, row.Status);
        Assert.Contains("cash register number", row.LastError);
        Assert.Empty(registrar.Sent);
    }

    [Fact]
    public async Task Refund_AfterTheSaleReceipt_SendsAReturnReceipt()
    {
        var cafe = await NewCafeAsync();
        await TurnOnAsync(cafe);
        var orderId = await AddOrderAsync(cafe, status: OrderStatus.Completed, method: PaymentMethod.Cash);
        var registrar = new FakeRegistrar();
        await RunAsync(cafe, registrar);

        using (var scope = ScopeFor(cafe))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Orders.Where(o => o.Id == orderId).ExecuteUpdateAsync(o => o.SetProperty(x => x.PaymentStatus, PaymentStatus.Refunded));
        }
        await RunAsync(cafe, registrar);
        await RunAsync(cafe, registrar);

        var rows = await ReceiptsAsync(cafe, orderId);
        Assert.Equal([FiscalReceiptKind.Sale, FiscalReceiptKind.Return], rows.Select(r => r.Kind));
        Assert.All(rows, r => Assert.Equal(FiscalReceiptStatus.Sent, r.Status));
        Assert.Equal(2, registrar.Sent.Count);
        Assert.Equal(FiscalReceiptKind.Return, registrar.Sent[1].Kind);
        Assert.Equal($"{orderId:N}-r", registrar.Sent[1].ExternalId);
    }

    [Fact]
    public async Task Settings_RejectUnknownProviderAndMethod_AndOffClearsNothingElse()
    {
        var cafe = await NewCafeAsync();
        using var scope = ScopeFor(cafe);
        var settings = Settings(scope);

        Assert.Equal(400, (await settings.SaveAsync(new SaveFiscalSettingsDto("acme", null, null, null))).StatusCode);
        Assert.Equal(400, (await settings.SaveAsync(new SaveFiscalSettingsDto("webkassa", ["Bitcoin"], null, null))).StatusCode);
        Assert.Equal(400, (await settings.SaveAsync(new SaveFiscalSettingsDto("webkassa", null, 120, null))).StatusCode);

        var on = await settings.SaveAsync(new SaveFiscalSettingsDto("webkassa", ["kaspibank"], 16,
            [new FiscalBranchInputDto(cafe.LocationId, " SWK7 ")]));
        Assert.Equal("webkassa", on.Data!.Provider);
        Assert.NotNull(on.Data.Since);
        Assert.Equal(["KaspiBank"], on.Data.SkipMethods);
        Assert.Equal("SWK7", Assert.Single(on.Data.Branches).RegisterNumber);
        Assert.False(on.Data.CredentialsSet);

        var off = await settings.SaveAsync(new SaveFiscalSettingsDto("none", null, null, null));
        Assert.Null(off.Data!.Provider);
        Assert.Equal("SWK7", Assert.Single(off.Data.Branches).RegisterNumber); // branches left out = unchanged
    }
}

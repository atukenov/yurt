using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Printing;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Fiscal;

/// <summary>
/// Official tax receipts (Phase 6, <c>own_pos</c>). Works as a queue so a sale never waits for the cash register:
/// the background job finds paid orders without a receipt, queues one, and sends it through the cafe's provider,
/// trying again with growing pauses until it goes through. A refunded order whose sale receipt was sent gets a return
/// receipt the same way. Off unless the cafe has a provider set and <c>own_pos</c> is on.
/// </summary>
public class FiscalReceiptService(
    IApplicationDbContext db,
    IModuleGate modules,
    ITenantContext tenant,
    ITenantSecrets secrets,
    IEnumerable<IFiscalRegistrar> registrars,
    PrintService print,
    IQrCodeImages qr,
    ILogger<FiscalReceiptService> logger)
{
    private const int BatchSize = 50;
    private static readonly TimeSpan MaxPause = TimeSpan.FromMinutes(30);

    /// <summary>The current cafe when fiscal receipts are on for it, else null.</summary>
    public async Task<Tenant?> ActiveCafeAsync(CancellationToken ct = default)
    {
        var cafe = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenant.TenantId, ct);
        if (cafe?.FiscalProvider is null) return null;
        return await modules.IsEnabledAsync(ModuleKeys.OwnPos, ct) ? cafe : null;
    }

    /// <summary>One run of the background job for the current cafe: queue new receipts, then send the ones due.</summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        var cafe = await ActiveCafeAsync(ct);
        if (cafe is null) return;
        await QueueAsync(cafe, ct);
        await SendDueAsync(cafe, ct);
    }

    internal async Task QueueAsync(Tenant cafe, CancellationToken ct)
    {
        var since = cafe.FiscalSince ?? DateTime.MinValue;
        var skip = ParseMethods(cafe.FiscalSkipMethods);

        var sales = await db.Orders
            .Where(o => o.CreatedAt >= since
                && (o.PaymentStatus == PaymentStatus.Paid || o.PaymentStatus == PaymentStatus.Refunded)
                // A sale once the cafe took the order on. Orders still waiting (Created) can be declined, so they
                // get no receipt yet; declined orders never get one.
                && o.Status != OrderStatus.Created && o.Status != OrderStatus.Declined
                && !db.FiscalReceipts.Any(r => r.OrderId == o.Id && r.Kind == FiscalReceiptKind.Sale))
            .OrderBy(o => o.CreatedAt).Take(BatchSize * 4)
            .ToListAsync(ct);
        if (sales.Count > 0)
        {
            var ids = sales.Select(o => o.Id).ToList();
            var splits = await SplitsAsync(ids, ct);
            foreach (var order in sales)
            {
                var payments = Payments(order, splits.GetValueOrDefault(order.Id));
                var row = new FiscalReceipt
                {
                    OrderId = order.Id, LocationId = order.LocationId, Kind = FiscalReceiptKind.Sale,
                    Amount = ToPay(order), Provider = cafe.FiscalProvider!,
                };
                if (row.Amount <= 0) Skip(row, "Paid fully with bonus points; no money was taken.");
                else if (payments.All(p => skip.Contains(p.Method)))
                    Skip(row, $"Paid with {string.Join(", ", payments.Select(p => p.Method).Distinct())}, which this cafe skips (its own receipt comes from elsewhere).");
                db.FiscalReceipts.Add(row);
            }
        }

        // A refund after the sale receipt went out needs a return receipt for the same lines and payments.
        var refunded = await db.FiscalReceipts
            .Where(r => r.Kind == FiscalReceiptKind.Sale && r.Status == FiscalReceiptStatus.Sent
                && r.Order.PaymentStatus == PaymentStatus.Refunded
                && !db.FiscalReceipts.Any(x => x.OrderId == r.OrderId && x.Kind == FiscalReceiptKind.Return))
            .Take(BatchSize)
            .ToListAsync(ct);
        foreach (var sale in refunded)
            db.FiscalReceipts.Add(new FiscalReceipt
            {
                OrderId = sale.OrderId, LocationId = sale.LocationId, Kind = FiscalReceiptKind.Return,
                Amount = sale.Amount, Provider = sale.Provider,
            });

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex)
        {
            // Another server queued the same order at the same moment (unique order + kind); the next run picks up the rest.
            logger.LogInformation(ex, "Fiscal queue: receipt already queued by another run");
        }
    }

    private async Task SendDueAsync(Tenant cafe, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var due = await db.FiscalReceipts
            .Where(r => r.Status == FiscalReceiptStatus.Pending && r.NextAttemptAt <= now)
            .OrderBy(r => r.CreatedAt).Take(BatchSize)
            .ToListAsync(ct);
        if (due.Count == 0) return;

        var context = await SendContextAsync(cafe, ct);
        foreach (var row in due)
        {
            ct.ThrowIfCancellationRequested();
            await SendOneAsync(row, cafe, context, ct);
        }
    }

    private sealed record SendContext(IFiscalRegistrar? Registrar, FiscalCredentials? Credentials);

    private async Task<SendContext> SendContextAsync(Tenant cafe, CancellationToken ct)
    {
        var registrar = registrars.FirstOrDefault(r => r.Provider == cafe.FiscalProvider);
        var login = await secrets.GetAsync(FiscalProviders.LoginKey, ct);
        var password = await secrets.GetAsync(FiscalProviders.PasswordKey, ct);
        var creds = string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password)
            ? null
            : new FiscalCredentials(login, password, await secrets.GetAsync(FiscalProviders.ApiKeyKey, ct));
        return new SendContext(registrar, creds);
    }

    private async Task SendOneAsync(FiscalReceipt row, Tenant cafe, SendContext context, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Items).Include(o => o.Location)
            .FirstOrDefaultAsync(o => o.Id == row.OrderId, ct);
        var register = order?.Location?.FiscalRegisterNumber?.Trim();

        FiscalResult result;
        if (order is null) result = FiscalResult.Fail("The order is missing.");
        else if (context.Registrar is null) result = FiscalResult.Fail($"Unknown fiscal provider '{cafe.FiscalProvider}'.");
        else if (context.Credentials is null) result = FiscalResult.Fail("The cafe's fiscal login and password are not set (Platform page, secrets).");
        else if (string.IsNullOrEmpty(register)) result = FiscalResult.Fail("This branch has no cash register number yet (Platform page, fiscal receipts).");
        else
        {
            var splits = await SplitsAsync([order.Id], ct);
            var request = BuildRequest(order, splits.GetValueOrDefault(order.Id), row.Kind, register, cafe.FiscalVatPercent,
                $"{order.Id:N}-{(row.Kind == FiscalReceiptKind.Sale ? "s" : "r")}");
            try { result = await context.Registrar.SendReceiptAsync(context.Credentials, request, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Fiscal provider call failed for order {OrderId}", row.OrderId);
                result = FiscalResult.Fail("The cash register service did not answer.");
            }
            row.RegisterNumber = register;
        }

        row.Attempts++;
        row.UpdatedAt = DateTime.UtcNow;
        if (result.Succeeded)
        {
            row.Status = FiscalReceiptStatus.Sent;
            row.SentAt = DateTime.UtcNow;
            row.CheckNumber = Cut(result.CheckNumber, 100);
            row.TicketUrl = Cut(result.TicketUrl, 1000);
            row.LastError = null;
        }
        else
        {
            row.LastError = Cut(result.Error, 500) ?? "Unknown error.";
            row.NextAttemptAt = DateTime.UtcNow + Pause(row.Attempts);
            logger.LogWarning("Fiscal receipt for order {OrderId} not sent (try {Attempt}): {Error}", row.OrderId, row.Attempts, row.LastError);
        }
        await db.SaveChangesAsync(ct);

        if (ShouldReprint(row, DateTime.UtcNow)) await print.ReprintWithTaxQrAsync(row.OrderId, ct);
    }

    /// <summary>
    /// A sale receipt that just went through, with a link to put in the QR code, for a guest who is most likely still
    /// there: queued at most <see cref="PrintService.MaxAge"/> ago. A receipt sent much later (the cash register service
    /// was down for a while) is not printed again by itself; staff can still press Print receipt.
    /// </summary>
    public static bool ShouldReprint(FiscalReceipt row, DateTime now)
        => row is { Kind: FiscalReceiptKind.Sale, Status: FiscalReceiptStatus.Sent }
            && FiscalLinks.Safe(row.TicketUrl) is not null
            && row.CreatedAt >= now - PrintService.MaxAge;

    /// <summary>1, 2, 4, 8 ... minutes between tries, at most 30. Never gives up: every sale needs its receipt.</summary>
    public static TimeSpan Pause(int attempts)
    {
        var minutes = Math.Pow(2, Math.Clamp(attempts - 1, 0, 10));
        return TimeSpan.FromMinutes(Math.Min(minutes, MaxPause.TotalMinutes));
    }

    /// <summary>Receipts not sent yet (the "not fiscalized" list), oldest first. No branch means all branches.</summary>
    public async Task<Result<List<FiscalReceiptDto>>> ListWaitingAsync(Guid? locationId, CancellationToken ct = default)
    {
        var rows = await db.FiscalReceipts.AsNoTracking()
            .Where(r => r.Status == FiscalReceiptStatus.Pending && (locationId == null || r.LocationId == locationId))
            .OrderBy(r => r.CreatedAt).Take(200).ToListAsync(ct);
        return Result<List<FiscalReceiptDto>>.Success(rows.Select(ToDto).ToList());
    }

    public async Task<Result<List<FiscalReceiptDto>>> ListForOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var rows = await db.FiscalReceipts.AsNoTracking().Where(r => r.OrderId == orderId)
            .OrderBy(r => r.CreatedAt).ToListAsync(ct);
        return Result<List<FiscalReceiptDto>>.Success(rows.Select(r => ToDto(r) with
        {
            TicketQr = r.Status == FiscalReceiptStatus.Sent && FiscalLinks.Safe(r.TicketUrl) is { } url ? qr.SvgDataUri(url) : null,
        }).ToList());
    }

    /// <summary>Staff pressed "Try again": send this receipt now instead of waiting for the next pause to end.</summary>
    public async Task<Result<FiscalReceiptDto>> RetryAsync(Guid receiptId, CancellationToken ct = default)
    {
        var row = await db.FiscalReceipts.FirstOrDefaultAsync(r => r.Id == receiptId, ct);
        if (row is null) return Result<FiscalReceiptDto>.Failure("Receipt not found.", 404);
        if (row.Status != FiscalReceiptStatus.Pending) return Result<FiscalReceiptDto>.Success(ToDto(row));
        var cafe = await ActiveCafeAsync(ct);
        if (cafe is null) return Result<FiscalReceiptDto>.Failure("Fiscal receipts are off for this cafe.", 409);

        await SendOneAsync(row, cafe, await SendContextAsync(cafe, ct), ct);
        return Result<FiscalReceiptDto>.Success(ToDto(row));
    }

    /// <summary>How many receipts of a branch are still waiting. Zero while fiscal receipts are off.</summary>
    public async Task<int> WaitingCountAsync(Guid locationId, CancellationToken ct = default)
        => await ActiveCafeAsync(ct) is null
            ? 0
            : await db.FiscalReceipts.CountAsync(r => r.LocationId == locationId && r.Status == FiscalReceiptStatus.Pending, ct);

    /// <summary>
    /// Closes the fiscal shift of the branch's register (fiscal Z report). Null result = nothing to do (fiscal receipts
    /// off). Never throws for provider errors; the error text comes back for the cashier.
    /// </summary>
    public async Task<FiscalResult?> CloseShiftAsync(Guid locationId, CancellationToken ct = default)
    {
        var cafe = await ActiveCafeAsync(ct);
        if (cafe is null) return null;
        var context = await SendContextAsync(cafe, ct);
        var register = (await db.Locations.AsNoTracking().Where(l => l.Id == locationId)
            .Select(l => l.FiscalRegisterNumber).FirstOrDefaultAsync(ct))?.Trim();

        if (context.Registrar is null) return FiscalResult.Fail($"Unknown fiscal provider '{cafe.FiscalProvider}'.");
        if (context.Credentials is null) return FiscalResult.Fail("The cafe's fiscal login and password are not set.");
        if (string.IsNullOrEmpty(register)) return FiscalResult.Fail("This branch has no cash register number yet.");
        try { return await context.Registrar.CloseShiftAsync(context.Credentials, register, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fiscal Z report failed for branch {LocationId}", locationId);
            return FiscalResult.Fail("The cash register service did not answer.");
        }
    }

    /// <summary>
    /// The receipt for an order: one line per order line, the order discount and bonus points spread over the lines
    /// (so the receipt total is what the guest paid in money), and the payments as the cashier recorded them.
    /// </summary>
    public static FiscalReceiptRequest BuildRequest(
        Order order, IReadOnlyList<OrderPayment>? splits, FiscalReceiptKind kind, string register, int? vatPercent, string externalId)
    {
        var toPay = ToPay(order);
        var items = order.Items.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id).ToList();
        var gross = items.Sum(i => i.LineTotal);
        var off = Math.Max(0m, gross - toPay);

        var lines = new List<FiscalLine>();
        var given = 0m;
        for (var n = 0; n < items.Count; n++)
        {
            var i = items[n];
            // Spread by line value; the last line takes what rounding left over so the total is exact.
            var discount = n == items.Count - 1
                ? off - given
                : gross == 0 ? 0m : Math.Round(off * i.LineTotal / gross, 2, MidpointRounding.AwayFromZero);
            discount = Math.Min(discount, i.LineTotal);
            given += discount;
            var name = string.IsNullOrWhiteSpace(i.VariantLabel) ? i.MenuItemName : $"{i.MenuItemName} ({i.VariantLabel})";
            lines.Add(new FiscalLine(Cut(name, 200)!, i.Quantity, i.UnitPrice, discount));
        }

        return new FiscalReceiptRequest(register, kind, externalId, lines, Payments(order, splits), vatPercent);
    }

    private static List<FiscalPayment> Payments(Order order, IReadOnlyList<OrderPayment>? splits)
        => splits is { Count: > 0 }
            ? splits.Select(p => new FiscalPayment(p.Method, p.Amount)).ToList()
            : [new FiscalPayment(order.PaymentMethod ?? PaymentMethod.Cash, ToPay(order))];

    private async Task<Dictionary<Guid, List<OrderPayment>>> SplitsAsync(List<Guid> orderIds, CancellationToken ct)
        => (await db.OrderPayments.AsNoTracking().Where(p => orderIds.Contains(p.OrderId)).ToListAsync(ct))
            .GroupBy(p => p.OrderId).ToDictionary(g => g.Key, g => g.ToList());

    private static decimal ToPay(Order o) => Math.Max(0m, o.Total - (o.LoyaltyPointsSpent ?? 0m));

    private static void Skip(FiscalReceipt row, string why)
    {
        row.Status = FiscalReceiptStatus.Skipped;
        row.LastError = why;
    }

    internal static HashSet<PaymentMethod> ParseMethods(string? csv)
        => (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<PaymentMethod>(s, true, out var m) ? m : (PaymentMethod?)null)
            .OfType<PaymentMethod>().ToHashSet();

    private static string? Cut(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];

    public static FiscalReceiptDto ToDto(FiscalReceipt r) => new(
        r.Id, r.OrderId, r.LocationId, r.Kind.ToString(), r.Status.ToString(), r.Amount, r.Provider,
        r.Attempts, r.LastError, r.CreatedAt, r.NextAttemptAt, r.SentAt, r.RegisterNumber, r.CheckNumber, r.TicketUrl);
}

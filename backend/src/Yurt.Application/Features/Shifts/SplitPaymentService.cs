using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Common.Models;
using Yurt.Application.Features.Printing;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.Application.Features.Shifts;

/// <summary>
/// Split payments (Phase 6, <c>own_pos</c>): the cashier says how much of an order was paid with each method.
/// The parts must add up to what the guest pays in money (total less bonus points). Saving marks the order paid and
/// keeps the biggest part as the order's main method, so older screens and reports still show something sensible.
/// </summary>
public class SplitPaymentService(IApplicationDbContext db, IAuditLogService audit, PrintService? print = null)
{
    private const int MaxLines = 5;

    public async Task<Result<SplitPaymentDto>> GetAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return Result<SplitPaymentDto>.Failure("Order not found.", 404);
        var rows = await db.OrderPayments.Where(p => p.OrderId == orderId).OrderBy(p => p.CreatedAt).ToListAsync(ct);
        return Result<SplitPaymentDto>.Success(ToDto(order, rows));
    }

    public async Task<Result<SplitPaymentDto>> SaveAsync(Guid orderId, SplitPaymentInputDto dto, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return Result<SplitPaymentDto>.Failure("Order not found.", 404);
        if (order.Status == OrderStatus.Declined)
            return Result<SplitPaymentDto>.Failure("A declined order cannot be paid.", 409);
        if (order.PaymentStatus == PaymentStatus.Refunded)
            return Result<SplitPaymentDto>.Failure("A refunded order cannot be paid again.", 409);

        var lines = dto.Lines ?? [];
        if (lines.Count == 0 || lines.Count > MaxLines)
            return Result<SplitPaymentDto>.Failure($"Give between 1 and {MaxLines} payment parts.", 400);

        var parsed = new List<(PaymentMethod Method, decimal Amount)>();
        foreach (var l in lines)
        {
            if (!Enum.TryParse<PaymentMethod>(l.Method, true, out var method) || !Enum.IsDefined(method))
                return Result<SplitPaymentDto>.Failure($"Unknown payment method '{l.Method}'.", 400);
            var amount = Math.Round(l.Amount, 2);
            if (amount <= 0) return Result<SplitPaymentDto>.Failure("Each part must be more than zero.", 400);
            parsed.Add((method, amount));
        }
        // Two parts with the same method are one part.
        var merged = parsed.GroupBy(p => p.Method).Select(g => (Method: g.Key, Amount: g.Sum(x => x.Amount))).ToList();

        var toPay = ToPay(order);
        if (Math.Abs(merged.Sum(m => m.Amount) - toPay) > 0.01m)
            return Result<SplitPaymentDto>.Failure($"The parts must add up to {toPay:0.##}.", 400);

        var old = await db.OrderPayments.Where(p => p.OrderId == orderId).ToListAsync(ct);
        db.OrderPayments.RemoveRange(old);
        var rows = merged.Select(m => new OrderPayment { OrderId = orderId, Method = m.Method, Amount = m.Amount }).ToList();
        db.OrderPayments.AddRange(rows);

        order.PaymentStatus = PaymentStatus.Paid;
        order.PaymentMethod = merged.OrderByDescending(m => m.Amount).First().Method;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("OrderSplitPayment", "Order", orderId.ToString(),
            string.Join(", ", merged.Select(m => $"{m.Method} {m.Amount:0.##}")), ct);
        // A part paid in cash opens the cash drawer on the branch's receipt printer.
        if (print is not null && merged.Any(m => m.Method == PaymentMethod.Cash)) await print.OpenDrawerForCashAsync(order, ct);
        return Result<SplitPaymentDto>.Success(ToDto(order, rows));
    }

    /// <summary>What the guest pays in money: the total less bonus points.</summary>
    public static decimal ToPay(Order o) => Math.Max(0m, o.Total - (o.LoyaltyPointsSpent ?? 0m));

    private static SplitPaymentDto ToDto(Order order, List<OrderPayment> rows)
        => new(order.Id, ToPay(order), rows.Select(r => new SplitPaymentLineDto(r.Method.ToString(), r.Amount)).ToList());
}

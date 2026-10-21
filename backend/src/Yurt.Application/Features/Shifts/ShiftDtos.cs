namespace Yurt.Application.Features.Shifts;

public record OpenShiftDto(Guid LocationId, decimal OpeningCash);
public record CloseShiftDto(decimal CountedCash, string? Note);
public record CashMovementInputDto(decimal Amount, bool IsOut, string Reason);

public record ShiftDto(
    Guid Id, Guid LocationId, string LocationName, DateTime OpenedAt, string OpenedBy, decimal OpeningCash,
    DateTime? ClosedAt, string? ClosedBy, decimal? CountedCash, string? Note);

public record CashMovementDto(Guid Id, decimal Amount, bool IsOut, string Reason, string CreatedBy, DateTime CreatedAt);

public record PaymentMethodTotalDto(string Method, int Orders, decimal Amount);

/// <summary>X report while the shift is open, Z report once it is closed. Amounts are what the guest actually pays (total minus points).</summary>
public record ShiftReportDto(
    ShiftDto Shift,
    int OrdersCompleted,
    int OrdersDeclined,
    decimal Subtotal,
    decimal Discounts,
    decimal PointsSpent,
    decimal Takings,
    List<PaymentMethodTotalDto> ByMethod,
    decimal CashSales,
    decimal CashIn,
    decimal CashOut,
    decimal ExpectedCash,
    decimal? Difference,
    List<CashMovementDto> Movements);

public record SplitPaymentLineDto(string Method, decimal Amount);
public record SplitPaymentInputDto(List<SplitPaymentLineDto> Lines);
public record SplitPaymentDto(Guid OrderId, decimal ToPay, List<SplitPaymentLineDto> Lines);

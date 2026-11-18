namespace Yurt.Application.Features.Payroll;

/// <summary>One account of the cafe with what it is paid every month (0 = not on the payroll).</summary>
public record PayrollStaffDto(Guid Id, string Username, string Role, bool IsActive, decimal MonthlySalary, decimal HourlyRate = 0);

/// <param name="HourlyRate">Pay per worked hour from the time clock; leave out to keep the current rate.</param>
public record SetSalaryDto(decimal MonthlySalary, decimal? HourlyRate = null);

public record PayAdjustmentDto(Guid Id, bool IsDeduction, decimal Amount, string Reason, string CreatedBy, DateTime CreatedAt);

public record PayrollLineDto(
    Guid AdminUserId, string Username, string Role,
    decimal Salary, decimal Bonuses, decimal Deductions, decimal Total,
    List<PayAdjustmentDto> Adjustments,
    decimal Hours = 0, decimal HourlyRate = 0, decimal HourlyPay = 0);

public record PayrollSheetDto(
    string Month, bool Closed, DateTime? ClosedAt, string? ClosedBy,
    List<PayrollLineDto> Lines, decimal TotalSalary, decimal TotalBonuses, decimal TotalDeductions, decimal TotalToPay,
    decimal TotalHourlyPay = 0, decimal TotalHours = 0);

public record AddAdjustmentDto
{
    public Guid AdminUserId { get; init; }
    /// <summary>"yyyy-MM".</summary>
    public string Month { get; init; } = "";
    /// <summary>"bonus" or "deduction".</summary>
    public string Kind { get; init; } = "bonus";
    public decimal Amount { get; init; }
    public string Reason { get; init; } = "";
}

using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// What one staff account is paid every month (module <c>payroll</c>). A fixed amount, not worked out from hours.
/// No row means the person is not on the payroll. Only the CEO sees or changes it.
/// </summary>
public class StaffSalary : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid AdminUserId { get; set; }
    public decimal MonthlySalary { get; set; }

    /// <summary>Paid per worked hour (from the time clock), on top of the monthly salary. 0 = no hourly pay.</summary>
    public decimal HourlyRate { get; set; }
}

/// <summary>A one-off bonus or deduction (advance paid, shortage at the till...) for one person in one month.</summary>
public class PayAdjustment : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid AdminUserId { get; set; }

    /// <summary>The month it belongs to, "yyyy-MM".</summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>Always positive; <see cref="IsDeduction"/> says whether it is taken off the pay.</summary>
    public decimal Amount { get; set; }
    public bool IsDeduction { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>A month the CEO has closed after paying. The lines below keep the amounts as they were when it was closed.</summary>
public class PayrollMonth : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    /// <summary>"yyyy-MM".</summary>
    public string Month { get; set; } = string.Empty;
    public DateTime ClosedAt { get; set; } = DateTime.UtcNow;
    public string ClosedBy { get; set; } = string.Empty;

    public ICollection<PayrollLine> Lines { get; set; } = new List<PayrollLine>();
}

public class PayrollLine : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public Guid PayrollMonthId { get; set; }
    public PayrollMonth PayrollMonth { get; set; } = null!;

    public Guid AdminUserId { get; set; }
    /// <summary>Kept as text so the closed sheet still reads right after the account is renamed.</summary>
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public decimal Salary { get; set; }
    /// <summary>Hours worked that month (time clock) and the rate they were paid at, as they were when the month was closed.</summary>
    public decimal Hours { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal HourlyPay { get; set; }
    public decimal Bonuses { get; set; }
    public decimal Deductions { get; set; }
    public decimal Total { get; set; }
}

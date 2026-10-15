namespace Yurt.Application.Features.Reports;

public record CreateReportDto(string Text);

public record ReportDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    string Text,
    bool IsResolved,
    DateTime? ResolvedAt,
    DateTime CreatedAt);

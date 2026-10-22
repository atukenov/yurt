namespace Yurt.Application.Features.Fiscal;

public record FiscalReceiptDto(
    Guid Id, Guid OrderId, Guid LocationId, string Kind, string Status, decimal Amount, string Provider,
    int Attempts, string? LastError, DateTime CreatedAt, DateTime NextAttemptAt,
    DateTime? SentAt, string? RegisterNumber, string? CheckNumber, string? TicketUrl,
    // The link as a QR code picture (data: link), for the receipt printed from the browser. Only on a sent receipt with a web link.
    string? TicketQr = null);

public record FiscalBranchDto(Guid LocationId, string Name, string? RegisterNumber);

/// <param name="Provider">"webkassa", or null when fiscal receipts are off.</param>
/// <param name="CredentialsSet">Whether the cafe's Fiscal:Login and Fiscal:Password secrets are stored.</param>
public record FiscalSettingsDto(
    string? Provider, DateTime? Since, List<string> SkipMethods, int? VatPercent,
    List<FiscalBranchDto> Branches, List<string> Providers, bool CredentialsSet);

public record FiscalBranchInputDto(Guid LocationId, string? RegisterNumber);

/// <param name="Branches">Leave out to keep the register numbers as they are.</param>
public record SaveFiscalSettingsDto(
    string? Provider, List<string>? SkipMethods, int? VatPercent, List<FiscalBranchInputDto>? Branches);

public record FiscalZReportDto(Guid ShiftId, DateTime? FiscalZReportAt, string? FiscalZReportError);

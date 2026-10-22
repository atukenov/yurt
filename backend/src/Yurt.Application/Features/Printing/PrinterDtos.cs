namespace Yurt.Application.Features.Printing;

public record PrinterDto(
    Guid Id, Guid LocationId, string Name, string Kind, Guid? StationId, string Host, int Port,
    int LineWidth, int CodeTable, string Language, bool AutoPrint, bool ViaAgent, bool CashDrawer,
    bool ReprintWithTaxQr = false, bool ViaBluetooth = false);

public record SavePrinterDto(
    Guid LocationId, string Name, string Kind, Guid? StationId, string? Host, int Port = 9100,
    int LineWidth = 48, int CodeTable = 17, string Language = "ru", bool AutoPrint = true, bool ViaAgent = false,
    bool CashDrawer = false, bool ReprintWithTaxQr = false, bool ViaBluetooth = false);

public record PrintJobDto(
    Guid Id, Guid PrinterId, string PrinterName, Guid? OrderId, string Kind, string Status, int Attempts,
    string? LastError, DateTime CreatedAt, DateTime? PrintedAt);

/// <summary>How many printers got the job. 0 means the branch has no printer for it, so the screen prints in the browser.</summary>
public record PrintQueuedDto(int Printers);

/// <summary>Printed is true when the page went out; false when it waits for the print agent or the Bluetooth tablet to pick it up.</summary>
public record PrintTestDto(bool Printed);

/// <summary>The print agent of one branch: whether it has a key and when it last asked for jobs.</summary>
public record PrintAgentDto(Guid LocationId, bool HasKey, DateTime? LastSeenAt);

/// <summary>A new agent key. It is shown once; only its hash is kept.</summary>
public record PrintAgentKeyDto(Guid LocationId, string Key);

/// <summary>A job handed to a print agent: where to send it and the bytes (base64 in JSON).</summary>
public record AgentJobDto(Guid Id, string Host, int Port, byte[] Data);

public record AgentJobResultDto(bool Ok, string? Error = null);

/// <summary>The Bluetooth printers a tablet is connected to right now; it only gets jobs for these.</summary>
public record BluetoothClaimDto(List<Guid> PrinterIds);

/// <summary>A job handed to a tablet for one of its Bluetooth printers: the bytes (base64 in JSON).</summary>
public record BluetoothJobDto(Guid Id, Guid PrinterId, string Kind, byte[] Data);

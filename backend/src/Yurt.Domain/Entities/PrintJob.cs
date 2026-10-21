using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// One ticket or receipt waiting for, or sent to, a <see cref="Printer"/> (Phase 6, <c>own_pos</c>). The bytes are made
/// when the job is queued, so a reprint later prints exactly the same thing.
/// </summary>
public class PrintJob : BaseEntity, ITenantOwned
{
    public const string Pending = "Pending", Sending = "Sending", Printed = "Printed", Failed = "Failed";
    public const string TicketKind = "Ticket", ReceiptKind = "Receipt", TestKind = "Test", DrawerKind = "Drawer";

    public Guid TenantId { get; set; }

    public Guid PrinterId { get; set; }
    public Printer Printer { get; set; } = null!;

    public Guid? OrderId { get; set; }

    public string Kind { get; set; } = TicketKind;
    public byte[] Data { get; set; } = [];

    public string Status { get; set; } = Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime? PrintedAt { get; set; }
}

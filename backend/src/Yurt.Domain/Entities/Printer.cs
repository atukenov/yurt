using Yurt.Domain.Common;

namespace Yurt.Domain.Entities;

/// <summary>
/// A thermal printer at one branch that gets ESC/POS bytes over the network or over Bluetooth (Phase 6, <c>own_pos</c>).
/// A kitchen printer prints the items of one station (or all items when no station is set); a receipt printer prints
/// the customer receipt.
/// </summary>
public class Printer : BaseEntity, ITenantOwned
{
    public const string KitchenKind = "Kitchen", ReceiptKind = "Receipt";

    public Guid TenantId { get; set; }

    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary><see cref="KitchenKind"/> or <see cref="ReceiptKind"/>.</summary>
    public string Kind { get; set; } = KitchenKind;

    /// <summary>Kitchen printers only: the station whose items this printer gets. Null = every item.</summary>
    public Guid? StationId { get; set; }

    /// <summary>Network address of the printer, such as 192.168.1.50. Empty for a Bluetooth printer.</summary>
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 9100;

    /// <summary>Characters per line: 32 for 58 mm paper, 48 for 80 mm paper.</summary>
    public int LineWidth { get; set; } = 48;

    /// <summary>The ESC/POS character table number the printer uses for Cyrillic (code page 866). 17 on most printers.</summary>
    public int CodeTable { get; set; } = 17;

    /// <summary>Language of the printed words (ru, kk or en).</summary>
    public string Language { get; set; } = "ru";

    /// <summary>Print by itself when an order is accepted. Off = only when staff press Print.</summary>
    public bool AutoPrint { get; set; } = true;

    /// <summary>
    /// Sent through the branch's <see cref="PrintAgent"/> instead of by the server itself. Needed when the server runs in
    /// the cloud and the printer is on the cafe's own network.
    /// </summary>
    public bool ViaAgent { get; set; }

    /// <summary>
    /// Printed by a tablet in the cafe that is paired with the printer over Bluetooth: the admin app on that tablet fetches
    /// the jobs and sends them. Such a printer has no network address.
    /// </summary>
    public bool ViaBluetooth { get; set; }

    /// <summary>
    /// Receipt printers only: a cash drawer is plugged into this printer. It opens when a cash payment is taken and when
    /// the cashier presses Open drawer.
    /// </summary>
    public bool CashDrawer { get; set; }

    /// <summary>
    /// Receipt printers only: print the receipt once more, now with the QR code of the official tax receipt, as soon as
    /// that tax receipt is sent. The first receipt usually prints before it is sent, so it has no QR code. Off by default.
    /// </summary>
    public bool ReprintWithTaxQr { get; set; }
}

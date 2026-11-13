using Yurt.Domain.Common;
using Yurt.Domain.Enums;

namespace Yurt.Domain.Entities;

public class Order : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }

    // The guest's account. Null for a counter sale to a walk-in guest who has no account (Yurt POS).
    public Guid? CustomerUserId { get; set; }

    // Where the order came from: the guest's app (default) or a cashier at the counter.
    public OrderSource Source { get; set; } = OrderSource.App;
    // The staff member who rang up a counter sale. Null for app orders.
    public Guid? SoldByAdminId { get; set; }
    public Guid LocationId { get; set; }

    // Here or to go. Shown on the kitchen screen, tickets, cup labels and the order-ready screen.
    public OrderType OrderType { get; set; } = OrderType.TakeAway;

    // The table the guest ordered to by scanning its QR code (table_ordering). Null = pick up at the counter. The label is
    // kept on the order so it still reads right after the table is renamed or deleted.
    public Guid? DiningTableId { get; set; }
    public string? TableLabel { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Created;
    public string? DeclineReason { get; set; }
    public int? EtaMinutes { get; set; }
    // When the guest asked to pick the order up (UTC). Null = as soon as possible.
    public DateTime? PickupAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public PaymentMethod? PaymentMethod { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public decimal Total { get; set; }

    // A tip the guest left on top of the bill (own_pos). It is NOT part of Total: sales, profit, tax receipts and online
    // payments stay as they were. It belongs to the staff member who served (TipForAdminId) and is paid out by the cafe.
    public decimal TipAmount { get; set; } = 0;
    // How the tip was paid. Null = the same way as the bill (PaymentMethod).
    public PaymentMethod? TipMethod { get; set; }
    public Guid? TipForAdminId { get; set; }

    public Guid? DiscountCodeId { get; set; }
    public DiscountCode? DiscountCode { get; set; }

    public bool EtaNotif5MinSent { get; set; } = false;
    public bool EtaNotif1MinSent { get; set; } = false;

    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedAt { get; set; }

    // Customer's post-completion feedback. Null until rated; a completed order may be
    // rated at most once.
    public int? Rating { get; set; }
    public string? RatingComment { get; set; }
    public DateTime? RatedAt { get; set; }

    public string? IdempotencyKey { get; set; }

    // Loyalty points credited to the customer's iiko wallet when the order completed.
    // Null = not credited (loyalty disabled or order not yet completed).
    public decimal? LoyaltyPointsEarned { get; set; }

    // Points applied as (partial) payment at checkout. Held in the iiko wallet on
    // placement, charged off on completion, released on decline.
    public decimal? LoyaltyPointsSpent { get; set; }

    // Active iiko hold transaction id; null once the hold is consumed or released.
    public Guid? LoyaltyHoldTransactionId { get; set; }

    // Wallet operation still owed to iiko (retried by LoyaltyRetryService).
    public LoyaltyPendingAction LoyaltyPendingAction { get; set; } = LoyaltyPendingAction.None;

    // Order-push side-channel (reporting/kitchen-routing only — never drives Status above).
    public Guid? IikoDeliveryOrderId { get; set; }
    public IikoOrderSyncStatus IikoOrderSyncStatus { get; set; } = IikoOrderSyncStatus.NotPushed;
    public IikoOrderSyncPendingAction IikoOrderSyncPendingAction { get; set; } = IikoOrderSyncPendingAction.None;

    public CustomerUser? CustomerUser { get; set; }
    public Location Location { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    // Stations that have finished their part of this order (own_pos). Empty when stations are not used.
    public ICollection<OrderStationReady> ReadyStations { get; set; } = new List<OrderStationReady>();
}

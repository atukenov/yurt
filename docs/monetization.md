# Yurt — Monetization Playbook

This document breaks down each monetization option into concrete implementation steps: what to build, in what order, and what to watch for. Options are ordered from lowest friction (build first) to highest complexity (build later).

---

## Option 1 — SaaS Subscription (Primary Revenue)

**What it is:** Cafés pay a recurring monthly or annual fee to use the platform.

**Tiers:**

| Tier | Price | Feature gates |
|---|---|---|
| Starter | Free | 1 location, 100 orders/month, basic analytics |
| Growth | ₸25,000 / month | 3 locations, unlimited orders, full analytics, promo codes |
| Business | ₸60,000 / month | Unlimited locations, worker accounts, loyalty program, priority support |
| Enterprise | Custom | White-label, custom domain, SLA, dedicated onboarding |

### Step-by-step implementation

**Step 1 — Add a `Tenant` entity**

Each café is a tenant. Add:
```csharp
public class Tenant : BaseEntity
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";       // subdomain or unique ID
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Starter;
    public DateTime? PlanExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum SubscriptionPlan { Starter, Growth, Business, Enterprise }
```

**Step 2 — Link existing entities to a tenant**

Add `TenantId` to `Location`, `AdminUser`, `Order`, `MenuItem`, `Promotion`. Add a `TenantMiddleware` that reads the tenant from the subdomain (e.g. `rosie.yurt.kz`) or a JWT claim and stores it in `ICurrentTenantService`.

**Step 3 — Gate features by plan**

Create a `PlanGate` service:
```csharp
public class PlanGate(ICurrentTenantService tenant)
{
    public bool CanAddLocation(int currentCount) =>
        tenant.Plan switch {
            SubscriptionPlan.Starter  => currentCount < 1,
            SubscriptionPlan.Growth   => currentCount < 3,
            _                         => true
        };

    public bool HasAnalytics()       => tenant.Plan >= SubscriptionPlan.Growth;
    public bool HasLoyaltyProgram()  => tenant.Plan >= SubscriptionPlan.Business;
    public bool HasPromoCodes()      => tenant.Plan >= SubscriptionPlan.Growth;
}
```

Call `PlanGate` in service methods before executing the action. Return `Result.Failure("Upgrade your plan to unlock this feature.", 402)` when the check fails. The frontend shows a locked state with an upgrade prompt.

**Step 4 — Build the billing portal**

- Integrate with a payment provider (Kaspi, Stripe, or local bank acquiring) to charge monthly.
- Create an `InvoiceService` that generates invoices and updates `PlanExpiresAt`.
- Add a `SuperAdmin` role (your own account) with a `/super-admin` panel to see all tenants, change plans, and suspend accounts.

**Step 5 — Add a plan downgrade grace period**

If payment fails, set a 3-day grace period flag on the tenant before locking access. Send an email or SMS warning on day 1 and day 3.

**Launch order:** Start with free tier only. Flip the plan gates on once you have 10+ active cafés.

---

## Option 2 — Transaction Fee

**What it is:** Take 0.5–1% of every paid order processed through the platform's payment flow.

**Why it works:** Low friction for cafés — they only pay when they earn. Scales automatically with their volume.

### Step-by-step implementation

**Step 1 — Add payment processing fields to `Order`**

```csharp
public decimal PlatformFeeAmount { get; set; }   // calculated at order creation
public decimal PlatformFeePercent { get; set; }  // captured from config at that moment
```

**Step 2 — Calculate the fee when payment is confirmed**

In `OrderService.UpdatePaymentAsync`, when `PaymentStatus` moves to `Paid`:
```csharp
var feePercent = _feeConfig.GetFeeForTenant(order.TenantId);  // e.g. 0.01m (1%)
order.PlatformFeeAmount = Math.Round(order.Total * feePercent, 2);
```

Store the rate at the time of capture — rates may change, and you need an accurate record.

**Step 3 — Build a payout ledger**

Add a `PlatformLedger` table that accumulates fee records:
```csharp
public class LedgerEntry : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public decimal FeeAmount { get; set; }
    public bool IsSettled { get; set; }
    public DateTime? SettledAt { get; set; }
}
```

Run a monthly settlement job that sums the fee and creates an invoice for the café.

**Step 4 — Exempt cash orders**

Cash orders bypass the payment processor, so the platform can't auto-deduct. Either:
- Exclude cash from the fee (simpler, lower friction for cafés)
- Include cash but invoice manually at month-end

**Step 5 — Surface fees to café owner**

Show a "Platform fees this month" line in the analytics dashboard so there are no surprises on the invoice.

**Launch order:** Implement only after real Kaspi QR integration is live. Announce the fee 30 days before activation so existing cafés can budget for it.

---

## Option 3 — Loyalty & Wallet Module (Add-on)

**What it is:** The in-app wallet top-up and loyalty points system are Premium features that Starter-tier cafés cannot use without upgrading.

**Revenue levers:**
- Upsell cafés to a higher plan tier
- Alternatively, charge 1% of every wallet top-up as a platform fee

### Step-by-step implementation

**Step 1 — Build the loyalty points backend (already in todo.md)**

```csharp
public class LoyaltyAccount : BaseEntity
{
    public Guid CustomerUserId { get; set; }
    public Guid TenantId { get; set; }
    public int PointsBalance { get; set; }
}

public class LoyaltyTransaction : BaseEntity
{
    public Guid LoyaltyAccountId { get; set; }
    public int PointsDelta { get; set; }          // positive = earned, negative = redeemed
    public Guid? OrderId { get; set; }
    public string Reason { get; set; } = "";
}
```

**Step 2 — Gate loyalty behind `Business` plan**

In `CheckoutService`, before applying points:
```csharp
if (!_planGate.HasLoyaltyProgram())
    return Result.Failure("Loyalty program is not available on your current plan.", 402);
```

The admin menu shows the Loyalty section greyed out with an "Upgrade to Business" button.

**Step 3 — Build the wallet top-up flow (already in todo.md)**

Customers top up via Kaspi QR. Store `WalletBalance` on `CustomerUser`. Deduct at checkout. A `WalletTransaction` ledger entry is written for every credit and debit.

**Step 4 — Charge the platform fee on wallet loads**

When a wallet top-up is confirmed (Kaspi webhook fires), write:
```csharp
var fee = Math.Round(topUpAmount * 0.01m, 2);
var credited = topUpAmount - fee;
customer.WalletBalance += credited;
ledger.Add(new LedgerEntry { TenantId = ..., FeeAmount = fee, Source = "WalletTopUp" });
```

**Step 5 — Notify the café owner about loyalty activity**

Add a "Loyalty program summary" card to the analytics dashboard: total points issued, total redeemed, estimated outstanding liability (unredeemed points × redemption value).

---

## Option 4 — Promoted / Featured Items

**What it is:** Cafés pay to pin up to 3 menu items at the top of the customer menu with a "Featured" badge. Flat fee: ₸5,000 per slot per month.

### Step-by-step implementation

**Step 1 — Add a `FeaturedSlot` entity**

```csharp
public class FeaturedSlot : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid MenuItemId { get; set; }
    public int SlotPosition { get; set; }    // 1, 2, or 3
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; }
}
```

**Step 2 — Surface featured items to the customer app**

In `MenuService.GetItemsAsync`, fetch active featured slots for the tenant and prepend them to the result list (deduplicating if the item also appears normally). Return a `IsFeatured = true` flag on the DTO.

**Step 3 — Show the badge in the customer app**

Add a "Featured" amber pill badge to the item card when `isFeatured === true`. Optionally animate it (pulse ring) to draw attention.

**Step 4 — Admin UI to purchase featured slots**

In the admin panel's Menu Management screen, add a "Feature this item" button per item. On click:
- Show a date-range picker (up to 30 days)
- Show the cost: ₸5,000 × (days / 30) rounded
- On confirm, redirect to payment (Kaspi or invoice the café)
- On payment confirmation, create the `FeaturedSlot` record

**Step 5 — Auto-expire slots**

Add a nightly job (Hangfire) that sets `IsActive = false` on slots where `EndsAt < DateTime.UtcNow`. Send a "Your featured slot for *Flat White* expires tomorrow" push notification to the café owner.

**Step 6 — Enforce the 3-slot limit**

```csharp
var activeSlots = await _db.FeaturedSlots
    .CountAsync(s => s.TenantId == tenantId && s.IsActive);
if (activeSlots >= 3)
    return Result.Failure("You can feature a maximum of 3 items at a time.", 400);
```

---

## Option 5 — Setup & Onboarding Fee

**What it is:** A one-time ₸30,000–₸80,000 fee for assisted onboarding: importing the menu, training staff, applying custom branding, and first-month support.

**This is a sales motion, not a feature.** Most of the implementation is operational, but a few things help automate it.

### Step-by-step implementation

**Step 1 — Build a menu import tool**

Accept a CSV or Excel file from the café and bulk-insert categories, items, prices, and images in one shot. This is the highest time-cost task during onboarding — automating it saves hours per client.

```
POST /api/v1/admin/menu/import
Content-Type: multipart/form-data
Body: file=menu.csv
```

CSV format: `Category, Item Name, Description, Price, ImageUrl, Available`

**Step 2 — Create a tenant provisioning script**

A single CLI command that creates the tenant record, seeds the default admin account, and sends a welcome email with credentials:

```bash
dotnet yurt provision --name "Rosie Coffee" --slug rosie --admin-email owner@rosie.kz
```

**Step 3 — Build a simple branded theming layer**

Add a `TenantBranding` table:
```csharp
public class TenantBranding : BaseEntity
{
    public Guid TenantId { get; set; }
    public string LogoUrl { get; set; } = "";
    public string PrimaryColor { get; set; } = "#F59E0B";  // amber default
    public string AppName { get; set; } = "Yurt";
}
```

The customer app reads branding on load and applies the primary color as a CSS variable. Logo is shown in the header and push notifications.

**Step 4 — Track onboarding progress**

Add a simple `OnboardingStatus` enum on the `Tenant`: `Provisioned → MenuLoaded → StaffTrained → GoLive`. Show a progress bar in the super-admin panel. Billing starts when status reaches `GoLive`.

**Step 5 — Define pricing tiers for onboarding**

| Package | Price | Includes |
|---|---|---|
| Self-serve | ₸0 | Documentation, video walkthrough |
| Assisted | ₸30,000 | Remote session, menu import help |
| Full-service | ₸80,000 | On-site training, custom branding, 30-day support |

The self-serve tier reduces your manual work while still generating plan subscription revenue.

---

## Option 6 — White-Label Licensing

**What it is:** Sell the entire platform to a café chain or POS reseller as their own branded product. They pay a one-time licensing fee (₸500,000+) or a recurring royalty (₸150,000/month).

### Step-by-step implementation

**Step 1 — Extract all Yurt branding into configuration**

Every hardcoded "Yurt" string, color, and logo reference becomes a config value. This is mostly a frontend task — replace brand constants in `environment.ts` and move them to an API-served config endpoint.

**Step 2 — Support custom domains**

The tenant's subdomain (`order.rosie.kz`) points to the platform via CNAME. Add a `CustomDomain` field on `Tenant`. The TenantMiddleware resolves the tenant from the `Host` header.

**Step 3 — Separate the infrastructure per white-label client**

Large licensees typically want data isolation. Use schema-per-tenant in PostgreSQL or a separate database per licensee. EF Core's `UseSchema()` call or a factory pattern that creates the DbContext with the correct connection string per request.

**Step 4 — Build a licensing agreement workflow**

Contracts, NDAs, and support SLAs are handled outside the platform. Internally, mark the tenant as `Plan = Enterprise` and set a `LicenseKey` field. Validate the key on startup — the platform refuses to boot without a valid key (protects against unauthorized copies).

**Step 5 — Create a white-label delivery checklist**

1. Remove all Yurt wordmarks from the app
2. Deploy to a separate cloud instance (or provide Docker images)
3. Provide admin credentials for the licensee's super-admin
4. Hand over the `appsettings.Production.json` template with their connection strings and JWT secrets

**Step 6 — Define support boundaries**

Decide upfront what you maintain (core platform updates, security patches) vs. what the licensee owns (their deployment, their data). Document this in the license agreement.

---

## Option 7 — SMS / Notification Credits

**What it is:** Monthly push notification and SMS quota included in each plan tier; overage charged per 1,000 messages.

**Included credits by tier:**

| Tier | Included pushes/month | Overage |
|---|---|---|
| Starter | 500 | Not available (upgrade required) |
| Growth | 5,000 | ₸200 per 1,000 |
| Business | 20,000 | ₸150 per 1,000 |

### Step-by-step implementation

**Step 1 — Add a notification counter to the tenant**

```csharp
public class TenantUsage : BaseEntity
{
    public Guid TenantId { get; set; }
    public int Month { get; set; }   // e.g. 202601
    public int PushNotificationsSent { get; set; }
    public int SmsSent { get; set; }
}
```

**Step 2 — Increment the counter on every send**

In `PushNotificationService.SendAsync` and `SmsService.SendAsync`:
```csharp
await _usageTracker.IncrementPushAsync(tenantId);
```

Write this as a fire-and-forget async call so it never blocks the notification send.

**Step 3 — Enforce the quota**

Before sending, check:
```csharp
var usage = await _db.TenantUsages.FindMonthAsync(tenantId, currentMonth);
var limit = _planGate.GetPushLimit();
if (usage.PushNotificationsSent >= limit && !tenant.AllowOverage)
{
    _logger.LogWarning("Push quota exceeded for tenant {TenantId}", tenantId);
    return;  // silently skip — or notify the café owner instead
}
```

**Step 4 — Notify the café at 80% quota**

When usage crosses 80%, send a single warning push to the café owner's admin account: "You've used 4,000 of your 5,000 monthly push notifications."

**Step 5 — Bill overage monthly**

The monthly invoice job sums `PushNotificationsSent - IncludedLimit` for each tenant and adds an overage line to the invoice.

---

## Option 8 — Data & Benchmarking Reports

**What it is:** Sell monthly industry reports to café owners showing how their metrics compare to anonymized platform-wide averages. High perceived value; low marginal cost once analytics are built.

### Step-by-step implementation

**Step 1 — Build the aggregation pipeline**

Run a monthly background job that reads completed, non-archived orders across all consenting tenants and computes:
- Average order value by city / neighbourhood
- Peak hours distribution
- Top 10 item categories by volume
- Average items per order

Store results in a `BenchmarkSnapshot` table keyed by `(Month, City)`.

**Step 2 — Add a consent flag on tenants**

```csharp
public bool ConsentToAnonymizedBenchmarking { get; set; } = true;
```

Shown as an opt-out checkbox in tenant settings. Required for GDPR-adjacent compliance in Kazakhstan's data protection rules (Law on Personal Data, 2013).

**Step 3 — Generate the report PDF**

Use a templating library (e.g. QuestPDF for .NET) to render a branded PDF:
- Cover: café name, month, city
- Your numbers: revenue, avg order value, peak hour, top 3 items
- Benchmark: where you rank vs. similar cafés (anonymized, no competitor names)
- Recommendation: "Your peak is 9–10 AM. Consider a breakfast bundle promotion."

**Step 4 — Deliver the report**

- Email it to the café owner on the 1st of each month
- Also available as a download in the admin panel: `Analytics → Monthly Report`

**Step 5 — Price and gate the report**

| Access | Plan |
|---|---|
| Current month's own data only | All plans |
| Benchmark comparison | Growth and above |
| Downloadable PDF report | Business and above |
| Custom report (specific date range, item-level) | Enterprise (manual request) |

---

## Recommended Launch Path

```
Month 1–3    Launch free Starter tier
             Goal: 10–20 active cafés, daily orders flowing

Month 3–6    Introduce Growth plan (₸25,000/month)
             Existing free cafés now locked out of advanced analytics — upgrade prompt kicks in
             Offer 20% discount for first 3 months to smooth the transition

Month 6      Real Kaspi QR integration live
             Activate 0.8% transaction fee (announce 30 days ahead)

Month 9      Add featured item slots (₸5,000/slot)
             Low friction, instant revenue from active cafés

Month 12     Business plan with loyalty program & wallet module
             Data benchmarking reports available as Business perk

Month 18+    Approach café chains for white-label licensing deal
             Requires multi-tenant isolation work to be solid first
```

---

## Key Metrics to Track (from Day 1)

| Metric | Why it matters |
|---|---|
| Monthly Recurring Revenue (MRR) | Primary health indicator |
| Average Revenue Per Account (ARPA) | Tells you which plan tier dominates |
| Churn rate | >3% monthly churn = product-market fit problem |
| Orders per active café per day | Proxy for customer stickiness |
| Feature adoption rate per gate | Reveals which gates convert upgrades |
| Transaction fee as % of MRR | As Kaspi volume grows, this should grow faster than subscriptions |

---

## Quick-Win Priority Order

1. **Plan gates** — zero revenue without them; build before any other monetization work
2. **Onboarding fee** — immediate cash from every new client; no code required
3. **Transaction fee** — automated revenue; needs Kaspi integration first
4. **Featured items** — quick upsell to already-active cafés
5. **Loyalty add-on** — needs loyalty feature to exist first (already in todo.md)
6. **Notification credits** — add once push volume is measurable
7. **Benchmarking reports** — best when you have 20+ tenants with meaningful data
8. **White-label** — highest value, highest complexity; pursue when platform is stable

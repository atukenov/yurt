# Monthly billing of cafes (Stripe subscriptions)

The platform owner charges each cafe a monthly fee by card through **the owner's own Stripe account**. This is separate from
online order payments (`online_payments`), where customers pay into **the cafe's** Stripe account.

## How it works

1. On **Platform → Monthly billing** (cafe picked at the top) the owner sets the cafe's monthly price, the currency
   (USD, EUR or KZT) and which modules a paid month covers (default: every module except core).
2. The cafe's admin opens **Subscription** and presses **Pay monthly by card**. Stripe's own page takes the card
   (Apple Pay / Google Pay too, if switched on in Stripe) and starts a monthly subscription.
3. Every time Stripe charges the card (`invoice.paid`), Yurt sets the covered modules to *on*, source *Paid*, valid
   until the end of the paid month. A late or repeated webhook never moves a date back.
4. **Manage card and invoices** opens Stripe's billing page (change card, download invoices, cancel).
5. If a charge fails, Stripe retries on its own schedule (Settings → Billing → Revenue recovery). Yurt does not switch
   anything off: when no payment arrives, the modules expire at their date and the usual grace period
   (`Modules:GraceDays`) applies, data is never deleted.

Without a plan (no price) nothing changes: modules are managed by hand on the Platform screen as before.

## Setup (test mode)

1. In your Stripe account, test mode: **Developers → API keys**, copy the secret key (`sk_test_...`).
2. **Developers → Webhooks → Add endpoint**: the address shown on the Platform screen
   (`https://<api>/api/v1/billing/stripe/webhook`, the same for all cafes), events
   `checkout.session.completed`, `invoice.paid`, `invoice.payment_failed`, `customer.subscription.created`,
   `customer.subscription.updated`, `customer.subscription.deleted`. Copy its signing secret (`whsec_...`).
3. **Settings → Billing → Customer portal**: press *Save* once (Stripe needs a saved portal setup before
   "Manage card and invoices" works).
4. On the server set `Billing__Stripe__SecretKey` and `Billing__Stripe__WebhookSecret`.
5. Test card: `4242 4242 4242 4242`, any future date, any CVC.

Live keys (`sk_live_...`) are refused until `Stripe__AllowLiveKeys=true`, the same switch as order payments.

## Details

- Data: table `CafeBillings` (one row per cafe): price, currency, modules, Stripe customer and subscription ids,
  status, paid-until date, last payment.
- The cafe is found from `tenant_id` in the subscription metadata (Yurt sets it at checkout), else from the stored
  subscription or customer id. An `invoice.paid` that matches no cafe answers 409 so Stripe sends it again later.
- A price change applies to new subscriptions only. Change a running one in the Stripe dashboard.
- Starting and opening the billing page is for the cafe's Admin (and SuperAdmin), not Worker accounts.
- Stripe does not accept businesses registered in Kazakhstan; live billing needs a company in a supported country.

# Go-live checklist

Admin app → **Platform → Go-live checklist** (SuperAdmin only) shows, for the cafe you choose at the top, what is
done and what is still missing before real customers use it. It is read only: for secrets it says only "set" or
"missing" and the name of what is missing (for example `Stripe:WebhookSecret`), never a value.

Each line has a **Guide** link. Most of them point to an existing guide; the ones that had none are below.

The page has two parts:

- **Server (shared by every cafe)**: Railway variables you set once. Master key, payment return link, Telegram
  alerts, and your own Stripe account for monthly fees.
- **This cafe**: the cafe's own setup. A line is **Not needed** when its module is off for the cafe (for example
  Stripe while `online_payments` is off), so it never counts as missing.

States: **Done**, **Missing**, **Check** (works, but test keys: no real money moves yet), **Not needed**.

API: `GET /api/v1/platform/go-live` with `X-Tenant: <cafe key>`.

| Line | Counts as done when | Guide |
| --- | --- | --- |
| Master key | `Secrets__MasterKey` is set | [live-server-setup.md](live-server-setup.md#1-master-key-locks-the-cafes-secret-keys) |
| Return link after payment | `Payment__CustomerAppUrl` is set | [live-server-setup.md](live-server-setup.md#2-return-link-after-online-payment) |
| Telegram alerts | `Alerts__TelegramBotToken` and `Alerts__TelegramChatId` are set | [monitoring.md](monitoring.md#1-error-alerts-in-telegram-about-5-minutes) |
| Your Stripe for monthly fees | `Billing__Stripe__*` keys are set ("Check" while they are test keys) | [billing.md](billing.md#setup-test-mode) |
| Web address | the cafe has a domain (the first cafe uses the main address) | [Domain](#domain) |
| Brand | logo and app icon are uploaded | [Brand](#brand) |
| Menu | at least one dish is on sale | [Menu](#menu) |
| Branches | at least one branch is open | [Branches](#branches) |
| Online payments | provider is Stripe and both Stripe secrets are set (only when `online_payments` is on) | [stripe-payments.md](stripe-payments.md#setting-up-a-cafe-about-10-minutes) |
| Push notifications | `Firebase__Enabled=true` on the server and the cafe's `Firebase:ServiceAccountJson` (only when `push` is on) | [push-notifications-setup.md](push-notifications-setup.md) |
| WhatsApp codes | `Otp__Enabled=true` on the server and the cafe's `Otp:IdInstance` + `Otp:ApiTokenInstance` (only when `verification` is on) | [WhatsApp codes](#whatsapp-codes-green-api) |
| Tax receipts | provider chosen, its login set, every open branch has a register number (only when `own_pos` is on) | [Tax receipts](#tax-receipts) |
| Monthly fee | the cafe has a monthly price and its card payment is running (not for the first cafe) | [billing.md](billing.md#how-it-works) |

## Domain

The address customers open, for example `order.mycafe.kz`.

1. The cafe adds a DNS record for that name pointing at the customer web app (on Vercel: add the domain to the
   customer app project, then create the CNAME record Vercel shows).
2. Enter the domain when you create the cafe (Platform → New cafe). The server lets that address call the API at
   once, no redeploy needed.

There is no screen yet to change a cafe's domain after it is created.

## Brand

Platform → choose the cafe at the top → **Brand**. Upload the logo and the app icon (the icon is used for the
branded phone app, see [branded-app-release.md](branded-app-release.md)). Colours, font and links are optional.

## Menu

The cafe admin adds dishes on the **Menu** screen, or imports an Excel/CSV file there (template on the same screen).
The line is done when at least one dish is on sale.

## Branches

The cafe admin adds branches on the **Locations** screen: name, address, working hours, phone. The line is done when
at least one branch is open.

## WhatsApp codes (Green API)

Customers get their sign-up code by WhatsApp, sent from the cafe's own WhatsApp number through
[Green API](https://green-api.com/).

1. The cafe (or you for them) creates an instance at green-api.com and links the cafe's WhatsApp by scanning the QR
   code.
2. Copy the **idInstance** and **apiTokenInstance** from the instance page.
3. Platform → Cafes → the cafe → **Secrets**: set `Otp:IdInstance` and `Otp:ApiTokenInstance`.
4. On the server, `Otp__Enabled=true` must be set once (it is shared by all cafes).

Without these the cafe cannot send codes. To let people sign up without a code for a while, switch the
`verification` module off for the cafe.

## Tax receipts

Only for cafes that sell through Yurt's own POS (`own_pos`). Background and the choice of provider:
[fiscal-receipts-kz.md](fiscal-receipts-kz.md). Steps:

1. Platform → choose the cafe → **Official tax receipts**: pick Webkassa or ReKassa and enter the register number of each
   branch.
2. Platform → Cafes → the cafe → **Secrets**: Webkassa needs `Fiscal:Login` and `Fiscal:Password`; ReKassa needs
   `Fiscal:Password` and `Fiscal:Pin`.
3. Before the first real day, run the test check in [webkassa-test-check.md](webkassa-test-check.md).

The checklist lists the branches that still have no register number.

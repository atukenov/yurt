# YURT — Café Ordering Platform

A white-label platform for cafés: each café gets its own branded customer app, an admin console with its own
point of sale, kitchen screen and stock, and online payments, all running on one shared server. Built with .NET 10,
PostgreSQL and Angular, with Capacitor for the iOS and Android apps.

---

## Quick Start (local)

```bash
./scripts/dev.sh        # Postgres + API (Docker) + customer app + admin panel
```

| What | URL / login |
|---|---|
| Customer app | http://localhost:4200 |
| Admin panel | http://localhost:4300 — café owner (CEO) `ceo` / `ceo12345`, café admin `admin` / `admin12345`, platform operator `superadmin` / `superadmin12345` (all must change the password at first login) |
| Yurt POS (till) | http://localhost:4400 — sign in with a staff login ([docs/yurt-pos.md](docs/yurt-pos.md)) |
| API + Swagger | http://localhost:8080, http://localhost:8080/swagger |

The local cafe comes filled with made-up demo data (about 2,000 orders, stock, suppliers, staff, payroll...). Demo
staff sign in as `demo.manager` / `demo12345`, customers as `+77015550001` with PIN `1234`. See
[docs/demo-data.md](docs/demo-data.md).

Needs Docker (with compose) and Node 20+. The .NET SDK is not needed, the API builds in Docker.
`./scripts/dev.sh stop` stops everything, `./scripts/dev.sh reset` also deletes the local database,
`./scripts/dev.sh phone` lets a phone on the same Wi-Fi open the customer app. More in [docs/runbook.md](docs/runbook.md);
the platform roadmap is [docs/MODULARIZATION_TODO.md](docs/MODULARIZATION_TODO.md).

---

## What It Does

Customers browse a café's menu, build a cart, pay (card, Apple Pay / Google Pay or at the counter) and pick up their
order, or scan a QR code on a table and order to the table. Staff see orders live, work them on the kitchen screen or
the cashier screen, and print tickets and tax receipts. Café owners manage menu, branches, staff, promotions and their
brand. The platform operator adds new cafés, switches paid modules on or off per café and bills each café monthly.

---

## Feature Overview

### Customer App

| Feature | Description |
|---|---|
| Phone + PIN auth | Register and log in with a mobile number and 4-digit PIN; optional phone check by a WhatsApp code. |
| Café branding | Logo, colours, font, welcome text and links come from the café's Brand settings; a café can also have its own store app. |
| Multi-location | Pick a branch; a shared app can offer a café picker. |
| Searchable menu | Categories, search, photos, grouped and free-pick toppings. |
| Cart and checkout | Persistent cart, per-item notes, discount codes, pickup ASAP or at a chosen 15-minute slot within branch hours. |
| Online payments | Per-café Stripe Checkout (cards, Apple Pay, Google Pay) with refunds; cash / pay at the counter still works. |
| Table QR ordering | Scan a table's QR code to order to that table (module `table_ordering`). |
| Order tracking | Live status via SignalR, push notifications, order history, receipt view and tax receipt link / QR. |
| Group ordering | Shared cart with a join code/link; one person pays. |
| Extras | Favorites, "order again", loyalty points, promotions and news, ratings and achievements. |
| Allergens and calories | Allergen icons and calories on dishes, on the menu and the QR menu ([docs/allergens-nutrition.md](docs/allergens-nutrition.md)). |
| Order number | A short number that starts again from 1 each day per branch, shown on the order, the TV and the cup label ([docs/daily-order-numbers.md](docs/daily-order-numbers.md)). |
| Languages | Russian, Kazakh and English, including dates and numbers in the app language. |
| Profile | Edit name, change PIN, lifetime stats, delete account. |

### Admin Panel

| Feature | Description |
|---|---|
| Roles | Worker, Admin (runs the floor, no money screens), CEO (owner: money, payroll, audit log) and platform operator (SuperAdmin) ([docs/roles.md](docs/roles.md)). |
| Live orders | Status tabs, branch filter, accept with ETA, decline with reason, payment status and refunds. |
| Kitchen and cashier | Kitchen screen, cashier screen with cash drawer, shift and Z reports, here / to go orders. |
| Yurt POS (till) | Separate cashier app for walk-in guests that keeps selling without internet, prints receipts, takes tips, scans barcodes and weighs by weight ([docs/yurt-pos.md](docs/yurt-pos.md), [docs/barcodes-and-scales.md](docs/barcodes-and-scales.md), [docs/tips.md](docs/tips.md)). |
| Guest screens | Guest-facing screen for the till, order-ready TV and cup labels, and a self-order kiosk (module `kiosk`) ([docs/guest-screen.md](docs/guest-screen.md), [docs/order-ready-tv-and-labels.md](docs/order-ready-tv-and-labels.md), [docs/self-order-kiosk.md](docs/self-order-kiosk.md)). |
| Manager PIN | Cancels, refunds and discounts at the till need a manager's PIN and a reason, written to the audit log ([docs/roles.md](docs/roles.md)). |
| Staff hours | Staff PIN to switch the till and clock in or out, a Worked hours report with the Yurt Watch camera check, hourly pay in payroll ([docs/time-clock.md](docs/time-clock.md)). |
| Printers | Network and Bluetooth printers, a print agent for the café PC, kitchen tickets and receipt reprints. |
| Tax receipts (KZ) | Cloud cash register through Webkassa or ReKassa (off by default, not yet verified with a live account). |
| Menu | Categories, dishes (photo, sizes, options, branch prices), option groups with rules, bulk edit, combos and time menus under one entry, translations, picture upload, import from Excel / CSV. |
| Stock | Ingredients, recipes, restocking history; suppliers, delivery invoices and a "what to order" list ([docs/purchasing.md](docs/purchasing.md)). |
| Forecast | Sales forecast and a daily prep plan per branch ([docs/forecast.md](docs/forecast.md)). |
| Branches and tables | Working hours, temporary closing, tables with printable QR codes. |
| Marketing | Promotions and discount codes. |
| Analytics | Dashboard KPIs, revenue and top items, peak hours, CSV export. Profit page: monthly profit after ingredients, write-offs, wages and other costs, and profit per dish (Excel download). Menu analysis: stars, plowhorses, puzzles and dogs, and ABC (CEO only, [docs/profit.md](docs/profit.md)). Excel and 1C export for the accountant. |
| Customers and staff | Customer list and history, staff accounts, audit log, guest groups and automatic messages ([docs/guest-messages.md](docs/guest-messages.md)). |
| Subscription | The café's own Yurt plan and invoices; unpaid cafés go into limited mode. |
| Platform (superadmin) | Cafés, new-café wizard, modules per café, brand, per-café secrets, app releases, data export for a leaving café. |
| Languages | Full admin UI in Russian, Kazakh and English. |
| Tablet app | Native Android / iPad wrapper of the admin panel for classic Bluetooth printers ([docs/tablet-app.md](docs/tablet-app.md)). |

### Backend & Infrastructure

| Feature | Description |
|---|---|
| Clean Architecture | Domain / Application / Infrastructure / WebApi layers. |
| Multi-tenant | One shared PostgreSQL database; every café's data is filtered by tenant. |
| Paid modules | Features are switched per café ([docs/modules.md](docs/modules.md)); everything is on by default. |
| Secrets | Per-café keys (Stripe, fiscal, WhatsApp, Firebase…) encrypted with `Secrets__MasterKey`. |
| Payments and billing | Stripe Checkout + webhooks per café; monthly café billing through Stripe subscriptions ([docs/stripe-payments.md](docs/stripe-payments.md), [docs/billing.md](docs/billing.md)). |
| Auth | JWT access + refresh tokens, BCrypt hashing, rate limits on auth endpoints. |
| Real-time | SignalR hub for live orders, per café. |
| Background jobs | Order archival, fiscal receipts, print jobs, iiko and loyalty retries (iiko is kept for the first café only). |
| Monitoring | `/health/ready`, error alerts to Telegram ([docs/monitoring.md](docs/monitoring.md)). |
| Migrations | EF Core, additive only (CI blocks dropped or renamed columns). |
| Swagger | OpenAPI docs at `/swagger`. |

---

## Architecture

```
yurt/
├── backend/
│   ├── src/
│   │   ├── Yurt.Domain/          # Entities, enums
│   │   ├── Yurt.Application/     # Services, interfaces, DTOs
│   │   ├── Yurt.Infrastructure/  # EF Core (Postgres), payments, fiscal, printing, jobs
│   │   └── Yurt.WebApi/          # Controllers, middleware, startup
│   └── tests/                    # Yurt.UnitTests, Yurt.IntegrationTests (Testcontainers)
├── frontend/
│   ├── projects/
│   │   ├── yurt-customer/        # Customer app (web + iOS/Android via Capacitor)
│   │   ├── yurt-admin/           # Admin console
│   │   ├── yurt-pos/             # Cashier (till) app, offline-capable, also the kiosk screen
│   │   ├── shared-models/        # TypeScript interfaces and enums
│   │   ├── shared-api/           # API services, auth state, SignalR
│   │   └── shared-ui/            # Shared components, translations, date/number pipes
│   ├── android/, ios/            # Native customer app projects
│   └── admin-native/             # Native tablet app for the admin console
├── cafes/                        # Per-café store app config (cafes/README.md)
├── landing/                      # Landing page for café owners
├── watch/                        # Yurt Watch camera box (Python)
├── scripts/                      # dev.sh, apply-cafe, app builds, print agent, checks
└── docs/                         # Guides (see below)
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core, Clean Architecture |
| Database | PostgreSQL 16, EF Core 10, code-first migrations |
| Auth | JWT Bearer with refresh tokens, BCrypt, role-based policies |
| Real-time | ASP.NET Core SignalR |
| Frontend | Angular 21, Tailwind CSS, standalone components, signals |
| Mobile | Capacitor 8 (customer app per café, admin tablet app) |
| Payments | Stripe |
| Logging | Serilog |
| API docs | Swagger / OpenAPI |

---

## Running Without Docker

Use this only if you want to run the API with your own .NET SDK; `./scripts/dev.sh` is easier.

1. Start PostgreSQL (for example `docker compose up -d postgres`) and set environment variables:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=YurtDb;Username=postgres;Password=postgres"
export Jwt__Secret="any-random-string-of-at-least-32-characters"
export SEED_ADMIN_USERNAME=admin SEED_ADMIN_PASSWORD=admin12345
export SEED_CEO_USERNAME=ceo SEED_CEO_PASSWORD=ceo12345   # the cafe owner: sales, billing, payroll (see docs/roles.md)
```

2. Run the API (migrations run at start-up):

```bash
cd backend
dotnet run --project src/Yurt.WebApi
```

3. Run the apps:

```bash
cd frontend
npm install
npx ng serve yurt-customer --port 4200
npx ng serve yurt-admin --port 4300
```

Tests: `dotnet test` in `backend/` (integration tests need Docker), `npx ng test shared-ui --watch=false` and
`node --test scripts/*.test.mjs` for the frontend and scripts.

---

## Going Live

- [docs/go-live.md](docs/go-live.md) — the go-live checklist screen (Platform → Go-live checklist): what each café still needs
- [docs/live-server-setup.md](docs/live-server-setup.md) — step-by-step server setup (env vars, master key, payments link)
- [docs/runbook.md](docs/runbook.md) — staging, smoke test, backup, deploy order, rollback, first-day steps after a release
- [docs/monitoring.md](docs/monitoring.md) — error alerts and uptime check
- [docs/app-builds.md](docs/app-builds.md) — signed iOS / Android builds in GitHub Actions
- [docs/branded-app-release.md](docs/branded-app-release.md) — a café's own store app, store screenshots
- [docs/deploy-ios-appstore.md](docs/deploy-ios-appstore.md), [docs/deploy-android-playmarket.md](docs/deploy-android-playmarket.md) — store submission

## Till, staff and guest screens

- [docs/yurt-pos.md](docs/yurt-pos.md) — the Yurt POS cashier app: sales, offline mode, printing, deploying it
- [docs/roles.md](docs/roles.md) — Worker, Admin, CEO, SuperAdmin; manager PIN
- [docs/time-clock.md](docs/time-clock.md) — staff PIN, clock-in, Worked hours
- [docs/tips.md](docs/tips.md), [docs/barcodes-and-scales.md](docs/barcodes-and-scales.md) — tips, barcode scanner and scales
- [docs/guest-screen.md](docs/guest-screen.md), [docs/order-ready-tv-and-labels.md](docs/order-ready-tv-and-labels.md), [docs/self-order-kiosk.md](docs/self-order-kiosk.md), [docs/daily-order-numbers.md](docs/daily-order-numbers.md) — guest screen, TV, cup labels, kiosk, order numbers
- [docs/guest-messages.md](docs/guest-messages.md) — guest groups and automatic messages
- [docs/demo-data.md](docs/demo-data.md) — demo data for local and staging

## Other Docs

- [docs/stripe-payments.md](docs/stripe-payments.md), [docs/billing.md](docs/billing.md) — online payments and café billing
- [docs/fiscal-receipts-kz.md](docs/fiscal-receipts-kz.md), [docs/webkassa-test-check.md](docs/webkassa-test-check.md) — tax receipts
- [docs/print-agent.md](docs/print-agent.md), [docs/bluetooth-printers.md](docs/bluetooth-printers.md), [docs/tablet-app.md](docs/tablet-app.md) — printers and hardware
- [docs/modules.md](docs/modules.md) — what each paid module covers
- [docs/stock-pack.md](docs/stock-pack.md), [docs/purchasing.md](docs/purchasing.md) — stock costs, write-offs, suppliers and deliveries
- [docs/forecast.md](docs/forecast.md) — sales forecast and daily prep plan
- [docs/option-rules.md](docs/option-rules.md) — required choices (milk), min / max, options per dish, prices per branch
- [docs/menu-management.md](docs/menu-management.md) — the admin Menu section: dishes list, editor tabs, options, bulk edit
- [docs/time-menus.md](docs/time-menus.md) — breakfast / happy hour: dishes and prices by hours and days, per branch clock
- [docs/allergens-nutrition.md](docs/allergens-nutrition.md) — allergens and calories on dishes, shown to guests on the menu and QR menu
- [docs/combos.md](docs/combos.md) — dishes sold together at a set price: stock per dish, stop-list, kitchen ticket, CEO report
- [docs/profit.md](docs/profit.md) — monthly profit and profit by dish (CEO only)
- [docs/accounting-export.md](docs/accounting-export.md) — Excel and 1C files for the cafe's accountant (CEO only)
- [docs/cafe-data-export.md](docs/cafe-data-export.md) — exporting a café's data
- [docs/cutover-from-iiko.md](docs/cutover-from-iiko.md) — moving a café off iiko
- [docs/push-notifications-setup.md](docs/push-notifications-setup.md) — push notifications
- [landing/README.md](landing/README.md) — the landing page for café owners (prices, modules, demo form) and how to publish it

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
| Admin panel | http://localhost:4300 — café admin `admin` / `admin12345`, platform operator `superadmin` / `superadmin12345` (both must change the password at first login) |
| API + Swagger | http://localhost:8080, http://localhost:8080/swagger |

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
| Languages | Russian, Kazakh and English, including dates and numbers in the app language. |
| Profile | Edit name, change PIN, lifetime stats, delete account. |

### Admin Panel

| Feature | Description |
|---|---|
| Roles | Café admin, staff (orders only) and platform operator (superadmin). |
| Live orders | Status tabs, branch filter, accept with ETA, decline with reason, payment status and refunds. |
| Kitchen and cashier | Kitchen screen, cashier / point-of-sale screen with cash drawer, shift and Z reports. |
| Printers | Network and Bluetooth printers, a print agent for the café PC, kitchen tickets and receipt reprints. |
| Tax receipts (KZ) | Cloud cash register through Webkassa or ReKassa (off by default, not yet verified with a live account). |
| Menu | Categories, items, toppings, translations, picture upload, import from Excel / CSV. |
| Stock | Ingredients, recipes, restocking history. |
| Branches and tables | Working hours, temporary closing, tables with printable QR codes. |
| Marketing | Promotions and discount codes. |
| Analytics | Dashboard KPIs, revenue and top items, peak hours, CSV export. |
| Customers and staff | Customer list and history, staff accounts, audit log. |
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
│   │   ├── shared-models/        # TypeScript interfaces and enums
│   │   ├── shared-api/           # API services, auth state, SignalR
│   │   └── shared-ui/            # Shared components, translations, date/number pipes
│   ├── android/, ios/            # Native customer app projects
│   └── admin-native/             # Native tablet app for the admin console
├── cafes/                        # Per-café store app config (cafes/README.md)
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

- [docs/live-server-setup.md](docs/live-server-setup.md) — step-by-step server setup (env vars, master key, payments link)
- [docs/runbook.md](docs/runbook.md) — staging, smoke test, backup, deploy order, rollback
- [docs/monitoring.md](docs/monitoring.md) — error alerts and uptime check
- [docs/app-builds.md](docs/app-builds.md) — signed iOS / Android builds in GitHub Actions
- [docs/branded-app-release.md](docs/branded-app-release.md) — a café's own store app, store screenshots
- [docs/deploy-ios-appstore.md](docs/deploy-ios-appstore.md), [docs/deploy-android-playmarket.md](docs/deploy-android-playmarket.md) — store submission

## Other Docs

- [docs/stripe-payments.md](docs/stripe-payments.md), [docs/billing.md](docs/billing.md) — online payments and café billing
- [docs/fiscal-receipts-kz.md](docs/fiscal-receipts-kz.md), [docs/webkassa-test-check.md](docs/webkassa-test-check.md) — tax receipts
- [docs/print-agent.md](docs/print-agent.md), [docs/bluetooth-printers.md](docs/bluetooth-printers.md), [docs/tablet-app.md](docs/tablet-app.md) — printers and hardware
- [docs/modules.md](docs/modules.md) — what each paid module covers
- [docs/cafe-data-export.md](docs/cafe-data-export.md) — exporting a café's data
- [docs/cutover-from-iiko.md](docs/cutover-from-iiko.md) — moving a café off iiko
- [docs/push-notifications-setup.md](docs/push-notifications-setup.md) — push notifications
- [landing/README.md](landing/README.md) — the landing page for café owners (prices, modules, demo form) and how to publish it

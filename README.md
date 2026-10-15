# YURT — Coffee Ordering Platform

A full-stack café ordering platform with a mobile-first customer app, a desktop admin console, real-time order management, and a clean .NET 10 / Angular 17+ architecture.

---

## What It Does

Yurt connects café customers with café staff in real time. Customers browse the menu, build a cart, and place pickup orders from their phone. Staff see incoming orders instantly and manage them through a live dashboard. Admins control the full menu, locations, staff accounts, promotions, and analytics from a separate admin panel.

---

## Feature Overview

### Customer App

| Feature | Description |
|---|---|
| Phone + PIN auth | Register and log in with a mobile number and 4-digit PIN. Auto-sign-in on correct PIN entry. |
| Persistent sessions | Session survives app restarts; expires after 7 days of inactivity with auto-renewal. |
| Multi-location support | Browse active café locations and pick a pickup store. |
| Searchable menu | Filter by category, search by item name. |
| Toppings & add-ons | Grouped toppings (radio select) and free-pick toppings (checkboxes) per item. |
| Cart with notes | Persistent cart across sessions, quantity controls, per-item special instructions. |
| Favorites | Heart icon on items; dedicated Favorites tab for quick reorder. |
| Order tracking | Real-time status updates via SignalR (Placed → Accepted → Preparing → Ready → Completed). |
| Order history | View active, past, and declined orders with full item breakdown. |
| Receipts | View Receipt bottom-sheet on completed orders with toppings, totals, and payment info. |
| "Order again" | One-tap shortcut on the home screen to re-add the last completed order to cart. |
| Personalized greeting | Home screen shows the customer's first name. |
| Push notifications | Opt-in notifications when order status changes; deep-links to the order. |
| Profile & settings | Edit display name, change PIN, view lifetime stats, delete account. |
| Language switcher | UI available in Russian, English, and Kazakh; selection persists across sessions. |
| Group ordering | Create a group cart, share a code/link, participants add items, one person pays. |

### Admin Panel

| Feature | Description |
|---|---|
| Role-based access | Admins have full access; Workers see only the live orders screen. |
| Live orders | SignalR-powered order list with status tabs (Active / Done / All) and location filter. |
| Order actions | Accept with ETA, decline with reason, mark Preparing / Ready / Completed. |
| Payment tracking | Record payment method (Cash / Card / Other) and payment status (Unpaid / Paid / Refunded). |
| Menu management | Full CRUD for categories, items, and toppings with pricing, availability, image URL, and topping groups. |
| Multi-language menu | Enter menu content translations per language (RU / EN / KK) with a single-language-at-a-time form. |
| Location management | Edit name, working hours, contact phone, and active status; temporarily close a location. |
| Promotions | Create, edit, and deactivate timed promotions with title, description, and active period. |
| Analytics | Revenue and order count over time (week / month / 6M / 1Y / all), top-selling items, peak hours heatmap, export to CSV. |
| Customer list | All registered customers with registration date, order count, and total spend; search by phone. |
| Customer detail | Full order history per customer with status, location, item count, and totals. |
| Worker accounts | Create, deactivate, and reset passwords for worker accounts. |
| Dashboard | Live KPIs: orders today, revenue today, average order value, pending count; hourly orders bar chart. |
| Admin multi-language UI | Full UI in Russian, English, and Kazakh; RU / EN / KK switcher in the sidebar. |

### Backend & Infrastructure

| Feature | Description |
|---|---|
| Clean Architecture | Separate Domain / Application / Infrastructure / WebApi layers. |
| JWT auth | Role-based JWT bearer tokens; BCrypt PIN hashing for customers. |
| SignalR hub | Live order notifications pushed to admin clients on every order event. |
| Rate limiting | Per-IP limits on auth endpoints. |
| Order archival | Orders older than 90 days are archived; job runs nightly. |
| EF Core migrations | SQL Server via EF Core with full migration history. |
| Data seeding | Sample locations, menu items, toppings, and admin accounts seeded on first run. |
| Serilog | Structured request logging and developer-friendly error output. |
| Swagger | Full OpenAPI documentation available at `/swagger`. |

---

## Architecture

```
altyncup/
├── backend/
│   └── src/
│       ├── Yurt.Domain/          # Entities, enums, value objects
│       ├── Yurt.Application/     # Services, interfaces, DTOs, helpers
│       ├── Yurt.Infrastructure/  # EF Core, JWT, BCrypt, SignalR hubs
│       └── Yurt.WebApi/          # Controllers, middleware, startup
└── frontend/
    └── projects/
        ├── yurt-customer/        # Customer mobile-first Angular app
        ├── yurt-admin/           # Admin desktop-first Angular app
        ├── shared-models/        # TypeScript interfaces and enums
        ├── shared-api/           # API service, auth state, SignalR service
        └── shared-ui/            # Shared UI components, pipes, utilities
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core, Clean Architecture |
| Database | SQL Server, EF Core 10, code-first migrations |
| Auth | JWT Bearer, BCrypt, role-based policies |
| Real-time | ASP.NET Core SignalR |
| Frontend | Angular 17+, Tailwind CSS, standalone components, signals |
| State | Angular signals + computed |
| Logging | Serilog |
| API docs | Swagger / OpenAPI |

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server, SQL Server Express, or LocalDB
- [Node.js 18+](https://nodejs.org/)
- Angular CLI: `npm install -g @angular/cli`

### Backend

1. Set the connection string and JWT secret in `backend/src/Yurt.WebApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=YurtDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Secret": "CHANGE_THIS_TO_A_STRONG_SECRET_KEY_AT_LEAST_32_CHARS",
    "Issuer": "yurt-api",
    "Audience": "yurt-clients",
    "ExpiryDays": "7"
  },
  "AllowedOrigins": ["http://localhost:4200", "http://localhost:4300"]
}
```

2. Restore and migrate:

```bash
cd backend
dotnet restore
dotnet ef database update --project src/Yurt.Infrastructure --startup-project src/Yurt.WebApi
```

3. Run:

```bash
dotnet run --project src/Yurt.WebApi
```

API: `http://localhost:5000` — Swagger: `http://localhost:5000/swagger`

### Frontend

```bash
cd frontend
npm install

# Customer app (port 4200)
ng serve yurt-customer --port 4200

# Admin app (port 4300)
ng serve yurt-admin --port 4300
```

### Production Build

```bash
# Backend
cd backend
dotnet publish src/Yurt.WebApi/Yurt.WebApi.csproj -c Release -o publish

# Frontend
cd frontend
ng build yurt-customer --configuration production
ng build yurt-admin --configuration production
```

---

## Default Accounts

### Admin

| Username | Password | Role |
|---|---|---|
| admin | Admin@123! | Admin |
| worker1 | Worker@123! | Worker |

### Customers

Customers self-register in the customer app with a mobile number and 4-digit PIN.

---

## Additional Docs

- `docs/setup.md` — local setup
- `docs/deployment-guide.md` — hosting strategy
- `docs/production-deployment.md` — production plan and budget
- `docs/DEPLOYMENT.md` — conversion and deployment guide

# Yurt Project Setup

How to run the whole project on your own computer. The short way is `./scripts/dev.sh`; the long way (your own .NET
SDK) is below. Server settings for a real deployment are in [live-server-setup.md](live-server-setup.md) and
[runbook.md](runbook.md).

## 1. The short way (recommended)

Needs Docker (with the compose plugin) and Node.js 20+. The .NET SDK is not needed, the API builds in Docker.

```bash
./scripts/dev.sh          # PostgreSQL + API + customer app + admin panel + Yurt POS
./scripts/dev.sh stop     # stop everything
./scripts/dev.sh reset    # stop and delete the local database
./scripts/dev.sh phone    # let a phone on the same Wi-Fi open the customer app
YURT_DEMO_DATA=false ./scripts/dev.sh   # start with an empty cafe instead of demo data
```

| What | URL |
|---|---|
| Customer app | http://localhost:4200 |
| Admin panel | http://localhost:4300 |
| Yurt POS (till) | http://localhost:4400 |
| API + Swagger | http://localhost:8080, http://localhost:8080/swagger |

Local logins (each must choose a new password at the first sign-in): café owner `ceo` / `ceo12345`, café admin
`admin` / `admin12345`, platform operator `superadmin` / `superadmin12345`. The demo data adds more staff and
customers, see [demo-data.md](demo-data.md).

## 2. The long way (own .NET SDK)

### 2.1 Prerequisites

- .NET 10 SDK (`dotnet --version`)
- PostgreSQL 16 (for example `docker compose up -d postgres`)
- Node.js 20+ and `npm`
- Angular CLI 21 is optional, `npx ng ...` works without installing it

### 2.2 Environment variables

Set these instead of editing `appsettings.json`:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=YurtDb;Username=postgres;Password=postgres"
export Jwt__Secret="any-random-string-of-at-least-32-characters"
export SEED_ADMIN_USERNAME=admin SEED_ADMIN_PASSWORD=admin12345
export SEED_CEO_USERNAME=ceo SEED_CEO_PASSWORD=ceo12345                    # see roles.md
export SEED_SUPERADMIN_USERNAME=superadmin SEED_SUPERADMIN_PASSWORD=superadmin12345
export SEED_DEMO_DATA=true                                                 # optional, see demo-data.md
```

### 2.3 Run the API

Migrations run by themselves at start-up, so there is no separate `dotnet ef` step.

```bash
cd backend
dotnet run --project src/Yurt.WebApi
```

Swagger is at the API address followed by `/swagger` in Development mode.

### 2.4 Run the apps

```bash
cd frontend
npm install
npx ng serve yurt-customer --port 4200
npx ng serve yurt-admin --port 4300
npx ng serve yurt-pos --configuration local --port 4400
```

## 3. Useful commands

```bash
cd frontend
npx ng build yurt-customer --configuration production
npx ng build yurt-admin --configuration production
npx ng build yurt-pos --configuration production
npx ng test shared-ui --watch=false         # frontend unit tests (CI does not run these)
node --test scripts/*.test.mjs              # script and translation checks

cd ../backend
dotnet test                                 # integration tests need Docker
```

## 4. Notes

- The backend allows the local app addresses by default. For a real host, set `AllowedOrigins__0..n`.
- A new database migration must be additive only, see [runbook.md](runbook.md#migration-rules).
- After the first sign-in as Admin or CEO, set your manager PIN under *Settings → Manager PIN* (see [roles.md](roles.md)).

# Yurt runbook

Short, practical procedures for running the platform. Production currently runs on Railway
(`railway.toml`, `dockerfile`); the customer and admin apps are static builds (Vercel).

## Run everything locally

```bash
./scripts/dev.sh          # Postgres + API in Docker, customer and admin apps via ng serve
./scripts/dev.sh stop
./scripts/dev.sh reset    # also deletes the local database
```

| What | URL |
|---|---|
| Customer app | http://localhost:4200 |
| Admin panel | http://localhost:4300 (`admin` / `admin12345`, you must change it at first login) |
| API + Swagger | http://localhost:8080, http://localhost:8080/swagger |

Needs Docker with compose and Node 20+. The .NET SDK is not needed. Phone verification is off locally
(`Otp__Required=false`), so registration returns the account straight away.

## Staging environment

Staging is a full copy of production with its own data, so a migration or a new module can be tried
without touching a live cafe. Nothing in the repo may point staging at production resources.

Checklist (one-time, done in the hosting dashboard):
1. Second backend service built from the same repo and `dockerfile`, branch `staging` (or manual deploys).
2. Its **own** Postgres database. Never share the production `DATABASE_URL`.
3. Environment variables (same names as production, different values):
   `DATABASE_URL`, `Jwt__Secret` (new random 32+ chars), `SEED_ADMIN_USERNAME` / `SEED_ADMIN_PASSWORD`,
   `Secrets__MasterKey` (Base64 of 32 random bytes, e.g. `openssl rand -base64 32`; encrypts per-cafe secrets, keep it safe: losing it makes stored secrets unreadable),
   `AllowedOrigins__0..n` (staging frontend URLs), `Otp__Required=false` unless a test OTP channel exists,
   and optionally `Alerts__TelegramBotToken` / `Alerts__TelegramChatId` with `Alerts__ServerName=staging` (see `monitoring.md`).
4. Integrations in sandbox mode only: payments (Stripe test keys `sk_test_...` per cafe; `Stripe__AllowLiveKeys` left off),
   iiko test organisation (or `Iiko` disabled), a separate Firebase project (`Firebase__Enabled=true` only if
   you want to test pushes with test devices), and `Webkassa__BaseUrl=https://devkkm.webkassa.kz` (Webkassa's
   test server; production uses the default live server).
5. Two frontend deployments (customer and admin) built with `apiUrl` of the staging backend.

Every release goes to staging first. Promote to production only after the smoke test below passes.

## Smoke test (staging and after every production deploy)

1. `GET /health/ready` returns `Healthy` (server and database answer).
2. Admin login works; the menu loads in the admin panel.
3. Customer app: browse the menu, add to cart, place an order (sandbox payment or cash).
4. The order appears live in the admin panel and its status can be changed.
5. API logs show no new `ERR` lines, and the alerts chat got "server started" and no errors (see `monitoring.md`).

## Backup

Always before a deploy that contains a migration (a new file under `backend/src/Yurt.Infrastructure/Migrations`).

```bash
pg_dump --format=custom --no-owner "$DATABASE_URL" > yurt-$(date +%F-%H%M).dump
```

Keep the dump somewhere other than the hosting provider. Verify it once in a while by restoring it into a scratch
database: `pg_restore --no-owner -d "<scratch-url>" yurt-....dump`.

## Deploy order

1. CI is green on the branch (backend tests, frontend builds, migration guard).
2. Back up the production database if the release has a migration.
3. Deploy to staging, run the smoke test.
4. Deploy the **backend** to production first. Migrations run automatically at start-up and are additive, so the
   currently released apps keep working.
5. Deploy the frontends (admin, customer) afterwards.
6. Mobile apps: submit to the stores last (see `deploy-android-playmarket.md` and `deploy-ios-appstore.md`). Old
   app builds must keep working against the new backend; use the forced-update minimum version only for
   breaking changes.
7. Run the smoke test against production.

## Rollback

- **Code only (no migration in the release):** redeploy the previous backend / frontend build.
- **Release with a migration:** because migrations are additive, redeploying the previous backend build is safe and
  the new tables/columns are simply unused. Do not roll the schema back by hand.
- **Data damage:** restore the pre-deploy dump into a new database, point `DATABASE_URL` at it, redeploy, then
  investigate. Accept that orders placed after the dump are lost, so export them first if possible.

## Migration rules

- Additive only: new tables and columns, no drops or renames in `Up()`. CI runs
  `scripts/check-migrations.sh` on every pull request and fails otherwise.
- To remove something, do it over two releases: first stop using it, later drop it (add
  `// migration-guard: allow` on that line and say so in the pull request).
- New columns need a default or must be nullable so the previous backend can still insert rows.

## Platform operator (SuperAdmin)

The `SuperAdmin` role opens the **Platform** page in the admin panel: module licenses (on/off, expiry, trial/paid, notes),
maintenance mode, integration status (configured yes/no, never the values) and app releases. SuperAdmins have every
admin permission, are hidden from the cafe's staff page, and can only be created out-of-band.

- Create the first one by setting `SEED_SUPERADMIN_USERNAME` and `SEED_SUPERADMIN_PASSWORD` (12+ characters) on the
  backend and restarting. It is only created while no SuperAdmin exists; the password must be changed at first login.
  Remove the variables afterwards.
- A lost SuperAdmin password: delete that row in `AdminUsers` (or reset it with SQL), then seed again.
- **Maintenance mode** answers customer API calls with `503 { code: "maintenance", message }`; the apps show the message.
  Staff (`/admin`), `/platform`, `/app` (config, update check) and payment/POS webhooks keep working, so orders in
  progress can still be finished. Switch it on before risky deploys.
- Every change made on the Platform page is written to the audit log with the operator's name.

## Secrets and per-cafe files

Never commit: `google-services.json`, `GoogleService-Info.plist`, signing keys, `.env*`, production appsettings
(all already in `.gitignore`). Per-cafe values live in the hosting provider's environment or, for native builds, in
`cafes/<name>/secrets/` on the build machine (git-ignored).

## Branded app builds

Each cafe with its own store app has a folder `cafes/<slug>/` (see `cafes/README.md`): `cafe.json` holds the app id,
name and version, and the git-ignored `secrets/` folder holds that cafe's Firebase files.

```bash
node scripts/apply-cafe.mjs mycafe --generate # app id, name, version, Firebase files and icons/splash into android/ and ios/
cd frontend && npm run build && YURT_CAFE=mycafe npx cap sync
```

Do this on the build machine or CI checkout, because the script rewrites tracked native files. Icons and splash are generated from `cafes/mycafe/assets/icon-only.png` and `splash.png`. Still manual per
cafe: signing keys and the store listing.

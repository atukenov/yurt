# Yurt: modular, white-label platform — TODO

Source of truth for converting the single-cafe Altyncup code into a platform where each
optional feature is a paid **module** and each cafe gets its own **brand**.
Decisions and background: https://github.com/atukenov/altyncup/issues/52 (and its
"Decisions" comment). Tick items off as they ship.

## Decisions already made
- Keep the module list below (Engagement and Staff & audit stay separate add-ons).
- Sell **bundles + add-ons** (Starter / Pro / Business) with usage-based extras.
- **Manual invoicing** first; the owner sets expiry dates by hand. No billing integration yet.
- White-label: shared app with per-cafe colors/logo by default; **own store app only for premium**.
- Cancellation: keep all data, switch paid modules off after a grace period.
- Super-admin tools live **inside the admin panel** (`/platform`), `SuperAdmin` role only.
- **Own POS instead of iiko:** the platform should not depend on a third-party POS. iiko stays only as an optional
  adapter (`iiko_sync`) for cafes that already use it; everything iiko gives us today is rebuilt as a self-developed
  module (`own_pos`, Phase 6). Business logic talks to our own interfaces, never to iiko directly.

## Ground rules (so a live cafe never breaks)
- Everything **defaults to on**; if the module lookup fails or the table is empty, treat all modules as enabled (fail-open).
- Migrations are **additive only** (new tables/columns; no renames or drops) so a previous deploy can still run against the migrated database.
- Old app builds must keep working: new endpoints are additive, a missing config means "everything enabled".
- Gate **one module per release**. The foundation release must change no behavior.
- Try everything on a **staging** service with its own database first; back up the production database before any deploy that contains a migration.

---

## Phase 0 — Repository and environments
- [x] Decide the platform name and replace "Altyncup" in code and docs (about 120 mentions in ~40 files, e.g. `capacitor.config.ts`, Android `strings.xml`, iOS `Info.plist`, admin login/shell, OTP message template, docs). _(Done: the platform is **Yurt**. Remaining mentions are the persisted loyalty comment prefix `Altyncup order ` in `LoyaltyService`, kept so existing iiko transactions still match.)_
- [ ] Replace the `com.yurt.app` bundle id / merchant id defaults with a placeholder that each cafe overrides. _(Partly: `capacitor.config.ts` reads `YURT_APP_ID` / `YURT_APP_NAME`. Android package and iOS bundle id are generated per cafe in Phase 4. `com.yurt.app` stays as the platform default.)_
- [x] Remove tracked files that should not be in git: iOS `GoogleService-Info.plist` (per-cafe secret-ish config), anything else cafe-specific; extend `.gitignore`.
- [ ] Create a **staging** environment (own backend service + own database + own Firebase/iiko/payment sandbox settings). _(Checklist in `docs/runbook.md`; the hosted service itself still has to be created in the hosting dashboard.)_
- [x] CI: keep backend unit + integration tests and frontend builds green on the new repo; add a check that fails if a new migration drops or renames anything.
- [x] Write a short runbook: backup, deploy order, rollback. _(`docs/runbook.md`)_

## Phase 1 — Module foundation (no behavior change)
- [x] `ModuleLicense` table: module key, enabled, valid-until (nullable), source (manual/trial/paid), notes, timestamps.
- [x] Migration seeds **every module enabled** for the existing cafe. _(Done by `DbSeeder` at start-up: it adds a row for every known key that is missing, enabled and without expiry; the migration itself only creates the table.)_
- [x] `IModuleGate` service with a small in-memory cache and fail-open behavior; unit tests for the fail-open case.
- [x] `[RequireModule("key")]` attribute/filter returning `403` with `{ code: "module_disabled", module: "..." }`.
- [x] Background jobs (loyalty retry, iiko order push, order timers, archival) check the gate before working. _(Loyalty retry checks `loyalty`, iiko push retry checks `iiko_sync`. Order timers and archival belong to `core`, which can never be off.)_
- [x] Public `GET /api/v1/app/config`: enabled module keys + branding values (branding arrives in Phase 4; cached on the client with the existing stale-while-revalidate cache).
- [x] Customer app and admin: `ModuleService` (`isEnabled('key')`), route guard that redirects away from disabled modules. _(`ModuleService` and `moduleGuard('key')` in `shared-api`; no route uses the guard yet, that happens per module in Phase 3.)_
- [x] Admin **Subscription** page (read-only): enabled modules, expiry dates, how to upgrade.
- [x] Grace period logic: an expired license keeps working for N days with a warning, then behaves as disabled. Never delete data.

## Phase 2 — Super admin
- [x] Add `AdminRole.SuperAdmin` and a `SuperAdminOnly` policy.
- [x] Create super-admin accounts out-of-band (environment variable seed or CLI), not through the workers page. _(Decision: environment variable seed, `SEED_SUPERADMIN_USERNAME` / `SEED_SUPERADMIN_PASSWORD`, see `docs/runbook.md`.)_
- [x] `/platform` section in the admin app (lazy-loaded; every endpoint enforced server-side): toggle modules, set trial/expiry/notes, see dependency warnings.
- [x] Move **App releases** (forced-update minimums, release notes) under `/platform`. _(API now at `/api/v1/platform/app-releases`, page at `/platform/app-releases`; the old `/app-releases` URL redirects.)_
- [x] Maintenance-mode switch.
- [x] Show whether integration secrets are configured (never their values).
- [x] Every change is written to the audit log with the actor.

## Phase 3 — Gate the modules (one per release, each with tests)
Module keys and what they cover:

| Key | Name | Covers |
|---|---|---|
| `core` | Core (always on) | Menu, ordering and live status, cancel before accept, customer accounts, 1 location, admin orders, 2 staff accounts, EN/RU/KK, working hours and closed notice, favorites |
| `multi_location` | Multi-location | More than one branch, per-branch menu availability and hours |
| `marketing` | Marketing | Promotions/banners, discount codes |
| `loyalty` | Loyalty | Bonus points earn/spend/hold, balance, history |
| `iiko_sync` | iiko POS sync (optional adapter) | Order push, terminal-group routing, product/size mapping. Only for cafes that already run iiko |
| `own_pos` | Own POS (self-developed) | Replaces iiko: kitchen/cashier flow, stock and stop-list, native loyalty wallet, payment types, shifts and reports (see Phase 6) |
| `push` | Push notifications | Remote order-status pushes (FCM/APNs) |
| `analytics` | Analytics and reports | Dashboard KPIs, trends, reports |
| `group_orders` | Group orders | Shared carts and join links |
| `engagement` | Engagement | Ratings, achievements, 2GIS directions |
| `staff_audit` | Staff and audit | Extra staff accounts/roles, full audit log |
| `online_payments` | Online payments | Card/Kaspi/Apple Pay providers |
| `verification` | Phone verification | OTP channel (WhatsApp/SMS/Firebase) |
| `branding` | Branding / white-label app | Own colors, logo, store-listed app |

Indicative prices (placeholders, validate locally): Core 15–25k ₸/mo, Multi-location +8–12k per extra branch,
Marketing 7–10k, Loyalty 15–25k, iiko sync 10–20k, Push 5–8k, Analytics 8–12k, Group orders 4–6k,
Engagement 3–5k, Staff & audit 4–7k, Payments and Verification usage-based, Branding one-time 150–400k ₸.
Bundles: **Starter** = Core; **Pro** = Core + Marketing + Analytics + Engagement + Push (+2 locations);
**Business** = everything except usage-based items.

Gating order (lowest risk first):
- [x] `analytics` (admin-only, no customer impact) _(details in `docs/modules.md`)_
- [x] `marketing` (promotions, discount codes) _(details in `docs/modules.md`)_
- [x] `group_orders` _(details in `docs/modules.md`)_
- [x] `engagement` (ratings, achievements, 2GIS button) _(details in `docs/modules.md`)_
- [x] `staff_audit` _(details in `docs/modules.md`)_
- [x] `multi_location` (enforce the 1-location limit in core) _(details in `docs/modules.md`)_
- [x] `push` _(details in `docs/modules.md`)_
- [x] `loyalty` (+ its background jobs) _(details in `docs/modules.md`)_
- [x] `iiko_sync` (adapter only; keep it working, no new features) _(details in `docs/modules.md`)_
- [ ] `own_pos` (gated as it ships, see Phase 6)
- [x] `online_payments` _(details in `docs/modules.md`)_
- [x] `verification` (builds on the `Otp:Required` switch) _(details in `docs/modules.md`)_
For each: backend `RequireModule`, hide UI, deep-link redirect, test that a disabled module returns `403`, document dependencies.

## Phase 4 — White-label design
Today the brand is hard-coded: about 49 uses of the accent color `#ffd119` and over 300 `amber-*` utility classes in the Angular apps, plus fixed texts, icons and splash.

**Design system / theming**
- [ ] Define brand **design tokens** as CSS variables: primary, primary-contrast, accent, surface, text, radius, font family (light and dark). _(Partly done: `shared-ui/src/lib/theme/brand.css` has the primary scale `--brand-50..950`, `--brand-on-primary`, `--brand-accent`; surface, text, radius, font and dark mode are still to do.)_
- [x] Replace hard-coded `#ffd119` / `amber-*` in `yurt-customer` and `yurt-admin` with the tokens (Tailwind theme mapped to the variables). _(Done: utilities are now `brand-*`, inline styles use `var(--brand-500)`, glows use `rgb(var(--brand-500-rgb) / a)`. Default values are unchanged, so nothing looks different.)_
- [ ] Brand-agnostic **component audit**: buttons, badges, cards, popups (closed notice, update sheet, 2GIS button), toasts, skeletons.
- [x] Contrast checks: auto-pick readable text color for any primary color; provide a safe fallback palette. _(Done: text on the primary follows `--brand-on-primary`; the near-black buttons (add to cart, checkout) use `--brand-on-dark`, the primary lightened until it reads on near-black; the Brand screen warns when the primary is so light or so dark that buttons or shades vanish. The "fallback" is that warning plus the Yurt default when the module is off; the save is not blocked.)_
- [ ] Optional per-cafe font (self-hosted, loaded from config).

**Brand configuration**
- [ ] `Brand` settings per cafe: display name, logo (light/dark), icon, splash, primary/accent colors, support contact, social links, welcome texts. _(Partly done: name, primary and accent colour, logo URL, icon URL, splash image URL and support contact are stored; the icon swaps the tab/home-screen icon and the splash image replaces the logo on the loading screen. Light/dark logos wait for dark mode (no dark surface shows a logo yet); the sign-in welcome line replaces the built-in tagline, and up to 6 labelled https links plus the support contact appear on the profile screen in place of the hard-coded @altyncup Instagram link, so the existing cafe adds that link in the Brand screen.)_
- [x] Serve through `GET /app/config`; apply at app start before first paint (no color flash); cache with stale-while-revalidate. _(Done: `brand` is in the app config, served only while the `branding` module is on; `BrandService` overrides the `--brand-*` variables and caches the last brand in localStorage.)_
- [x] Replace hard-coded "Altyncup" strings in UI, translations, push/OTP/WhatsApp templates, receipts and emails with `{brand}`. _(Done: apps show the brand name (login, register, shell, receipt, page title, update gate, splash logo fallback); the OTP message uses `{brand}`. Push copy has no brand name.)_
- [x] Admin page (super admin) to edit brand values with a live preview. _(Done: a Brand section on the Platform page; the preview uses scoped variables, so the real app changes only after Save.)_

**Native shells (premium own-app)**
- [ ] Per-cafe Capacitor config: app id, app name, icons and splash generated from the logo (script). _(Partly done: `cafes/<slug>/cafe.json` plus `scripts/apply-cafe.mjs` apply app id, name and version to Android and iOS, and `capacitor.config.ts` reads it with `YURT_CAFE`; icons and splash are still to do.)_
- [ ] Per-cafe secrets/config files (Firebase `google-services.json` / `GoogleService-Info.plist`, Apple merchant id) kept outside git and injected at build time. _(Partly done: the script copies the Firebase files from the git-ignored `cafes/<slug>/secrets/`; the Apple merchant id is still to do.)_
- [ ] Build matrix in CI to produce a branded Android/iOS build for one cafe from a config folder.
- [ ] Store-listing checklist (screenshots, privacy policy, Apple/Google accounts, review notes) and a release/versioning scheme per cafe.
- [ ] Decide whether branded apps use a separate bundle id per cafe or a shared one with the cafe picker.

**Quality**
- [ ] Visual regression screenshots for two sample brands (warm yellow and a dark/blue palette) on the main screens.
- [ ] Accessibility pass (contrast, tap targets) for custom palettes.

## Phase 5 — Multi-tenant (only when running several cafes in one deployment)
- [ ] `Tenant` table and `TenantId` on all tenant data; global query filters; per-tenant credentials (iiko, payments, Firebase, OTP).
- [ ] Per-tenant domains/subdomains and CORS.
- [ ] Data-isolation test suite (one cafe can never read another's data).
- [ ] Tenant provisioning flow in `/platform` (create cafe, seed menu, first admin).
- [ ] Until then: one deployment per cafe is acceptable for the first customers.

## Phase 6 — Own POS module (`own_pos`), replaces iiko
What iiko gives the platform today (found in `IIikoApiClient`, `AdminIikoController`, `IikoOrderSyncService`, the `Iiko*` columns
and the webhook controller), and the self-developed counterpart. Every row is its own release behind `own_pos`.

| iiko capability today | Own replacement |
|---|---|
| Nomenclature (products, sizes) mapped to `MenuItem.IikoProductId` / `IikoProductSizeId` | Menu is the source of truth in our database (already is); drop the mapping need, keep categories, sizes, toppings, tags |
| Terminal groups per branch (`Location.IikoTerminalGroupId`) | Branch with its own stations/terminals (cashier, kitchen, bar) and order routing rules |
| Order push to the POS (`CreateDeliveryOrderAsync`, sync status/pending action on `Order`) | Orders land directly in our kitchen/cashier flow: kitchen display (KDS) screen, ticket printing, accept/prepare/ready/handed-over statuses |
| Order status webhooks back from iiko | Not needed, statuses are changed by our own staff screens and emit the same SignalR events |
| Stop-list (out-of-stock) | Stock per item and ingredient with automatic stop-list and low-stock alerts |
| Customer wallet and loyalty (`GetCustomerByPhone`, `Topup`, `Chargeoff`, `Hold`, `CancelHold`, `IikoCustomerId/WalletId`) | Native loyalty ledger in our DB: balance, earn/spend/hold/release, expiry, referral bonuses, wallet top-up |
| Payment types | Own payment methods list per branch: cash, card, Kaspi, Apple Pay, split payments |
| Coupon series for promo codes | Native promo/coupon engine (extends existing discount codes) |
| Cashier terminal, shifts, cash drawer, X/Z reports | Cashier screen in the admin app: open/close shift, cash in/out, receipts, daily Z report |
| Reports from the POS | Sales, product mix, staff and shift reports from our own data (extends the analytics module) |

Steps:
- [ ] Introduce neutral interfaces (`IPosOrderSink`, `ILoyaltyLedger`, `IStockService`, `IPaymentMethodCatalog`) and make iiko one adapter behind them; no behavior change.
- [ ] Native loyalty ledger and migration of existing iiko balances (read-only import, then switch per cafe).
- [ ] Kitchen display and order-routing per branch.
- [ ] Stock, ingredients and automatic stop-list.
- [ ] Cashier screen, shifts, receipts and Z report.
- [ ] Printer support (network/Bluetooth thermal printers) for kitchen tickets and receipts.
- [ ] Per-cafe cut-over checklist: run both side by side, compare totals for a week, then switch off the adapter.

## Open items
- [ ] Final prices and bundle contents.
- [ ] Define the "limited core" state after a cafe stops paying (e.g. login and order history stay, new orders blocked).
- [x] How SuperAdmin accounts are created: environment variable seed (done in Phase 2).
- [ ] Data export format for departing cafes.
- [ ] `own_pos`: which hardware to support first (receipt printer model, cash drawer, tablets for the kitchen).
- [ ] `own_pos`: fiscal/legal requirements for receipts and cash registers in Kazakhstan.
- [ ] Terms of service / data ownership wording.

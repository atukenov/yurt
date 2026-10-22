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
- [ ] Define brand **design tokens** as CSS variables: primary, primary-contrast, accent, surface, text, radius, font family (light and dark). _(Partly done: `shared-ui/src/lib/theme/brand.css` has the primary scale `--brand-50..950`, `--brand-on-primary`, `--brand-accent`; the page surface, body text, font family and a corner-radius scale are now tokens too (`--surface-page`, `--text-body`, `--brand-font`, `--radius-scale`; defaults equal today's look, Tailwind `font-sans` and `rounded-lg..3xl` follow them). Corner style (square, default, rounder) and font (default Inter, system, serif, or the cafe's own font file) are settable on the Brand screen; surface and text colours are not. Dark mode is done for the customer app: a Light / Dark / Auto choice on the profile screen (default Light, saved on the device), driven by `data-theme` on `<html>`; the neutral scale, card and page surfaces flip, and the brand tints and text shade are rebuilt from the cafe's colour, so any brand works in dark. The admin app has the same choice (Light / Dark / Auto in the sidebar footer, same saved setting; the slate scale flips, the login backdrop stays dark, the logo sits on a light tile). Not done: some pale status chips (payment, warnings) that keep their light tint.)_
- [x] Replace hard-coded `#ffd119` / `amber-*` in `yurt-customer` and `yurt-admin` with the tokens (Tailwind theme mapped to the variables). _(Done: utilities are now `brand-*`, inline styles use `var(--brand-500)`, glows use `rgb(var(--brand-500-rgb) / a)`. Default values are unchanged, so nothing looks different.)_
- [x] Brand-agnostic **component audit**: buttons, badges, cards, popups (closed notice, update sheet, 2GIS button), toasts, skeletons. _(Done: the shared buttons, badges, cards and skeletons already used only brand tokens. Fixed: the update sheet's glow, the PIN box focus ring and the admin loyalty card followed the old yellow; the warning toast is now a fixed amber so a blue brand does not get a blue warning. Left on purpose as amber because they mean "warning", "rating star" or "pending" and not the brand: the rating stars, the pending-payment hints, the select-a-cafe chip and the Brand-screen colour warning.)_
- [x] Contrast checks: auto-pick readable text color for any primary color; provide a safe fallback palette. _(Done: text on the primary follows `--brand-on-primary`; the near-black buttons (add to cart, checkout) use `--brand-on-dark`, the primary lightened until it reads on near-black; the Brand screen warns when the primary is so light or so dark that buttons or shades vanish. The "fallback" is that warning plus the Yurt default when the module is off; the save is not blocked.)_
- [x] Optional per-cafe font (self-hosted, loaded from config). _(The Brand screen offers Inter, a system font or a serif font, all without a download, plus an optional link to the cafe's own font file (https .woff2/.woff/.ttf/.otf). The apps load the file and fall back to the chosen preset while it loads or if it fails. The file is hosted by the cafe or a CDN like the logo; Yurt has no file upload yet.)_

**Brand configuration**
- [ ] `Brand` settings per cafe: display name, logo (light/dark), icon, splash, primary/accent colors, support contact, social links, welcome texts. _(Partly done: name, primary and accent colour, logo URL, icon URL, splash image URL and support contact are stored; the icon swaps the tab/home-screen icon and the splash image replaces the logo on the loading screen. An optional dark mode logo URL is stored too: the customer splash and the admin login, password and sidebar show it while dark mode is on, and fall back to the normal logo when it is empty (the admin's light tile behind the logo is dropped when the dark one shows); the sign-in welcome line replaces the built-in tagline, and up to 6 labelled https links plus the support contact appear on the profile screen in place of the hard-coded @altyncup Instagram link, so the existing cafe adds that link in the Brand screen.)_
- [x] Serve through `GET /app/config`; apply at app start before first paint (no color flash); cache with stale-while-revalidate. _(Done: `brand` is in the app config, served only while the `branding` module is on; `BrandService` overrides the `--brand-*` variables and caches the last brand in localStorage.)_
- [x] Replace hard-coded "Altyncup" strings in UI, translations, push/OTP/WhatsApp templates, receipts and emails with `{brand}`. _(Done: apps show the brand name (login, register, shell, receipt, page title, update gate, splash logo fallback); the OTP message uses `{brand}`. Push copy has no brand name.)_
- [x] Admin page (super admin) to edit brand values with a live preview. _(Done: a Brand section on the Platform page; the preview uses scoped variables, so the real app changes only after Save.)_

**Native shells (premium own-app)**
- [x] Per-cafe Capacitor config: app id, app name, icons and splash generated from the logo (script). _(Done: `cafes/<slug>/cafe.json` plus `scripts/apply-cafe.mjs` apply app id, name and version to Android and iOS, and `capacitor.config.ts` reads it with `YURT_CAFE`; icons and splash come from the cafe's `assets/` folder through `--generate`, which runs `@capacitor/assets`. Generating them straight from the Brand logo URL is not done: the source images are supplied per cafe.)_
- [x] Per-cafe secrets/config files (Firebase `google-services.json` / `GoogleService-Info.plist`, Apple merchant id) kept outside git and injected at build time. _(Done: the script copies the Firebase files from the git-ignored `cafes/<slug>/secrets/`; the Apple merchant id is a field in `cafe.json` that goes into the iOS entitlements.)_
- [ ] Build matrix in CI to produce a branded Android/iOS build for one cafe from a config folder.
- [x] Store-listing checklist (screenshots, privacy policy, Apple/Google accounts, review notes) and a release/versioning scheme per cafe. _(Done: `docs/branded-app-release.md`; the version lives in `cafe.json`.)_
- [ ] Decide whether branded apps use a separate bundle id per cafe or a shared one with the cafe picker.

**Quality**
- [ ] Visual regression screenshots for two sample brands (warm yellow and a dark/blue palette) on the main screens. _(Partly done: `frontend/scripts/brand-screenshots.mjs` takes login and register screenshots for three sample brands (Yurt, a blue and a dark green one) in headless Chromium, with the brand seeded through the app's own cache. It found that the gradient ends were still hard-coded orange; that is fixed with the `accent-*` tokens. The menu, cart and two order status screens are now covered too: the script signs in a fake user, fills the cart and answers the API calls with fake data, so no backend is needed. Profile and automatic image comparison are still to do.)_
- [x] Accessibility pass (contrast, tap targets) for custom palettes. _(Done: `brand-screenshots.mjs --a11y` runs axe-core's colour-contrast rule per brand and screen. Sign-in and register now pass for the blue, green and pale sample brands: text on the pale-brand header follows `--brand-hero-text`, outline-button text uses `--brand-ink` (the primary darkened to contrast 4.5), and the grey helper text is darker. The Yurt yellow text on light backgrounds (prices, links, the "Create account" button) is now a darker gold (`#80680c`, contrast 5.2), so every sample brand passes. Text on light backgrounds across the customer app (prices, links, icons) now uses `--brand-ink`, and brand text on the dark profile card uses `--brand-on-dark`; the default look is unchanged because both default to the old colours. The admin app got the same change. The same run now also checks tap-target size (axe's 24px rule); sign-in and register pass for every sample brand. Tap targets do not depend on the brand colour, so the remaining work is running both checks on the screens that need the backend (menu, cart, profile, admin) Now also run on the menu, cart and order status screens (signed in, fake API data), for all four sample brands in light and dark mode, and in CI on every pull request (`--fail` stops the build on any failure). Fixed what it found: grey labels in the bottom bar and on the cart and order screens were too light, the active tab used the raw brand colour, the reached steps of the order timeline were invisible (white on a light page), the favourite heart was 22px, and the green, teal, amber and red status texts were too light (new `--tone-*` colours that switch in dark mode). axe skips text on gradients, so the script also measures the brand header text (`text-brand-hero`) on both gradient ends; the menu header now follows `--brand-hero-text` like sign-in. The default Yurt look keeps white text on its yellow header on purpose (contrast 1.5); the script prints it but does not fail. The profile screen and the admin app are now checked the same way (`--app=admin` opens sign-in, orders, kitchen, cashier, menu, stock and customers on a tablet-sized screen with fake data), also in CI. Fixed what they found: on profile, the light grey and see-through header and bonus-card texts, the inactive language and theme buttons and the red sign-out text; in admin, the light grey helper text (`text-slate-400` to `text-slate-500` everywhere), the red texts (now `--tone-red`), selected buttons on the pale brand tint (`bg-brand-50` instead of `bg-brand-100`), grey text on grey chips, and the yellow badge, which turned dark-on-dark in dark mode. In admin dark mode the slate greys from 400 up now skip one step, like the stone greys, so grey text stays readable on dark cards. Other admin screens (analytics, promotions, workers and so on) got the same class changes but are not opened by the check.)_

## Phase 5 — Multi-tenant (only when running several cafes in one deployment)
- [ ] `Tenant` table and `TenantId` on all tenant data; global query filters; per-tenant credentials (iiko, payments, Firebase, OTP).
  Approach (Lev chose shared database, step by step, 2026-10-16): each step is its own PR and the current cafe keeps working unchanged.
  - [x] `Tenant` table and one seeded default tenant (fixed id `Tenant.DefaultId`); no table is filtered yet.
  - [x] Request-to-tenant resolution by host name (`TenantMiddleware`, `ITenantContext`; unknown host, inactive tenant or a database error fall back to the default tenant). Still to do: the signed-in user's tenant, once users carry a `TenantId`.
  - [ ] `TenantId` (not null, existing rows get the default tenant) plus global query filter, one table group per PR:
    - [x] Menu and locations (`Location`, `MenuCategory`, `MenuItem`, `MenuTopping`, `MenuItemVariant`, `MenuItemLocation`, `MenuToppingCategory`). New rows are stamped from `ITenantContext` on save. 
    - [x] Orders, payments, group carts, promotions and favorites (`Order`, `OrderItem`, `OrderItemTopping`, `Payment`, `PaymentWebhookLog`, `GroupCart*`, `Promotion`, `Favorite`). The startup warnings about required navigations are gone. Payment webhooks resolve the cafe by host name like any request; giving each cafe its own webhook address and credentials is part of the per-tenant credentials step.
    - [x] Users and admin data (`CustomerUser`, `PhoneVerification`, `AdminUser`, `RefreshToken`, `CustomerDeviceToken`, `AuditLog`, `DiscountCode`, `UserReport`, `AppRelease`). Phone number, discount code and release version are now unique per cafe (the same person can join two cafes); admin usernames stay globally unique. SuperAdmin accounts are visible from every cafe's host. Sign-in, refresh and checkout all find the cafe by host name, so a token from one cafe's host does not work on another's.
  - [x] Background jobs (order timers, archival, loyalty retry, iiko order retry) run once per active cafe (`TenantJobs.ForEachTenantAsync`); a failure in one cafe does not stop the others. The loyalty and iiko on/off switches are read per cafe.
  - [x] The sign-in token carries the cafe (`tenant_id` claim) and a request whose token belongs to another cafe than its host gets 401 (`TenantTokenPolicy`). Tokens issued before this change count as the default cafe; SuperAdmin works on every cafe.
  - [ ] Per-tenant settings and credentials:
    - [x] Module licenses, brand, maintenance mode and other platform settings are per cafe (`ModuleLicense` and `PlatformSetting` carry `TenantId`; the module and maintenance gates cache per cafe through `TenantAmbient`). The `/platform` screens act on the cafe of the host they are opened from. A new cafe starts with no rows, which means every module is on (fail-open) until the operator switches one off.
    - [x] Credentials: iiko, payments, Firebase, OTP (were environment variables for the whole deployment). Lev chose encrypted storage in the database (2026-10-17).
      - [x] Storage: `TenantSecret` table, AES-256-GCM with a server master key (`Secrets__MasterKey`, Base64 of 32 random bytes; bound to cafe and key), `ITenantSecrets` (the default cafe falls back to the old environment variables, every other cafe gets nothing from them), write-only `/platform/cafes/{id}/secrets` API and a Secrets panel per cafe. Without the master key nothing can be stored (503) and the first cafe is unaffected.
      - [x] Payments: the webhook check reads the cafe's own `Payment:WebhookSecret` (a cafe with none accepts no webhooks; the first cafe still uses the environment value). The sandbox payment simulator signs with that cafe's secret and sends an `X-Tenant` header so its callback is checked as the right cafe.
      - [x] iiko: its settings (key, organisation, loyalty program, payment type, webhook token) are one set for the whole server, so iiko is available to the first cafe only; every other cafe gets "iiko off" in loyalty and order push, a 404 on the iiko admin tools and a rejected iiko webhook. Without this a second cafe would have written its customers into the first cafe's iiko. Per-cafe iiko is not planned, because iiko is being replaced by the self-developed modules (an optional adapter). The iiko keys were taken off the per-cafe secrets list.
      - [x] Firebase push and WhatsApp phone codes: each cafe sends through its own Firebase service account (one cached Firebase app per cafe and credential) and its own WhatsApp (Green API) instance. A cafe without its own credentials sends nothing and never borrows the first cafe's; the first cafe keeps its environment values.
  - [x] `TenantId` is required on every table (NOT NULL, default cafe for old rows); a test fails if a new table has no cafe filter or a nullable `TenantId`.
- [x] Per-tenant domains and CORS: the cafe is picked by the `X-Tenant` slug header, then the `Origin` host, then the request host (all matched to `Tenant.Domain`), then the default; CORS also allows any active cafe domain, so a new cafe needs no redeploy. The apps send `X-Tenant` (REST and SignalR) when built with a `tenant` key (optional `tenant` in `cafes/<slug>/cafe.json`, written into each app's `environment.ts` by `apply-cafe`).
- [x] Data-isolation test suite (one cafe can never read another's data): per-area isolation tests plus a guard test over the whole model.
  - [x] Full isolation check (2026-10-22, after the Phase 6 tables): every cafe table is filtered, and background jobs, caches and print agents work per cafe. Two leaks found and fixed: (1) live order updates (SignalR) went to one shared "admins" group, so every cafe's staff got every cafe's new orders with the customer's name and phone; groups now carry the cafe. (2) A save could point a row at another cafe's row by id (for example moving a menu item into another cafe's category, which the item update did not check); the database context now refuses any link to another cafe's row and any change of a row's cafe (`CrossTenantReferenceException`, answered as 404), and the item update checks its category. `CafeDataIsolationTests` seeds a second cafe with a row in every admin-reachable table (stock, recipes, stations, printers, print jobs, shifts, discount codes, workers, ...) and proves the first cafe's admin can neither read, change nor link to any of them.
- [x] Tenant provisioning flow in `/platform`: a Cafes section lists cafes, creates one (key, name, optional web address, first admin who must change the password at first login, starting brand) and switches a cafe on or off. The default cafe cannot be switched off. Menu and locations start empty (the new admin fills them in); the operator picks which cafe the module, maintenance and brand screens act on with a select in `/platform` (sent as `X-Tenant`).
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
- [x] Introduce neutral interfaces and make iiko one adapter behind them; no behavior change.
  - [x] `IPosOrderSink` (push and close an order): `OrderService` no longer knows about iiko; `IikoOrderSyncService` is the only adapter.
  - [x] `ILoyaltyLedger` (bonus wallet: account lookup, enroll, top-up, charge-off, hold and release, history): `LoyaltyService` talks to it; `IikoLoyaltyLedger` is the iiko adapter. The wallet-link columns on the customer keep their `Iiko*` names until the native ledger replaces them.
  - [x] Stock and payment methods need no interface yet: today stock is only the manual `IsAvailable` flag on menu items and toppings (no iiko stop-list call exists), and iiko payment types are only an admin lookup in `AdminIikoController`. `IStockService` arrives with the stock step and `IPaymentMethodCatalog` with the cashier step, when there is something real behind them.
- [ ] Native loyalty ledger and migration of existing iiko balances (read-only import, then switch per cafe). Lev chose "import, then switch" (2026-10-17).
  - [x] Wallet tables (`LoyaltyAccount`, `LoyaltyEntry`; balance is the sum of entries) and `NativeLoyaltyLedger` behind `ILoyaltyLedger`, tested per cafe. Not used by any cafe yet.
  - [x] Read-only import of each customer's iiko balance into the native wallet: operator endpoints `POST /platform/loyalty/import` and `GET /platform/loyalty/compare` act on the cafe picked with `X-Tenant`. A repeat run skips customers already imported; compare lists customers whose iiko and native balances differ. Run it when no orders are in flight (iiko reports the balance without points on hold). The Platform page has a **Loyalty wallet** section with the three buttons (copy, check, switch).
  - [x] Per-cafe switch: `GET/PUT /platform/loyalty/ledger` (body `{ "mode": "iiko" | "native" }`) acts on the cafe picked with `X-Tenant` and is saved on the cafe (`Tenant.LoyaltyLedger`; empty means iiko, as before). A cafe with iiko can only move to `native` while the compare shows every balance matches (otherwise `409`); switching back to `iiko` is the way out. A cafe without iiko can use `native` straight away and then gets loyalty without iiko. Switch when no orders are in flight: a hold made in one wallet cannot be released in the other, and points earned in the own wallet are not copied back to iiko. The own wallet is now safe against two simultaneous spends. The same Platform section switches back.
- [ ] Kitchen display and order-routing per branch.
  - [x] Simple kitchen screen (`/kitchen`, gated by `own_pos`): three columns per branch, one tap per step, live updates, sound for a new accepted order. Details in `docs/modules.md`.
  - [x] Stations (Kitchen, Bar) and routing a menu category to a station; the kitchen screen can be set to one station and then shows only its items. Details in `docs/modules.md`.
  - [x] Ready per station: each station marks its own part of an order ready; the order is Ready once every station's part is. Details in `docs/modules.md`.
- [ ] Stock, ingredients and automatic stop-list.
  - [x] Stock per product per branch with the automatic stop-list (admin Stock page, orders limited to what is left, accept lowers the count, sold-out items leave the customer menu). Details in `docs/modules.md`. Also fixed the customer menu cache so it is kept per cafe.
  - [x] Ingredients and recipes: ingredient counts per branch, a recipe per dish (optionally per size), dishes leave the menu when an ingredient runs out, accepting an order lowers the counts. Details in `docs/modules.md`.
  - [x] Toppings can use ingredients (hidden from a branch's menu and refused when short, lowered on accept).
  - [x] Low-stock alerts: a badge on the Stock menu entry and a summary on the Stock page.
  - [x] Low-stock phone alerts: a push to the cafe app of up to 5 chosen phone numbers when an order takes stock down to its warn-at number or to zero (needs `push` and the cafe's Firebase).
  - [x] Restocking history: a History tab on the Stock page lists each hand change to a count (before, after, who, when).
- [ ] Cashier screen, shifts, receipts and Z report.
  - [x] Cashier page: open and close a shift with the cash counted, cash in/out, X report while open, Z report on close with the cash difference, earlier shifts, print. Details in `docs/modules.md`.
  - [x] Print a receipt per order from the order details (browser print, 80 mm layout).
  - [x] Split payments: the cashier pays one order in parts with different methods; the Z report counts each part under its own method. Details in `docs/modules.md`.
  - [x] Per-branch payment method list: tick boxes on the branch dialog; the customer app and the server follow it. Details in `docs/modules.md`.
  - [x] Official tax receipts through a cloud cash register: `IFiscalRegistrar` slot with a Webkassa adapter, off by default, per-cafe provider/skip methods/VAT and register number per branch, queue with retry, "not fiscalized" badge, fiscal Z report on shift close. Details in `docs/modules.md`. _(The Webkassa calls are not verified yet: needs Webkassa partner docs and a test account. ReKassa adapter not built.)_
  - [x] Tax receipt link for the guest: a **Tax receipt** button on the customer order screen and a QR code on receipts printed by network printers, once the receipt went through. Details in `docs/modules.md`.
  - [x] Tax QR code on the browser-printed receipt too, and an optional tick per receipt printer to print the receipt again with the QR code once the tax receipt is sent (off by default). Details in `docs/modules.md`.
- [ ] Printer support (network/Bluetooth thermal printers) for kitchen tickets and receipts.
  - [x] Kitchen ticket: a "Print ticket" button on each kitchen card (80 mm browser print, only the items of the chosen station, notes in bold, no prices).
  - [x] Network printers (ESC/POS over TCP): kitchen and receipt printers per branch, tied to a station or to all items, print by themselves on accept, test print, job list with retries. Details in `docs/modules.md`.
  - [x] Print agent that runs in the cafe, for servers in the cloud that cannot reach the cafe's network (`scripts/print-agent.mjs`, setup in `docs/print-agent.md`).
  - [x] Cash drawer on the receipt printer: opens on cash payments and with the **Open drawer** button on the Cashier page (per-printer tick). Details in `docs/modules.md`.
  - [ ] Bluetooth printers (native plugin in the tablet app).
- [ ] Per-cafe cut-over checklist: run both side by side, compare totals for a week, then switch off the adapter. _(Checklist written: `docs/cutover-from-iiko.md`. Using it on the first cafe is Lev's call.)_

## Open items
- [ ] Final prices and bundle contents.
- [ ] Define the "limited core" state after a cafe stops paying (e.g. login and order history stay, new orders blocked).
- [x] How SuperAdmin accounts are created: environment variable seed (done in Phase 2).
- [ ] Data export format for departing cafes.
- [ ] `own_pos`: which hardware to support first (receipt printer model, cash drawer, tablets for the kitchen).
- [x] `own_pos`: fiscal/legal requirements for receipts and cash registers in Kazakhstan. _(Researched: `docs/fiscal-receipts-kz.md`. Plan: cloud cash register via provider API, Webkassa first. Building it is still to do.)_
- [ ] Terms of service / data ownership wording.

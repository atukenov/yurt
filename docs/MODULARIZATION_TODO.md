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

## Ground rules (so a live cafe never breaks)
- Everything **defaults to on**; if the module lookup fails or the table is empty, treat all modules as enabled (fail-open).
- Migrations are **additive only** (new tables/columns; no renames or drops) so a previous deploy can still run against the migrated database.
- Old app builds must keep working: new endpoints are additive, a missing config means "everything enabled".
- Gate **one module per release**. The foundation release must change no behavior.
- Try everything on a **staging** service with its own database first; back up the production database before any deploy that contains a migration.

---

## Phase 0 — Repository and environments
- [ ] Decide the platform name and replace "Altyncup" in code and docs (about 120 mentions in ~40 files, e.g. `capacitor.config.ts`, Android `strings.xml`, iOS `Info.plist`, admin login/shell, OTP message template, docs).
- [ ] Replace the `com.yurt.app` bundle id / merchant id defaults with a placeholder that each cafe overrides.
- [ ] Remove tracked files that should not be in git: iOS `GoogleService-Info.plist` (per-cafe secret-ish config), anything else cafe-specific; extend `.gitignore`.
- [ ] Create a **staging** environment (own backend service + own database + own Firebase/iiko/payment sandbox settings).
- [ ] CI: keep backend unit + integration tests and frontend builds green on the new repo; add a check that fails if a new migration drops or renames anything.
- [ ] Write a short runbook: backup, deploy order, rollback.

## Phase 1 — Module foundation (no behavior change)
- [ ] `ModuleLicense` table: module key, enabled, valid-until (nullable), source (manual/trial/paid), notes, timestamps.
- [ ] Migration seeds **every module enabled** for the existing cafe.
- [ ] `IModuleGate` service with a small in-memory cache and fail-open behavior; unit tests for the fail-open case.
- [ ] `[RequireModule("key")]` attribute/filter returning `403` with `{ code: "module_disabled", module: "..." }`.
- [ ] Background jobs (loyalty retry, iiko order push, order timers, archival) check the gate before working.
- [ ] Public `GET /api/v1/app/config`: enabled module keys + branding values (cached on the client with the existing stale-while-revalidate cache).
- [ ] Customer app and admin: `ModuleService` (`isEnabled('key')`), route guard that redirects away from disabled modules.
- [ ] Admin **Subscription** page (read-only): enabled modules, expiry dates, how to upgrade.
- [ ] Grace period logic: an expired license keeps working for N days with a warning, then behaves as disabled. Never delete data.

## Phase 2 — Super admin
- [ ] Add `AdminRole.SuperAdmin` and a `SuperAdminOnly` policy.
- [ ] Create super-admin accounts out-of-band (environment variable seed or CLI), not through the workers page.
- [ ] `/platform` section in the admin app (lazy-loaded; every endpoint enforced server-side): toggle modules, set trial/expiry/notes, see dependency warnings.
- [ ] Move **App releases** (forced-update minimums, release notes) under `/platform`.
- [ ] Maintenance-mode switch.
- [ ] Show whether integration secrets are configured (never their values).
- [ ] Every change is written to the audit log with the actor.

## Phase 3 — Gate the modules (one per release, each with tests)
Module keys and what they cover:

| Key | Name | Covers |
|---|---|---|
| `core` | Core (always on) | Menu, ordering and live status, cancel before accept, customer accounts, 1 location, admin orders, 2 staff accounts, EN/RU/KK, working hours and closed notice, favorites |
| `multi_location` | Multi-location | More than one branch, per-branch menu availability and hours |
| `marketing` | Marketing | Promotions/banners, discount codes |
| `loyalty` | Loyalty | Bonus points earn/spend/hold, balance, history |
| `iiko_sync` | iiko POS sync | Order push, terminal-group routing, product/size mapping |
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
- [ ] `analytics` (admin-only, no customer impact)
- [ ] `marketing` (promotions, discount codes)
- [ ] `group_orders`
- [ ] `engagement` (ratings, achievements, 2GIS button)
- [ ] `staff_audit`
- [ ] `multi_location` (enforce the 1-location limit in core)
- [ ] `push`
- [ ] `loyalty` (+ its background jobs)
- [ ] `iiko_sync`
- [ ] `online_payments`
- [ ] `verification` (builds on the `Otp:Required` switch)
For each: backend `RequireModule`, hide UI, deep-link redirect, test that a disabled module returns `403`, document dependencies.

## Phase 4 — White-label design
Today the brand is hard-coded: about 49 uses of the accent color `#ffd119` and over 300 `amber-*` utility classes in the Angular apps, plus fixed texts, icons and splash.

**Design system / theming**
- [ ] Define brand **design tokens** as CSS variables: primary, primary-contrast, accent, surface, text, radius, font family (light and dark).
- [ ] Replace hard-coded `#ffd119` / `amber-*` in `yurt-customer` and `yurt-admin` with the tokens (Tailwind theme mapped to the variables).
- [ ] Brand-agnostic **component audit**: buttons, badges, cards, popups (closed notice, update sheet, 2GIS button), toasts, skeletons.
- [ ] Contrast checks: auto-pick readable text color for any primary color; provide a safe fallback palette.
- [ ] Optional per-cafe font (self-hosted, loaded from config).

**Brand configuration**
- [ ] `Brand` settings per cafe: display name, logo (light/dark), icon, splash, primary/accent colors, support contact, social links, welcome texts.
- [ ] Serve through `GET /app/config`; apply at app start before first paint (no color flash); cache with stale-while-revalidate.
- [ ] Replace hard-coded "Altyncup" strings in UI, translations, push/OTP/WhatsApp templates, receipts and emails with `{brand}`.
- [ ] Admin page (super admin) to edit brand values with a live preview.

**Native shells (premium own-app)**
- [ ] Per-cafe Capacitor config: app id, app name, icons and splash generated from the logo (script).
- [ ] Per-cafe secrets/config files (Firebase `google-services.json` / `GoogleService-Info.plist`, Apple merchant id) kept outside git and injected at build time.
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

## Open items
- [ ] Final prices and bundle contents.
- [ ] Define the "limited core" state after a cafe stops paying (e.g. login and order history stay, new orders blocked).
- [ ] How SuperAdmin accounts are created (environment variable vs CLI).
- [ ] Data export format for departing cafes.
- [ ] Terms of service / data ownership wording.

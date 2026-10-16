# Modules: what each gate covers

Reference for the paid modules once they are gated (Phase 3 of `MODULARIZATION_TODO.md`). One module is gated per
release. A disabled module answers `403 { code: "module_disabled", module: "<key>" }`; its screens disappear from the
apps; its data is never deleted. Everything is on by default (fail-open).

How a gate is built:
1. Backend: `[RequireModule(ModuleKeys.X)]` on the controller or action (`Yurt.WebApi/Common/RequireModuleAttribute.cs`).
   Background jobs ask `IModuleGate.IsEnabledAsync` before working.
2. Frontend: `moduleGuard('key', '/fallback')` on the route and `modules.isEnabled('key')` around the menu entry.
3. Test: switching the module off through `/api/v1/platform/modules/{key}` gives `403` and switching it back on restores access.
4. Document it here.

## Gated

### `analytics` (admin only, no customer impact)
- **Covers:** `GET /api/v1/admin/analytics`, `GET /api/v1/admin/dashboard`; the admin **Analytics** page and menu entry.
- **When off:** the page redirects to Orders, the menu entry is hidden, both endpoints return `403 module_disabled`.
- **Does not cover:** the customer-report moderation page (`/admin/reports`, `ReportsController`), which belongs to `core`.
- **Depends on:** nothing.

### `marketing` (promotions and discount codes)
- **Covers:** `/api/v1/promotions/*`, `/api/v1/discount-codes/*`, `/api/v1/admin/promotions/*`, `/api/v1/admin/discount-codes/*`; the admin **Promotions** and **Discount codes** pages and menu entries; in the customer app the promo popup, the promotions strips on Menu and News, and the code field in the cart.
- **When off:** those endpoints return `403 module_disabled`, admin routes redirect to Orders, the customer UI hides the promo surfaces, and a code already applied in the cart is cleared.
- **Checkout is unaffected:** `POST /orders` ignores a `discountCode` while the module is off and creates the order at full price (no error).
- **Existing data:** promotions, codes and the discount recorded on past orders stay as they are and show again when the module is back on.
- **Depends on:** nothing.

### `group_orders` (shared carts and join links)
- **Covers:** every `/api/v1/group-orders/*` endpoint (create, join, view, add/remove item, checkout); in the customer app the `group-order/*` routes.
- **When off:** endpoints return `403 module_disabled` and the routes redirect to the cart. The "Start/Join group order" links in the cart are currently commented out in the template; when they are re-enabled they must be wrapped in `modules.isEnabled('group_orders')`.
- **Checkout is unaffected:** regular orders do not touch group carts. A group cart created before the module was turned off cannot be continued until it is back on.
- **Depends on:** nothing.

### `engagement` (ratings, achievements, 2GIS directions)
- **Covers:** `POST /api/v1/orders/{id}/rating`; in the customer app the rating card on a completed order, the achievements route, the achievements tiles on News and Profile, and the 2GIS "route" button on an accepted order; in the admin app the rating shown on an order and the 2GIS link field on a location.
- **When off:** the rating endpoint returns `403 module_disabled`, `/achievements` redirects to News, and the UI above is hidden. Achievements are computed in the app from the customer's own stats, so there is no endpoint to gate.
- **Existing data:** ratings already given and each location's stored 2GIS link are kept and show again when the module is back on. The location API still accepts and returns the 2GIS link; only the UI is hidden.
- **Depends on:** nothing.

### `staff_audit` (staff accounts and the audit log)
- **Covers:** `/api/v1/admin/workers/*` (create, edit, activate/deactivate staff accounts) and `GET /api/v1/admin/audit-log`; the admin **Staff** and **Audit log** pages and menu entries.
- **When off:** those endpoints return `403 module_disabled`, the pages redirect to Orders and the menu entries are hidden.
- **Keeps working:** existing staff accounts can still sign in and work orders (the module only covers managing accounts). The audit log keeps being written in the background so the history is complete when the module is switched back on; only reading it is gated.
- **Depends on:** nothing.

### `multi_location` (more than one branch)
- **Core includes one location.** This is the only module that limits instead of blocking a feature, because locations are part of ordering.
- **When off:** `POST /api/v1/admin/locations` returns `403 module_disabled` once one location exists; the admin **Add location** button is replaced by a note; `GET /api/v1/locations` (customer app) returns only the first active location (by name).
- **Kept working:** viewing and editing locations, working hours, the closed notice, and ordering for the first location. Extra branches that already exist are **not deleted or deactivated**, and their menu availability and orders are untouched; they simply are not offered to customers. A customer who selected another branch earlier can still finish an order there until they pick again.
- **When it comes back on:** all active branches are offered again.
- **Depends on:** nothing.

### `push` (remote push notifications)
- **Covers:** remote order-status pushes (FCM/APNs). `PushNotificationService` sends nothing while the module is off; in the customer app, push registration is not started and the notifications switch on Profile is hidden.
- **Not an endpoint gate:** `POST /api/v1/auth/device-tokens` keeps accepting tokens (it never returns `module_disabled`), so devices that registered earlier, or an older app build, are not broken and pushes resume as soon as the module is back on. The module is an on/off for *sending*.
- **Keeps working:** order updates for an open app over SignalR and the in-app/local notifications (core), and all ordering.
- **Depends on:** nothing. Firebase credentials are still configured separately (`Firebase:Enabled`); the module is the commercial switch on top.

### `loyalty` (bonus points: earn, spend, hold, balance, history)
- **Covers:** `LoyaltyService` new activity: balance, offsite history, earning on completion, and placing a new hold when a customer pays with bonuses. In the customer app the bonus balance on Profile, the bonuses tile on News and the "pay with bonuses" row in the cart; in the admin app the customer's bonus balance and the bonuses column.
- **How it switches off:** like `Loyalty` being unconfigured, the API answers `enabled: false` instead of an error (the apps already hide everything for `enabled: false`, and `GET /loyalty/*` is read in the background, so a 403 would only produce noise). It is a second switch on top of `Iiko:Enabled`; both must be on.
- **Background jobs:** the loyalty retry job checks the gate and does nothing while the module is off (done in Phase 1). Nothing is deleted.
- **Never strands points:** finalising or releasing a hold that already exists ignores the module, so switching it off mid-order cannot leave points reserved in iiko.
- **When it comes back on:** completed orders that earned nothing while the module was off are picked up by the existing safety-net sweep and credited, so customers are not penalised for the gap.
- **Existing data:** points spent/earned on past orders still show on those orders.
- **Depends on:** iiko adapter or, later, `own_pos` (see Phase 6); the dependency check is a warning only.

## Not gated yet
`iiko_sync`,
`online_payments`, `verification`, `own_pos`, `branding` (see the gating order in the plan).

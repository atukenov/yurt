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

## Not gated yet
`engagement`, `staff_audit`, `multi_location`, `push`, `loyalty`, `iiko_sync`,
`online_payments`, `verification`, `own_pos`, `branding` (see the gating order in the plan).

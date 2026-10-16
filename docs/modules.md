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

## Not gated yet
`marketing`, `group_orders`, `engagement`, `staff_audit`, `multi_location`, `push`, `loyalty`, `iiko_sync`,
`online_payments`, `verification`, `own_pos`, `branding` (see the gating order in the plan).

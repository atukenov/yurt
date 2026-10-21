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

### `iiko_sync` (optional iiko adapter, no new features)
- **Covers:** pushing new accepted orders to iiko (`IikoOrderSyncService.PushOrderAsync`), the iiko webhook (`/webhooks/iiko`) and the admin lookups (`/admin/iiko/*`: nomenclature, payment types, terminal groups, webhook registration). In the admin app the iiko product mapping on menu items, the terminal group on branches and the "Synced to iiko" badge on live orders are hidden.
- **How it switches off:** `403 {code:"module_disabled"}` on the endpoints; new orders are simply not pushed (they stay `NotPushed`). It is a second switch on top of `Iiko:Enabled` and `Iiko:PushOrdersEnabled`.
- **Background jobs:** the push retry job checks the gate (done in Phase 1).
- **Never leaves orders open:** closing an order that was already pushed ignores the module.
- **Existing data:** iiko ids on menu items and branches stay in the database, so switching the module back on needs no re-mapping.
- **Not covered:** loyalty talks to iiko too, but it has its own `loyalty` module.
- **Depends on:** nothing; replaced by `own_pos` in Phase 6.

### `online_payments` (Apple Pay / Freedom Pay and other online providers)
- **Covers:** starting a new online payment (`POST /payments/create`) and showing Apple Pay in the customer cart.
- **How it switches off:** `POST /payments/create` returns `403 {code:"module_disabled"}`; `GET /payments/apple-pay/config` answers `enabled: false` (same as "not configured") so the app hides the Apple Pay button on its own.
- **Never strands a payment:** payment status, Apple Pay confirm and the provider webhook ignore the module, so a payment that already started still finishes and is recorded.
- **Keeps working:** the Kaspi / Halyk / Freedom bank choices in the cart are labels for paying at the counter (staff mark the order paid), so they stay in core; an Apple Pay order that was started on an older app falls back to paying at the counter, as it already does when the sheet is cancelled.
- **Depends on:** nothing. Provider credentials are still configured separately (`FreedomPay:Enabled`, `Payment:*`).

### `verification` (phone verification code at sign-up and phone change)
- **Covers:** the one-time code sent when a customer registers or changes their phone number (`AuthService`). Verification is required only when `Otp:Required` is on **and** the module is licensed.
- **How it switches off:** the same path as `Otp:Required=false`: registration creates the account immediately and returns the tokens (`verificationRequired: false`), and a phone change is applied immediately. Both apps already follow `verificationRequired`, so no UI change was needed. Nothing returns `module_disabled`, because sign-up is core.
- **Caveat:** without verification anyone can register with any phone number, and loyalty is linked by phone, so a cafe running `loyalty` without `verification` can have bonus balances claimed by someone else's number. Show this in the commercial terms; the platform does not block the combination.
- **Never strands a sign-up:** confirming a code that was already sent (`register/verify`, `me/phone/verify`) ignores the module.
- **Depends on:** nothing technically; commercially pairs with `loyalty`. The WhatsApp / SMS provider is still configured separately (`Otp:Enabled`).

### `branding` (white-label brand)
- **Covers:** the saved brand (name, primary and accent colour, logo, app icon and splash image URLs (all https), welcome line, up to 6 links, support contact) in `GET /api/v1/app/config`. The apps turn the primary colour into the `--brand-*` scale and cache it, so a returning customer sees the cafe's colours on the first paint.
- **How it switches off:** the config serves the Yurt default brand and the saved brand is kept, so switching the module back on restores it. The platform operator can still edit the brand while it is off (`GET`/`PUT /api/v1/platform/brand`, SuperAdmin only).
- **Admin screen:** the Brand section on the Platform page (SuperAdmin) edits these values with a live preview.
- **Accent scale:** gradient ends and soft tints use `accent-50/400/500`, generated from the accent colour (defaults are the orange the gradients always had).
- **Readability tokens:** `--brand-hero-text` (text on gradient headers), `--brand-ink` (brand-coloured text on light pages) and `--brand-on-dark` are computed from the primary colour; for the Yurt default they keep the old look.
- **Readability:** text on the primary colour follows `--brand-on-primary`, and near-black buttons use `--brand-on-dark` (the primary lightened until it reads on near-black). The Brand screen warns about a primary that is too light or too dark.
- **Not done yet:** light/dark logos (waiting for dark mode).
- **Depends on:** nothing.

### `own_pos` (self-developed POS, shipped one feature at a time)
Gated so far: the **Kitchen** screen and **Stock**. The other `own_pos` features (cashier, printers) are added here as they ship.
- **Kitchen screen:** admin route `/kitchen` and menu entry. A tablet page per branch with three columns (New, Preparing, Ready) of the accepted orders, oldest first, with items, toppings, notes and the wait time (red when past the promised minutes). One big button per card moves it on (Start, Ready, Handed over). It uses the same order API and live events as Live Orders, so no backend gate is needed and nothing changes for customers.
- **When off:** the route redirects to Orders and the menu entry is hidden. Live Orders keeps working.
- **Depends on:** nothing. It works next to iiko; orders still go to iiko as before while the cafe uses it.
- **Stations:** the **Stations** button on the kitchen screen opens a setup panel to add stations (for example Kitchen and Bar, up to 10) and choose a station for each menu category (`GET admin/stations/routing`, `POST admin/stations`, `DELETE admin/stations/{id}`, `PUT admin/stations/categories/{categoryId}`, gated by `own_pos`). Once stations exist, tabs appear on the kitchen screen: **All** or one station. A screen set to one station shows only the items made there, and an order with none of its items there is left out. Categories with no station show on every screen so nothing is missed. The choice is remembered per tablet. Deleting a station only un-routes its categories.
- **Ready per station:** on a screen set to one station, the **Ready** button of an order that has items made there becomes **Our part is ready** (`POST admin/orders/{id}/stations/{stationId}/ready`, `DELETE` to undo while the order is still being made, gated by `own_pos`; stored in `OrderStationReadies`). Cards with items for more than one station show a small chip per station (✓ when that part is done). When every station that has items in the order is ready, the order moves to **Ready** by itself and the customer is told as usual. If the order also has items whose category has no station, nobody owns those, so the order does not move by itself and staff press **Ready** on the **All** screen as before. The **All** screen keeps the whole-order buttons, so a cafe without stations works exactly as before.
- **Kitchen ticket:** each kitchen card has a **Print ticket** button. It prints a narrow 80 mm slip through the browser (order number, station, time, customer, the items to make with sizes, toppings and notes, no prices). With a station chosen it lists only that station's items. No backend change.
- **Not done yet (kitchen):** sending tickets straight to a network printer.

#### Stock and the automatic stop-list (per product, per branch)
- **Covers:** `GET /api/v1/admin/stock?locationId=` and `PUT /api/v1/admin/stock/{menuItemId}/{locationId}` (body `{ quantity, lowStockAt }`; a null `quantity` stops counting the item); the admin **Stock** page and menu entry.
- **How it works:** an item with a count is limited to what is left in that branch. An order for more than is left is refused with a plain message ("Only 2 left of ..." or "... is sold out"). Accepting an order lowers the count (never below zero, safe when two orders are accepted together). At zero the item disappears from that branch's customer menu by itself; setting a number again or clearing the count brings it back. Items without a count are unlimited, as before. The admin list marks items at or below their warning number as running low.
- **When off:** the Stock endpoints return `403 module_disabled`, the page redirects to Orders and the menu entry is hidden. Counts are **not enforced** while the module is off (menu and orders ignore them), so nothing can stay stuck at zero without a way to fix it. The counts are kept and apply again when the module is back on.
- **Low-stock alerts:** `GET /api/v1/admin/stock/alerts?locationId=` (no branch means all branches) lists dishes and ingredients that are out or at/under their warn-at number, out first, up to 100. The admin shows a red number badge on the **Stock** menu entry (refreshed every 2 minutes) and a "Needs attention" summary on the Stock page for the chosen branch. While `own_pos` is off the list is empty.
- **Not done yet:** push or phone alerts for low stock, restocking history.

#### Cashier, shifts and the Z report (per branch)
- **Covers:** `GET shifts/current?locationId=` (empty body when none is open), `GET shifts?locationId=`, `POST shifts/open` (`{ locationId, openingCash }`), `POST shifts/{id}/cash` (`{ amount, isOut, reason }`), `POST shifts/{id}/close` (`{ countedCash, note }`, returns the Z report), `GET shifts/{id}/report`, all under `/api/v1/admin/`. The admin **Cashier** page and menu entry. Workers can use it, not only admins.
- **How it works:** one open shift per branch (the database enforces it). The report counts the orders completed at that branch between opening and closing: orders, declined, before discounts, discounts, points spent, takings in money (total minus points), takings by payment method. The drawer should hold: opening cash + cash sales + cash put in − cash taken out; closing with the cash counted shows the difference. While the shift is open the same numbers are the X report. Orders with no payment method recorded count as cash. The Z report can be printed from the browser.
- **When off:** endpoints return `403 module_disabled`, the page redirects to Orders and the menu entry is hidden. Shifts are kept.
- **Receipts:** the order details in Orders have a **Print receipt** button (only while `own_pos` is on). It prints a narrow 80 mm receipt (cafe name, branch, date, short order number, items with sizes and toppings, discount, points, total, payment method) through the browser print dialog, so any printer the computer can print to works. It is built in the browser from the order already on screen; there is no backend change.
- **Split payments:** the payment box of an order has a **Split payment** link (only while `own_pos` is on). The cashier lists up to 5 parts, each with a method and an amount; the parts must add up to what the guest pays in money (total minus points), else `400`. `GET/PUT admin/orders/{id}/split-payment` (gated by `own_pos`). Saving replaces earlier parts, marks the order paid and keeps the biggest part as the order's main method. The Z report counts each part under its own method, so cash sales and the cash that should be in the drawer only include the cash part. Other reports (Analytics) still show the one main method. The printed receipt lists each part when an order has two or more.
- **Payment methods per branch:** the branch dialog (Locations) has a **Payment methods** group of tick boxes (only while `own_pos` is on). Tick none to offer every method (the default). The customer app shows only the ticked methods at checkout (`paymentMethods` on the location), and the server refuses an order with another method (`422`). `Other` (used by group orders) is always accepted, and the check is skipped while `own_pos` is off. Cash is not shown in the customer app anyway; ticking it only matters for the cashier. Saving a location without the field leaves the setting as it was.
- **Not done yet:** direct thermal-printer support (the browser print dialog works today).

#### Ingredients and recipes (per ingredient, per branch)
- **Covers:** `GET/POST /api/v1/admin/ingredients`, `PUT/DELETE .../{id}`, `PUT .../{id}/stock/{locationId}` (null `quantity` stops counting), `GET/PUT .../recipes/{menuItemId}` (saving replaces the whole recipe). The **Ingredients** and **Recipes** tabs on the Stock page.
- **How it works:** a recipe line says how much of an ingredient one portion uses, for every size or one size. A dish leaves the customer menu when an every-size line needs more than the branch has. An order is refused when any line (size-specific ones included) cannot be covered. Accepting an order lowers the ingredient counts, never below zero.
- **When off:** same as Stock: endpoints return `403 module_disabled`, counts are not enforced.
- **Toppings:** a topping can use ingredients too (`GET/PUT /api/v1/admin/ingredients/toppings/{toppingId}`, set on the Recipes tab by picking a topping). Each time a guest adds the topping to an item, that amount is used once per portion ordered. A topping leaves a branch's customer menu when an ingredient it uses is short there, an order with it is refused when the amount cannot be covered, and accepting the order lowers the ingredient. A topping with no ingredient is not counted.

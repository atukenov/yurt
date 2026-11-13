# Yurt POS (cashier app that keeps working offline)

Yurt POS is a separate cashier app for a tablet at the counter, next to the admin app (owners and managers) and the
customer app. It follows the iikoFront idea: the till keeps the menu on the tablet, saves each sale there first and
sends it to Yurt Cloud (the one server) when it can. The server stays the one source of truth.

Built in steps, one pull request each:

1. **Counter sales on the server** (done, PR #179). Staff can ring up a paid sale for a walk-in guest with no account.
2. **Yurt POS app, online** (done, PR #180). Tap items, add-ons, pay by cash or card, send to the kitchen.
3. **Offline** (done). App and menu saved on the tablet, sales queued there and uploaded when the internet is back.
4. **Printing and tax receipts offline** (done). Receipts from the till's own Bluetooth printer; tax receipts catch up after upload.

Later, only for cafes with 2+ tills: a small in-cafe box that keeps the tills in sync while the internet is down.

## Counter sale API

`POST /api/v1/admin/counter-sales` (any staff, module `own_pos`, blocked in limited mode)

```json
{
  "locationId": "...",
  "paymentMethod": "Cash",            // Cash, Card, KaspiBank, HalykBank, FreedomBank
  "idempotencyKey": "uuid made by the till for this sale",
  "items": [{ "menuItemId": "...", "quantity": 2, "variantId": null, "toppingIds": ["..."], "notes": null }],
  "etaMinutes": 5,                    // optional, for the kitchen screen
  "handedOver": false,                // true = the guest already has everything, nothing for the kitchen
  "soldAt": null                      // optional: when a sale made offline really happened (UTC)
}
```

- The answer is the order (`201`). Sending the same `idempotencyKey` again for the same branch returns the first sale
  (`200`), so the till can retry as often as it likes.
- Prices come from the server's menu, never from the till. Add-ons must belong to the item's category.
- The order has no guest account (`customerUserId` empty), `source` = `Counter`, is `Paid` and starts `Accepted`
  (or `Completed` with `handedOver`). Staff move it on like any other order. Stock goes down, the profit cost is
  kept, and the tax receipt job picks it up like any paid order.
- A live sale respects the stop-list and the branch menu. A sale with `soldAt` more than 2 minutes ago is an offline
  sale: it is taken in even if the item was stopped since, keeps its own time (at most 7 days back), and the server
  does not print or open the drawer again (the till did that itself).
- Walk-in sales show as "Walk-in guest (counter)" on the orders and kitchen screens, and do not count as customers in
  analytics.

## The Yurt POS app (`frontend/projects/yurt-pos`)

- **Sign in** with any staff login of the cafe (worker, admin or CEO), the same as in the admin app. A login that must
  change its password does that in the admin app first. Platform operators can't use the till.
- **Branch:** picked once per tablet (asked only when the cafe has more than one open branch) and remembered.
- **Menu:** the branch's menu as guests see it (stop-list and branch-only items applied), by category or by search, in
  Russian, Kazakh or English. Items with sizes or add-ons open a small chooser; add-ons in the same group (milk, syrup)
  replace each other.
- **Sale:** + / − per line, "Give it now" (nothing for the kitchen) or a ready time for the kitchen screen. Then the
  payment method (only the counter methods the branch takes), cash given and the change, and "Paid, finish sale".
- **Never twice:** each sale gets its own key when the cashier first presses "Paid". If the answer is lost (bad
  internet), pressing again sends the same key and the server returns the first sale. A changed cart gets a new key.
- **Sales on this till:** today's sales from this tablet (kept on the tablet only). Sales not uploaded yet say "waiting".

### Guest kiosk
With the `kiosk` module on, a **Guest kiosk** button hands the tablet to guests as a self-order screen. See `docs/self-order-kiosk.md`.

### Barcode scanner and scales
Scan a pack to add the dish, weigh dishes sold by weight, or read a label from a shop scale, also offline. See `docs/barcodes-and-scales.md`.

### Staff PIN
The **Staff PIN** button in the top bar takes a PIN: *Take over the till* signs the till in as that person (the sale on screen stays), *Clock in / out* starts or ends their working hours, Needs the internet. See `docs/time-clock.md`.

### Tip
A **combo** (set up in Admin → Combos, see `docs/combos.md`) shows in the menu like a dish with what is inside on its tile. Tapping it adds it to the sale at its set price, with no sizes or add-ons; the dishes inside are taken off the stock, also for a sale made offline.

The payment step has a **Tip** row: *No tip*, 5%, 10%, 15% of what the guest pays for the bill, or *Other amount*. The tip is on top of the bill and not part of the total, so tax receipts and sales numbers stay as they were. No PIN is needed to add a tip at the till, and it works offline: the tip is kept with the sale, printed on the till's own receipt and sent with the sale later. If the bill is paid by card but the guest gave the tip in cash, tick *Guest gives the tip in cash*. With a cash bill, the change is worked out on the bill plus the tip. The tip goes to the cashier who rang the sale up. Changing or removing a tip afterwards is done in the admin app and asks for a manager PIN (see `docs/tips.md`).

### Discount and cancelling a sale
The **Discount** button on the payment step asks a manager for their PIN and a reason, then takes a percent or an amount off. The sale is sent with the discount and the server checks the PIN; a wrong or locked PIN shows its message and the sale stays on screen. A discount needs the internet, so without it the till says so and the cashier removes the discount or waits (a plain sale still works offline). **Cancel** on a sale in "Sales on this till" asks for the same PIN and a reason, and the cashier hands the money back. See `docs/roles.md` (Manager PIN).

### Without internet

- **The app itself** is saved on the tablet after the first visit (`public/sw.js`, a small service worker that keeps
  `index.html` and the app's files; it never touches API calls). It opens with no internet, also after a reload.
  `ng serve` does not use it; only built versions do.
- **Branches and menu:** each time they load, a copy is saved on the tablet (per branch and language). With no
  internet the till uses that copy and says when it was saved. Prices are always taken from the server when the sale
  goes up, so an old copy can't change what the cafe earns.
- **Sales:** if a sale can't reach the server (no internet, or the server is down), it is saved on the tablet with its
  key and the time it was made, and the cashier sees "Sale saved on this tablet". A yellow bar shows "No internet" and
  how many sales wait. They go up by themselves, oldest first, when the internet is back (also every 20 seconds, at
  sign-in, and with "Send now"). Saved sales are sent as handed over (the guest already has the order) and with their
  real time, so the server counts them on the right day and does not print or open the drawer late.
- **Never twice:** the key stays with the sale, so a lost answer or a second try returns the first sale.
- **Refused sales** (for example an item deleted meanwhile) stay on the tablet in the yellow bar with the server's
  reason, "Try again" and "Remove". Older than 7 days the server can't take them any more; enter those by hand.
- **Sign-in ran out:** waiting sales stay on the tablet (per cafe, also through sign-out) and go up after the next
  sign-in.
- Signing in needs the internet. One tablet = one till: two tablets offline don't see each other's sales until both
  have uploaded.

### Running it

- Locally: `./scripts/dev.sh` also starts it on http://localhost:4400.
- Build: `cd frontend && npx ng build yurt-pos` (output `dist/yurt-pos/browser`). Tests: `npx ng test yurt-pos`.

### Printer and tax receipts

- **Printer:** the top bar has **Receipt printer**. It lists the branch's Bluetooth printers (add them first in the admin
  app: Printers, "Bluetooth, through a tablet in the cafe"). **Connect** opens Chrome's Bluetooth list; pick the
  printer once, and the till reconnects to it by itself later. It needs Chrome (Android, Windows, Mac) and a Bluetooth
  Low Energy printer, the same limits as `docs/bluetooth-printers.md`. The Yurt POS till does not run in the Capacitor
  tablet app, so classic Bluetooth printers and iPad printers are still only for the admin tablet app.
- **Online sales:** the server makes the receipt (and opens the drawer for cash) like for any order and the till, which
  claims that printer's jobs every 3 seconds, prints it. The kitchen ticket goes to the kitchen printer as before.
- **Sales made without internet:** the server does not know them yet, so the till prints the guest's receipt itself
  (same layout as the server's, in the printer's language and paper width) and opens the drawer for cash. It says the
  tax receipt follows. Its number is `T-` and six characters of the sale's key, because the server has not given it one.
  If the printer is off or out of range the cashier sees why, and the sale is still saved.
- **Tax receipt (Kazakhstan):** the server sends it to the cash register service after the sale is uploaded, like for any
  other sale. The receipt is **not printed again by itself** with the tax QR for a sale made long before it was uploaded (the guest has
  left); staff can press Print receipt on the order in the admin app.
- The shared Bluetooth code lives in `shared-api` (`bluetooth-printing.ts`), used by both the admin app and the till.

### Putting it online (one time)

1. Vercel: add a new project from the same repository. Root directory `frontend`, build command
   `npx ng build yurt-pos`, output directory `dist/yurt-pos/browser`. `frontend/vercel.json` already sends every
   address to the app.
2. Railway (the API): add the new address to the allowed apps, e.g. `AllowedOrigins__12=https://yurt-pos.vercel.app`
   (use the next free number), then redeploy.
3. On the till tablet: open the address in Chrome and "Add to home screen".

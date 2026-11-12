# Yurt POS (cashier app that keeps working offline)

Yurt POS is a separate cashier app for a tablet at the counter, next to the admin app (owners and managers) and the
customer app. It follows the iikoFront idea: the till keeps the menu on the tablet, saves each sale there first and
sends it to Yurt Cloud (the one server) when it can. The server stays the one source of truth.

Built in steps, one pull request each:

1. **Counter sales on the server** (done, PR #179). Staff can ring up a paid sale for a walk-in guest with no account.
2. **Yurt POS app, online** (done). Tap items, add-ons, pay by cash or card, send to the kitchen.
3. **Offline.** App and menu saved on the tablet, sales queued there and uploaded when the internet is back.
4. **Printing and tax receipts offline.** Receipts from the tablet's own printer; tax receipts catch up after upload.

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
- **Sales on this till:** today's sales from this tablet (kept on the tablet only).

### Running it

- Locally: `./scripts/dev.sh` also starts it on http://localhost:4400.
- Build: `cd frontend && npx ng build yurt-pos` (output `dist/yurt-pos/browser`). Tests: `npx ng test yurt-pos`.

### Putting it online (one time)

1. Vercel: add a new project from the same repository. Root directory `frontend`, build command
   `npx ng build yurt-pos`, output directory `dist/yurt-pos/browser`. `frontend/vercel.json` already sends every
   address to the app.
2. Railway (the API): add the new address to the allowed apps, e.g. `AllowedOrigins__12=https://yurt-pos.vercel.app`
   (use the next free number), then redeploy.
3. On the till tablet: open the address in Chrome and "Add to home screen".

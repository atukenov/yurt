# Yurt POS (cashier app that keeps working offline)

Yurt POS is a separate cashier app for a tablet at the counter, next to the admin app (owners and managers) and the
customer app. It follows the iikoFront idea: the till keeps the menu on the tablet, saves each sale there first and
sends it to Yurt Cloud (the one server) when it can. The server stays the one source of truth.

Built in steps, one pull request each:

1. **Counter sales on the server** (this step). Staff can ring up a paid sale for a walk-in guest with no account.
2. **Yurt POS app, online.** Tap items, add-ons, pay by cash or card, send to the kitchen.
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

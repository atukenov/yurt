# Self-order kiosk (module `kiosk`)

A tablet or touch screen in the cafe where guests order by themselves, get an order number and pay at the counter.
It is a locked, fullscreen screen inside the **Yurt POS app** (`/kiosk`), so it reuses the till's menu, add-on rules and
staff sign-in. Guests need no account and no phone.

## Setting it up (once per kiosk tablet)

1. Platform → Cafes → Modules: switch **Self-order kiosk** on (it needs **Own POS**).
2. On the kiosk tablet open the Yurt POS web app and sign in with a **staff login** (a Worker login is enough; make one
   just for the kiosk if you like, so its orders are easy to tell apart in the audit log).
3. Pick the branch (asked only when the cafe has several), then press **Guest kiosk** in the top bar.
4. Put the tablet in kiosk mode in its own settings (Android "screen pinning" or a kiosk launcher, iPad "Guided Access"),
   so a guest cannot leave the browser. The page also asks the browser for fullscreen and blocks the back button, but only
   the tablet itself can really lock it.

## What the guest sees

1. **Welcome** screen with the cafe logo and a big "Touch to order" button (RU / KK / EN buttons at the top).
2. **Here or to go.**
3. **Menu** by category: dishes with price, allergens (⚠ Milk, Gluten...) and combo contents. A dish with sizes or add-ons
   opens a chooser that follows the **option rules** (required choice, at most N, per-branch prices). Dishes sold by
   weight are left out (they are weighed at the counter). The stop-list, the branch menu and time menus apply.
4. **Check my order** (+ / − per line), then **Send my order**.
5. **Order number** in huge digits (the last 6 characters of the order id, the same number the app and the order-ready TV
   screen use) and "Go to the counter and pay". After 25 seconds, or on "Done", the screen goes back to the welcome screen.

**Idle reset:** after 45 s without a touch the screen asks "Are you still there?" with a countdown; after 60 s the order is
thrown away and the welcome screen returns. The welcome screen never times out.

**Staff exit:** tap the logo five times quickly, type a staff PIN (the same PIN as "Staff PIN" at the till). A right PIN
leaves the kiosk (and makes that person the signed-in staff member of the tablet).

## What the cashier sees

The order arrives like an **unpaid cash order from the app**, with the source "Kiosk" and the name "Kiosk guest (pays at the
counter)": it shows in Live Orders (and the kitchen display once accepted) at once, with the number the guest was given.
The cashier takes the money, marks the order **Paid**, then **Accepts** it. Stock, kitchen tickets, labels and the receipt
happen on acceptance, exactly as for any order. An order nobody pays is declined or cancelled like any other.

## API

`POST /api/v1/admin/kiosk-orders` (any staff login, module `kiosk`, blocked in limited mode)

```json
{
  "locationId": "...",
  "idempotencyKey": "uuid made by the kiosk for this order",
  "orderType": "DineIn",                // or "TakeAway" (default)
  "items": [{ "menuItemId": "...", "quantity": 2, "variantId": null, "toppingIds": ["..."] }]
}
```

- Answer `201` with the order (`source` = `Kiosk`, `status` = `Created`, `paymentStatus` = `Unpaid`, no `customerUserId`).
  The same `idempotencyKey` for the same branch returns the first order (`200`), so a retry never makes a second one.
- Prices, names, add-ons and option rules come from the server's menu, never from the tablet. A stop-listed dish, a dish not
  sold at the branch, a missing required choice, or not enough stock (checked now, as for an app order) gives `400` with the reason.
- The order is written to the audit log as `KioskOrder`.

## Not included yet

- **Paying on the kiosk.** A guest can't pay by card on the screen yet: it needs a card terminal link (see
  `docs/card-terminals.md` once that is in) or Stripe on a guest with no account. Until then everyone pays at the counter.
- Photos of the dishes are shown when the menu has them; there is no separate "kiosk menu" or promo screen.
- Screen text in RU / KK is written by Claude, not checked by a native speaker.
- Not tried on a real kiosk tablet.

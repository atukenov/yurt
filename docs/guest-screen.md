# Guest-facing screen (customer display) for the till

A second screen that faces the guest while the cashier rings up the sale: the items, the discount, the tip, the total
to pay, what to do to pay (cash / card / banking app), the change, and a thank-you with the order number. When nothing is
being rung up it shows the cafe's name and its active promotions, one by one.

## Set up
1. On the till computer or tablet open Yurt POS and sign in as usual.
2. Press **Guest screen** in the top bar. A second window opens at `/guest`. Drag it to the customer monitor and press
   F11 for full screen.
3. That is all. The guest window needs no sign-in. It has the till's language and the cafe's colours and logo.

## How it works
The till sends what it shows to the guest window through the browser itself (a broadcast channel and the last view saved
in the browser), so it works **with no internet**, the same as the till. Promotions are loaded from the server when there
is internet and the last ones are kept for when there is none; with no promotions it shows "Welcome!".
Closing the till sends the welcome picture. A view older than an hour is treated as "nothing being rung up".

## Limits
- The guest window must be in the **same browser on the same computer** as the till (two monitors on one computer, or a
  tablet that can show two windows). A separate tablet on its own is not supported yet; it would need a small in-cafe
  hub or the server, which would not work offline.
- The guest cannot press anything (tip, payment): the cashier does that on the till and the guest screen shows the result.
- Not tried on real customer-facing hardware.

Code: `core/guest-display.ts` (what is shown, tested in `guest-display.spec.ts`), `core/guest-display.service.ts`
(sending), `features/guest/guest.component.ts` (the screen).

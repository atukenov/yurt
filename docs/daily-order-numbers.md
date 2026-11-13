# Daily order numbers

Guests and staff say a short number out loud: **1, 2, 3 …** It starts again at **1 every day** at each branch. The order keeps its real id (`Order.Id`) for everything inside the system; the short number is only what people read and say.

## Rules

- **Per branch.** Every branch counts on its own (branch A can be on 40 while branch B is on 7).
- **The day ends at the branch's own midnight.** It uses the branch time zone (Locations → time zone, `Asia/Almaty` when empty).
- **No wrap-around.** After 999 the number is simply 1000. Nothing is reused within a day.
- **Same moment, different numbers.** The counter is one atomic Postgres statement (`INSERT … ON CONFLICT DO UPDATE SET LastNumber = LastNumber + 1 RETURNING`) in the `OrderDayCounters` table (branch + day → last number). A unique index on `Orders (LocationId, BusinessDate, DailyNumber)` refuses a repeat even if code is ever wrong.
- **Gaps are possible, repeats are not.** If an order fails to save after taking a number, that number is skipped. A resent sale (same idempotency key) gets its first order back and uses no new number.

## Where it is given

App orders, table-QR orders, counter sales at the Yurt POS, and kiosk orders all get the number when the order is created (`OrderNumbers.AssignAsync`).

### Sales made offline at the till

A till without internet cannot know the day's counter, and two tills must never print the same number. So:

- The offline till prints its own receipt number `T-3FA9C1` (the start of the sale's key), exactly as before.
- When the sale reaches the server it still gets a normal day number (so the admin lists stay in order), but `number` in the API stays `T-3FA9C1`, so the paper receipt, the kitchen screen, the TV and the admin list all show the same thing for that guest. `dailyNumber` has the integer.

## What the API returns

`OrderDto.number` (string) is what to show everywhere. `dailyNumber` is the integer (null on an old order or on an offline `T-` sale). For an order from before this feature that was not backfilled, `number` is the last 6 characters of the id, as before.

## Where it is shown

Order-ready TV (`/tv`), kitchen screen, live orders list and toasts (admin), customer app order list/detail, POS (done screen, "sales on this till", guest display, kiosk), kitchen tickets, cup labels, printed receipts (`#42 · A1B2C3D4`, the second part is the id reference the tax receipts and accounting export use), browser-printed receipts and tickets.

The accounting export and tax receipts still use the id-based reference (`ShortId`) on purpose: it never repeats.

## Existing orders

The migration numbers every existing order per branch and day (in creation order, using the branch time zone) and sets the counters to continue from there. It only fills new empty columns, so it is safe on a live database.

## Code

`OrderNumbers` (Application/Features/Orders/Services), `OrderDayCounter` entity, tests in `DailyOrderNumberTests` and `OrderNumbersTests`.

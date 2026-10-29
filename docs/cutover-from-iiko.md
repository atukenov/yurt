# Moving a cafe from iiko to the own POS

A step-by-step list for one cafe at a time. Nothing here is forced: every step can be undone until the last one, and iiko stays untouched (only switched off for that cafe) until you decide.

Do the steps in order. Use the cafe picker on the Platform page (`X-Tenant` for the operator endpoints) so you act on the right cafe.

## 0. Before you start
- [ ] Back up the production database (see `docs/runbook.md`).
- [ ] The `own_pos` module is on for the cafe (Platform page).
- [ ] `Secrets__MasterKey` is set on the server and backed up.
- [ ] Staff know the new screens: Kitchen, Cashier, Stock.

## 1. Set up the cafe's own data
- [ ] **Stock page:** add ingredients, give each dish a recipe, set the "Warn at" numbers.
- [ ] **Kitchen page:** add stations (for example Kitchen and Bar) and pick one per menu category.
- [ ] **Cashier page:** open a test shift, record a cash in/out, close it, check the Z report.
- [ ] Print one receipt (order details) and one kitchen ticket (kitchen card) on the real printer.

## 2. Run side by side for about a week
- [ ] Keep iiko on. Staff use the own Kitchen and Cashier screens next to the iiko terminal.
- [ ] Every evening compare the day: Z report totals (orders, takings by payment method) against the iiko report. Write down any difference and its cause.
- [ ] Check the Stock page "Needs attention" box matches what the staff see on the shelves.

## 3. Move the loyalty points
- [ ] On the Platform page pick the cafe, open **Loyalty wallet**. When no orders are in flight, press **1. Copy balances from iiko**.
- [ ] Press **2. Check they match**. It must show nobody different. If it lists customers, wait for quiet time and copy again.
- [ ] Press **3. Switch to own wallet**. It refuses while balances differ.
- [ ] Place and finish one test order with points. Check the balance in the app.
- [ ] Undo, if needed: **Switch back to iiko** in the same section. Points earned in the own wallet are not copied back to iiko.

## 4. Switch iiko off for the cafe
Only when the week of comparing showed no unexplained differences.
- [ ] Turn off the `iiko_sync` module for the cafe (Platform page). Orders no longer go to iiko.
- [ ] **Official tax receipts:** iiko (or the cafe's old cash register) stops printing them now. Make sure the cafe has its own cloud cash register ready the same day, or keeps a separate one, so no sale goes without a receipt. See `docs/fiscal-receipts-kz.md`.
- [ ] Watch the first day: orders arrive in Kitchen, Cashier totals look right, no errors in the logs.
- [ ] If Yurt sends the tax receipts: the same day, on the Platform page set **Official tax receipts** to Webkassa or ReKassa, enter each branch's register number and the login in Secrets. The next orders show "Tax receipt"; any "Not fiscalized" badge says why.
- [ ] Keep the iiko account for a few weeks as a safety net before cancelling it. Turning `iiko_sync` back on is the way back.

## Known limits today
- An order has one status for the whole order, not one per station.
- Tickets and receipts print through the browser print dialog; there is no direct network or Bluetooth printer yet.
- Split payments and a per-branch payment method list are not built yet.

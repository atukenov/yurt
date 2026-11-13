# Tips

Part of the `own_pos` module. A tip is money a guest leaves **on top of the bill**. It belongs to the staff member who served, not to the cafe, so Yurt keeps it apart from sales.

## What a tip is, and is not
- It is **not part of the order total**. Sales, analytics, profit, the accounting export, online payments (Stripe) and the official tax receipt are all unchanged. The printed guest receipt shows a *Tip* line under the total.
- It has an amount, an optional own payment method (a card sale where the guest gave the tip in cash) and an owner (the staff member it is for).
- A tip can be at most the size of the bill (100%) and never more than 1,000,000.

## Adding a tip
- **At the till (Yurt POS):** payment step → *Tip*: No tip, 5 / 10 / 15%, or another amount. No PIN is needed, it works offline, and the tip goes to the cashier who rang the sale up. See `docs/yurt-pos.md`.
- **API:** `tip` and `tipMethod` on `POST admin/counter-sales`.

## Changing or removing a tip (manager PIN)
In the admin app, **Live orders → open an order → Tip** (any order that is not cancelled). Type the new amount (0 removes the tip), a reason and a manager PIN. This is on purpose: a cashier must not be able to move staff money alone. The old and new amount, the reason and who approved go into the audit log (`OrderTipChanged`, `OrderTipRemoved`). A sale that is cancelled carries no tip in the reports, and a cash sale that is cancelled counts the tip in the money to hand back.

- A tip on an **app order** (the guest paid in the app) has no cashier, so it shows as *Not assigned* in the reports unless `staffId` is given in the API call.
- With `own_pos` off there are no tips.

## Reports
- **Cashier shift report (X/Z):** a *Tips* block with the total and a line per staff member (cash and card/other). Tips paid in cash are in the drawer, so they are **added to the cash expected** (they are not part of Takings). When the cashier hands the tips to staff, record it with *Cash out* so the count matches.
- **Tips page (CEO only, Reports):** pick two days and a branch: total, cash, card and other, a table per staff member and a table per cashier shift. `GET admin/tips?from=yyyy-MM-dd&to=yyyy-MM-dd&locationId=` (days counted in UTC, at most a year). Tips are staff money, so Admins and Workers do not see this page; they do see the tips in a shift report.
- Only finished (Completed) orders count in the reports.

## Not built yet
- Tip choice in the customer app (the guest tipping when paying online).
- Paying tips out (a payroll line or a cash-out tied to the report) and splitting a tip between several staff members.
- A staff picker for app-order tips in the admin app (the API already takes `staffId`).

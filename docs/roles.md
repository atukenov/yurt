# Roles: who can do what in a cafe

Every staff account has one role. Four roles exist:

| Role | Who | Sees and does |
|---|---|---|
| **Worker** | Barista, cashier | Live orders, their own cashier shift, write-offs and moves, typing in deliveries. No settings, no money numbers. |
| **Admin** | Cafe administrator, runs the cafe day to day | Everything a Worker does, plus stock and ingredients (counts, restocking, stocktake), suppliers and purchase orders / invoices, shifts and cash counts, menu, tables, printers, branches, customers, promotions and codes, customer complaints, staff accounts of Workers. **Does not see** sales analytics, the dashboard, billing, the audit log or payroll. |
| **CEO** | The cafe's owner (and an accountant, if the owner wants to share the login) | Everything an Admin does, plus the money side: **Analytics** and dashboard, **Profit** (monthly profit and profit by dish), **Tips** report (per staff member and shift), **Food cost** and write-off money on the Stock page, **Subscription** (what the cafe pays Yurt), **Audit log**, and **Payroll**. Only the CEO adds or switches off **Admin** accounts and resets their passwords. |
| **SuperAdmin** | You, the platform operator | Only the Platform screens. Never listed on a cafe's staff page. |

A separate Accountant role (money reports and payroll, but no menu or orders) is not built yet; a CEO login covers it.

## Rules that protect accounts
- A Worker account is managed by any Admin or the CEO.
- An Admin account is managed (added, renamed, switched off, password reset) by the CEO only.
- A CEO account is managed by the platform operator only (Platform → Cafes → Admins). Nobody in the cafe can switch off or take over the owner's login.
- Every cafe always keeps at least one active CEO: the operator cannot demote the last one.

## Where the first CEO comes from
- **New cafe** (Platform → New cafe, or Platform → Cafes): the first account created is the **CEO**.
- **The default Yurt cafe** (the first cafe, seeded at start-up): set `SEED_CEO_USERNAME` and `SEED_CEO_PASSWORD` (8+ characters) on the server and restart. A CEO is created only while the cafe has none, and must choose a new password at the first sign-in. Remove the variables afterwards.
- **An existing Admin becomes CEO** (nothing is promoted automatically): Platform → Cafes → *Admins* → role picker, or from the server shell `dotnet Yurt.WebApi.dll set-admin-role <username> Ceo`. The change applies at the person's next sign-in.
- Before a cafe has a CEO, nobody there sees analytics, subscription, audit log or payroll. The server logs a warning at start-up.

## Manager PIN (cancels, refunds and discounts)
With the `own_pos` module on, a cashier cannot cancel a sale, give money back or take money off a bill alone. Each of these asks for a **manager PIN** and a **reason**; both go into the audit log (the CEO sees it there) together with who approved.

- **Who has a PIN:** Admins and the CEO, each their own. Set it under *Settings → Manager PIN* in the admin app: 4 to 6 digits, confirmed with the account password. No two managers can share a PIN, so the audit log always says who approved. A Worker has no PIN and cannot approve. A switched-off account's PIN stops working.
- **What asks for it:**
  - *Cancel order* (a button on an order that was taken on, and on a finished counter sale): the order ends as Declined with the reason. A sale paid at the till is marked Refunded and the cashier hands the money back; an online payment goes back to the card.
  - *Refund to card* (online payments).
  - *Discount*: on the till (Yurt POS) at payment, and as a button on an unfinished order in the admin app. One discount per order, either an amount or a percent, never more than the bill. An order the guest already paid online can't be lowered.
  - *Declining an order the guest already paid online* (it refunds the money), and moving a payment from **Paid** back to Unpaid or Refunded.
  - Declining a new, unpaid order, and everything the customer does in their app, need no PIN.
- **Yurt POS:** the **Discount** button is on the payment step; **Cancel** is on each sale in "Sales on this till". Both need the internet because the server checks the PIN; a sale made offline never carries a discount. A sale resent after a lost answer never asks for the PIN twice.
- **Guessing:** 5 wrong PINs in a row lock that staff member's approvals for 5 minutes. Every wrong PIN is written to the audit log (`ManagerPinRejected`).
- **First setup:** until at least one manager has a PIN, these actions are refused with a message that says so. Set the PIN before the cashiers start using the new till.
- **API:** `GET admin/manager-pin` (is a PIN asked for, does anyone have one, do I), `PUT admin/manager-pin` `{ currentPassword, pin }` (Admin and CEO). `POST admin/orders/{id}/refund|cancel` take `{ pin, reason }`, `POST admin/orders/{id}/discount` takes `{ percent | amount, pin, reason }`, decline takes `pin` next to `reason`, payment takes `pin` and `reason` when it leaves Paid, and a counter sale takes a `discount` object. Audit actions: `ManagerPinSet`, `ManagerPinRejected`, `OrderCancelled`, `OrderDiscount`, `CounterSale` (with the discount), `OrderRefunded`, `OrderDeclined`.
- **Tips:** changing or removing a tip after the sale (`PUT admin/orders/{id}/tip`, `{ amount, method, staffId, pin, reason }`; amount 0 removes it) asks for the PIN and a reason too. Audit actions `OrderTipChanged`, `OrderTipRemoved`. Adding a tip when the sale is rung up at the till does not. See `docs/tips.md`.
- **Not covered yet:** stock a cancelled order already used stays used (write it off if it was made); a tax receipt already sent for a cancelled sale is not reversed automatically.

## Technical notes
- The role is stored as text in `AdminUsers.Role` (`Worker`, `Admin`, `SuperAdmin`, `Ceo`); adding `Ceo` needed no database change.
- Policies: `AdminOnly` (any staff login), `AdminRoleAdmin` (Admin, CEO, SuperAdmin), `CeoOnly` (CEO, SuperAdmin: analytics, dashboard, audit log, billing checkout, food cost, write-off totals, profit by dish, monthly costs), `CeoPayroll` (CEO only, not even the operator: payroll and the month's profit, which includes wages).
- Admin app: sidebar entries carry `ceoOnly`, routes use `ceoGuard`; both are conveniences, the server enforces everything.

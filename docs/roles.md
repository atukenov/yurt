# Roles: who can do what in a cafe

Every staff account has one role. Four roles exist:

| Role | Who | Sees and does |
|---|---|---|
| **Worker** | Barista, cashier | Live orders, their own cashier shift, write-offs and moves, typing in deliveries. No settings, no money numbers. |
| **Admin** | Cafe administrator, runs the cafe day to day | Everything a Worker does, plus stock and ingredients (counts, restocking, stocktake), suppliers and purchase orders / invoices, shifts and cash counts, menu, tables, printers, branches, customers, promotions and codes, customer complaints, staff accounts of Workers. **Does not see** sales analytics, the dashboard, billing, the audit log or payroll. |
| **CEO** | The cafe's owner (and an accountant, if the owner wants to share the login) | Everything an Admin does, plus the money side: **Analytics** and dashboard, **Profit** (monthly profit and profit by dish), **Food cost** and write-off money on the Stock page, **Subscription** (what the cafe pays Yurt), **Audit log**, and **Payroll**. Only the CEO adds or switches off **Admin** accounts and resets their passwords. |
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

## Technical notes
- The role is stored as text in `AdminUsers.Role` (`Worker`, `Admin`, `SuperAdmin`, `Ceo`); adding `Ceo` needed no database change.
- Policies: `AdminOnly` (any staff login), `AdminRoleAdmin` (Admin, CEO, SuperAdmin), `CeoOnly` (CEO, SuperAdmin: analytics, dashboard, audit log, billing checkout, food cost, write-off totals, profit by dish, monthly costs), `CeoPayroll` (CEO only, not even the operator: payroll and the month's profit, which includes wages).
- Admin app: sidebar entries carry `ceoOnly`, routes use `ceoGuard`; both are conveniences, the server enforces everything.

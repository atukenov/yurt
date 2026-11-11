# Demo data

The local cafe starts full of made-up data, so every screen has something to show: reports, profit, payroll, stock,
purchasing, shifts, camera watch, the customer app. It is added by `DemoDataSeeder`
(`backend/src/Yurt.Infrastructure/Persistence/DemoDataSeeder.cs`) when the API starts with `SEED_DEMO_DATA=true`.
`docker-compose.yml` sets this, so `./scripts/dev.sh` gets it without doing anything.

- **Only adds.** It never deletes or changes your rows. The only edits to existing rows: empty ru/kk names on the
  seeded menu are filled, categories without a station get Bar or Kitchen, and a branch without a 2GIS link gets one.
  It also adds S/M/L sizes to Americano, Cappuccino and Latte when they have none, and makes Avocado Toast
  available at the first branch only.
- **Runs once.** Each part looks for its own demo rows first, so restarting the API adds nothing.
- **Fresh start:** `./scripts/dev.sh reset`, then `./scripts/dev.sh`.
- **Empty cafe instead:** `YURT_DEMO_DATA=false ./scripts/dev.sh` (on an empty database).
- **Never on a real cafe's server.** The orders, customers and staff are not real. To fill a separate demo cafe on a
  server on purpose, run `dotnet Yurt.WebApi.dll seed-demo <cafe-slug>` once.

## Sign-ins

| Who | Login | Password / PIN |
|---|---|---|
| Cafe admin (floor manager) | `demo.manager` | `demo12345` |
| Baristas / cook (Worker) | `demo.aigerim`, `demo.daniyar`, `demo.madina` | `demo12345` |
| Owner (CEO, sees money) | `ceo` (from `SEED_CEO_USERNAME`) | `ceo12345`, change at first sign-in |
| Customers | `+77015550001` ... `+77015550030` | PIN `1234` (the last one is blocked) |

Demo staff do not have to change their password. A demo cafe made with `seed-demo <slug>` uses `<slug>.manager`
and so on instead of `demo.`.

## What is in it

About 2,000 orders over the last 75 days at the active branches (more on weekends and at lunch), with sizes, extras,
discount codes, table and pickup orders, ratings, a few declined and refunded ones, split cash/card payments, Stripe
payment records, tax receipts and printed tickets for the last days, and six live orders right now (new, accepted,
being made, ready). Also:

- **Menu:** ru/kk names, sizes, Bar and Kitchen stations, QR tables (8 and 5), pastries counted on the shelf.
- **Stock:** 27 ingredients with costs and recipes for every dish and extra, so profit by dish has real numbers;
  some ingredients are low, so "What to order" has suggestions. Write-offs, monthly stocktakes and a transfer
  between branches in the history.
- **Purchasing:** 5 suppliers with pack sizes, about two months of posted delivery invoices (some unpaid with a due
  date), one ordered, one draft and one cancelled.
- **Staff:** salaries, bonuses and deductions, last month closed in payroll, one switched-off former employee.
- **Money:** rent, utilities, ads, taxes and a repair as monthly costs; cash shifts for the last two weeks with
  small differences at closing, and today's shift open.
- **Guests:** 30 customers (regulars and new ones), favourites, phone push tokens, bonus wallets, a group order,
  three problem reports.
- **Camera watch:** two cameras with zones, the box, staff shifts, yesterday's and today's minutes, alerts and a
  daily report at the first branch.
- **Other:** promotions, audit log entries, an app release note.

Left empty on purpose: cafe keys and secrets, sign-in codes and sessions, billing (the operator's Stripe), uploaded
pictures, module switches and platform settings.

The test `DemoDataSeederTests` fails when a new cafe table gets no demo rows. When you add a table, add a few demo
rows for it in `DemoDataSeeder`, or list it in the test's `LeftEmpty` with a reason.

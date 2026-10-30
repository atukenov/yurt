# Exporting a cafe's data (when a cafe leaves Yurt)

A cafe owns its data. When it leaves, the platform operator gives it everything Yurt stored for it as one ZIP file.

## How to do it

1. Sign in to the admin app as the platform operator (SuperAdmin).
2. Open **Platform → Cafes** and press **Download data** next to the cafe.
3. Send the ZIP to the cafe owner over a safe channel: it holds customers' phone numbers and names.

Server side this is `GET /api/v1/platform/cafes/{id}/export` (SuperAdmin only). Every export is written to the
platform audit log as `CafeDataExported` (operators see it, the cafe does not); the cafe's exported audit log leaves out platform rows. The export does not switch the cafe off or delete anything; do that separately
with the **Active** switch once the cafe has its file.

## What is in the ZIP

- `tables/<Table>.csv`: one file for every table that belongs to a cafe (menu, branches, customers, orders, order
  items, payments, bonus wallets, stock, recipes, shifts, cash movements, staff accounts, settings, audit log and so
  on). UTF-8 with a BOM so Excel shows Cyrillic correctly, comma separated, first line = column names. Rows refer to
  each other by `Id`.
- `cafe.json`: the cafe row, the row count of every file, and the list of what was left out and why.
- `README.txt`: the same explanation in plain words, in English and Russian, for the cafe owner.

Times are UTC (ISO 8601). Numbers use a dot. Text starting with `=`, `+`, `-` or `@` gets a leading apostrophe so a
spreadsheet does not run it as a formula.

## What is left out on purpose

- Passwords, PIN codes, print-agent keys and login codes (only hashes are stored; they are useless elsewhere).
- Encrypted per-cafe secrets (Stripe, Firebase, WhatsApp keys). The cafe has the originals in those services.
- Login sessions, push notification addresses of Yurt's app, raw payment-provider messages, the print queue.
- Platform operators (SuperAdmin accounts), who are not part of any cafe.

## For developers

The exporter (`backend/src/Yurt.Infrastructure/Persistence/CafeDataExporter.cs`) finds the tables from the
database model: every entity that implements `ITenantOwned` is exported unless it is in `SkippedTables`. So a new
table is included automatically. `CafeDataExportTests.NoSecretLookingColumn_IsExported` fails when a new column whose
name contains Hash, Secret, Cipher, Token or Password would be exported; add it to `SkippedColumns` (or rename it)
on purpose.

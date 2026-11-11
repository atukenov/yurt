# Suppliers and deliveries (purchasing)

Part of the `own_pos` module, next to the [stock pack](stock-pack.md). Sidebar → **Suppliers and deliveries**
(`/purchasing`). Three tabs: **What to order**, **Orders and deliveries**, **Suppliers**.

## What each person can do

| Action | Who |
|---|---|
| See what to order, make an order and send it in WhatsApp | Staff and admins |
| Type in a delivery (supplier, invoice number, date, lines, photo) and post it to stock | Staff and admins |
| Set "Order up to" for an ingredient at a branch | Staff and admins |
| Add and edit suppliers, what they sell, pack sizes and prices | Admins |
| Mark an invoice paid or not paid, see what is owed and overdue | Admins |
| Cancel a posted delivery | Admins |

## How it works

- **Suppliers**: name, contact, phone / WhatsApp, BIN, how the cafe pays (cash on delivery, bank transfer, or pay later
  in N days) and what they sell. For each ingredient: pack name, pack size in the ingredient's unit (a box of 12 × 1 L
  of milk counted in ml is 12000) and the usual pack price. A supplier with deliveries is never deleted, only switched off.
- **Orders and deliveries** are one document with a status:
  - *Ordered*: sent (or to be sent) to the supplier; nothing on the shelf yet.
  - *Not posted* (draft): a delivery being typed in.
  - *On the shelf* (posted): every line was added to the branch, like "Delivery arrived" on the Stock page, so the
    cost per unit becomes the running average. Each line is in Stock → History, linked to the invoice.
  - *Cancelled*: a posted delivery taken back off the shelf. The cost goes back to what it would be without that
    delivery. Refused when part of it was already used (write off or count the shelf instead).
- When the goods of an order come, open the order, check amounts and prices against the paper invoice, type its
  number and press **Post to stock**. Nobody types it twice.
- **Paid**: a cash supplier's delivery is marked paid when posted. "Pay later" gets a due date (delivery date + N days)
  and shows as overdue after it. Admins see what the cafe owes each supplier on the Suppliers tab.
- **What to order** lists every counted ingredient at or under its "Warn at" number at the branch. The amount fills the
  shelf up to **Order up to** (empty = twice "Warn at"), minus what is already on open orders, rounded up to whole packs
  of the supplier that is cheapest per unit. Ingredients no supplier sells are listed under "No supplier yet".
  **Make order and send** saves the order and opens WhatsApp with the text; **Copy text** is for other messengers.

## API

All under `/api/v1/admin/purchasing` (admin token, `own_pos` module):

| Method | Path | Notes |
|---|---|---|
| GET | `suppliers` | `owed`/`overdue` only for Admin/SuperAdmin |
| POST, PUT, DELETE | `suppliers`, `suppliers/{id}` | Admins. DELETE returns `false` when the supplier was only switched off |
| GET | `purchases?locationId=&supplierId=&status=ordered,draft&unpaid=true&take=` | newest first |
| GET, POST, PUT, DELETE | `purchases`, `purchases/{id}` | only `ordered`/`draft` can be changed or deleted |
| POST | `purchases/{id}/post` | puts it on the shelf; returns the invoice and the new counts |
| POST | `purchases/{id}/cancel` | Admins; 409 with what is left when it was already used |
| PUT | `purchases/{id}/paid` | Admins; `{ "paid": true }` |
| GET | `to-order?locationId=` | groups by supplier |
| PUT | `order-up-to/{ingredientId}/{locationId}` | `{ "orderUpTo": 10000 }` or `null` |

## Not done yet

Sending orders to suppliers automatically, ordering from supplier websites, supplier price history and 1C export.

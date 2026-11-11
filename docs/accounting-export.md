# Export for accounting

Sidebar → **For the accountant** (module `analytics`). **Only the CEO** sees it (an accountant can use the CEO login,
see [roles.md](roles.md)).

Pick the days (ready buttons: last month, this month, last quarter, last year; at most 366 days) and a branch or all
branches, then download one of two files. Days follow the cafe's time (Asia/Almaty). Every download is written to the
audit log.

## Excel

One workbook, headings in the language the admin app is shown in (en / ru / kk):

| Sheet | One row per | Columns |
|---|---|---|
| Sales by day | branch and day | orders, sales before discounts, discounts, paid with points, paid, one column per payment method used (cash, card, Kaspi, Halyk, Freedom, Apple Pay, other), refunds, money taken; a total row |
| Orders | completed order | completed time, order number (as on the kitchen ticket), branch, table, amounts, payment (split payments written out, e.g. `Cash 500; Card 1000`), refunded, tax receipt number and status |
| Items sold | dish and size | quantity, money taken after each order's discount, ingredients cost (saved at sale; empty when a sale has no saved cost). Refunded orders left out |
| Shifts | cashier shift opened in the period | opened/closed by and when, orders, takings, opening cash, cash sales, cash in/out, expected, counted, difference, Z report sent |
| Deliveries | line of a posted supplier invoice | date, invoice number, supplier and BIN, branch, ingredient, quantity, unit, sum, invoice total, paid and when. Cancelled invoices and orders not yet delivered are left out |
| Write-offs | write-off or stocktake difference | when, branch, kind, item, quantity (negative = found extra), unit, reason, note, cost per unit, sum, who |

How the money adds up (the same rules as the shift Z report and the Profit page):

- **Paid** = order total after the discount code, less loyalty points. A split payment counts each part under its own
  method; an order with no method recorded counts as cash.
- **Refunds** = what refunded orders paid. A refunded order counts on the day it was sold.
- **Money taken** = paid − refunds.

## 1C

An XML exchange file in the **CommerceML 2** layout (`КоммерческаяИнформация`, version 2.10), the format 1C uses for
exchange with web shops. It holds:

- **Каталог** — every dish sold (`ВидНоменклатуры` = Блюдо, unit шт / 796) and every ingredient delivered or written off
  (units mapped to OKEI: г 163, кг 166, мл 111, л 112, шт 796). Ids are Yurt's ids, so they stay the same between files;
  a dish size is `<dish id>#<size id>`.
- **Документ** per branch per day — retail sales report: `ХозОперация` = Отпуск товара, the cafe as seller and
  "Розничный покупатель" as buyer, lines at the price after discounts. Requisites: `Вид документа` = Отчет о
  розничных продажах, `Склад` = branch, sum before discounts, discounts, paid with points, and `Оплата: <method>` for
  each payment method.
- **Документ** per branch per day with refunds — `Возврат товара`, with `Возврат: <method>` requisites.
- **Документ** per supplier invoice — `Отпуск товара` with the supplier as seller (`ИНН` = BIN) and the cafe as buyer,
  `Вид документа` = Поступление товаров, number = the supplier's invoice number.
- **Документ** per branch per day — `Вид документа` = Списание товаров (write-offs and stocktake shortages, each line
  with its `Причина`), and Оприходование товаров for stock found extra at a stocktake.

1C setups differ from cafe to cafe, so **the first file should be loaded with the accountant** (for example through the
standard "Universal data exchange" / "Загрузка из CommerceML" processing or a matching rule). If something needs to be
named or grouped differently, change `OneCExchangeWriter` — the data comes from `AccountingExportService` and does not
need to change.

Not in the files yet: payroll (see the Payroll page), the other monthly costs typed on the Profit page (download them
from Profit → Month), and bank fees on card payments.

## API

Both under `/api/v1/admin/accounting`, CEO only, module `analytics`:

- `GET excel?from=yyyy-MM-dd&to=yyyy-MM-dd&locationId=&lang=en|ru|kk` → `.xlsx`
- `GET 1c?from=yyyy-MM-dd&to=yyyy-MM-dd&locationId=` → `.xml` (UTF-8)

Code: `backend/src/Yurt.Application/Features/Accounting/` (`AccountingExportService` gathers the data,
`AccountingSpreadsheet` writes Excel, `OneCExchangeWriter` writes 1C). Tests: `AccountingExportTests`.

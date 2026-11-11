# Profit reports

Sidebar → **Profit** (module `analytics`). **Only the CEO** sees it. The month's profit includes wages, so like Payroll it
is hidden from the platform operator too.

## Month tab

Pick a month and a branch (or all branches). The page shows, with last month next to it:

| Line | Where it comes from |
|---|---|
| Sales before discounts | Completed orders of the month (by the time they were completed, Almaty time) |
| Discounts and points | Discount codes and loyalty points used on those orders |
| Refunds | Completed orders whose money was given back |
| **Money taken** | Sales − discounts − refunds |
| Ingredients | What the ingredients of every sold item cost (refunded orders too: the drinks were made) |
| **After ingredients** | Money taken − ingredients |
| Write-offs | Stock → *Write off*, by reason, at the cost per unit at that moment |
| Stocktake: missing / found extra | Value of the differences found by stocktakes |
| Wages | Payroll → the month's sheet (only with the `payroll` module, and only for **all branches**) |
| Other costs | Typed in on this page: rent, utilities, marketing, repairs, taxes, other |
| **Profit** | All of the above |

**Other costs** belong to one branch or to the whole cafe. For one branch, wages and whole-cafe costs are left out.
*Copy last month's costs* fills an empty month with last month's rows, to change from there.

Supplier invoices are **not** a cost line: buying beans is not spending them. They show up as *Ingredients* when the
drinks are sold, and as *Write-offs* or *Stocktake* when they are lost.

## By dish tab

Every dish and size sold in the month: how many, money taken (after each order's discount, shared out over its lines),
ingredient cost (toppings included), profit, profit per item and food cost %. Sort by total profit, profit per item,
lowest food cost, or most sold. Refunded orders are left out. A dish without a recipe shows "—" for its cost.

## How the cost of a sale is kept

When an order is **accepted**, Yurt saves what one portion of each line costs right then (recipe for that size + each
topping, × the ingredients' cost per unit) in `OrderItems.UnitCost`. Later price changes do not change past months.
Orders accepted before this was added have no saved cost: the reports price them at **today's** costs and say how many.
An ingredient with no cost per unit counts as 0 and the dish is flagged.

Both tabs download as Excel in the language the admin app is shown in.

API (all under `/api/v1/admin/profit`): `GET items?month=&locationId=`, `GET items/export`, `GET month?month=&locationId=`,
`GET month/export`, `GET|POST expenses`, `PUT|DELETE expenses/{id}`, `POST expenses/copy-previous`.

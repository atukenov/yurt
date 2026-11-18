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

## Menu analysis tab

Which dishes to keep, push, fix or drop. All sizes of a dish are counted together; dishes without a recipe are left out
(listed under the table) because their profit is unknown.

Two lines split the menu:

- **Popular**: the dish sold at least 70% of an even share of all portions (portions ÷ number of dishes × 0.7).
- **Earns well**: profit per portion is at or above what a portion earns on average over the whole menu.

| Group | Popular | Earns well | What to do |
|---|---|---|---|
| Star | yes | yes | Keep as is |
| Puzzle | no | yes | **Push**: put it first, tell the staff to suggest it |
| Plowhorse | yes | no | **Fix**: raise the price or cut the recipe cost (the table says how much more a portion must earn to reach the average) |
| Dog | no | no | **Drop**, or rethink it |

**ABC** is on top of that: A dishes bring the first 80% of the month's profit, B the next 15%, C the last 5% and anything
that loses money. Click a group card to show only its dishes. It uses the same sales, discounts and costs as *By dish*
(refunded orders are left out), for the month and branch chosen at the top.

The three tabs download as Excel in the language the admin app is shown in.

API (all under `/api/v1/admin/profit`): `GET items?month=&locationId=`, `GET items/export`, `GET menu-analysis?month=&locationId=`, `GET menu-analysis/export`, `GET month?month=&locationId=`,
`GET month/export`, `GET|POST expenses`, `PUT|DELETE expenses/{id}`, `POST expenses/copy-previous`.

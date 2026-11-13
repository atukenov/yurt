# Combos

A combo is several dishes sold together at one set price, for example a cappuccino and a croissant. The guest app and
Yurt POS list a combo like any dish; the cafe sets it up on its own screen.

## What you can set (Admin → Combos)

- **Name** (and Russian / Kazakh names), **description**, **category** and **price**. The combo sits in the category you
  choose, so it shows among the coffee, or in a "Combos" category of its own.
- **What is in it**: pick dishes, how many of each (1 to 20) and, for a dish with sizes, which size. At least two pieces
  in total. A combo cannot contain another combo.
- **Branches**: tick none to sell it everywhere, or only the branches that should have it.
- **On sale**: switch a combo off without deleting it.
- The form shows what the dishes cost bought separately and what the guest saves.
- A combo that was already sold cannot be deleted (old orders still show it): it is switched off instead.
- A dish that is inside a combo cannot be deleted from the menu until the combo drops it.

A combo has a **set price**: it offers no add-ons or sizes of its own, and sale prices come from the menu, never from the
app or the till. The combo price is the same at every branch.

## What happens when a combo is sold

- **Order total**: the combo price times how many were bought. Discount codes, the manager-PIN discount and tips work as
  for any dish (a tip is never part of the total).
- **Stock**: each dish inside takes its count and its recipe ingredients off the stock, per combo sold (for example 2 buns
  per combo). Counts and ingredients are taken off when an app order is accepted, or at once for a counter sale. The
  combo itself can also have a count on the Stock page.
- **Stop-list**: a combo is off the guest menu and the till (and refused at checkout) while any dish inside it is on the
  stop-list, sold out, short of ingredients, or not sold at that branch. It comes back by itself when the dish does.
- **Kitchen ticket**: the combo line lists what is in it, so the kitchen sees the pieces. The guest's receipt shows the
  combo as one line.
- **Cost and profit**: the cost saved on the sale is the sum of the dishes inside (their recipes). It is empty when none of
  the dishes has a recipe.
- **History**: what was in the combo is saved on every sold line, so changing a combo later does not change old orders,
  their stock or the report.

## Reports (CEO only)

Admin → Combos has a **Combo sales** block for the CEO: for a month and (optionally) a branch, per combo: how many were
sold, sales after discounts, what the same dishes would have cost separately, **what guests saved**, ingredient cost and
profit, plus the combos' share of all sales. Combos also appear in Profit by dish and in the accounting export as a
dish of their own. API: `GET /admin/combos/report?month=2026-11&locationId=`, CEO only, module `analytics`.

## Where it works

- **Guest app**: the dish page and the menu tile list what is inside ("Includes"). The order screens show it too.
- **Yurt POS**: the tile shows "Combo: Latte + 2 × Bun". Tapping adds it straight to the sale. The menu is saved on the
  till, so combos can be sold offline: a sale made while offline keeps its own time and the server still takes the dishes
  inside off the stock. Offline sales skip the stop-list check (they already happened) but use the menu's price.
- **Roles**: Admins and the CEO make and change combos (like the menu); only the CEO sees the money report.

## For developers

- `MenuItems.IsCombo` marks a combo (it is a menu item, so pricing, branches, photos, categories and ordering all reuse
  the dish code). Tables: `ComboParts` (dishes inside a combo; `ItemId` is restricted so an used dish can't vanish) and
  `OrderItemComboParts` (snapshot saved on a sold line: name, size, quantity, separate price).
- `ComboSales` (Yurt.Application/Features/Combos) is the single place the two order paths (`OrderService`,
  `CounterSaleService`) use to read parts, check the stop-list and take the snapshot. `StockLine.Expand` opens a combo
  into its dishes for stock counts, ingredient needs and shortage checks; `IngredientCostCalculator` sums the dishes' cost.
- `DishOptions` offers no add-ons for a combo. The admin dish list leaves combos out; they are managed at `admin/combos`.
- Not done: combos with choices inside ("any drink from this list"), different prices per branch, and time-limited combos.

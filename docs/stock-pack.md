# Stock pack: costs, write-offs, moves and stocktakes

Part of the `own_pos` module. Builds on the Stock page (dishes, ingredients, recipes).

## What each person can do

| Action | Where | Who |
|---|---|---|
| Write off (spilled, expired, staff drink, broken, other) | Sidebar → **Write off / move** | Staff and admins |
| Move stock to another branch | Sidebar → **Write off / move** | Staff and admins |
| Delivery arrived (amount + what it cost) | Stock → Ingredients → *Delivery arrived* | Staff and admins (page is in the admin menu) |
| Cost per unit by hand | Stock → Ingredients → *Cost per unit* | Admins |
| Stocktake | Stock → Stocktake | Staff and admins (page is in the admin menu) |
| Food cost and margin per dish | Stock → Food cost | Admins |
| Write-offs of the last 30 days by reason | Stock → History | Admins |

## How the numbers work

- **Cost per unit** of an ingredient (for example 1 g of beans) is a cafe-wide number. A delivery with a price makes it the
  average of what is already on the shelf at the old cost and the new delivery at its price.
- **Food cost** of a dish = recipe amounts × cost per unit, per size. Toppings are not included. Above 35 % is shown in red.
  An ingredient without a cost counts as 0 and is named under the dish.
- **Write-offs and moves never take a count below zero.** The screen warns as soon as the amount is more than is left,
  and the server refuses it too.
- **Stocktake** replaces the counts with what was counted and shows the difference from what was expected, with its
  money value. Only items the branch counts can be in a stocktake.
- **Orders**: before accepting an order, the Orders screen checks whether the branch has enough. If not, it asks first.
  Accepting anyway stops the count at zero and saves a *shortfall* row in the history with what was missing.

Every change is in Stock → History with who did it, when, the reason or other branch, the note and its cost.

API (all under `/api/v1/admin/stock`): `POST restock`, `POST write-off`, `POST transfer`, `POST stocktake`,
`GET write-offs`, `GET food-cost`, `GET order-check/{orderId}`, `GET history?types=`; and
`PUT /api/v1/admin/ingredients/{id}/cost`.

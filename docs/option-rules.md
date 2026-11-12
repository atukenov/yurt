# Option rules for dishes

Options are the add-ons of a dish: milk, syrups, sauces, extras. This page says what a café can set up and how the
customer app, the admin app and Yurt POS follow it.

## What you can set (Admin → Menu)

- **Rules for choices** (Menu → the add-ons tab, panel below the list). Options with the same *group* (milk, syrup...) form one
  choice. For each group you can say:
  - **Required**: the guest must choose from this group before adding the dish (for example milk).
  - **At least / at most**: how many options of the group may be picked. Empty "at most" means no limit.
  - A title for guests in English, Russian and Kazakh.
  - A group with no rule keeps the old behaviour: at most one, may be none. "Reset" returns a group to that.
  - Tip for a required group: add a free option such as "Regular milk", so the guest is never forced to pay extra.
- **Options for this dish** (dish form). Tick the options this dish offers. Leave all unticked and the dish offers the
  options of its category, as before. A dish with ticks offers only those.
- **Prices by branch**. In the dish form and in the option form, a price typed for a branch replaces the usual price
  at that branch only. Leave a branch empty to use the usual price. Sizes (variants) keep their own prices at every
  branch. The branch price of a dish shows when the dish is limited to a branch or when the branch has its own price.

A group that has nothing left to pick (every option sold out at the branch) never blocks a dish.

## Where the rules apply

- **Customer app**: the dish page and the quick-add sheet show "Required" / "Choose 1–2" / "Up to 2" next to each
  group, swap the choice when only one is allowed, stop at the largest number, and ask for a missing required choice.
- **Yurt POS**: the same choice sheet; the *Add* button stays off until every required group is chosen. The menu is
  saved on the till, so the rules work offline too.
- **Server**: orders (app, group orders, POS) are checked again. A sale that breaks a rule is refused with a message the
  guest or cashier can read. Prices and option names always come from the menu at the branch, never from the app, so
  an old app cannot sell at an old price. A POS sale made while the internet was down is not refused for breaking a
  rule (it already happened), but its prices are still the menu's.

## For developers

- Tables: `MenuOptionGroups` (rule per group key), `MenuItemToppings` (options picked for a dish),
  `MenuToppingLocations` (option price per branch), and `MenuItemLocations.Price` (dish price per branch).
- One place decides what a dish offers and checks it: `DishOptions` in `Yurt.Application/Features/Menu/Services`.
- API: `GET/PUT/DELETE /admin/menu/option-groups`; dish and option create / update take `toppingIds` and
  `locationPrices`; the guest menu item carries `optionRules`.

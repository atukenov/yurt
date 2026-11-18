# Menu management (admin app)

How the **Menu** section of the admin app is laid out. Same data, same roles (Admin and CEO; see [roles.md](roles.md)); only
the screens changed. ru / kk / en wording is in `core/lang.service.ts`.

## One sidebar entry, three screens

The sidebar has one **Menu** entry. A bar at the top of each screen switches between:

- **Dishes** (`/menu`): dishes and options, described below.
- **Combos** (`/combos`): see [combos.md](combos.md).
- **Time menus** (`/time-menus`): see [time-menus.md](time-menus.md).

The old links still work. The bar is the `app-menu-sections` component; add a new menu screen there.

## Dishes

- **Categories** are on the left (a menu button on phones). Pick one to drag dishes into order.
- **Search and filters**: search by name in English, Russian or Kazakh, and filter by **All / Unavailable / No photo / No price**.
- Each row shows the photo, the name, how many **sizes** and **options** the dish has, the price and an **availability switch**.
  The switch saves at once (and flips back if the save fails). The server replaces the whole dish on save, so the list sends
  everything it already knows about the dish.
- **Bulk edit**: tick dishes (or the box in the header for every dish shown). A bar offers **Show**, **Hide**, **Move to category**
  and **Change prices** by a percent. A price change asks to confirm and shows one example; it also changes sizes and branch
  prices. Dishes are saved one by one; any that fail stay ticked and a toast says how many. Combos are not included.

### The dish editor

Four tabs, so the form stays short:

| Tab | What is in it |
| --- | --- |
| **Basics** | Names and descriptions (EN / RU / KZ), price, category, photo (upload or link), available, new. |
| **Sizes & options** | Sizes with their own price, and the options picked by hand for this dish (see [option-rules.md](option-rules.md)). |
| **Branches** | "Only at" branches and branch prices. |
| **Details** | iiko mapping (when the module is on), allergens and nutrition ([allergens-nutrition.md](allergens-nutrition.md)), barcode and sold by weight ([barcodes-and-scales.md](barcodes-and-scales.md)). |

A save error about names or price jumps back to **Basics**.

## Options

The second tab of the Dishes screen (it was called "Toppings"). Every option group (milk, syrup...) is one card:

- On top: the rule in short ("Required · Min 1 · Max 2") and a **Rules** button that opens the rule fields (required, at least,
  at most, title in three languages, Save / Reset).
- Inside: the options of the group with price, categories and availability, and **Add option**, which starts with that group.
- Options without a group sit in a card of their own.

## Code

- `frontend/projects/yurt-admin/src/app/features/menu/menu-management.component.{ts,html}`: dishes, options, editor, bulk edit.
- `.../shared/menu-sections/menu-sections.component.ts`: the Dishes / Combos / Time menus bar.
- `.../layout/admin-nav.ts`: sidebar data (one `/menu` entry).
- Tests: `menu/menu-bulk.spec.ts` (bulk price change), `layout/admin-nav.spec.ts`; run with `npx ng test yurt-admin --watch=false`.

## On a phone

On screens narrower than 768 px the admin app shows a top bar with a menu button. The button opens the side menu as a drawer, and the drawer closes when you pick a page. Live orders show the list first; tap an order to see it and use "Back" to return. Tables scroll sideways instead of being cut off, and the menu toolbar wraps onto two lines.

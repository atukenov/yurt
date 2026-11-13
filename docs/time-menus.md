# Time menus

Breakfast, business lunch, happy hour: a dish or a whole category can be on the menu **only in certain hours and days**, and a **special price** can apply only in those hours. Customers in the app, table QR orders and Yurt POS all follow it, each branch by its own clock. Core menu feature, no module switch.

## What a cafe can set up (Admin → Menu → Time menus)
A **time menu** is a window of the week:
- **Name** (for you only), **days** (any of Mon-Sun), **from / until** (HH:mm). An end earlier than the start runs past midnight: 22:00 to 03:00 on Saturday belongs to Saturday night. An end of 00:00 means midnight.
- **Branch**: all branches, or one. A window for one branch does nothing at the others.
- **Switched on**: a switched-off window is ignored (what is tied to it stays on the menu).
- **On the menu only in these hours**: tick categories and dishes. Outside the window they leave the menu in the app, the till and the table link, and cannot be ordered. A dish or category can sit in one window only (ticking it in another window moves it).
- **Special prices in these hours**: for a category or for one dish, either a **percent off** or a **new price**. A rule for a dish wins over a rule for its category. If several windows run at once the lowest price wins.
  - A new price is for the dish itself. **Dishes with sizes follow only a percent off** (sizes keep their own prices otherwise).
  - Add-on (option) prices never change.
  - The branch price of a dish (see `option-rules.md`) is replaced by a new price, or reduced by a percent.

The **stop-list and the branch lists** of dishes work as before: a time menu hides on top of them, it never shows something that is off.

## Branch clock
Each branch has a **time zone** (Locations → Clock of this branch, default Asia/Almaty). A window runs by that clock, so a breakfast at 07:00-11:00 starts at 07:00 in every branch, wherever it is. Day-light saving is handled by the zone.

## Where it applies
- **Customer app / table QR**: menu, categories and a single dish only show what is on the menu now. A time price shows as the price now, with the usual price crossed out. The app asks the server when the next window opens or closes (`GET menu/next-change?locationId=`) and loads the menu again at that moment.
- **Yurt POS**: same as the app, and the till reloads its menu by itself when a window opens or closes (it keeps what is on screen, a sale in progress is not interrupted).
- **Server (the real check)**: orders (app, group orders, table QR) are refused with "'X' is not on the menu at this time." outside the window. Prices are always set by the server from the clock at that moment, never taken from the app.
- **Counter sales (POS)**: a dish is allowed for **10 minutes after its window closes**, so a dish rung up at 10:59 can still be paid at 11:01. Prices follow the time of the sale.
- **Offline POS**: the till shows the menu it saved last, so prices on its screen and receipt may be those of the moment it was saved. Offline sales are never refused for time, and the server sets the price from the time of the sale when the sale arrives. Reports use the server's price.

## For developers
- Tables: `MenuSchedules` (window), `MenuTimePrices` (rule: schedule + dish or category + `Price` or `PercentOff`), `MenuItems.ScheduleId` and `MenuCategories.ScheduleId` (what the window controls; deleting a window sets them back to null), `Locations.TimeZone`.
- `TimedMenu` (Yurt.Application/Features/Menu/Services) is the one place that decides what is running, what is on the menu and the price now. `MenuService`, `OrderService` and `CounterSaleService` all use it. The guest menu cache is keyed by the set of windows running, so a cached menu is never served after a window opens or closes.
- API: `GET/POST admin/menu/schedules`, `PUT/DELETE admin/menu/schedules/{id}`; guest `GET menu/next-change`; guest menu items carry `regularPrice` while a time price runs (also on sizes).
- Demo data has a weekday 16:00-18:00 "Happy hour" with 20% off the cold drinks.

## Not built yet
- Date ranges (a seasonal menu between two dates) and holidays.
- A label such as "Breakfast until 11:00" on the dish in the app, and showing a dish greyed out instead of hiding it.
- Time-based add-on prices, and a time price that is higher than the usual one for sizes.

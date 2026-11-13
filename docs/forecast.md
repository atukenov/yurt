# Sales forecast and prep plan

Sidebar → **Forecast and prep** (module `own_pos`). Pick a branch and a day (Today, Tomorrow, or any date) and Yurt says
how many of each dish to expect, hour by hour, and how much of each ingredient they will use compared with what the
branch has now. **Every staff role** can open it: it shows item counts and ingredient amounts, never money (see
[roles.md](roles.md)). The **Print** button prints the plan for the kitchen wall.

## How the forecast is made

- It looks at the **same weekday** over the last **8 weeks** (a Saturday is learned from past Saturdays) at that branch.
- Only **completed** orders count. Declined orders were never made. Counter sales and app orders both count.
- A past day on which the branch sold nothing is treated as **closed** and left out, so a holiday does not drag the
  forecast down.
- Recent weeks count more: the last week weighs 8, the one before 7, and so on down to 1.
- A **combo** counts as the dishes inside it, and a dish counts per size.
- The day is split by the cafe's local time (Asia/Almaty) into *before 11*, *11–15*, *15–18* and *after 18*, so the plan
  says how much to have ready for each part of the day, plus the busiest hour.
- Portions are rounded **up**, so the shelf is not short.

Two knobs on the page change the numbers without touching the history:

| Knob | Use it for |
|---|---|
| **Busier or quieter** (−30 % … +50 %) | A holiday, an event next door, bad weather. |
| **Safety margin** (0–30 %, default 10 %) | How much extra to prepare on top of the average. |

**How sure** shows how many past days it learned from: none (0), low (1–2), medium (3–5), high (6–8). With fewer than
3 days the page says the numbers are a rough guide. A new branch has nothing to learn from yet and shows an empty plan.

## Ingredients

Each dish's recipe (for its size) and the recipes of the toppings on it are applied to every past day, then averaged the
same way as the dishes. The table lists, for each ingredient, what the day needs, what the branch has in stock (— when
the branch does not count it) and what is **missing**. Missing ingredients come first and are highlighted: restock them or
add them to a supplier order (see [purchasing.md](purchasing.md)). Dishes without a recipe cannot be turned into
ingredients; the page says so when no dish of the day has one.

Stock is compared as it is **now**. For tomorrow's plan, deliveries that arrive in between are not counted.

## Not covered yet

- **Semi-finished items** (a batch of syrup or dough with its own recipe) do not exist in Yurt yet, so the plan lists the
  raw ingredients. Once they exist, the plan can say how many batches to make.
- Weather, holidays and promotions are not learned automatically; use *Busier or quieter* for them.
- It does not place orders by itself.

## API

`GET /api/v1/admin/forecast?locationId=…&date=yyyy-MM-dd&adjust=0&buffer=10` (any staff login, module `own_pos`). `date`
defaults to today in the cafe's time, `adjust` is −100…200 and `buffer` 0…200 (percent). It returns the weekday,
`daysUsed` and `confidence`, `totalItems`, `hourly` (24 numbers) with `peakHour`, `dishes` (expected for the day and for
each part of the day), `ingredients` (needed, in stock, missing) and `history` (the past days it used). The maths lives
in `ForecastMath` and `ForecastService`; tests are `ForecastMathTests`, `ForecastTests` and `forecast.spec.ts`.

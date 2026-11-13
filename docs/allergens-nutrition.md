# Allergens and nutrition on the menu

Guests can see which allergens a dish contains and how many calories it has, in the customer app, on the QR
(table) menu and on the dish page. The text follows the app language (ru / kk / en).

## What is stored

Per dish (table `MenuItems`, all columns optional, nothing is required):

| Field | Meaning |
|---|---|
| `Allergens` | Comma-separated codes of the 14 EU allergens: `gluten, crustaceans, eggs, fish, peanuts, soy, milk, nuts, celery, mustard, sesame, sulphites, lupin, molluscs` |
| `Calories` | kcal per serving |
| `Protein`, `Fat`, `Carbs` | grams per serving (one decimal) |
| `WeightGrams` | serving weight in grams |

Numbers are **per serving of the dish as listed**. Sizes (small / large) and add-ons do not change them; if a size
differs a lot, say so in the dish description.

The migration `NutritionAllergens` only adds nullable columns, so it is safe to deploy first and roll back.

## Admin

Menu → edit a dish → box **Allergens and nutrition**: five number fields and a tick box per allergen. Leave a
number empty if you do not know it. Saving an older admin build that does not send the box keeps what is stored.
Roles: same as editing the menu (Admin, CEO); nothing here is money.

## API

- `GET /menu` (guest) and favorites: each dish has `nutrition` = `{ calories, protein, fat, carbs, weightGrams, allergens[] }`,
  or `null` when nothing is filled in. Allergens are codes; the apps translate them (`allergen.<code>` keys).
- Admin dish create / update take the same `nutrition` object; admin dish reads return it.
- Unknown allergen codes and negative numbers are dropped by the server (`Nutrition.cs`).

## In the apps

- Menu card: `NNN kcal` and a short allergen line, only when filled in.
- Dish page: a "Contains" chip row and a "Nutrition" block (kcal, protein, fat, carbs, serving weight).

## Tests

- `backend/tests/Yurt.UnitTests/NutritionTests.cs` — cleaning, round trip, negatives, the 14 codes.
- `scripts/nutrition.test.mjs` — backend and frontend code lists match, every allergen has ru / kk / en names in both apps.
- Demo data (`docs/demo-data.md`) fills allergens and nutrition for the demo dishes.

## Not done

- Excel menu import does not have nutrition columns yet.
- Allergens of add-ons (oat milk, syrups) are not tracked; only the dish itself.
- The offline till (POS) does not show nutrition.
- ru / kk allergen names are not checked by a native speaker.
- Not click-tested in a browser; the apps build and the unit tests pass.

This is information for guests, not a legal guarantee: the cafe is responsible for keeping it correct.

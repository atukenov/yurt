# Guest messages (guest groups, automatic pushes, personal promo codes)

Admin → **Guests → Guest messages** (cafe admins, `marketing` module). Pushes also need Firebase set up and the
`push` module on ([push-notifications-setup.md](push-notifications-setup.md)); without them the page shows a warning and
nothing is sent, but groups and results still work.

## Guest groups

Worked out live from completed orders (nothing is stored or synced):

| Group | Who |
|---|---|
| All guests | every active account |
| New guests | exactly one completed order |
| Regulars | 5+ completed orders in the last 30 days |
| Not seen for 30 / 60 / 90 days | ordered before, no completed order in that many days |
| Birthday this week | birthday today or in the next 6 days (birthday from the profile) |
| Big spenders | top 10% of paying guests by money spent |
| Signed up, never ordered | account but no completed order |

"Push reaches" counts guests with the app on a phone (a registered device) who did not switch offers off.

## Messages

- **Automatic** (one of each per cafe, on/off switch): *Come back* (no order for N days, once until they order again),
  *Birthday* (N days before, once a year), *Thanks after the first order* (N days after the first order, once; only
  guests whose first order was within the last week of that window, so switching it on doesn't message old guests).
  A background job (`MarketingJob`) runs every 15 minutes, sends only from 10:00 to 21:00 cafe time (Asia/Almaty), at
  most 300 guests per message per run.
- **One-off**: written once, sent by an admin with **Send now** to one group, between 09:00 and 22:00; each is sent once.

Rules for every message: a guest gets at most one message in 3 days (a birthday message is the exception), never one
when they switched **Offers from the cafe** off on Profile, and only on phones with the app. Order-status pushes are not
affected by that switch.

Texts are written in ru / kk / en; each phone gets its app language (sent with the push token), falling back to Russian,
then English. `{name}`, `{code}`, `{discount}` and `{days}` are filled in per guest. If a message gives a code and the
text has no `{code}`, the code is added on a new line.

## Personal promo codes

A message can give each guest their own code (`BACK-…`, `BDAY-…`, `THANKS-…`, `GIFT-…`): one use, only for that guest
(`DiscountCode.CustomerUserId`), valid N days, optional minimum order. These codes are not listed on the Discount codes
page. The guest sees them under the code field in the cart (`GET /discount-codes/mine`) and taps one to apply it.

**Results** per message: guests it was sent to, personal codes used in orders (declined orders don't count), and the
money of those orders.

Tables: `MarketingCampaigns`, `MarketingMessages` (one row per guest per message: used for the rules above and results).

# ALTYNCUP achievements — badge reference

28 badges. Each entry gives the artwork, what the badge stands for, and the condition that unlocks it.

**Status (2026-09-12):** 18 of the 28 are wired and live in `achievements.ts`, backed by `GET /auth/me/stats` (expanded to return streaks, timing, distinct locations, and repeat-item counts alongside order count and spend). The remaining 10 are documented below but not wired, for two different reasons:

- **Needs a feature that doesn't exist yet** — Social Butterfly (referrals) and Barista Fan (order ratings) need product features with no data model at all today. First Order is dropped rather than deferred: with no "browsing vs. buying" distinction in the order flow, its condition would be identical to First Sip's.
- **Needs item classification** — Milk Artist, Iced Coffee Ace, Roast Master, Pour-Over Pro, Decaf Diplomat, Flavor Adventurer, Sweet Tooth, and Double Shot ("2+ *coffees*" specifically, not just 2+ items) all need to tell a coffee from a pastry or a latte from a decaf. `MenuItem` only carries a free-text category name synced from iiko, with no structured tag for this — add one before wiring these.

Rows below are marked **live** with their current condition, or **not wired** with the blocking reason.

---

## Getting started

### First Drink — `first-drink.png` · live (`first_sip`)
A trophy with an "i" marker. The entry point of the passport: it records the very first drink bought through the app.
**Claim:** complete your first order of any drink.

### First Order — `first-order.png` · not wired (dropped)
A companion to First Drink, marking the first completed and paid transaction rather than the first drink. Not wired: a "Completed" order in this system is already a paid one, so this condition would fire at the exact same instant as First Sip — effectively a duplicate badge under different art.
**Claim:** complete and pay for your first order.

### Coffee Beginner — `coffee-beginner.png` · live (`coffee_rookie`)
The Level 1 cup. Confirms the account is genuinely active rather than a single trial order.
**Claim:** complete 3 orders.

---

## Volume milestones

### Gold Cup — `gold-cup.png` · live (`golden_cup`)
A gold trophy for a substantial order history — the first milestone that takes real commitment.
**Claim:** complete 10 orders. *(Suggest raising to 50 if you want more space between this and Constant; the current code uses 10.)*

### Constant — `constant.png` · live (`altyn_regular`)
A looping arrow around a cup: awarded for rhythm rather than raw volume. The customer who comes back on a schedule.
**Claim:** order at least once a week for 8 consecutive weeks — the customer's best-ever streak, computed from order history (`maxWeeklyStreak`), not the currently-active one, so it can't be revoked by a later idle week.

### Star of the ALTYNCUP — `star-of-altyncup.png` · live (`altyncup_star`)
A gold star carrying the ALTYNCUP mark. The house's standing honour, tied to overall loyalty rather than one behaviour.
**Claim:** 50 orders. The doc's original proposal — reach the highest loyalty tier and hold it for 3 months — needs a tier concept the iiko integration doesn't track; order count stands in until that exists.

### 100 Cups — `hundred-cups.png` · live (`century_sipper`)
The numeric hundredth-cup badge, rendered as "100" with a gold cup. A landmark worth celebrating in the UI, not just listing.
**Claim:** order 100 drinks in total (`totalDrinks`, the sum of item quantities across completed orders — not order count).

### Coffee Baron — `coffee-baron.png` · live (`big_spender`)
A jewelled crown. The prestige badge for lifetime spend.
**Claim:** reach 500,000 ₸ in lifetime spend.

### Champion — `champion.png` · live (`altyn_champion`)
A star trophy, competitive in tone — better tied to a contest than to cumulative spend.
**Claim:** 750,000 ₸ lifetime spend. The doc's original proposal — finish first in a monthly ALTYNCUP challenge — needs a contest/leaderboard feature that doesn't exist; spend stands in, set above Coffee Baron's threshold so Champion still reads as the more prestigious of the two.

---

## Habits and timing

### Early Riser — `early-riser.png` · live (`early_riser`)
A rooster against a sunrise. For the morning regulars who arrive before the rush.
**Claim:** 10 orders before 09:00 Almaty time (`earlyOrders`).

### Midnight Brew — `midnight-brew.png` · live (`midnight_brew`)
A cup under a moon and stars. The counterpart to Early Riser, for late-evening visits.
**Claim:** 5 orders after 21:00 Almaty time (`lateOrders`).

### Weekend Warrior — `weekend-warrior.png` · live (`weekend_warrior`)
A cup with a shield. For customers whose habit is specifically the weekend coffee, not the weekday commute.
**Claim:** order on 4 consecutive weekends, Saturday or Sunday counting as the same weekend (`maxWeekendStreak`, best-ever run).

### Streak Master — `streak-master.png` · live (`streak_master`)
A flame. Unbroken daily attendance — the most demanding of the habit badges, and the one most worth surfacing progress for.
**Claim:** order on 7 consecutive days (`maxDailyStreak`, best-ever run).

### Caffeine Shield — `caffeine-shield.png` · live (`caffeine_shield`)
A crest reading "Caf+". For high-volume days rather than high-volume months.
**Claim:** 3 or more drinks in a single day (`maxDailyDrinks`).

### Double Shot — `double-shot.png` · not wired (needs item classification)
Two cups on a saucer. For ordering more than one coffee in a single visit — often the customer buying for someone else.
**Claim:** place one order containing 2 or more coffees. Not wired: telling a coffee item from a pastry needs the same item-tagging this doc's taste section is blocked on below — a total-item-count proxy would misfire on "1 latte + 1 croissant."

---

## Taste and exploration

### Flavor Adventurer — `flavor-adventurer.png` · not wired (needs item classification)
A compass. Rewards breadth: trying across the menu instead of repeating one favourite.
**Claim:** order 5 different drinks from at least 3 categories.

### Milk Artist — `milk-artist.png` · not wired (needs item classification)
A latte with rosetta art. For customers who favour milk-based drinks.
**Claim:** order 20 milk-based drinks (latte, cappuccino, flat white, raf).

### Iced Coffee Ace — `iced-coffee-ace.png` · not wired (needs item classification)
A tall iced cup. For cold-drink loyalty across the year, not just in summer.
**Claim:** order 15 iced drinks.

### Roast Master — `roast-master.png` · not wired (needs item classification)
A roasting drum with beans and flame. For the darker, stronger end of the menu.
**Claim:** order 10 espresso-based or dark-roast drinks.

### Pour-Over Pro — `pour-over-pro.png` · not wired (needs item classification)
A gooseneck kettle over a dripper. For customers choosing filter methods over espresso.
**Claim:** order 10 filter or pour-over drinks.

### Decaf Diplomat — `decaf-diplomat.png` · not wired (needs item classification)
A shield marked "Decaf". The quiet badge — decaf drinkers get the same recognition as anyone else, which is the point of including it.
**Claim:** order 10 decaf drinks.

### Sweet Tooth — `sweet-tooth.png` · not wired (needs item classification)
A croissant beside a cup. For pairing coffee with something from the pastry case.
**Claim:** add a dessert or pastry to 10 orders.

These seven all need `MenuItem` to carry a structured tag (milk-based, iced, roast level, filter method, decaf, dessert) — today it only has a free-text category name synced from iiko, too unreliable to pattern-match against reliably. Add the tag field (and a way to set it per item) before wiring any of these.

### Perfect Brew — `perfect-brew.png` · live (`perfect_brew`)
A cup with rising steam. For dialling in one drink exactly to preference and sticking with it.
**Claim:** order the same drink (same menu item + variant) 5 times (`maxRepeatItemCount`) — a repeat-behavior proxy, since there's no "save a custom drink" feature to hang the literal claim on.

---

## Program engagement

### Bean Collector — `bean-collector.png` · live (`bean_collector`)
A treasure chest of beans. A collector's badge for accumulating bonus points instead of spending them immediately.
**Claim:** hold 5,000 bonus points at once — reads the live iiko loyalty balance, fetched separately from `CustomerStats` since it's not order-derived; locked whenever that balance hasn't loaded rather than treated as unlocked.

### Bean Counter — `bean-counter.png` · live (`bean_counter`)
An abacus strung with beans. The opposite instinct to Bean Collector: using the bonus system actively and well.
**Claim:** redeem bonus points on 5 separate orders (`redeemedOrders`, orders with `LoyaltyPointsSpent > 0`).

### Globetrotter — `globetrotter.png` · live (`globetrotter`)
A globe with a cup. For visiting multiple ALTYNCUP locations rather than a single home café.
**Claim:** order from 5 different locations (`distinctLocations`). Repointed here from `wolt_rider`, which now falls back to its 🛵 emoji with no dedicated art — Wolt delivery and location coverage are different behaviours and Globetrotter's art fits this one better.

### Social Butterfly — `social-butterfly.png` · not wired (needs a feature)
Cups linked in a network. For bringing other people in — referrals and group orders.
**Claim:** refer 3 friends who each complete an order. No referral system exists to track invites or attribute the resulting orders — this is a product feature to build, not a threshold to wire.

### Barista Fan — `barista-fan.png` · not wired (needs a feature)
A tamper with a thumbs-up. For engaging with the craft side: rating drinks and leaving feedback.
**Claim:** rate 5 completed orders. No order-rating feature exists — same as Social Butterfly, this needs new product surface before it can be evaluated.

---

## Implementation notes

**Locked state.** Every badge renders its own locked state with `grayscale` + `opacity-40` in the `<img>` itself — no second set of files. Achievements without art (`wolt_rider`) fall back to their emoji `icon` the same way.

**Show progress, not just status.** `progressInfo()` in `achievements.component.ts` covers all 18 live badges now, each mapped to the stat (or, for Bean Collector, the live loyalty balance) that drives its condition.

**Unlock moment.** Implemented on the Achievements page itself: opening it compares the unlocked set against `localStorage['yurt_seen_achievements']` and shows a toast for anything newly earned since the last visit (skipped on a customer's very first visit, when everything already-earned would otherwise read as "new"). It is **not** wired into the order-confirmation screen as originally proposed — that would need the confirmation flow to load stats/achievements too, which is a separate, more invasive change than this pass covers.

**Streaks.** Constant, Streak Master, and Weekend Warrior are all computed server-side in `GET /auth/me/stats` from the customer's full completed-order history (`maxWeeklyStreak`, `maxDailyStreak`, `maxWeekendStreak`) — the *best-ever* run, not the currently-active one, so a badge can't be revoked by a later idle day/week. Recomputed on every call rather than stored, since a single customer's order history is small enough that this is cheap; revisit if that stops being true.

**Wording.** Names above match the reference sheet labels. The sheet contained one typo ("Bean Cuanter") which is corrected to Bean Counter here. Two labels repeat across the sheet (Sweet Tooth, Bean Counter) — one file each is kept.

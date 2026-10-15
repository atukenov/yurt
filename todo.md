# Altyncup Product Roadmap

Open work is tracked in **[GitHub Issues](https://github.com/atukenov/altyncup/issues)** — each issue has a full description, a ready-to-copy-paste implementation prompt, a suggested model/effort, and acceptance criteria. This file used to hold the full checklist directly; it was migrated to GitHub on 2026-09-17 so there's one source of truth instead of two drifting copies. Shipped work isn't re-listed here — see git history / merged PRs for what's already done.

## 🔴 Critical

- [ ] [#32](https://github.com/atukenov/altyncup/issues/32) — Payments: replace sandbox mock with real Kaspi Pay integration (no real money is processed today)

## Admin Panel

- [ ] [#17](https://github.com/atukenov/altyncup/issues/17) — Upload menu item images directly (replace URL-paste) _(low priority)_
- [ ] [#18](https://github.com/atukenov/altyncup/issues/18) — Inventory and stock tracking per menu item _(low priority)_
- [ ] [#19](https://github.com/atukenov/altyncup/issues/19) — Time-based menu availability scheduling
- [ ] [#20](https://github.com/atukenov/altyncup/issues/20) — Thermal receipt printer integration for kitchen tickets
- [ ] [#21](https://github.com/atukenov/altyncup/issues/21) — Scheduled monthly summary email report _(low priority)_
- [ ] [#22](https://github.com/atukenov/altyncup/issues/22) — Telegram bot for critical alerts _(low priority)_
- [ ] [#23](https://github.com/atukenov/altyncup/issues/23) — Structured classification tags on MenuItem (unlocks 8 achievement badges)
- [ ] [#37](https://github.com/atukenov/altyncup/issues/37) — Complete label/for association on all admin text-input fields

## iiko Integration

- [ ] [#24](https://github.com/atukenov/altyncup/issues/24) — Sync stop-list (out-of-stock) items to the customer menu
- [ ] [#25](https://github.com/atukenov/altyncup/issues/25) — Use iiko's coupon-series engine for promo codes _(low priority)_

## Customer App

- [ ] [#26](https://github.com/atukenov/altyncup/issues/26) — Scheduled pickup time (pre-orders) _(low priority)_
- [ ] [#27](https://github.com/atukenov/altyncup/issues/27) — Wire up Barista Fan achievement + order-confirmation unlock toast
- [ ] [#28](https://github.com/atukenov/altyncup/issues/28) — iOS home screen widget for active order status _(low priority)_
- [ ] [#29](https://github.com/atukenov/altyncup/issues/29) — Real remote push notifications (FCM/APNs)
- [ ] [#30](https://github.com/atukenov/altyncup/issues/30) — Referral program (invite-a-friend)
- [ ] [#31](https://github.com/atukenov/altyncup/issues/31) — Face ID / Touch ID quick unlock

## Payments & Monetization

- [ ] [#33](https://github.com/atukenov/altyncup/issues/33) — Refund handling and webhook reconciliation
- [ ] [#34](https://github.com/atukenov/altyncup/issues/34) — Verify webhook idempotency holds for the real payment provider

## Security & Reliability

- [ ] [#35](https://github.com/atukenov/altyncup/issues/35) — Optimistic concurrency control on GroupCart
- [ ] [#36](https://github.com/atukenov/altyncup/issues/36) — Close remaining test-coverage gaps (integration tests, FindAsync paths, push notifications)

## UX & Accessibility

- [ ] [#38](https://github.com/atukenov/altyncup/issues/38) — Global HTTP error interceptor (frontend)
- [ ] [#39](https://github.com/atukenov/altyncup/issues/39) — Offline detection and checkout guard

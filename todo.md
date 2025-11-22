# Project Plan & Roadmap — OrderWorder

This `todo.md` consolidates current work, near-term priorities, feature ideas, optimizations, and maintenance tasks derived from `description.md` and repository inspection.

## Current status — recent fixes

- Fix HTML layout/hydration issues (root & nested layouts) — completed
- Sidebar icon visibility and white icon/text styling — completed
- Introduced `react-icons` and migrated a few key icons (partial) — in-progress
- Added a common Button component and replaced MenuEditorItem usage — completed

## Short-term (high priority)

1. Consolidate theme injection (root)
   - Move themeController script into `src/app/layout.tsx`.
   - Acceptance: single theme injection applied to all pages; remove per-page injections (dashboard/home/restaurant).
   - Estimate: small (1-2 dev days).

2. Finish icon migration & create Icon wrapper
   - Inventory all `xtreme-ui` Icon usages and map to `react-icons` equivalents.
   - Introduce `src/components/base/Icon.tsx` that can accept legacy codes or ReactNode; centralize mapping.
   - Acceptance: visual parity, single switch for icon provider.
   - Estimate: medium (2-4 days).

3. Kitchen page implementation
   - Replace `UnderConstruction` with the kitchen dashboard UI (order queue, actions, filters, bulk operations). Reuse AdminContext APIs.
   - Acceptance: kitchen receives & acts on orders using existing endpoints; UI updates reflect actions.
   - Estimate: medium (3-5 days).

4. Replace remaining xtreme-ui Buttons where needed
   - Migrate critical interactions to the new common Button component.
   - Acceptance: consistent look & behavior, tests for essential flows.
   - Estimate: small-medium (2-3 days).

## Medium-term (important features)

- Restaurant pages: Explore, Reviews, Contact — implement full features and endpoints.
- Real-time notifications & alerts (WS or SSE) for admin/kitchen.
- Payments integration (Stripe) and secure webhook handling.
- Print/invoice generation (PDF + thermal printer support).
- Bulk menu import/export (CSV/JSON) with preview and rollback.

Each of these should include acceptance criteria, API contract changes, and tests. Budget 1–3 sprints depending on scope.

## Long-term & strategic

- Analytics & reporting (charts, CSV export)
- Table management & floor plan UI
- Customer profiles & loyalty program
- Order ETA & live tracking for customers
- Internationalization (i18n)
- Accessibility audit and fixes
- PWA / mobile improvements

## Ops, quality, and security

- Add tests (Jest + Testing Library) and API integration tests
- CI pipeline (GitHub Actions) to run lint/tests on PRs
- Dockerfile / docker-compose and staging deployment docs
- Security review: CSP, secure cookies, input validation, rate limiting

## Performance & optimization

- Run Lighthouse audits and prioritize fixes (image optimization, code splitting, caching)
- Tune SWR usage and caching/refresh intervals for admin/kitchen pages
- Use incremental static generation (ISG) or SSG for public restaurant pages where appropriate

## Documentation & community

- Update README using `description.md` as base (add setup, run, tests, contributing)
- Add issue/PR templates and contribution guide
- Create a `CONTRIBUTING.md` and `ROADMAP.md` with milestones and owners

## Suggested next steps (concrete first tasks)

1. Move theme injection to root layout (small, quick win).
2. Create `src/components/base/Icon.tsx` wrapper and map the most-used legacy codes.
3. Finish migrating icons for dashboard and menu UIs.
4. Implement the Kitchen page core (order list and actions).
5. Add unit tests for AdminContext and new Button component.

---

If you want, I can turn these items into GitHub issues (one per todo), or begin implementing one of them now — tell me which to start with and I will create a detailed subtask list and implement the first changes.

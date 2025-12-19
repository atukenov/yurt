# Project Plan & Roadmap — OrderWorder

This `todo.md` consolidates current work, near-term priorities, feature ideas, optimizations, and maintenance tasks derived from `description.md` and repository inspection.

## Current status — recent fixes

- Fix HTML layout/hydration issues (root & nested layouts) — completed
- Sidebar icon visibility and white icon/text styling — completed
- Introduced `react-icons` and migrated a few key icons (partial) — **completed**
- Added a common Button component and replaced MenuEditorItem usage — completed
- Consolidated theme injection to root layout — **completed**
- Icon wrapper with centralized mapping — **completed**
- Migrated all xtreme-ui Buttons to custom Button component — **completed**

## Short-term (high priority)

1. Consolidate theme injection (root) — **COMPLETED**
   - Moved themeController script into `src/app/layout.tsx` with default theme.
   - Removed per-page injections from homepage, admin login, about-us, and logout pages.
   - Restaurant layout retains override capability for restaurant-specific themes.
   - Acceptance: single theme injection at root; page-specific themes work via nested layouts.

2. Finish icon migration & create Icon wrapper — **COMPLETED**
   - Created centralized `src/components/base/iconMap.tsx` with 30+ icon code mappings.
   - Updated `src/components/base/Icon.tsx` to import and use centralized mapping.
   - Refactored `src/components/base/Button.tsx` to use centralized icon map (removed duplicate).
   - Icon wrapper now handles all xtreme-ui to react-icons conversions.
   - Acceptance: visual parity achieved; single icon provider; no code duplication.
   - Icons actively used in: Textfield, SearchButton, Collapsible, MenuCard, Invoice, etc.

3. Kitchen page implementation
   - Replace `UnderConstruction` with the kitchen dashboard UI (order queue, actions, filters, bulk operations). Reuse AdminContext APIs.
   - Acceptance: kitchen receives & acts on orders using existing endpoints; UI updates reflect actions.
   - Estimate: medium (3-5 days).

4. Replace remaining xtreme-ui Buttons where needed — **COMPLETED**
   - Migrated all xtreme-ui Button imports to custom Button component.
   - Files updated: LandingSection, LoginSection, AboutSection, Invoice, ContactPage, ReviewsPage, OrderDetail, ThemeSettings, SettingsAccount, PasswordSettings, address-selection.
   - Acceptance: consistent look & behavior, all interactions working with new component.
   - 11 critical files migrated; 0 compilation errors.

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

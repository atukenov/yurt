# Yurt landing page

The public page for cafe owners: what Yurt does, every module with its monthly price, a price calculator, bundles,
one-time fees, how launch works, what we need from the cafe, FAQ and a request form. Russian by default, English with
the RU/EN switch.

It is one plain HTML file (`index.html`) with no build step, so it can be opened straight from disk.

## Before going live

1. Fill `CONTACT` at the top of the `<script>` in `index.html` (WhatsApp number as digits, Telegram username, email).
   Until then the request form shows the request text to copy instead of opening WhatsApp.
2. Check the prices in `MODULES`, `BUNDLES`, `ONCE` and `EXTRA_BRANCH`. The module keys are the same as in
   Platform → Modules and monthly price, so keep the two in step when a price changes.

## Deploy (Vercel, its own project)

1. Vercel → Add New → Project → import `atukenov/yurt`.
2. Root Directory: `landing`. Framework preset: Other. Build command: none. Output directory: `.`
3. Add the domain (for example `yurt.kz`) under Settings → Domains.

Every push to `main` that changes `landing/` updates the page. The customer and admin apps are not affected.

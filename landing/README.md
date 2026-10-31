# Yurt landing page

One static page for cafe owners (Russian): what Yurt does, a savings calculator, a module price builder,
the three packages, onboarding steps, hardware and FAQ. No build step, no dependencies besides Google Fonts.

Prices on the page are the proposal from 2026-10-31 (thread "Check demo readiness and test plan"). Keep them in
step with Platform → Modules and monthly price: the module list and prices are the `MODS` and `PLANS` arrays in
the `<script>` at the bottom of `index.html`.

## Before publishing

Fill in `CONTACT` at the top of the script (WhatsApp number, Telegram, email). With a WhatsApp number the form opens
WhatsApp with a ready message; without one the form says the request is not sent.

## Publish

Make it its own hosting project with the root folder set to `landing/` (no build command, output = the folder):

- **Vercel:** New Project → this repo → Root Directory `landing` → Framework "Other". Vercel Hobby is for
  non-commercial use only, so use the Pro plan you need for the apps anyway.
- **Cloudflare Pages** (free): Create → Connect to Git → this repo → Build output directory `landing`.

Point `yurt.kz` (or `www.`) at it. The customer and admin apps keep their own projects.

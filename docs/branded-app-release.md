# Releasing a cafe's branded app

Checklist for putting one cafe's own app in the App Store and Google Play. The build steps are in
`docs/deploy-ios-appstore.md` and `docs/deploy-android-playmarket.md`; the per-cafe setup is in `cafes/README.md`.

## Before the first release

- [ ] Decide the app id (reverse-DNS, e.g. `kz.mycafe.app`). It cannot change after the first store release.
- [ ] Apple Developer and Google Play accounts: the cafe's own, or yours with the cafe's app inside. Decide who owns the listing.
- [ ] `cafes/<slug>/cafe.json` (app id, name, version, optional Apple merchant id) committed.
- [ ] `cafes/<slug>/assets/icon-only.png` (1024x1024) and `splash.png` (2732x2732).
- [ ] Firebase project for the cafe, with `google-services.json` and `GoogleService-Info.plist` in `cafes/<slug>/secrets/`.
- [ ] Online payments: the cafe's Stripe keys and webhook set up as in `docs/stripe-payments.md` (Apple Pay and Google Pay come with Stripe's payment page; no Apple merchant id is needed for it).
- [ ] Backend for the cafe: Brand saved (name, colours, links), modules switched on, push and payments configured.
- [ ] Store listing: name, short and full description (EN/RU/KK as needed), screenshots from the branded app (see
      "Store screenshots" below), privacy policy URL, support contact, age rating, and review notes with a test account.
- [ ] Signing secrets added on GitHub once (Android upload key, Apple Distribution certificate), plus this cafe's
      `cafe-<slug>` environment with its iOS provisioning profile and Firebase files; see `docs/app-builds.md`.

## Every release

- [ ] Bump `versionName` (and `versionCode` for Android) in `cafes/<slug>/cafe.json`.
- [ ] On GitHub: **Actions > App builds > Run workflow** with the cafe's folder name (see `docs/app-builds.md`); it
      applies the cafe, builds and signs both apps, and can send them to Play internal testing and TestFlight.
      (By hand instead: `scripts/build-app.sh <slug> android|ios` on a spare checkout, or the platform deploy docs.)
- [ ] Test on a real device: sign-in, order, push, payment.
- [ ] Publish an app release entry in the admin panel so older app versions show the update popup.
- [ ] When building by hand: discard the changes the script made to tracked native files (they belong to this cafe only).

## Store screenshots

One command makes the screenshots for both stores, for one cafe and one language, in light mode. It opens the real
customer app in a hidden browser with fake sample data (a menu, a cart, an order being made, a profile), so it needs no
server, no phone and no real customer data.

```bash
cd frontend
npm ci                                                   # once
npx ng build yurt-customer --configuration production    # after every app change
npx playwright@1.56.1 install chromium                   # once
NODE_PATH="$(npm root -g)" node scripts/brand-screenshots.mjs --store=<slug> --lang=ru
```

(If `node` says it cannot find `playwright`, run `npm install -g playwright@1.56.1` once.)

- `--lang=ru`, `--lang=kk` or `--lang=en`; run it once per language the listing has.
- Colours, name, logo and welcome line come from the `brand` part of `cafes/<slug>/cafe.json` (same fields as the
  Brand screen, see `cafes/README.md`). Add `--api=https://your-server` to take them from the live server instead, so
  the pictures match exactly what the cafe saved in the Brand screen.
- The pictures land in `frontend/store-screenshots/<slug>/<lang>/`, one folder per screen size, numbered in the order
  to upload them (01-menu, 02-cart, 03-order-status, 04-profile, 05-sign-in):

| Folder          | Size (pixels) | Upload to                                         |
| --------------- | ------------- | ------------------------------------------------- |
| `ios-6.9in`     | 1320 x 2868   | App Store Connect, iPhone 6.9" (required)         |
| `ios-6.5in`     | 1242 x 2688   | App Store Connect, iPhone 6.5" (if it asks)       |
| `android-phone` | 1080 x 1920   | Google Play Console, Phone screenshots            |

The iOS app is iPhone-only, so no iPad pictures are needed. The menu and order texts are sample data, not the cafe's
real menu.

Each picture has a short caption at the top on the cafe's colour, with the app screen below it in a dark phone-like
frame (the store size stays exact). The captions come in Russian, Kazakh and English. To use your own wording for a
screen, add `storeCaptions` to `cafe.json`, for example
`"storeCaptions": { "ru": { "menu": "Ваш кофе без очереди" } }` (screens: `menu`, `cart`, `order-status`, `profile`,
`sign-in`; up to 60 characters). Add `--no-captions` to get the plain app screens instead.

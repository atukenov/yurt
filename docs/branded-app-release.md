# Releasing a cafe's branded app

Checklist for putting one cafe's own app in the App Store and Google Play. The build steps are in
`docs/deploy-ios-appstore.md` and `docs/deploy-android-playmarket.md`; the per-cafe setup is in `cafes/README.md`.

## Before the first release

- [ ] Decide the app id (reverse-DNS, e.g. `kz.mycafe.app`). It cannot change after the first store release.
- [ ] Apple Developer and Google Play accounts: the cafe's own, or yours with the cafe's app inside. Decide who owns the listing.
- [ ] `cafes/<slug>/cafe.json` (app id, name, version, optional Apple merchant id) committed.
- [ ] `cafes/<slug>/assets/icon-only.png` (1024x1024) and `splash.png` (2732x2732).
- [ ] Firebase project for the cafe, with `google-services.json` and `GoogleService-Info.plist` in `cafes/<slug>/secrets/`.
- [ ] Apple Pay: merchant id created in the Apple Developer account and set as `appleMerchantId`; certificate set up as in `docs/apple-pay-setup.md`.
- [ ] Backend for the cafe: Brand saved (name, colours, links), modules switched on, push and payments configured.
- [ ] Store listing: name, short and full description (EN/RU/KK as needed), screenshots from the branded app, privacy policy URL,
      support contact, age rating, and review notes with a test account.
- [ ] Android signing key created and backed up outside git (`key.properties`); iOS signing set up in Xcode.

## Every release

- [ ] Bump `versionName` (and `versionCode` for Android) in `cafes/<slug>/cafe.json`.
- [ ] On the build machine: `node scripts/apply-cafe.mjs <slug> --generate`, then build the web app and
      `YURT_CAFE=<slug> npx cap sync`.
- [ ] Build, sign and upload as in the platform deploy docs; test on a real device: sign-in, order, push, payment.
- [ ] Publish an app release entry in the admin panel so older app versions show the update popup.
- [ ] Discard the changes the script made to tracked native files (they belong to this cafe only).

## Not automated yet

- A CI build matrix that produces the signed apps from a cafe folder (needs signing secrets and a macOS runner).
- Generating store screenshots.

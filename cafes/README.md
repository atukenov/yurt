# Per-cafe native app config

One folder per cafe that gets its own branded store app:

```
cafes/<slug>/cafe.json      committed: app id, app name, version (no secrets)
cafes/<slug>/secrets/       git-ignored: google-services.json, GoogleService-Info.plist
cafes/<slug>/assets/        committed: icon-only.png (1024x1024), splash.png (2732x2732); optional
                            icon-foreground.png, icon-background.png, splash-dark.png
```

`cafe.json` fields: `appId` (reverse-DNS, e.g. `kz.mycafe.app`), `appName` (shown under the icon, up to 30 characters),
optional `versionName` (`1.2.3`), `versionCode` (whole number, Android) and `appleMerchantId`
(`merchant.kz.mycafe.app`, the Apple Pay merchant in the iOS entitlements; leave it out for a cafe without Apple Pay).
Optional `tenant` is the cafe's key on the platform (the slug shown in Platform > Cafes); when set, the app is built to
send it as `X-Tenant`, so it always talks to that cafe even before the cafe has its own web address.
Optional `brand` holds the Brand screen values (`name`, `primaryColor`, `accentColor`, and optionally `welcomeText`,
`logoUrl`, `links` and so on); only the store screenshot script reads it (see "Store screenshots" in
`docs/branded-app-release.md`). The live app still takes its brand from the server.

Apply a cafe to the native projects, then build as usual:

```bash
node scripts/apply-cafe.mjs sample-cafe            # edits frontend/android and frontend/ios
node scripts/apply-cafe.mjs sample-cafe --dry-run  # only prints what would change
node scripts/apply-cafe.mjs sample-cafe --generate # also makes every icon and splash size (needs internet for npx)
cd frontend && YURT_CAFE=sample-cafe npx cap sync
```

Run it on a build machine or CI checkout, not in your everyday working copy: it rewrites tracked native files.
`sample-cafe` is an example, not a real cafe.

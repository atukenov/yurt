# Per-cafe native app config

One folder per cafe that gets its own branded store app:

```
cafes/<slug>/cafe.json      committed: app id, app name, version (no secrets)
cafes/<slug>/secrets/       git-ignored: google-services.json, GoogleService-Info.plist
```

`cafe.json` fields: `appId` (reverse-DNS, e.g. `kz.mycafe.app`), `appName` (shown under the icon, up to 30 characters),
optional `versionName` (`1.2.3`) and `versionCode` (whole number, Android).

Apply a cafe to the native projects, then build as usual:

```bash
node scripts/apply-cafe.mjs sample-cafe            # edits frontend/android and frontend/ios
node scripts/apply-cafe.mjs sample-cafe --dry-run  # only prints what would change
cd frontend && YURT_CAFE=sample-cafe npx cap sync
```

Run it on a build machine or CI checkout, not in your everyday working copy: it rewrites tracked native files.
`sample-cafe` is an example, not a real cafe.

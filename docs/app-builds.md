# Signed app builds in GitHub

The **App builds** workflow (`.github/workflows/app-builds.yml`) makes a cafe's store apps on GitHub's machines, from
the cafe's folder in `cafes/<cafe>/`. You do not need Android Studio, Xcode or your own Mac for a release. GitHub's Mac
machines are free for this repository because it is public.

- Android: a signed `.aab` (what Google Play wants) and a signed `.apk` (to install directly on a test phone).
- iOS: a signed `.ipa` (what App Store Connect and TestFlight want).

The same steps are in `scripts/build-app.sh`, so a Mac can run them too (in a spare checkout, since the script rewrites
native files for that cafe).

## Making a build

1. Bump `versionName` and `versionCode` in `cafes/<cafe>/cafe.json` and merge that to `main`. Both stores refuse a
   build number they have already seen; `versionCode` is used as the build number on Android and on iOS.
2. On GitHub: **Actions > App builds > Run workflow**. Pick the cafe folder name, which apps (both, android or ios) and
   whether to send them to the stores.
3. When the run is green, the files are at the bottom of the run page under **Artifacts** (`<cafe>-android`,
   `<cafe>-ios`), kept for 14 days. With "send to the stores" ticked:
   - Android goes to Google Play's **Internal testing** track as a **draft**: open Play Console, check it, press roll out.
   - iOS goes to **TestFlight**; it shows up in App Store Connect after Apple has processed it (10 to 30 minutes).

A run stops with a clear error when a signing value it needs is missing, so it never hands you an unsigned app by
mistake. On pull requests that change the native apps or these scripts, the workflow also makes an **unsigned**
sample-cafe build of both apps, only to catch a broken build early. That check uses no secrets.

## The secrets to add (once)

Secrets are added in the repository on GitHub: **Settings > Secrets and variables > Actions > New repository secret**.
GitHub hides them in logs, and only people with write access to the repository can start a run. Never put these files
in git.

All cafe apps are published under your own developer accounts, so the account-wide values below are added **once** and
shared by every cafe. Only the items marked "per cafe" differ between cafes; they go into a GitHub **environment**
named `cafe-<cafe folder name>` (**Settings > Environments > New environment**, e.g. `cafe-sample-cafe`), which GitHub
uses for that cafe's runs. A secret in the environment wins over a repository secret with the same name, so a cafe
can also have its own Android key there if you ever want that.

To turn a file into the text GitHub needs (the `_BASE64` secrets), run `base64 -i FILE | pbcopy` on a Mac (then paste)
or `base64 -w0 FILE` on Linux.

### Android (account-wide)

| Secret                      | What it is                                                                 |
| --------------------------- | -------------------------------------------------------------------------- |
| `ANDROID_KEYSTORE_BASE64`   | Your upload key file (`.keystore` / `.jks`), as base64.                    |
| `ANDROID_KEYSTORE_PASSWORD` | The keystore password.                                                     |
| `ANDROID_KEY_ALIAS`         | The key name inside it (e.g. `altyncup`).                                  |
| `ANDROID_KEY_PASSWORD`      | The key password.                                                          |

Google Play keeps each app's real signing key itself ("Play App Signing", the default for new apps); you only sign
uploads with this upload key, so one upload key can serve every cafe. If you already published an app with a
keystore (see `docs/deploy-android-playmarket.md`), use that same keystore here. To make a new one, run this in your
own terminal and keep the file and passwords in your password manager:

```bash
keytool -genkey -v -keystore yurt-upload.keystore -alias yurt -keyalg RSA -keysize 2048 -validity 10000
```

### iOS (account-wide)

| Secret                       | What it is                                                                                     |
| ---------------------------- | ---------------------------------------------------------------------------------------------- |
| `APPLE_TEAM_ID`              | Your 10-character Team ID (developer.apple.com > Account > Membership details).               |
| `IOS_CERTIFICATE_P12_BASE64` | Your **Apple Distribution** certificate with its private key, exported as `.p12`, as base64.   |
| `IOS_CERTIFICATE_PASSWORD`   | The password you chose when exporting the `.p12`.                                              |

The certificate: in Xcode, **Settings > Accounts > your team > Manage Certificates > + > Apple Distribution**. Then in
the Keychain Access app, find "Apple Distribution: ...", right-click it, **Export**, save as `.p12` with a password. It
is valid for one year; after renewing, replace both secrets.

### Per cafe (in the `cafe-<cafe>` environment)

| Secret                  | What it is                                                                                  |
| ----------------------- | ------------------------------------------------------------------------------------------- |
| `IOS_PROFILE_BASE64`    | The cafe's **App Store** provisioning profile (`.mobileprovision`), as base64.               |
| `FIREBASE_ANDROID_JSON` | The cafe's `google-services.json` from Firebase, pasted as it is (not base64).               |
| `FIREBASE_IOS_PLIST`    | The cafe's `GoogleService-Info.plist` from Firebase, pasted as it is (not base64).           |

The provisioning profile ties the cafe's app id to your certificate. On developer.apple.com:

1. **Identifiers > +**: register an App ID with the cafe's `appId` from `cafe.json` (e.g. `kz.mycafe.app`) and tick
   **Push Notifications** (and **Apple Pay** only if the cafe has an `appleMerchantId`).
2. **Profiles > + > App Store Connect**: pick that App ID and your Apple Distribution certificate, name it after the
   cafe, download it.

It expires together with the certificate; make a new one then. The build checks that the profile matches the cafe's
app id and stops with a clear message if it does not.

Without the Firebase files the Android app works but gets no push notifications. The iOS build stops without
`FIREBASE_IOS_PLIST`, because the iOS app expects that file.

### Sending to the stores (optional, only for "send to the stores")

| Secret                      | What it is                                                                                   |
| --------------------------- | -------------------------------------------------------------------------------------------- |
| `PLAY_SERVICE_ACCOUNT_JSON` | A Google Cloud service account key (JSON, pasted as it is) that may release apps in Play Console. |
| `ASC_KEY_ID`                | App Store Connect API key id.                                                                |
| `ASC_ISSUER_ID`             | App Store Connect API issuer id (shown above the key list).                                  |
| `ASC_KEY_P8`                | The downloaded `AuthKey_XXXX.p8` file, pasted as it is.                                      |

- Google: in Google Cloud, create a service account and a JSON key for it; in Play Console, **Users and permissions >
  Invite new users** with the service account's email and give it release rights for your apps. Google only accepts
  automatic uploads for an app after its **first** build was uploaded by hand in Play Console, so do the first release
  of each new cafe by hand with the `.aab` from the run's Artifacts.
- Apple: App Store Connect > **Users and Access > Integrations > App Store Connect API > Team Keys > +**, role
  **App Manager**. The `.p8` file can be downloaded only once. The app must already exist in App Store Connect (made by
  hand with the cafe's app id).

## When something fails

| Message in the run                                         | What to do                                                                   |
| ---------------------------------------------------------- | ---------------------------------------------------------------------------- |
| "Android signing values are not set" / "iOS signing ..."   | A secret above is missing or empty; check its exact name.                    |
| "The provisioning profile is for ..."                      | The profile in the cafe's environment is for another app id or team.        |
| "... is not an Apple Distribution certificate"             | The `.p12` holds a development certificate; export the Distribution one.     |
| Google Play: "Package not found"                           | Upload the first build of this app by hand in Play Console.                  |
| Either store: "version code / build number already used"   | Raise `versionCode` in `cafe.json`.                                          |

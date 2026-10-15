# Remote push notifications — manual setup (issue #29)

The code for FCM/APNs remote push is fully wired (device-token registration, backend FCM
send, order-status hook — see `docs/push-notifications-issue-29-plan.md`), but it ships
**disabled by default** (`Firebase:Enabled = false`) because no real Firebase project or
APNs credentials exist in this environment. Until someone with access to the Firebase/
Apple Developer accounts completes the steps below, the feature is a no-op: device
tokens are still registered and stored, but no push is ever sent.

## 1. Create the Firebase project

1. Go to the [Firebase console](https://console.firebase.google.com/) and create a project
   (or use an existing one for this product).
2. Note the **Project ID** — it goes into `Firebase:ProjectId`.

## 2. Add the Android app

1. In the Firebase project, add an Android app with package name `com.yurt.app` (must
   match `appId` in `frontend/capacitor.config.ts`).
2. Download `google-services.json` and place it at `frontend/android/app/google-services.json`.
3. Add the Google Services Gradle plugin:
   - `frontend/android/build.gradle`: add `classpath 'com.google.gms:google-services:4.4.2'`
     (or current version) to the top-level `dependencies` block.
   - `frontend/android/app/build.gradle`: add `apply plugin: 'com.google.gms.google-services'`
     at the bottom of the file.

   **This was intentionally not done as part of the code change** — the Gradle plugin
   hard-fails the Android build if `google-services.json` is missing, which would break
   every other developer's and CI's build until this file exists. Do steps 2–3 together,
   in a follow-up commit, once the file is in hand.

## 3. Add the iOS app

1. In the same Firebase project, add an iOS app with bundle ID `com.yurt.app`.
2. Download `GoogleService-Info.plist` and add it to the Xcode project under
   `frontend/ios/App/App/` (already done — see `frontend/ios/App/App/GoogleService-Info.plist`).
3. In Xcode, confirm the **Push Notifications** and **Background Modes → Remote
   notifications** capabilities are enabled on the `App` target (already done).
4. In the [Apple Developer portal](https://developer.apple.com/account), create an APNs
   Auth Key (.p8) under Certificates, Identifiers & Profiles → Keys.
5. Upload that `.p8` key to Firebase Console → Project Settings → Cloud Messaging →
   Apple app configuration, along with its Key ID and your Apple Team ID.

## 4. Generate the service-account credential

1. Firebase Console → Project Settings → Service Accounts → **Generate new private key**.
   This downloads a JSON file — treat it as a secret, never commit it.
2. Set it on the backend's real environment (not `appsettings.json`):
   ```bash
   Firebase__ServiceAccountJson='<paste the full JSON contents as one line>'
   Firebase__ProjectId='<the project id from step 1>'
   Firebase__Enabled=true
   ```
   For local development, `dotnet user-secrets set Firebase:ServiceAccountJson "..."` works
   the same way the connection string and JWT secret are set — see `docs/setup.md`.

## 5. Verify

- Backend: with `Firebase:Enabled = true` and a real service account, place a test order
  and accept/update/decline it from the admin panel. The customer's registered device(s)
  should receive a push. `CustomerDeviceToken` rows the SDK reports as dead (uninstalled
  app, stale token) are pruned automatically on the next send.
- Frontend: no changes needed here — `PushNotificationService` (customer app) already
  registers the device token on login and sends it to `POST /api/v1/auth/device-tokens`
  regardless of whether the backend has real credentials yet.

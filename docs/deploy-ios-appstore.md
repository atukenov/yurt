# Deploy iOS App to App Store

**App:** Altyncup (`com.yurt.app`)  
**Min iOS:** 15.6 · **Current version:** 1.0 (build 1)

---

## Prerequisites

- [ ] Apple Developer account ($99/year) — [developer.apple.com](https://developer.apple.com)
- [ ] Xcode 15+ installed (Mac only)
- [ ] App record created in App Store Connect — [appstoreconnect.apple.com](https://appstoreconnect.apple.com)
- [ ] Bundle ID `com.yurt.app` registered in developer portal
- [ ] Signing certificate and provisioning profile set up (Xcode can do this automatically)

---

## Step 1 — Build the Web App

```bash
cd frontend
npx ng build yurt-customer --configuration production
```

Output goes to `dist/yurt-customer/browser/`.

---

## Step 2 — Sync to iOS

```bash
npx cap sync ios
```

This copies the built web assets into the Xcode project and updates any native plugins.

---

## Step 3 — Open in Xcode

```bash
npx cap open ios
```

Or open manually: `frontend/ios/App/App.xcworkspace` (**always open `.xcworkspace`, not `.xcodeproj`**).

---

## Step 4 — Configure Signing (first time only)

1. In Xcode, select the **App** target → **Signing & Capabilities**
2. Check **Automatically manage signing**
3. Select your **Team** (your Apple Developer account)
4. Xcode will create/download the certificate and provisioning profile automatically

If you see a "No account" error, go to **Xcode → Settings → Accounts** and sign in with your Apple ID.

---

## Step 5 — Bump the Version

In Xcode → **App** target → **General**:

| Field | Description |
|---|---|
| **Version** | User-visible version, e.g. `1.1.0` — increment for every release |
| **Build** | Internal build number, e.g. `2` — must be higher than the previous upload |

Or edit directly in `ios/App/App.xcodeproj/project.pbxproj`:

```
MARKETING_VERSION = 1.1.0;
CURRENT_PROJECT_VERSION = 2;
```

---

## Step 6 — Set the Scheme to Release

In Xcode, select the scheme in the toolbar:
1. Click the scheme selector next to the Run button
2. Choose **Edit Scheme**
3. Under **Archive**, confirm **Build Configuration** is set to **Release**

---

## Step 7 — Archive the App

1. Connect a real device **or** select **Any iOS Device (arm64)** from the device picker (do not select a simulator)
2. **Product → Archive**
3. Wait for the build to complete — the **Organizer** window opens automatically

---

## Step 8 — Upload to App Store Connect

In the **Organizer** window:

1. Select the archive you just built
2. Click **Distribute App**
3. Choose **App Store Connect** → **Upload**
4. Leave all checkboxes at their defaults (symbols, bitcode)
5. Click **Next → Upload**

Xcode uploads the build. It takes a few minutes to process on Apple's servers.

---

## Step 9 — Submit for Review in App Store Connect

1. Go to [appstoreconnect.apple.com](https://appstoreconnect.apple.com) → **My Apps → Altyncup**
2. Click **+ Version or Platform** → **iOS** → enter the version number
3. Fill in:
   - **What's New** — describe what changed in this version
   - **Screenshots** — required sizes: 6.7" (iPhone 16 Pro Max) and 5.5" (iPhone 8 Plus)
   - **App icon** — 1024×1024 PNG (no alpha channel, no rounded corners — Apple adds them)
4. Under **Build**, click **+** and select the uploaded build
5. Click **Add for Review → Submit to App Review**

### Review times

| Type | Typical wait |
|---|---|
| First submission | 1–3 days |
| Updates | 24–48 hours |
| Expedited review | Request at [developer.apple.com/contact/app-store/](https://developer.apple.com/contact/app-store/) |

---

## Step 10 — TestFlight (optional but recommended)

Test with real users before the public release:

1. Upload the build (Step 8)
2. In App Store Connect → **TestFlight**
3. Add internal testers (up to 100, instant access) or external testers (up to 10,000, requires Apple review)
4. Share the TestFlight invite link with testers

---

## App Store Requirements (one-time setup)

Complete these before the first submission or the review will be rejected:

- **Privacy policy URL** — required for any app (host it on your website or Notion)
- **App privacy** → Data types collected (phone number, purchase history, etc.)
- **Age rating** — complete the questionnaire in App Store Connect
- **Category** — set to **Food & Drink**
- **Support URL** — a page or email address for user support

---

## App Icon

The iOS icon file is already set:
```
ios/App/App/Assets.xcassets/AppIcon.appiconset/AppIcon-512@2x.png
```
Size: **1024×1024 PNG**, no transparency, no rounded corners.

To regenerate from `altyncup-logo.jpg`:

```bash
sips --padToHeightWidth 292 292 --padColor FFFFFF \
  frontend/projects/yurt-customer/public/altyncup-logo.jpg \
  --out /tmp/icon-square.png

sips --resampleHeightWidth 1024 1024 /tmp/icon-square.png \
  --out frontend/ios/App/App/Assets.xcassets/AppIcon.appiconset/AppIcon-512@2x.png

rm /tmp/icon-square.png
```

---

## Common Errors

| Error | Fix |
|---|---|
| "No signing certificate" | Xcode → Signing & Capabilities → Automatically manage signing |
| "Provisioning profile doesn't include entitlements" | Remove unused capabilities from the target |
| Build uploaded but not visible in App Store Connect | Wait 10–15 min for Apple to process it |
| "Missing compliance" | Add `ITSAppUsesNonExemptEncryption = NO` to `Info.plist` (no custom encryption used) |
| "ITMS-90078: Missing Push Notification entitlement" | Either add Push Notifications capability or remove it from entitlements |
| Screenshot wrong size | Use Xcode Simulator to capture the required screen sizes |
| Icon has alpha channel | Export icon as PNG without transparency |

---

## Missing Encryption Key (Info.plist)

Add this to `ios/App/App/Info.plist` to skip encryption export compliance questions:

```xml
<key>ITSAppUsesNonExemptEncryption</key>
<false/>
```

---

## Release Checklist

```
[ ] ng build --configuration production (no errors)
[ ] npx cap sync ios
[ ] Version and Build number incremented
[ ] Signing configured in Xcode
[ ] Any iOS Device (arm64) selected — not a simulator
[ ] Product → Archive completed
[ ] Uploaded via Organizer → Distribute App
[ ] Build visible in App Store Connect (wait ~15 min)
[ ] Screenshots added (6.7" and 5.5")
[ ] Privacy policy URL set
[ ] App privacy data types filled in
[ ] What's New text written
[ ] Submitted for review
[ ] TestFlight tested before production release
```

# Tablet app (admin app for Android tablets and iPad)

The admin app also comes as an app for a tablet in the cafe, called **Yurt Admin**. It is the same admin app as the
website, packed with [Capacitor](https://capacitorjs.com) in `frontend/admin-native/`, plus one piece of native code:
a Bluetooth printer plugin (`YurtPrinter`).

Why: in Chrome, a tablet can only reach printers with **Bluetooth Low Energy** (see `docs/bluetooth-printers.md`), and
an iPad cannot use Bluetooth from a web page at all. The tablet app can:

| | Chrome on a tablet | Tablet app on Android | Tablet app on iPad |
|---|---|---|---|
| Classic Bluetooth printers (most cheap 58/80 mm printers) | no | **yes** | no (Apple allows this only for MFi-certified printers) |
| Bluetooth Low Energy printers | yes | yes | **yes** |
| Keeps the screen on by itself | no | yes | yes |

Everything else (orders, kitchen, cashier, printers page) is the same as the website. The server needs no change: the
app uses the same Bluetooth jobs as Chrome does.

## How it works
- Android: staff first **pair** the printer in the tablet's own settings (Settings → Bluetooth; the PIN is often
  `0000` or `1234`). On **Printers → Connect this tablet** the app shows the paired devices; staff tap the printer.
  The app talks classic Bluetooth (serial port profile) to it, or BLE if the printer only has BLE.
- iPad: no pairing in Settings. On **Connect this tablet** the app searches for 4 seconds and lists the printers it
  found; staff tap the printer. iPadOS asks once to allow Bluetooth for the app.
- The app remembers the printer and connects again by itself when it starts.
- It prints only while the app is open on the screen, like Chrome. The app keeps the screen on; keep the tablet
  charging.

## Build it
Needs the same tools as the customer app (`docs/deploy-android-playmarket.md`, `docs/deploy-ios-appstore.md`):
Android Studio with JDK 21 for Android, a Mac with Xcode and CocoaPods for iPad.

```bash
cd frontend
npm ci
npx ng build yurt-admin --configuration production
cd admin-native
npx cap sync             # copies the app into android/ and ios/
npx cap open android     # Android Studio: Run on a tablet, or Build → Generate Signed Bundle / APK
npx cap open ios         # Xcode: pick your team under Signing, then Run on an iPad
```

- App id: `com.yurt.admin` (change with `YURT_ADMIN_APP_ID` and `YURT_ADMIN_APP_NAME` before `npx cap sync`, and in
  Xcode / `android/app/build.gradle` for a store build).
- Android release signing works like the customer app: `admin-native/android/key.properties` (not committed).
- **Which cafe**: like the website, the app talks to the server in `projects/yurt-admin/src/environments/environment.ts`.
  The first cafe works as is. For another cafe, set `tenant` there first (`node scripts/apply-cafe.mjs <cafe>` does
  this for both apps), then build.
- **Quick try without Android Studio**: every pull request builds an unsigned debug APK (CI job "Tablet app — Android
  build", download `yurt-admin-tablet-debug-apk` from the run). Copy it to an Android tablet and open it (allow
  "Install unknown apps" for the file manager). It talks to the live server.

The app does not need to be in a store: for a few cafes, install the APK directly on Android tablets, and use
TestFlight or Apple Business Manager (custom app) for iPads. Store review is possible too; it is a staff app, so give
the reviewers a staff login.

## What to test on real hardware (not done yet)
This was built and checked with a pretend printer only (unit tests, Android code compiled; the iPad code has not been
compiled, because that needs a Mac). Please try, and tell us what happened:

**Android tablet + classic Bluetooth printer (the main case)**
1. Pair the printer in the tablet's Bluetooth settings. Install the app, sign in with a staff account.
2. Printers → **Connect this tablet** on a Bluetooth printer. Android asks to allow "Nearby devices": allow. The list
   shows the printer; tap it. The page shows **Connected to this tablet**.
3. **Test print** comes out, Russian and Kazakh letters right.
4. A long receipt (an order with many items) prints in full.
5. Close the app and open it again: the printer connects by itself.
6. Switch the printer off: soon (up to about half a minute) the page shows **Not connected**. Switch it on, press **Connect** again.
7. With a cash drawer on the printer, take a cash payment: the drawer opens.
8. Leave it for an hour on the Kitchen page: the screen stays on and new orders still print.

**Android tablet + BLE-only printer**: steps 1-6 again (if it does not appear in the paired list, pair it in Settings
first).

**iPad + BLE printer**: steps 2-8 (on iPad, step 1 is just installing the app; the list comes from a search). Also
check that a classic-only printer does *not* show up, so staff are not confused.

If something fails, the error under the printer on the Printers page says why; send us that text and the printer's make
and model.

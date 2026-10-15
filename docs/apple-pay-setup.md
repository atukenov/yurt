# Apple Pay via Freedom Pay — manual setup (issue #49)

The code (backend `FreedomPayPaymentProvider`, checkout wiring, native `ApplePayPlugin.swift`
scaffold) is in place on branch `claude/apple-pay-freedom-pay`, but it ships **disabled by
default** (`FreedomPay:Enabled = false`) and the native Swift plugin's SDK calls are
commented-out TODOs — three things need to happen outside this repo before it can go live.

## 1. Apple Developer portal

1. [developer.apple.com/account](https://developer.apple.com/account) → Certificates,
   Identifiers & Profiles → Identifiers → register a **Merchant ID**
   (e.g. `merchant.com.yurt.app`).
2. On the app's own Identifier (`com.yurt.app`), enable the **Apple Pay** capability and
   link it to that Merchant ID.
3. Create an **Apple Pay Payment Processing Certificate**: Certificates → + → Apple Pay
   Payment Processing Certificate. This needs a CSR — generate one from a Mac via
   Keychain Access → Certificate Assistant → Request a Certificate from a Certificate
   Authority. Upload the resulting cert back to the portal.
4. In Xcode (`frontend/ios/App/App.xcworkspace`), target `App` → Signing & Capabilities →
   **+ Capability** → Apple Pay → select the Merchant ID from step 1. (This writes the
   entitlement and registers the capability with the portal — same reason the push
   notification entitlement needed to go through this UI, not a hand-edited file.)

## 2. Freedom Pay merchant setup

1. Apply for / confirm a Freedom Pay merchant account that supports **Apple Pay**
   specifically — this is a separate capability from normal card processing, confirm it's
   enabled for your merchant ID with their support team.
2. Get the merchant ID, secret key, and confirm which `Region` value to use (issue #49
   assumes `.kz`).
3. **Before writing the real SDK calls**, verify the exact method/type names in
   `ApplePayPlugin.swift`'s TODO comments (`StandardPaymentRequest`,
   `createApplePayment`, `confirmApplePayment`) against Freedom Pay's current SDK docs —
   these were taken from a description, not a verified live integration (see issue #49's
   "Open questions"). Ask Freedom Pay support directly if their docs are unclear on the
   Apple Pay flow specifically.

## 3. Add the Freedom Pay SDK to Xcode

1. Open `frontend/ios/App/App.xcworkspace` in Xcode.
2. File → Add Package Dependencies → paste
   `https://github.com/freedompay-global/merchant-sdk-ios.git`.
3. Add it to the `App` target.
4. In `frontend/ios/App/App/ApplePayPlugin.swift`, uncomment `import FreedomPaymentSdk`
   and the TODO blocks, initialize `freedomApi` with the real merchant id/secret/region
   (as native config — e.g. an `.xcconfig` build setting or `Info.plist` entry — never
   fetched over the network or committed to git).

**Not done in this change** — same reasoning as the Android FCM setup in
`docs/push-notifications-setup.md`: adding the package reference to the committed Xcode
project before it's actually available would break every other dev's/CI's build.

### ⚠️ `capacitor.config.json` and `npx cap sync`

`ApplePayPlugin` is a local plugin (a `.swift` file living directly in `ios/App/App/`, not a
separate npm/Capacitor plugin package), so it has to be registered manually in
`frontend/ios/App/App/capacitor.config.json`'s `packageClassList` array — Capacitor's bridge
reads this exact file at runtime (`CapacitorBridge.swift`'s `registerPlugins()`) and resolves
each entry via `NSClassFromString`; it does **not** scan the Objective-C/Swift runtime for
`CAPBridgedPlugin` conformance on its own. Without this entry, every call to the plugin fails
with `{"code":"UNIMPLEMENTED"}` even though the Swift file compiles and links fine — this bit
us once already.

**The catch**: `npx cap sync ios` regenerates this exact array from scratch
(`@capacitor/cli`'s `iosplugin.js` → `writePluginJSON`), built only by scanning plugin
*packages* in `node_modules` — it never looks at `ios/App/App/` at all, so it has no way to
know `ApplePayPlugin` should be there. **Every time someone runs `cap sync` (including when
the `FreedomPaymentSdk` package gets added per step 3 above), re-add `"ApplePayPlugin"` to
the array afterward**, or this regresses silently with no build error. The real long-term fix
is converting this into a proper local Capacitor plugin package (its own tiny `package.json`
+ podspec under e.g. `frontend/packages/apple-pay-plugin`, linked via a `file:` dependency) so
`cap sync` picks it up automatically — not done here since it's a bigger structural change
than this fix needed.

## 4. Backend config

Once the Apple Merchant ID exists, set it and flip the feature on:

- Local: `dotnet user-secrets set "FreedomPay:AppleMerchantId" "merchant.com.yurt.app"`
  (run from `backend/src/Yurt.WebApi`), then set `FreedomPay:Enabled = true` directly in
  `appsettings.Development.json` or via `dotnet user-secrets set "FreedomPay:Enabled" "true"`.
- Production (Railway): `FreedomPay__Enabled=true`, `FreedomPay__AppleMerchantId=merchant.com.yurt.app`
  as environment variables (same double-underscore convention as `Firebase__ServiceAccountJson`).

Neither of these is a secret — Freedom Pay's own merchant ID/secret key live natively in
the iOS app (step 3), not in backend config, since the SDK calls happen client-side.

## 5. Testing

- Apple Pay requires a physical device — Simulator can present the sheet but can't
  complete a real authorization.
- Add a test card to Wallet on the test device, signed in with a **Sandbox Apple ID**
  (Settings → Developer → Sandbox Apple Account, or via a TestFlight build).
- Confirm with Freedom Pay whether their Apple Pay integration has a sandbox mode or is
  production-only (issue #3's research on the Halyk alternative found Apple Pay by API
  integrations are sometimes production-only with no sandbox — confirm which applies
  here before assuming test transactions are free/reversible).

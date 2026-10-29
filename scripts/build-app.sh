#!/usr/bin/env bash
# Builds one cafe's store app from cafes/<cafe>/ (see docs/app-builds.md). The "App builds" GitHub workflow runs it; it
# also works on your own Mac, but only in a spare checkout: it rewrites tracked native files for that cafe.
#
#   scripts/build-app.sh <cafe> android   signed .aab (Google Play) and .apk (to install on a test phone)
#   scripts/build-app.sh <cafe> ios       signed .ipa (App Store / TestFlight); needs macOS with Xcode
#
# Signing comes from environment variables, never from files in git:
#   Android: ANDROID_KEYSTORE_BASE64, ANDROID_KEYSTORE_PASSWORD, ANDROID_KEY_ALIAS, ANDROID_KEY_PASSWORD
#   iOS:     APPLE_TEAM_ID, IOS_CERTIFICATE_P12_BASE64, IOS_CERTIFICATE_PASSWORD, IOS_PROFILE_BASE64
#   Push:    FIREBASE_ANDROID_JSON, FIREBASE_IOS_PLIST (the file contents; or put the files in cafes/<cafe>/secrets/)
# Without them the build is unsigned (a check that the app still compiles). REQUIRE_SIGNING=1 stops with an error
# instead. Bash 3.2 compatible (the default bash on macOS).
set -euo pipefail

CAFE="${1:-}"
PLATFORM="${2:-}"
if [[ ! "$CAFE" =~ ^[a-z0-9][a-z0-9-]*$ ]] || [[ "$PLATFORM" != android && "$PLATFORM" != ios ]]; then
  echo "Usage: scripts/build-app.sh <cafe> android|ios" >&2
  exit 2
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CAFE_DIR="$ROOT/cafes/$CAFE"
OUT="$ROOT/build/apps/$CAFE"
WORK="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/build-app.XXXXXX")"
cd "$ROOT"

die() { echo "::error::$*" >&2; exit 1; }
warn() { echo "::warning::$*" >&2; }
# True when every named variable is set and not empty.
have() { local v; for v in "$@"; do [[ -n "${!v:-}" ]] || return 1; done; }
# A signing value is missing: fatal for a release build, a warning for a compile check.
missing() { if [[ "${REQUIRE_SIGNING:-}" == 1 ]]; then die "$1"; else warn "$1"; fi; }

KEYCHAIN=""
cleanup() {
  rm -f "$ROOT/frontend/android/key.properties"
  if [[ -n "$KEYCHAIN" ]]; then security delete-keychain "$KEYCHAIN" 2>/dev/null || true; fi
  rm -rf "$WORK"
}
trap cleanup EXIT

[[ -f "$CAFE_DIR/cafe.json" ]] || die "cafes/$CAFE/cafe.json not found."
cafe_value() { node -e 'const c = require(process.argv[1]); process.stdout.write(String(c[process.argv[2]] ?? ""))' "$CAFE_DIR/cafe.json" "$1"; }
APP_ID="$(cafe_value appId)"
VERSION="$(cafe_value versionName)"
BUILD="$(cafe_value versionCode)"
NAME="$CAFE-${VERSION:-dev}-${BUILD:-0}"

# ── Push notification files ────────────────────────────────────────────────
mkdir -p "$CAFE_DIR/secrets"
[[ -n "${FIREBASE_ANDROID_JSON:-}" ]] && printf '%s' "$FIREBASE_ANDROID_JSON" > "$CAFE_DIR/secrets/google-services.json"
[[ -n "${FIREBASE_IOS_PLIST:-}" ]] && printf '%s' "$FIREBASE_IOS_PLIST" > "$CAFE_DIR/secrets/GoogleService-Info.plist"
if [[ "$PLATFORM" == android && ! -f "$CAFE_DIR/secrets/google-services.json" ]]; then
  warn "No google-services.json for $CAFE: this Android build gets no push notifications."
fi
IOS_PLIST_PLACEHOLDER=""
if [[ "$PLATFORM" == ios && ! -f "$CAFE_DIR/secrets/GoogleService-Info.plist" ]]; then
  missing "No GoogleService-Info.plist for $CAFE (FIREBASE_IOS_PLIST): the iOS app needs it for push notifications."
  IOS_PLIST_PLACEHOLDER=1
fi

# ── Cafe config, web app, native project ───────────────────────────────────
generate=""
[[ -f "$CAFE_DIR/assets/icon-only.png" && -f "$CAFE_DIR/assets/splash.png" ]] && generate="--generate"
node scripts/apply-cafe.mjs "$CAFE" $generate
if [[ -n "$IOS_PLIST_PLACEHOLDER" ]]; then
  # The Xcode project lists this file, so a compile check needs one; an empty one is never shipped (see above).
  printf '<?xml version="1.0" encoding="UTF-8"?>\n<plist version="1.0"><dict/></plist>\n' > frontend/ios/App/App/GoogleService-Info.plist
fi

(cd frontend && npx ng build yurt-customer --configuration production && YURT_CAFE="$CAFE" npx cap sync "$PLATFORM")
mkdir -p "$OUT"

# ── Android ────────────────────────────────────────────────────────────────
if [[ "$PLATFORM" == android ]]; then
  suffix="-unsigned"
  if have ANDROID_KEYSTORE_BASE64 ANDROID_KEYSTORE_PASSWORD ANDROID_KEY_ALIAS ANDROID_KEY_PASSWORD; then
    printf '%s' "$ANDROID_KEYSTORE_BASE64" | base64 --decode > "$WORK/upload.keystore"
    # Read by frontend/android/app/build.gradle; removed again on exit.
    {
      echo "storeFile=$WORK/upload.keystore"
      echo "storePassword=$ANDROID_KEYSTORE_PASSWORD"
      echo "keyAlias=$ANDROID_KEY_ALIAS"
      echo "keyPassword=$ANDROID_KEY_PASSWORD"
    } > frontend/android/key.properties
    suffix=""
  else
    missing "Android signing values are not set: making an unsigned build that Google Play will not accept."
  fi

  (cd frontend/android && ./gradlew --no-daemon bundleRelease assembleRelease)
  cp frontend/android/app/build/outputs/bundle/release/app-release.aab "$OUT/$NAME$suffix.aab"
  apk="$(ls frontend/android/app/build/outputs/apk/release/*.apk | head -n 1)"
  cp "$apk" "$OUT/$NAME$suffix.apk"
fi

# ── iOS ────────────────────────────────────────────────────────────────────
if [[ "$PLATFORM" == ios ]]; then
  [[ "$(uname)" == Darwin ]] || die "iOS apps can only be built on macOS."
  XCODE=(xcodebuild -workspace frontend/ios/App/App.xcworkspace -scheme App -configuration Release -destination generic/platform=iOS)

  if ! have APPLE_TEAM_ID IOS_CERTIFICATE_P12_BASE64 IOS_CERTIFICATE_PASSWORD IOS_PROFILE_BASE64; then
    missing "iOS signing values are not set: only checking that the app compiles (no .ipa)."
    "${XCODE[@]}" -derivedDataPath "$WORK/derived" CODE_SIGNING_ALLOWED=NO build
    exit 0
  fi

  # A throw-away keychain with the distribution certificate, deleted on exit.
  KEYCHAIN="$WORK/signing.keychain-db"
  kc_password="$(openssl rand -hex 16)"
  security create-keychain -p "$kc_password" "$KEYCHAIN"
  security set-keychain-settings -lut 3600 "$KEYCHAIN"
  security unlock-keychain -p "$kc_password" "$KEYCHAIN"
  printf '%s' "$IOS_CERTIFICATE_P12_BASE64" | base64 --decode > "$WORK/certificate.p12"
  security import "$WORK/certificate.p12" -P "$IOS_CERTIFICATE_PASSWORD" -A -t cert -f pkcs12 -k "$KEYCHAIN"
  security set-key-partition-list -S apple-tool:,apple: -s -k "$kc_password" "$KEYCHAIN" > /dev/null
  security list-keychains -d user -s "$KEYCHAIN" $(security list-keychains -d user | tr -d '"')
  identity="$(security find-identity -v -p codesigning "$KEYCHAIN" | awk '/Distribution/ { print $2; exit }')"
  [[ -n "$identity" ]] || die "The certificate in IOS_CERTIFICATE_P12_BASE64 is not an Apple Distribution certificate."

  # The cafe's App Store provisioning profile; it must be made for this cafe's app id.
  printf '%s' "$IOS_PROFILE_BASE64" | base64 --decode > "$WORK/profile.mobileprovision"
  security cms -D -i "$WORK/profile.mobileprovision" > "$WORK/profile.plist"
  profile_uuid="$(/usr/libexec/PlistBuddy -c 'Print :UUID' "$WORK/profile.plist")"
  profile_app="$(/usr/libexec/PlistBuddy -c 'Print :Entitlements:application-identifier' "$WORK/profile.plist")"
  [[ "$profile_app" == "$APPLE_TEAM_ID.$APP_ID" ]] ||
    die "The provisioning profile is for $profile_app, but this cafe is $APPLE_TEAM_ID.$APP_ID (check IOS_PROFILE_BASE64 and APPLE_TEAM_ID)."
  for dir in "$HOME/Library/MobileDevice/Provisioning Profiles" "$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles"; do
    mkdir -p "$dir" && cp "$WORK/profile.mobileprovision" "$dir/$profile_uuid.mobileprovision"
  done

  # Store builds use the production push service, and the App target signs with this certificate and profile (set
  # in the project, not on the command line, so the CocoaPods targets are left alone).
  plutil -replace aps-environment -string production frontend/ios/App/App/App.entitlements
  perl -pi -e "s/CODE_SIGN_STYLE = Automatic;/CODE_SIGN_STYLE = Manual; PROVISIONING_PROFILE_SPECIFIER = \"$profile_uuid\"; \"CODE_SIGN_IDENTITY[sdk=iphoneos*]\" = \"$identity\";/" \
    frontend/ios/App/App.xcodeproj/project.pbxproj

  "${XCODE[@]}" -archivePath "$WORK/App.xcarchive" DEVELOPMENT_TEAM="$APPLE_TEAM_ID" \
    OTHER_CODE_SIGN_FLAGS="--keychain $KEYCHAIN" archive

  cat > "$WORK/ExportOptions.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>method</key><string>app-store-connect</string>
  <key>teamID</key><string>$APPLE_TEAM_ID</string>
  <key>signingStyle</key><string>manual</string>
  <key>signingCertificate</key><string>$identity</string>
  <key>provisioningProfiles</key>
  <dict><key>$APP_ID</key><string>$profile_uuid</string></dict>
  <key>uploadSymbols</key><true/>
</dict>
</plist>
EOF
  xcodebuild -exportArchive -archivePath "$WORK/App.xcarchive" -exportOptionsPlist "$WORK/ExportOptions.plist" \
    -exportPath "$WORK/export"
  cp "$(ls "$WORK"/export/*.ipa | head -n 1)" "$OUT/$NAME.ipa"
fi

echo "Done: $(ls "$OUT" | tr '\n' ' ')in build/apps/$CAFE/"

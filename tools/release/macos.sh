#!/usr/bin/env bash
# Builds the macOS head as a signed .app and leaves it in Release/mac.
#
# A .app is not optional for the Mac App Store: review looks for the bundle
# layout, the privacy manifest at Contents/Resources and the distribution
# profile at Contents/embedded.provisionprofile. Signing with rel-slapper-mac
# means the Apple Distribution certificate, not the development one, so the
# result cannot be run from this machine's phone-style development identity -
# it is a build to upload.
#
#   ./tools/release/macos.sh [output-directory]
#
# Override the identity or the profile with MAC_SIGN_IDENTITY / MAC_PROFILE_NAME,
# or point MAC_PROFILE at a .provisionprofile file directly.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
out="${1:-$root/Release/mac}"
app="$out/SuperSlapper.app"
macos="$app/Contents/MacOS"
resources="$app/Contents/Resources"

identity="${MAC_SIGN_IDENTITY:-Apple Distribution: Paul Johnson (3UH7BE38T3)}"
profile_name="${MAC_PROFILE_NAME:-rel-slapper-mac}"
version="${MAC_BUNDLE_VERSION:-1.0.0}"
build_number="${MAC_BUILD_NUMBER:-4}"

profile="${MAC_PROFILE:-}"
if [ -z "$profile" ]; then
    profiles="$HOME/Library/MobileDevice/Provisioning Profiles"
    for candidate in "$profiles"/*; do
        name="$(security cms -D -i "$candidate" 2>/dev/null \
            | plutil -extract Name raw - 2>/dev/null || true)"
        if [ "$name" = "$profile_name" ]; then
            profile="$candidate"
            break
        fi
    done
fi

if [ -z "$profile" ] || [ ! -f "$profile" ]; then
    echo "No provisioning profile named $profile_name is installed." >&2
    echo "Download it from the developer portal, or set MAC_PROFILE=<path>." >&2
    exit 1
fi

echo "==> profile: $(basename "$profile") ($profile_name)"
echo "==> identity: $identity"

rm -rf "$out"
mkdir -p "$macos" "$resources"

echo "==> publishing osx-arm64"
dotnet publish "$root/src/SuperSlapper.Desktop/SuperSlapper.Desktop.csproj" \
    -f net10.0 -c Release -r osx-arm64 --self-contained -o "$macos" >/dev/null

# Review checks this exact path; beside the binary is not good enough.
rm -f "$macos/PrivacyInfo.xcprivacy"
cp "$root/src/SuperSlapper.Desktop/PrivacyInfo.xcprivacy" "$resources/PrivacyInfo.xcprivacy"

# createdump is the runtime's crash-dump helper, shipped as a nested executable.
# The store wants a provisioning profile with an application identifier for every
# executable in the bundle, and that helper has no reason to carry one: the game
# never asks the runtime to write a dump, so nothing ever starts it.
rm -f "$macos/createdump"

# The icon lives in Resources and Info.plist names it: without a 1024px
# icon_512x512@2x in the ICNS, validation reports the bundle as having no icon.
cp "$root/src/SuperSlapper.Desktop/Assets/SuperSlapper.icns" "$resources/SuperSlapper.icns"

# SDL answers a macOS bundle with Contents/Resources as its base path, and
# MonoGame reads Content relative to that. Left in Contents/MacOS the app starts
# with no art, no fonts and no sounds at all.
mv "$macos/Content" "$resources/Content"

cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleDevelopmentRegion</key>
	<string>en</string>
	<key>CFBundleExecutable</key>
	<string>SuperSlapper</string>
	<key>CFBundleIconFile</key>
	<string>SuperSlapper</string>
	<key>CFBundleIdentifier</key>
	<string>uk.co.allthejohnsons.superslapper</string>
	<key>CFBundleInfoDictionaryVersion</key>
	<string>6.0</string>
	<key>CFBundleName</key>
	<string>SuperSlapper</string>
	<key>CFBundleDisplayName</key>
	<string>SuperSlapper</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>CFBundleShortVersionString</key>
	<string>$version</string>
	<key>CFBundleVersion</key>
	<string>$build_number</string>
	<key>LSApplicationCategoryType</key>
	<string>public.app-category.games</string>
	<key>LSMinimumSystemVersion</key>
	<string>13.0</string>
	<key>NSHighResolutionCapable</key>
	<true/>
	<key>NSSupportsAutomaticGraphicsSwitching</key>
	<true/>
</dict>
</plist>
PLIST

cp "$profile" "$app/Contents/embedded.provisionprofile"
# The profile usually arrives from the developer portal by browser, so it can
# carry a quarantine flag. That flag travels into the installer payload as an
# AppleDouble ._ file, so clear it before the bundle is sealed.
xattr -c "$app/Contents/embedded.provisionprofile"

# --deep signs what is inside before sealing the bundle. A publish folder is all
# loose Mach-O files and managed assemblies: signing the bundle without it fails
# on the first assembly that is not already signed.
echo "==> signing bundle"
codesign --force --deep --sign "$identity" \
    --entitlements "$root/src/SuperSlapper.Desktop/Entitlements.plist" \
    "$app"

echo "==> verifying"
codesign --verify --deep --strict --verbose=2 "$app"
codesign -dvv "$app" 2>&1 | grep -E "Authority|TeamIdentifier"

# Both of these are rejections from the store, not warnings, so fail the build.
codesign -d --entitlements - "$app" 2>/dev/null \
    | grep -q "com.apple.security.app-sandbox" \
    || { echo "the signature is missing com.apple.security.app-sandbox" >&2; exit 1; }
[ -f "$resources/SuperSlapper.icns" ] \
    || { echo "Contents/Resources/SuperSlapper.icns is missing" >&2; exit 1; }
[ ! -e "$macos/createdump" ] \
    || { echo "createdump is still in Contents/MacOS" >&2; exit 1; }

echo "==> $app"

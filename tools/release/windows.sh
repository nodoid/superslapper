#!/usr/bin/env bash
# Builds the Windows distribution: two MSIX packages, one per architecture.
#
# The publish happens here, the packaging happens in the Windows VM, because
# makeappx and signtool are Windows tools. The VM reads the staged layout from
# its shared folder, packs and signs it, and writes the .msix back.
#
#   ./tools/release/windows.sh [output-directory]
#
# Environment:
#   WINDOWS_VM    Parallels VM name           (default: Windows 11)
#   MAC_SHARE     Mac folder the VM sees as \\Mac\Home  (default: $HOME)
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
out="${1:-$root/Release/windows}"
vm="${WINDOWS_VM:-Windows 11}"
stage="$root/out/msix"

# The VM shares the Mac home directory, so a Mac path maps onto \\Mac\Home.
share_unix="${stage/#$HOME/}"
share="\\\\Mac\\Home${share_unix//\//\\}"
scripts="$root/tools/release/windows"
scripts_share="\\\\Mac\\Home${scripts/#$HOME/}"
scripts_share="${scripts_share//\//\\}"

command -v prlctl >/dev/null || { echo "prlctl is not on the path" >&2; exit 1; }
prlctl list -a | grep -q "$vm" || { echo "no VM named $vm" >&2; exit 1; }

rm -rf "$stage"
mkdir -p "$stage"

for arch in x64 arm64; do
    echo "==> publishing $arch"
    dotnet publish "$root/src/SuperSlapper.Desktop/SuperSlapper.Desktop.csproj" \
        -f net10.0-windows -c Release -r "win-$arch" --self-contained -o "$stage/$arch" >/dev/null

    echo "==> staging MSIX layout for $arch"
    python3 "$root/tools/release/msix.py" --layout "$stage/$arch" --arch "$arch"
done

# The certificate is created once: re-signing every build with a new one would
# change the package identity and break existing installs.
if ! prlctl exec "$vm" cmd /c 'if exist C:\msix\superslapper.pfx (exit 0) else (exit 1)' >/dev/null 2>&1; then
    echo "==> creating the self-signed certificate"
    prlctl exec "$vm" powershell -NoProfile -ExecutionPolicy Bypass \
        -File "$scripts_share\\make-cert.ps1" -Share "$share" >/dev/null
fi

echo "==> packing and signing"
prlctl exec "$vm" powershell -NoProfile -ExecutionPolicy Bypass \
    -File "$scripts_share\\pack-sign.ps1" -Share "$share" |
    grep -E "packing|signing|Successfully signed|Package creation"

mkdir -p "$out"
mv "$stage"/SuperSlapper-*.msix "$out"/
[ -f "$stage/SuperSlapper-selfsigned.cer" ] && mv "$stage/SuperSlapper-selfsigned.cer" "$out"/

ls -lh "$out"/SuperSlapper-*.msix

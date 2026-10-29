#!/usr/bin/env python3
"""Stages a Windows publish folder as an MSIX layout.

MSIX needs three things the plain publish does not have: a manifest, the tile
images it points at, and a flat layout that makeappx understands. This writes the
manifest and the images for one architecture at a time; packing and signing are
`makeappx` and `signtool` in the Windows VM (see tools/release/windows.sh).

    python3 tools/release/msix.py --layout out/msix/x64 --arch x64
    python3 tools/release/msix.py --layout out/msix/arm64 --arch arm64

The publisher in the manifest has to match the certificate subject exactly, or
Windows refuses the signature, so both come from the same constant here and in
the signing step.
"""
from __future__ import annotations

import argparse
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

IDENTITY_NAME = "PaulFJohnson.SuperSlapper"
PUBLISHER = "CN=Paul F. Johnson"
# Partner Center checks this against the account's publisher display name and
# refuses the package if the two differ, so it is the account name, not the
# person's name. The Identity Publisher above is a separate field.
PUBLISHER_DISPLAY = "all-the-johnsons"
DISPLAY_NAME = "SuperSlapper"
DESCRIPTION = "Slap-fighting career sim"
VERSION = "1.0.0.0"
MIN_WINDOWS = "10.0.17763.0"

# The tile sizes Windows asks for on a desktop app. 44 and 150 are required by
# the manifest; the store logo is what the Store listing shows.
TILES = {
    "Square44x44Logo.png": 44,
    "Square150x150Logo.png": 150,
    "StoreLogo.png": 50,
}

MANIFEST = """<?xml version="1.0" encoding="utf-8"?>
<Package
    xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
    xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
    xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
    IgnorableNamespaces="uap rescap">

  <Identity Name="{name}" Publisher="{publisher}" Version="{version}"
            ProcessorArchitecture="{arch}" />

  <Properties>
    <DisplayName>{display}</DisplayName>
    <PublisherDisplayName>{publisher_display}</PublisherDisplayName>
    <Logo>Assets\\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="{min_windows}"
                        MaxVersionTested="10.0.26100.0" />
  </Dependencies>

  <Resources>
    <Resource Language="en-us" />
  </Resources>

  <Applications>
    <Application Id="App" Executable="SuperSlapper.exe"
                 EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="{display}" Description="{description}"
                          BackgroundColor="transparent"
                          Square150x150Logo="Assets\\Square150x150Logo.png"
                          Square44x44Logo="Assets\\Square44x44Logo.png" />
    </Application>
  </Applications>

  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
"""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--layout", required=True, help="publish folder to stage in place")
    parser.add_argument("--arch", required=True, choices=("x64", "arm64"))
    args = parser.parse_args()

    if not os.path.isfile(os.path.join(args.layout, "SuperSlapper.exe")):
        print(f"{args.layout} is not a Windows publish folder", file=sys.stderr)
        return 2

    try:
        from PIL import Image
    except ImportError:
        print("Pillow is needed: pip3 install pillow", file=sys.stderr)
        return 2

    assets = os.path.join(args.layout, "Assets")
    os.makedirs(assets, exist_ok=True)

    icon = Image.open(os.path.join(ROOT, "Content", "art", "appicon_1024.png")).convert("RGBA")
    for name, size in TILES.items():
        icon.resize((size, size), Image.LANCZOS).save(os.path.join(assets, name))

    manifest = MANIFEST.format(
        name=IDENTITY_NAME,
        publisher=PUBLISHER,
        publisher_display=PUBLISHER_DISPLAY,
        display=DISPLAY_NAME,
        description=DESCRIPTION,
        version=VERSION,
        arch=args.arch,
        min_windows=MIN_WINDOWS,
    )

    with open(os.path.join(args.layout, "AppxManifest.xml"), "w", encoding="utf-8") as handle:
        handle.write(manifest)

    print(f"staged {args.layout} for {args.arch}: {', '.join(TILES)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

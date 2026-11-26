#!/usr/bin/env python3
"""Guards the iOS head against the two declaration checks an upload runs.

Purpose strings (ITMS-90683). App Store Connect's scan reads the linked symbols,
not the code that runs. .NET for iOS links a framework for every binding, so the
app carries privacy-guarded APIs it never calls - the camera, the photo library,
motion, Face ID and the rest - and an upload without the matching
NS*UsageDescription in Info.plist is refused as an Invalid Binary before it
reaches TestFlight.

App tracking is the exception, and the check runs the other way round. The
runtime links AdSupport and AppTrackingTransparency whatever the app does, and
App Store Connect reads NSUserTrackingUsageDescription itself as a statement that
the app tracks: a binary carrying the key cannot be submitted unless the App
Privacy answers declare tracking. The game never asks for tracking permission,
so the guard fails when the key is present rather than when it is missing.

Privacy manifest (ITMS-91053, ITMS-91055). The same scan looks for the
required-reason APIs: a category the binary imports but the manifest leaves out
is ITMS-91053, and a reason code borrowed from another category is ITMS-91055,
because Apple's codes are specific to the category they are filed under.

Frameworks the app does not use (App Review). 1.0 build 7 was rejected for using
the SMS Fraud extension and HealthKit. Neither was ever called: an unlinked build
links a framework for every binding the runtime carries, and review reads the
link as functionality the app has no feature for. The distribution build trims
the SDK assemblies, which removed both, so the guard fails if HealthKit,
HealthKitUI or IdentityLookup is linked again and if either NSHealth* key is back
in Info.plist - a purpose string would claim the access back instead of fixing it.

    python3 tools/release/check_ios_plist.py [bundle-or-plist]

With no argument it checks Release/ios/SuperSlapper.app: the purpose strings are
taken from the frameworks that bundle's own executable links, and the manifest
from the bundle root. Pass an Info.plist and it checks the whole purpose-string
set instead, with the manifest beside it, which is what the runtime ends up
linking anyway. Pass any other .app and it reads that bundle.

Exits 1, naming the key or the category, when anything is missing, empty or
filed under a reason Apple does not publish, so the check works in a release
build. The tracking purpose string is checked the other way round: the check
fails when NSUserTrackingUsageDescription is present, because App Store Connect
reads it as a claim that the app tracks.
"""

from __future__ import annotations

import plistlib
import subprocess
import sys
from pathlib import Path
from typing import Iterator

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_APP = ROOT / "Release/ios/SuperSlapper.app"

# App Store Connect reads this key itself as a claim that the app tracks, so it
# is checked the other way round: the key has to be absent. See FORBIDDEN_KEYS.
TRACKING_KEY = "NSUserTrackingUsageDescription"

# Frameworks the binary must not link. App Review rejected 1.0 build 7 for using
# the SMS Fraud extension and HealthKit; an unlinked build links a framework for
# every binding the runtime carries, so both arrived without a line of code that
# reaches them. Trimming the SDK assemblies is what removed them, and a
# framework back in this set means the trimming was turned off again.
FORBIDDEN_FRAMEWORKS: dict[str, str] = {
    "IdentityLookup": "use of the SMS Fraud extension",
    "HealthKit": "use of health data",
    "HealthKitUI": "use of health data",
}

# Keys that must not appear in Info.plist at all. A purpose string does not
# remove a framework reference, it tells the store the app wants the access.
FORBIDDEN_KEYS: dict[str, str] = {
    TRACKING_KEY: (
        "App Store Connect reads it as a claim that the app tracks, and the game "
        "never asks for tracking permission"
    ),
    "NSHealthShareUsageDescription": (
        "HealthKit is no longer linked, and the key claims the app reads health data"
    ),
    "NSHealthUpdateUsageDescription": (
        "HealthKit is no longer linked, and the key claims the app writes health data"
    ),
}

# Framework -> the purpose strings App Store Connect wants when it is linked.
# A framework a build does not link asks for nothing, which is why the guard
# reads the real link list instead of demanding every key in the file.
FRAMEWORK_KEYS: dict[str, tuple[str, ...]] = {
    "AVFoundation": ("NSCameraUsageDescription", "NSMicrophoneUsageDescription"),
    "Photos": ("NSPhotoLibraryUsageDescription", "NSPhotoLibraryAddUsageDescription"),
    "PhotosUI": ("NSPhotoLibraryUsageDescription", "NSPhotoLibraryAddUsageDescription"),
    "MobileCoreServices": ("NSPhotoLibraryUsageDescription",),
    "CoreMotion": ("NSMotionUsageDescription",),
    "Speech": ("NSSpeechRecognitionUsageDescription",),
    "LocalAuthentication": ("NSFaceIDUsageDescription",),
    "EventKit": (
        "NSCalendarsUsageDescription",
        "NSCalendarsFullAccessUsageDescription",
        "NSRemindersUsageDescription",
        "NSRemindersFullAccessUsageDescription",
    ),
    "EventKitUI": (
        "NSCalendarsUsageDescription",
        "NSCalendarsFullAccessUsageDescription",
        "NSRemindersUsageDescription",
        "NSRemindersFullAccessUsageDescription",
    ),
    "Contacts": ("NSContactsUsageDescription",),
    "ContactsUI": ("NSContactsUsageDescription",),
    # HealthKit and HealthKitUI are absent on purpose: they must not be linked at
    # all. See FORBIDDEN_FRAMEWORKS.
    "HomeKit": ("NSHomeKitUsageDescription",),
    "CoreNFC": ("NFCReaderUsageDescription",),
    "NearbyInteraction": ("NSNearbyInteractionUsageDescription",),
    "Intents": ("NSSiriUsageDescription", "NSFocusStatusUsageDescription"),
    "IntentsUI": ("NSSiriUsageDescription", "NSFocusStatusUsageDescription"),
    "MediaPlayer": ("NSAppleMusicUsageDescription",),
    "CoreBluetooth": ("NSBluetoothAlwaysUsageDescription",),
    "CoreLocation": (
        "NSLocationWhenInUseUsageDescription",
        "NSLocationAlwaysAndWhenInUseUsageDescription",
    ),
    "CoreLocationUI": (
        "NSLocationWhenInUseUsageDescription",
        "NSLocationAlwaysAndWhenInUseUsageDescription",
    ),
}

# The approved reason codes per category. Apple publishes them under
# NSPrivacyAccessedAPITypes; a code is only valid under its own category.
APPROVED_REASONS: dict[str, tuple[str, ...]] = {
    "NSPrivacyAccessedAPICategoryFileTimestamp": ("C617.1", "DDA9.1", "3B52.1", "0A2A.1"),
    "NSPrivacyAccessedAPICategorySystemBootTime": ("35F9.1", "8FFB.1", "3D61.1"),
    "NSPrivacyAccessedAPICategoryDiskSpace": ("E174.1", "85F4.1", "7D9E.1", "B728.1"),
    "NSPrivacyAccessedAPICategoryUserDefaults": ("CA92.1", "1C8F.1", "C56D.1", "AC6B.1"),
    "NSPrivacyAccessedAPICategoryActiveKeyboards": ("3EC4.1", "54BD.1"),
}

# What a category looks like from outside the source: imported C functions and
# the Objective-C selectors the AOT build carries as strings. A category shows
# up here whether or not the game ever reaches it, which is the point.
API_MARKERS: dict[str, tuple[frozenset[str], frozenset[str]]] = {
    "NSPrivacyAccessedAPICategoryFileTimestamp": (
        frozenset({"stat", "fstat", "lstat", "fstatat", "getattrlist", "getattrlistbulk", "getattrlistat"}),
        frozenset(
            {
                "attributesOfItemAtPath:error:",
                "NSFileModificationDate",
                "NSFileCreationDate",
                "contentModificationDateKey",
                "creationDateKey",
            }
        ),
    ),
    "NSPrivacyAccessedAPICategorySystemBootTime": (
        frozenset({"mach_absolute_time", "mach_continuous_time"}),
        frozenset({"systemUptime"}),
    ),
    "NSPrivacyAccessedAPICategoryDiskSpace": (
        frozenset({"statfs", "fstatfs", "statvfs", "fstatvfs"}),
        frozenset(
            {
                "attributesOfFileSystemForPath:error:",
                "NSFileSystemFreeSize",
                "NSFileSystemSize",
                "volumeAvailableCapacityKey",
            }
        ),
    ),
    "NSPrivacyAccessedAPICategoryUserDefaults": (
        frozenset(),
        frozenset({"NSUserDefaults", "standardUserDefaults"}),
    ),
    "NSPrivacyAccessedAPICategoryActiveKeyboards": (
        frozenset(),
        frozenset({"activeInputModes"}),
    ),
}


def stream(command: list[str]) -> Iterator[str]:
    """Lines of a command's output, or a clean exit if the tool is missing."""
    try:
        process = subprocess.Popen(
            command,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
    except FileNotFoundError:
        sys.exit(f"{command[0]} is missing: run this on a Mac with Xcode installed.")

    assert process.stdout is not None
    for line in process.stdout:
        yield line.rstrip("\n")

    if process.wait() != 0:
        error = process.stderr.read().strip() if process.stderr else ""
        sys.exit(f"{' '.join(command)} failed: {error}")


def read_plist(path: Path) -> dict:
    with path.open("rb") as handle:
        return plistlib.load(handle)


# --- purpose strings -------------------------------------------------------


def linked_frameworks(executable: Path) -> set[str]:
    """The framework names the executable links, e.g. AVFoundation."""
    names = set()
    for line in stream(["otool", "-L", str(executable)]):
        line = line.strip()
        if not line or not line.split(" (")[0].startswith("/"):
            continue
        path = line.split(" (")[0]
        if ".framework" in path:
            names.add(path.split(".framework")[0].rsplit("/", 1)[-1])
    return names


def required_keys(frameworks: set[str] | None) -> dict[str, str]:
    """Key -> the framework that asks for it. None means every key."""
    if frameworks is None:
        wanted: dict[str, str] = {}
        for framework, keys in FRAMEWORK_KEYS.items():
            for key in keys:
                wanted.setdefault(key, framework)
        return wanted

    wanted = {}
    for framework in sorted(frameworks):
        for key in FRAMEWORK_KEYS.get(framework, ()):
            wanted.setdefault(key, framework)
    return wanted


def check_purpose_strings(plist_path: Path, frameworks: set[str] | None) -> list[str]:
    info = read_plist(plist_path)
    required = required_keys(frameworks)
    missing = [
        (key, framework)
        for key, framework in sorted(required.items())
        if not str(info.get(key, "")).strip()
    ]

    declared = sum(1 for key in info if key.endswith("UsageDescription"))
    scope = (
        "every guarded API the runtime links"
        if frameworks is None
        else f"{len(frameworks)} linked framework(s)"
    )
    print(f"[plist] {plist_path}")
    print(f"[plist] {scope}, {len(required)} purpose string(s) required, {declared} declared")

    problems = []
    for key, framework in missing:
        problems.append(f"{key} is missing or empty (required by {framework})")
    for key, reason in sorted(FORBIDDEN_KEYS.items()):
        if str(info.get(key, "")).strip():
            problems.append(f"{key} is present: {reason}")
    if not problems:
        print("[plist] PASS: nothing linked is left without a purpose string")
    return problems


# --- linked frameworks -----------------------------------------------------


def check_linked_frameworks(frameworks: set[str] | None) -> list[str]:
    """Frameworks App Review reads as functionality the app does not have."""
    if frameworks is None:
        print("[link] not checked: pass the .app to read the real link list")
        return []

    problems = [
        f"{framework} is linked, which App Review reads as {reason}; the binding "
        "has to be trimmed away, not declared"
        for framework in sorted(frameworks)
        if (reason := FORBIDDEN_FRAMEWORKS.get(framework)) is not None
    ]
    if not problems:
        print(
            "[link] PASS: no HealthKit and no SMS Fraud extension "
            f"({len(frameworks)} framework(s) linked)"
        )
    return problems


# --- privacy manifest ------------------------------------------------------


def scan_references(executable: Path) -> set[str]:
    """Required-reason API categories the executable imports or references."""
    symbols: dict[str, str] = {}
    selectors: dict[str, str] = {}
    for category, (symbol_set, selector_set) in API_MARKERS.items():
        for symbol in symbol_set:
            symbols[symbol] = category
        for selector in selector_set:
            selectors[selector] = category

    found = set()
    if symbols:
        for line in stream(["nm", "-u", str(executable)]):
            fields = line.split()
            if not fields:
                continue
            symbol = fields[-1].lstrip("_")
            if symbol in symbols:
                found.add(symbols[symbol])

    if selectors:
        for line in stream(["strings", "-a", str(executable)]):
            if line in selectors:
                found.add(selectors[line])

    return found


def check_manifest(manifest_path: Path, referenced: set[str] | None) -> list[str]:
    print(f"[privacy] {manifest_path}")
    if not manifest_path.is_file():
        return ["the bundle has no PrivacyInfo.xcprivacy"]

    manifest = read_plist(manifest_path)
    entries = manifest.get("NSPrivacyAccessedAPITypes", [])
    if not isinstance(entries, list):
        return ["NSPrivacyAccessedAPITypes is not an array"]

    problems: list[str] = []
    declared: dict[str, list[str]] = {}
    for entry in entries:
        if not isinstance(entry, dict):
            problems.append("an NSPrivacyAccessedAPITypes entry is not a dictionary")
            continue
        category = str(entry.get("NSPrivacyAccessedAPIType", "")).strip()
        reasons = [str(reason).strip() for reason in entry.get("NSPrivacyAccessedAPITypeReasons", [])]
        declared.setdefault(category, []).extend(reasons)

        approved = APPROVED_REASONS.get(category)
        if approved is None:
            problems.append(f"{category or '(no category)'} is not a required-reason API category")
            continue
        if not reasons:
            problems.append(f"{category} declares a category with no reason")
        for reason in reasons:
            if reason not in approved:
                problems.append(
                    f"{category} reason {reason} is not approved "
                    f"(allowed: {', '.join(approved)})"
                )

    if referenced is not None:
        for category in sorted(referenced - set(declared)):
            problems.append(f"{category} is referenced by the binary but not declared")

    categories = len(declared)
    if referenced is None:
        print(f"[privacy] {categories} category(ies) declared")
    else:
        print(f"[privacy] {len(referenced)} category(ies) referenced, {categories} declared")
    if not problems:
        print("[privacy] PASS: every category has an approved reason")
    return problems


# --- entry point -----------------------------------------------------------


def report(kind: str, problems: list[str]) -> int:
    if not problems:
        return 0
    print(f"[{kind}] FAIL: {len(problems)} problem(s)", file=sys.stderr)
    for problem in problems:
        print(f"[{kind}]   {problem}", file=sys.stderr)
    return 1


def check(plist_path: Path, manifest_path: Path, frameworks: set[str] | None, executable: Path | None) -> int:
    status = report("plist", check_purpose_strings(plist_path, frameworks))
    status |= report("link", check_linked_frameworks(frameworks))
    referenced = scan_references(executable) if executable is not None else None
    status |= report("privacy", check_manifest(manifest_path, referenced))
    return status


def main(argv: list[str]) -> int:
    if len(argv) > 2:
        sys.exit("usage: check_ios_plist.py [bundle-or-plist]")

    target = Path(argv[1]) if len(argv) == 2 else DEFAULT_APP
    if not target.exists():
        sys.exit(
            f"{target} does not exist. Build the bundle, or pass the path to a "
            ".app or an Info.plist."
        )

    if target.is_dir():
        plist_path = target / "Info.plist"
        if not plist_path.is_file():
            sys.exit(f"{target} has no Info.plist.")
        executable = target / str(read_plist(plist_path).get("CFBundleExecutable", ""))
        if not executable.is_file():
            sys.exit(f"{target} has no executable named {executable.name}.")
        return check(
            plist_path,
            target / "PrivacyInfo.xcprivacy",
            linked_frameworks(executable),
            executable,
        )

    return check(target, target.parent / "PrivacyInfo.xcprivacy", None, None)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))

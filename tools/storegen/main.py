#!/usr/bin/env python3
"""Generate every store submission image SuperSlapper needs.

Run from the repository root, after the self-test has written its captures:

    dotnet run --project src/SuperSlapper.Desktop -f net10.0 -- --selftest out/selftest
    python3 tools/storegen/main.py

Assets land in ./Stores, one folder per store, self-contained so a folder can be
uploaded as it stands. The copy that goes with them is hand-written and lives
beside the images as ``copy.md``.

The generator checks its own output: every file is reopened, measured and, where
the store forbids it, checked for an alpha channel. A wrong-sized asset fails the
run rather than reaching a submission.
"""
from __future__ import annotations

import argparse
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from PIL import Image  # noqa: E402

import images  # noqa: E402
import specs  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DEFAULT_CAPTURES = os.path.join(ROOT, "out", "selftest")
DEFAULT_OUT = os.path.join(ROOT, "Stores")

_written: list[tuple[str, str, tuple[int, int]]] = []


def save(path: str, image: Image.Image, alpha: bool, jpeg: bool = False) -> None:
    """Writes one asset, flattening it to RGB when the store bars transparency."""
    os.makedirs(os.path.dirname(path), exist_ok=True)

    if jpeg:
        image.convert("RGB").save(path, "JPEG", quality=92, subsampling=0)
    elif alpha:
        image.convert("RGBA").save(path, "PNG", optimize=True)
    else:
        flattened = Image.new("RGB", image.size, (0, 0, 0))
        flattened.paste(image.convert("RGBA"), mask=image.convert("RGBA").split()[3])
        flattened.save(path, "PNG", optimize=True)

    _written.append((path, os.path.basename(path), image.size))


def slug(text: str) -> str:
    """A filename from a caption: lowercase words joined by underscores."""
    keep = [c.lower() if c.isalnum() else "_" for c in text]
    joined = "".join(keep)
    while "__" in joined:
        joined = joined.replace("__", "_")
    return joined.strip("_")[:48]


def write_key_art(folder: str, group: tuple[specs.Spec, ...], builders: dict) -> None:
    """Writes a store's key art, each from its builder, honouring the spec.

    Specs without a builder here are written by a later step, which is how the
    icons are handled.
    """
    for spec in group:
        build = builders.get(spec.filename)
        if build is None:
            continue

        save(os.path.join(folder, spec.filename), build(spec.size), spec.alpha, spec.jpeg)


def write_shots(folder: str, captures: str, size: tuple[int, int], captioned: bool,
                raw: bool = False) -> None:
    """Writes the eight screenshots, either as raw captures or captioned."""
    for index, shot in enumerate(specs.SHOTS, start=1):
        if captioned:
            image = images.captioned_screenshot(captures, shot, size)
        else:
            image = images.raw_screenshot(captures, shot)

        name = f"{index:02d}_{slug(shot.headline)}.png"
        save(os.path.join(folder, name), image, alpha=False)


def require_captures(captures: str) -> None:
    """The generator needs the real captures; a missing one is a broken pipeline,
    not something to paper over with a placeholder."""
    missing = [shot.source for shot in specs.SHOTS
               if not os.path.exists(os.path.join(captures, shot.source))]

    if missing:
        print(f"missing {len(missing)} capture(s) in {captures}:")
        for name in missing:
            print(f"  {name}")
        print("run the self-test first, e.g.\n"
              "  dotnet run --project src/SuperSlapper.Desktop -f net10.0 -- --selftest out/selftest")
        raise SystemExit(2)


FIELD = re.compile(r'<!--\s*field\s+name="([^"]+)"\s+limit="([^"]+)"\s*-->')


def check_copy(out: str) -> list[str]:
    """Measures the hand-written listing copy against each store's field limits.

    The copy carries ``<!-- field name="..." limit="..." -->`` markers around every
    field with a limit, so the marketing text is checked the same way the images
    are: nothing goes to a store longer than the field allows.
    """
    problems: list[str] = []

    for store in ("MicrosoftStore", "Xbox", "AppStore", "GooglePlay"):
        path = os.path.join(out, store, "copy.md")
        if not os.path.exists(path):
            problems.append(f"{store}: copy.md is missing")
            continue

        with open(path, encoding="utf-8") as handle:
            text = handle.read()

        if not FIELD.search(text):
            continue

        for match in FIELD.finditer(text):
            field, limit = match.group(1), match.group(2)
            body = text[match.end():].split("<!-- /field -->")[0].strip()

            if " x " in limit:
                count_limit, per_limit = (int(part) for part in limit.split(" x "))
                bullets = [line.strip("- ") for line in body.splitlines() if line.strip()]
                longest = max((len(b) for b in bullets), default=0)
                print(f"  copy {store}: {field} {len(bullets)}/{count_limit} entries, "
                      f"longest {longest}/{per_limit} characters")
                if len(bullets) > count_limit:
                    problems.append(f"{store}/{field}: {len(bullets)} entries, the limit is "
                                    f"{count_limit}")
                for bullet in bullets:
                    if len(bullet) > per_limit:
                        problems.append(f"{store}/{field}: an entry is {len(bullet)} characters, "
                                        f"the limit is {per_limit}")
                continue

            length = len(body)
            print(f"  copy {store}: {field} {length}/{limit} characters")
            if length > int(limit):
                problems.append(f"{store}/{field}: {length} characters, the limit is {limit}")

    return problems


def verify() -> int:
    """Reopens everything written and checks it against what the store asked for."""
    problems: list[str] = []

    for path, name, size in _written:
        with Image.open(path) as image:
            if image.size != size:
                problems.append(f"{name}: wrote {image.size[0]}x{image.size[1]}, expected "
                                f"{size[0]}x{size[1]}")

    for spec_group in (specs.XBOX_ART, specs.MICROSOFT_ART, specs.GOOGLE_ICON, specs.APPLE_ICON):
        for spec in spec_group:
            if spec.alpha or spec.jpeg:
                continue
            for path, name, _ in _written:
                if name != spec.filename:
                    continue
                with Image.open(path) as image:
                    if image.mode != "RGB":
                        problems.append(f"{name}: {spec.requirement} asset must have no alpha, "
                                        f"it is {image.mode}")

    if problems:
        for problem in problems:
            print("FAIL " + problem)
        return 1

    print(f"  ok: {len(_written)} file(s), all at their required size")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--captures", default=DEFAULT_CAPTURES,
                        help="folder holding the self-test captures of the game")
    parser.add_argument("--out", default=DEFAULT_OUT, help="where to write the store folders")
    args = parser.parse_args()

    if not os.path.isdir(args.captures):
        print(f"no capture folder at {args.captures}")
        return 2

    require_captures(args.captures)

    microsoft = os.path.join(args.out, "MicrosoftStore", "images")
    xbox = os.path.join(args.out, "Xbox", "images")
    apple = os.path.join(args.out, "AppStore", "images")
    google = os.path.join(args.out, "GooglePlay", "images")

    small_icon = lambda size: images.icon_from_app(size[0])

    # ---- Microsoft Store
    write_key_art(microsoft, specs.MICROSOFT_ART, {
        "box_art_2160.png": images.box_art,
        "poster_art_1440x2160.png": images.poster_art,
        "super_hero_art_3840x2160.png": images.super_hero_art,
    })
    for spec in specs.MICROSOFT_ART:
        if spec.filename.startswith("store_icon_"):
            save(os.path.join(microsoft, spec.filename), small_icon(spec.size), spec.alpha)
    write_shots(os.path.join(microsoft, "screenshots_1920x1080"), args.captures,
                specs.SHOT_LANDSCAPE, captioned=False)

    # ---- Xbox
    write_key_art(xbox, specs.XBOX_ART, {
        "box_art_2160.png": images.box_art,
        "poster_art_1440x2160.png": images.poster_art,
        "titled_hero_art_1920x1080.png": images.titled_hero_art,
        "super_hero_art_3840x2160.png": images.super_hero_art,
        "featured_promotional_square_1080.png": images.featured_promotional_square,
    })
    write_shots(os.path.join(xbox, "screenshots_1920x1080"), args.captures,
                specs.SHOT_LANDSCAPE, captioned=False)

    # ---- Apple App Store
    write_key_art(apple, specs.APPLE_ICON, {"icon_1024.png": small_icon})
    write_shots(os.path.join(apple, "screenshots_iphone_medium_2622x1206"), args.captures,
                specs.APPLE_IPHONE_MEDIUM, captioned=True)
    write_shots(os.path.join(apple, "screenshots_iphone_6.9_2868x1320"), args.captures,
                specs.APPLE_IPHONE, captioned=True)
    write_shots(os.path.join(apple, "screenshots_ipad_13_2752x2064"), args.captures,
                specs.APPLE_IPAD, captioned=True)

    # ---- Google Play
    write_key_art(google, specs.GOOGLE_ICON, {
        "icon_512.png": small_icon,
        "feature_graphic_1024x500.png": images.feature_graphic,
    })
    write_shots(os.path.join(google, "screenshots_phone_1920x1080"), args.captures,
                specs.SHOT_LANDSCAPE, captioned=False)

    write_manifest(args.out)
    print("images:")
    code = verify()

    print("copy:")
    copy_problems = check_copy(args.out)
    for problem in copy_problems:
        print("FAIL " + problem)

    return 1 if (code or copy_problems) else 0


def write_manifest(out: str) -> None:
    """A list of what was produced, which store asked for it and how big it is."""
    rows: list[str] = []
    for path, name, size in _written:
        store = os.path.relpath(path, out).split(os.sep)[0]
        relative = os.path.relpath(path, os.path.join(out, store))
        rows.append(f"| {store} | `{relative}` | {size[0]}x{size[1]} | {_kb(path)} KB |")

    lines = [
        "# Store asset manifest",
        "",
        "Generated by `python3 tools/storegen/main.py`. Do not edit by hand.",
        "Exact requirements and their sources are in `tools/storegen/specs.py`.",
        "",
        "| Store | File | Size | Weight |",
        "| --- | --- | --- | --- |",
        *rows,
        "",
    ]

    with open(os.path.join(out, "MANIFEST.md"), "w") as handle:
        handle.write("\n".join(lines))


def _kb(path: str) -> int:
    return max(1, round(os.path.getsize(path) / 1024))


if __name__ == "__main__":
    raise SystemExit(main())

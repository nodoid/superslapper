#!/usr/bin/env python3
"""Generate every asset SuperSlapper ships.

Run from the repository root:

    python3 tools/artgen/main.py

Assets are written to ./Content so each platform head can pick them up:
  Content/art/*.png      sprites, backgrounds, ui
  Content/font/*.png|json bitmap fonts
  Content/audio/*.wav     slaps, crowd, bell, applause, music
Platform icons go to the individual head projects.
"""
from __future__ import annotations

import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import arena  # noqa: E402
import fighters  # noqa: E402
import photoreal  # noqa: E402
import softfighters  # noqa: E402
import fonts  # noqa: E402
import icons  # noqa: E402
import music  # noqa: E402
import sfx  # noqa: E402
import ui  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
CONTENT = os.path.join(ROOT, "Content")
ART = os.path.join(CONTENT, "art")
FONT = os.path.join(CONTENT, "font")
AUDIO = os.path.join(CONTENT, "audio")


def main() -> int:
    started = time.time()
    for folder in (ART, FONT, AUDIO):
        os.makedirs(folder, exist_ok=True)

    print("• arena, ropes, logo, posters")
    arena.build_all(ART)

    print("• fighters, referee, impacts, stars")
    softfighters.build_all(ART)
    fighters.build_referee().save(os.path.join(ART, "referee.png"), optimize=True)
    fighters.build_impact().save(os.path.join(ART, "impact.png"), optimize=True)
    fighters.build_stars().save(os.path.join(ART, "stars.png"), optimize=True)
    fighters.build_hand_closeup().save(os.path.join(ART, "hand_closeup.png"), optimize=True)

    print("• title illustration and slap replays")
    photoreal.build_hero().save(os.path.join(ART, "hero_slap.png"), optimize=True)
    photoreal.build_replay(False).save(os.path.join(ART, "replay_slap.png"), optimize=True)
    photoreal.build_replay(True).save(os.path.join(ART, "replay_ko.png"), optimize=True)

    print("• interface")
    ui.build_all(ART)

    print("• bitmap fonts")
    fonts.build_all(FONT)

    print("• sound effects")
    sfx.build_all(AUDIO)

    print("• background music")
    music.build_all(AUDIO)

    print("• application icons")
    icons.build_all(
        content_dir=CONTENT,
        android_res=os.path.join(ROOT, "src", "SuperSlapper.Android", "Resources"),
        ios_assets=os.path.join(ROOT, "src", "SuperSlapper.iOS", "Assets.xcassets"),
        mac_assets=os.path.join(ROOT, "src", "SuperSlapper.Desktop", "Assets"),
        windows_dir=os.path.join(ROOT, "src", "SuperSlapper.Desktop", "Windows"),
    )

    total = sum(
        len(files)
        for _, _, files in os.walk(CONTENT)
    )
    print(f"done: {total} content files in {time.time() - started:.1f}s")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Bitmap font atlas generation.

The game ships its own tiny font format so no content pipeline or system font
lookup is needed on any platform:

    font_<name>.png   white glyphs on transparent, tinted at draw time
    font_<name>.json  metrics per glyph
"""
from __future__ import annotations

import json
import os

from PIL import Image, ImageDraw, ImageFont

ROUNDED = "/System/Library/Fonts/Supplemental/Arial Rounded Bold.ttf"
IMPACT = "/System/Library/Fonts/Supplemental/Impact.ttf"
# Arial Rounded Bold has no triangles and no middle dot, so those characters are
# pulled from a face that does instead of baking in the .notdef box.
FALLBACK = "/System/Library/Fonts/Supplemental/Arial Unicode.ttf"

CHARS = (
    "".join(chr(c) for c in range(32, 127))
    + "▲▼–—·×°’‘“”£€½¼¾…©•✔✖"
)

FONTS = [
    ("small", ROUNDED, 22),
    ("body", ROUNDED, 30),
    ("medium", ROUNDED, 40),
    ("large", IMPACT, 64),
    ("huge", IMPACT, 96),
    ("tiny", ROUNDED, 17),
]

PAD = 3
COLUMNS = 16
NOTDEF = "\ue000"


def _renders_as_notdef(font: ImageFont.FreeTypeFont, ch: str) -> bool:
    """True when the face has no glyph for ch and would draw its .notdef box."""
    if ch == " ":
        return False

    box = max(16, font.size * 2)

    def render(c: str) -> bytes:
        img = Image.new("L", (box, box), 0)
        ImageDraw.Draw(img).text((font.size, font.size // 2), c, font=font, fill=255)
        return img.tobytes()

    return render(ch) == render(NOTDEF)


def build(name: str, ttf: str, size: int, out_dir: str) -> dict:
    font = ImageFont.truetype(ttf, size)
    fallback = ImageFont.truetype(FALLBACK, size) if os.path.exists(FALLBACK) else font
    ascent, descent = font.getmetrics()
    line_height = ascent + descent

    glyphs = {}
    cells = []
    cell_w, cell_h = 0, line_height + PAD * 2
    for ch in CHARS:
        face = fallback if _renders_as_notdef(font, ch) else font
        bbox = face.getbbox(ch)
        if bbox is None:
            continue
        x0, y0, x1, y1 = bbox
        w, h = max(0, x1 - x0), max(1, y1 - y0)
        adv = face.getlength(ch)
        glyphs[ch] = {
            "ox": x0,
            "oy": y0,
            "w": w,
            "h": h,
            "adv": round(float(adv), 2),
        }
        cells.append((ch, x0, y0, w, h, face))
        cell_w = max(cell_w, w + PAD * 2)

    rows = (len(cells) + COLUMNS - 1) // COLUMNS
    atlas = Image.new("RGBA", (cell_w * COLUMNS, cell_h * rows), (0, 0, 0, 0))
    draw = ImageDraw.Draw(atlas)

    index = 0
    for ch, x0, y0, w, h, face in cells:
        cx = (index % COLUMNS) * cell_w
        cy = (index // COLUMNS) * cell_h
        draw.text((cx + PAD - x0, cy + PAD), ch, font=face, fill=(255, 255, 255, 255))
        glyphs[ch]["x"] = cx + PAD
        # draw.text anchors the ascender line at cy + PAD, so the ink starts y0
        # lower. The atlas rect has to start there too or every glyph is cropped.
        glyphs[ch]["y"] = cy + PAD + y0
        # Re-origin the glyph so oy is relative to the line top.
        glyphs[ch]["oy"] = y0
        index += 1

    os.makedirs(out_dir, exist_ok=True)
    atlas.save(os.path.join(out_dir, f"font_{name}.png"), optimize=True)
    meta = {
        "name": name,
        "size": size,
        "lineHeight": line_height,
        "ascent": ascent,
        "descent": descent,
        "spaceAdvance": round(float(font.getlength(" ")), 2),
        "glyphs": glyphs,
    }
    with open(os.path.join(out_dir, f"font_{name}.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=1, ensure_ascii=False)
    return meta


def build_all(out_dir: str) -> list[str]:
    written = []
    for name, ttf, size in FONTS:
        build(name, ttf, size, out_dir)
        written.append(name)
    return written

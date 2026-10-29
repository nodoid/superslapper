"""The look of every store asset, and the helpers that draw it.

Everything here is drawn from the game's own assets and palette, so a store page
looks like the game rather than like a template: the colours are the ones in
``SuperSlapper.Game/Core/Gfx.cs``, the logo and art come out of ``Content/art``,
and the type is set in the same two faces ``tools/artgen/fonts.py`` bakes into
the game's bitmap fonts (Arial Rounded Bold for copy, Impact for headlines).

Nothing in this module writes a file; it composes images. ``main.py`` decides
where they go.
"""
from __future__ import annotations

import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
ART = os.path.join(ROOT, "Content", "art")

# ---------------------------------------------------------------- palette
INK = (11, 16, 28)
INK_SOFT = (22, 30, 48)
GOLD = (245, 197, 66)
GOLD_DARK = (168, 124, 18)
GOLD_LIGHT = (255, 232, 154)
WHITE = (247, 249, 255)
MUTED = (150, 163, 190)
BLUE = (47, 111, 224)
BLUE_LIGHT = (142, 194, 255)
RED = (224, 57, 46)
RED_LIGHT = (255, 171, 156)
GREEN = (67, 209, 122)
AMBER = (242, 211, 61)

# The same faces the game's bitmap fonts are baked from, so store type matches
# the type on screen.
_ROUNDED = "/System/Library/Fonts/Supplemental/Arial Rounded Bold.ttf"
_IMPACT = "/System/Library/Fonts/Supplemental/Impact.ttf"

_font_cache: dict[tuple[str, int], ImageFont.FreeTypeFont] = {}


def font(size: int, face: str = "copy") -> ImageFont.FreeTypeFont:
    """A rounded copy face or an impact headline face, cached by size."""
    path = _IMPACT if face == "headline" else _ROUNDED
    key = (path, size)
    if key not in _font_cache:
        _font_cache[key] = ImageFont.truetype(path, size)
    return _font_cache[key]


# ---------------------------------------------------------------- assets
def art(name: str) -> Image.Image:
    """One of the game's generated PNGs, loaded as RGBA."""
    return Image.open(os.path.join(ART, name)).convert("RGBA")


def logo(width: int) -> Image.Image:
    """The game's wordmark, scaled to a width, with its proportions kept."""
    source = art("logo.png")
    height = max(1, round(source.height * width / source.width))
    return source.resize((width, height), Image.LANCZOS)


def fighter(kit: str, frame: int, height: int, flipped: bool = False) -> Image.Image:
    """One frame of a fighter sheet, cropped out and scaled to a height.

    Sheets are laid out 480x540 a frame, eight across (see FighterSheet.cs). The
    frame numbers are the named poses: 11 is the victory pose, 4 a strike, 13 the
    taunt.
    """
    sheet = art(f"{kit}.png")
    column, row = frame % 8, frame // 8
    box = (column * 480, row * 540, column * 480 + 480, row * 540 + 540)
    frame_image = sheet.crop(box)

    if flipped:
        frame_image = frame_image.transpose(Image.FLIP_LEFT_RIGHT)

    scale = height / frame_image.height
    return frame_image.resize((max(1, round(frame_image.width * scale)), height), Image.LANCZOS)


# ---------------------------------------------------------------- drawing
def vertical_gradient(size: tuple[int, int], top: tuple[int, int, int],
                      bottom: tuple[int, int, int]) -> Image.Image:
    """A plain vertical gradient, which is the backdrop for everything else."""
    width, height = size
    column = Image.new("RGB", (1, height))
    pixels = column.load()
    for y in range(height):
        t = y / max(1, height - 1)
        pixels[0, y] = (
            round(top[0] + (bottom[0] - top[0]) * t),
            round(top[1] + (bottom[1] - top[1]) * t),
            round(top[2] + (bottom[2] - top[2]) * t),
        )
    return column.resize(size, Image.BILINEAR).convert("RGBA")


def radial_glow(size: tuple[int, int], colour: tuple[int, int, int], centre: tuple[float, float],
                radius: float, strength: float = 0.55) -> Image.Image:
    """A soft light behind a subject, drawn as one layer so it can be blended."""
    width, height = size
    glow = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(glow)
    steps = 26
    for i in range(steps, 0, -1):
        r = radius * i / steps
        alpha = round(255 * strength * (1 - i / steps) ** 1.7)
        draw.ellipse(
            [centre[0] - r, centre[1] - r * 0.78, centre[0] + r, centre[1] + r * 0.78],
            fill=(colour[0], colour[1], colour[2], alpha),
        )
    return glow.filter(ImageFilter.GaussianBlur(radius * 0.05))


def cover(image: Image.Image, size: tuple[int, int], focus_y: float = 0.5) -> Image.Image:
    """Scales an image to fill a box and centre-crops the overflow."""
    width, height = size
    scale = max(width / image.width, height / image.height)
    scaled = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))),
                          Image.LANCZOS)
    left = (scaled.width - width) // 2
    top = round((scaled.height - height) * focus_y)
    return scaled.crop((left, max(0, top), left + width, max(0, top) + height))


def contain(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    """Scales an image to fit inside a box, keeping its proportions."""
    scale = min(size[0] / image.width, size[1] / image.height)
    return image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))),
                        Image.LANCZOS)


def darken(image: Image.Image, amount: float) -> Image.Image:
    """Multiplies an image down, for art that has to sit behind type."""
    layer = Image.new("RGBA", image.size, (0, 0, 0, round(255 * amount)))
    return Image.alpha_composite(image, layer)


def vignette(image: Image.Image, strength: float = 0.6) -> Image.Image:
    """Darkens the corners so the middle of a composition holds the eye."""
    width, height = image.size
    mask = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(mask)
    inset_x, inset_y = width * 0.12, height * 0.12
    draw.ellipse([inset_x, inset_y, width - inset_x, height - inset_y], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(min(width, height) * 0.16))
    shade = Image.new("RGBA", (width, height), (0, 0, 0, round(255 * strength)))
    shade.putalpha(Image.eval(mask, lambda v: round(255 * strength * (1 - v / 255))))
    return Image.alpha_composite(image, shade)


def headline(image: Image.Image, text: str, size: int, position: tuple[float, float],
             fill: tuple[int, int, int] = WHITE, anchor: str = "mm", shadow: bool = True) -> None:
    """Draws a headline in the Impact face, optionally with a soft drop shadow."""
    draw = ImageDraw.Draw(image)
    face = font(size, "headline")
    if shadow:
        draw.text((position[0] + size * 0.03, position[1] + size * 0.05), text, font=face,
                  fill=(0, 0, 0, 190), anchor=anchor)
    draw.text(position, text, font=face, fill=fill + (255,) if len(fill) == 3 else fill, anchor=anchor)


def copy_line(image: Image.Image, text: str, size: int, position: tuple[float, float],
              fill: tuple[int, int, int] = MUTED, anchor: str = "mm", shadow: bool = False) -> None:
    """Draws a line of body copy in the rounded face."""
    draw = ImageDraw.Draw(image)
    face = font(size, "copy")
    if shadow:
        draw.text((position[0] + size * 0.05, position[1] + size * 0.06), text, font=face,
                  fill=(0, 0, 0, 170), anchor=anchor)
    draw.text(position, text, font=face, fill=fill + (255,) if len(fill) == 3 else fill, anchor=anchor)


def rule(image: Image.Image, x0: float, y: float, x1: float, colour: tuple[int, int, int] = GOLD,
         weight: int = 6) -> None:
    """A gold hairline, the game's own divider."""
    ImageDraw.Draw(image).rectangle([x0, y, x1, y + weight], fill=colour + (255,))


def paste(base: Image.Image, layer: Image.Image, xy: tuple[int, int]) -> None:
    """Alpha-composites a layer at a position given by its top-left corner."""
    base.alpha_composite(layer, dest=(max(0, xy[0]), max(0, xy[1])))


def centered(layer: Image.Image, base_size: tuple[int, int], y: int) -> tuple[int, int]:
    """Top-left corner that centres a layer horizontally at a given y."""
    return ((base_size[0] - layer.width) // 2, y)

"""Draws each store asset from the game's own art and screenshots.

Two kinds of picture live here:

* key art, composed from ``Content/art`` - the wordmark, the arena, the fighter
  sheets and the impact burst - for the listings that want a single image;
* store screenshots, which are real captures of the running game (the self-test
  harness writes them) put on a branded canvas with a caption.

Nothing here invents artwork: every element is an asset the game itself draws.
Two details of those assets matter to the compositions:

* the generated arena art carries a venue banner along its bottom edge
  ("SUPER SLAPPER - ALL THE JOHNSONS"), and the artwork-only store slots forbid
  text, so the arena is always cropped above that band;
* ``impact.png`` is a five-frame burst, 300x300 a frame, so one frame is cropped
  out where a slap lands rather than the whole strip being laid down.
"""
from __future__ import annotations

import os

from PIL import Image, ImageDraw, ImageFilter

import brand
from brand import GOLD, GOLD_DARK, GOLD_LIGHT, INK, INK_SOFT, MUTED, WHITE
from specs import Shot

# Fighter sheet frame numbers, from FighterSheet.cs.
IDLE, WINDUP, STRIKE, HIT, STAGGER, KO, VICTORY, BRACE, TAUNT = 0, 2, 4, 8, 9, 10, 11, 12, 13

# The arena art's bottom band, which holds the venue banner text.
ARENA_TEXT_TOP = 0.94

_impact_frames = 5


def impact(index: int = 4) -> Image.Image:
    """One frame of the five-frame impact burst: the last is the fullest."""
    sheet = brand.art("impact.png")
    width = sheet.width // _impact_frames
    index = max(0, min(_impact_frames - 1, index))
    return sheet.crop((index * width, 0, (index + 1) * width, sheet.height))


def _burst(height: int, tint: tuple[int, int, int] = GOLD_LIGHT,
           boost: float = 2.8) -> Image.Image:
    """The burst as light rather than as a shape.

    The art the game draws is a faint white flash, which over a dark store
    background reads as a grey polygon. Its alpha is lifted and the burst is
    tinted, so it lands as a slap instead of as a sticker.
    """
    frame = impact(4)
    alpha = frame.split()[3].point(lambda value: min(255, round(value * boost)))
    coloured = Image.new("RGBA", frame.size, tint + (255,))
    coloured.putalpha(alpha)
    return coloured.resize((height, height), Image.LANCZOS)


def _slap(base: Image.Image, size: tuple[int, int], centre: tuple[float, float], height: int,
          tint: tuple[int, int, int] = GOLD_LIGHT) -> None:
    """Lands a slap: a warm flare with the burst standing on it."""
    base.alpha_composite(brand.radial_glow(size, tint, centre, height * 0.9, 0.5))
    burst = _burst(height, tint)
    brand.paste(base, burst,
                (round(centre[0] - burst.width / 2), round(centre[1] - burst.height / 2)))


def _arena_band(size: tuple[int, int], bottom: float = 0.70, amount: float = 0.45,
                arena: str = "arena_world.png") -> Image.Image:
    """The venue, cropped above its banner and pushed back so type reads over it.

    ``bottom`` picks how much of the arena to keep: 0.70 is crowd and lights
    alone, and anything up to ARENA_TEXT_TOP keeps the court floor as well.
    """
    source = brand.art(arena)
    band = source.crop((0, 0, source.width, round(source.height * min(bottom, ARENA_TEXT_TOP))))

    base = brand.vertical_gradient(size, INK, INK_SOFT)
    base.alpha_composite(brand.darken(brand.cover(band, size, focus_y=0.55), amount))
    return base


def _ground(base: Image.Image, size: tuple[int, int], fighter: Image.Image,
            centre_x: float, floor: float = 0.0) -> None:
    """Stands a fighter on the bottom of a composition, feet just inside the frame."""
    x = round(size[0] * centre_x - fighter.width / 2)
    y = size[1] - fighter.height - round(size[1] * floor)
    brand.paste(base, fighter, (x, y))


def _title(size: tuple[int, int], width_ratio: float) -> Image.Image:
    return brand.logo(round(size[0] * width_ratio))


# ------------------------------------------------------------------ key art
def box_art(size: tuple[int, int] = (2160, 2160)) -> Image.Image:
    """1:1 box art. The title sits in the upper two-thirds - the store lays text
    and gradient over the bottom third - and no other words appear."""
    width, height = size
    poster = _arena_band(size, bottom=0.86, amount=0.44)

    poster.alpha_composite(brand.radial_glow(size, GOLD, (width * 0.5, height * 0.70),
                                             width * 0.44, 0.42))

    champion = brand.fighter("fighter_champion", VICTORY, round(height * 0.62))
    _ground(poster, size, champion, 0.5, floor=0.015)

    logo = _title(size, 0.68)
    brand.paste(poster, logo, brand.centered(logo, size, round(height * 0.105)))
    brand.rule(poster, width * 0.28, round(height * 0.295), width * 0.72, GOLD,
               max(4, round(height * 0.0035)))

    # The wordmark carries this one: no burst competing with the title.
    return brand.vignette(poster, 0.55)


def poster_art(size: tuple[int, int] = (1440, 2160)) -> Image.Image:
    """2:3 poster art: the title up top, the slap filling the frame below."""
    width, height = size
    poster = _arena_band(size, bottom=0.88, amount=0.40)

    poster.alpha_composite(brand.radial_glow(size, GOLD, (width * 0.5, height * 0.62),
                                             width * 0.52, 0.40))

    striker = brand.fighter("fighter_player", STRIKE, round(height * 0.55))
    _ground(poster, size, striker, 0.36, floor=0.02)

    rival = brand.fighter("fighter_rival", HIT, round(height * 0.50), flipped=True)
    _ground(poster, size, rival, 0.76, floor=0.06)

    _slap(poster, size, (width * 0.52, height * 0.52), round(height * 0.20))

    logo = _title(size, 0.70)
    brand.paste(poster, logo, brand.centered(logo, size, round(height * 0.085)))
    brand.rule(poster, width * 0.26, round(height * 0.255), width * 0.74, GOLD,
               max(4, round(height * 0.0028)))

    return brand.vignette(poster, 0.55)


def titled_hero_art(size: tuple[int, int] = (1920, 1080)) -> Image.Image:
    """16:9 build of the slap with the title in the upper two-thirds."""
    width, height = size
    hero = _arena_band(size, bottom=0.88, amount=0.36)

    hero.alpha_composite(brand.radial_glow(size, GOLD, (width * 0.5, height * 0.52),
                                           width * 0.34, 0.34))

    fighter_height = round(height * 0.62)
    striker = brand.fighter("fighter_player", STRIKE, fighter_height)
    rival = brand.fighter("fighter_rival", STAGGER, fighter_height, flipped=True)

    _ground(hero, size, striker, 0.30, floor=0.02)
    _ground(hero, size, rival, 0.75, floor=0.02)

    _slap(hero, size, (width * 0.5, height * 0.52), round(height * 0.26))

    logo = _title(size, 0.42)
    brand.paste(hero, logo, brand.centered(logo, size, round(height * 0.045)))

    return brand.vignette(hero, 0.5)


def super_hero_art(size: tuple[int, int] = (3840, 2160)) -> Image.Image:
    """16:9 artwork only. The store's rule for this slot is no title and no text,
    so neither the wordmark nor the tagline appears."""
    width, height = size
    hero = _arena_band(size, bottom=0.88, amount=0.32)

    hero.alpha_composite(brand.radial_glow(size, GOLD, (width * 0.5, height * 0.54),
                                           width * 0.32, 0.34))

    fighter_height = round(height * 0.70)
    striker = brand.fighter("fighter_player", STRIKE, fighter_height)
    rival = brand.fighter("fighter_rival", STAGGER, fighter_height, flipped=True)

    _ground(hero, size, striker, 0.31, floor=0.01)
    _ground(hero, size, rival, 0.74, floor=0.01)

    _slap(hero, size, (width * 0.5, height * 0.50), round(height * 0.30))

    return brand.vignette(hero, 0.5)


def featured_promotional_square(size: tuple[int, int] = (1080, 1080)) -> Image.Image:
    """1:1 artwork only, for the featured slot: no title, no text."""
    width, height = size
    scene = _arena_band(size, bottom=0.80, amount=0.28)

    scene.alpha_composite(brand.radial_glow(size, GOLD_LIGHT, (width * 0.52, height * 0.44),
                                            width * 0.40, 0.42))

    striker = brand.fighter("fighter_player", STRIKE, round(height * 0.86))
    _ground(scene, size, striker, 0.32, floor=-0.01)

    rival = brand.fighter("fighter_rival", HIT, round(height * 0.78), flipped=True)
    _ground(scene, size, rival, 0.86, floor=0.0)

    # Just past the striker's hand, not over his face.
    _slap(scene, size, (width * 0.60, height * 0.35), round(height * 0.24))

    return brand.vignette(scene, 0.5)


def feature_graphic(size: tuple[int, int] = (1024, 500)) -> Image.Image:
    """Google Play's feature graphic: no alpha, and the centre kept clear because
    the store lays its play badge over the middle of it.

    Type reads down the left, the fighter stands on the right, and nothing
    important sits between them.
    """
    width, height = size
    base = _arena_band(size, bottom=0.68, amount=0.30)

    base.alpha_composite(brand.radial_glow(size, GOLD, (width * 0.20, height * 0.44),
                                           width * 0.34, 0.52))

    logo = brand.logo(round(width * 0.36))
    brand.paste(base, logo, (round(width * 0.06), round(height * 0.16)))

    brand.rule(base, round(width * 0.065), round(height * 0.575), round(width * 0.30), GOLD,
               max(3, round(height * 0.008)))
    brand.copy_line(base, "SLAP-FIGHTING CAREER", round(height * 0.075),
                    (round(width * 0.065), round(height * 0.70)), GOLD, anchor="lm")

    card = brand.fighter("fighter_champion", VICTORY, round(height * 0.80))
    brand.paste(base, card, (round(width * 0.56), height - card.height - round(height * 0.06)))

    return brand.vignette(base, 0.40)


def icon_from_app(size: int) -> Image.Image:
    """The app icon at a store's size, from the artwork the app itself ships."""
    source = brand.art("appicon_1024.png")
    if size == source.width:
        return source
    return source.resize((size, size), Image.LANCZOS)


# ------------------------------------------------------------- screenshots
def raw_screenshot(capture_dir: str, shot: Shot) -> Image.Image:
    """A store screenshot that is the capture, untouched."""
    return Image.open(os.path.join(capture_dir, shot.source)).convert("RGBA")


def captioned_screenshot(capture_dir: str, shot: Shot, size: tuple[int, int]) -> Image.Image:
    """A capture on a branded canvas with the wordmark and a caption.

    The capture sits whole - nothing is cropped, because a cropped HUD is a lie
    about what the screen looks like - with a panel down each side rather than
    type laid over the picture.
    """
    width, height = size
    canvas = brand.vertical_gradient(size, INK, (16, 22, 38))

    canvas.alpha_composite(brand.radial_glow(size, GOLD, (width * 0.5, height * 0.46),
                                             width * 0.46, 0.30))

    # The side panel is driven by the height as well as the width, because the iPad
    # sheet is nearly square and a width-only panel leaves the caption nowhere to go.
    panel = round(min(width * 0.22, height * 0.30))
    inner_width = width - panel * 2
    capture = brand.contain(raw_screenshot(capture_dir, shot), (inner_width, round(height * 0.82)))

    # A soft edge under the capture so it sits on the canvas rather than floating.
    shadow = Image.new("RGBA", (capture.width + 40, capture.height + 40), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rectangle([40, 40, capture.width + 40, capture.height + 40],
                                     fill=(0, 0, 0, 150))
    shadow = shadow.filter(ImageFilter.GaussianBlur(18))
    brand.paste(canvas, shadow, ((width - capture.width) // 2 - 20, (height - capture.height) // 2 - 12))

    capture_x, capture_y = brand.centered(capture, size, (height - capture.height) // 2)
    brand.paste(canvas, capture, (capture_x, capture_y))
    ImageDraw.Draw(canvas).rectangle(
        [capture_x, capture_y, capture_x + capture.width - 1, capture_y + capture.height - 1],
        outline=GOLD_DARK + (180,), width=3)

    # Caption panel on the left: kicker, headline, subline, rule, wordmark, fitted
    # to the panel rather than assumed to fit it.
    left = round(panel * 0.12)
    available = panel - left * 2
    top = round(height * 0.13)
    budget = round(height * 0.62)
    kicker_size = max(16, round(height * 0.023))
    head_size, sub_size, lines, sub_lines = _fit_caption(shot, available, budget, height)

    brand.copy_line(canvas, "THE SLAP FIGHTING", kicker_size, (left, top), GOLD, anchor="lm")
    brand.copy_line(canvas, "CHAMPIONSHIP", kicker_size, (left, top + kicker_size + 8), GOLD,
                    anchor="lm")

    y = top + kicker_size * 2 + round(height * 0.055)
    for line in lines:
        brand.headline(canvas, line, head_size, (left, y), WHITE, anchor="lm")
        y += round(head_size * 1.03)

    brand.rule(canvas, left, y + round(height * 0.015), left + available * 0.5, GOLD,
               max(4, round(height * 0.0035)))

    y += round(height * 0.055)
    for line in sub_lines:
        brand.copy_line(canvas, line, sub_size, (left, y), MUTED, anchor="lm")
        y += round(sub_size * 1.3)

    logo = brand.logo(available)
    brand.paste(canvas, logo, (left, height - logo.height - round(height * 0.09)))

    return canvas


def _fit_caption(shot: Shot, available: int, budget: int, height: int) -> tuple:
    """Sizes a caption so its block fits the panel: the headline and the subline
    shrink together until the whole thing fits, which is what keeps the text off
    the wordmark on a nearly square iPad canvas."""
    head_size = max(30, round(height * 0.058))
    sub_size = max(18, round(height * 0.026))
    head_min = max(22, round(height * 0.028))
    sub_min = max(15, round(height * 0.018))

    while True:
        lines = _wrap(shot.headline, available, head_size, "headline")
        sub_lines = _wrap(shot.subline, available, sub_size, "copy")
        needed = (len(lines) * round(head_size * 1.03) + round(height * 0.055) +
                  len(sub_lines) * round(sub_size * 1.3))

        if needed <= budget or (head_size <= head_min and sub_size <= sub_min):
            return head_size, sub_size, lines, sub_lines

        head_size = max(head_min, head_size - 2)
        sub_size = max(sub_min, sub_size - 1)


def _wrap(text: str, width: int, size: int, face_kind: str = "headline") -> list[str]:
    """Breaks a caption to fit a column, the way the game wraps its own text."""
    face = brand.font(size, face_kind)
    words = text.split()
    lines: list[str] = []
    current = ""

    for word in words:
        candidate = f"{current} {word}".strip()
        if face.getlength(candidate) <= width or not current:
            current = candidate
        else:
            lines.append(current)
            current = word

    if current:
        lines.append(current)

    return lines

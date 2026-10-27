"""Arena backgrounds, ropes, logo and menu art."""
from __future__ import annotations

import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

from common import (
    Palette, hex_rgb, mix, radial_glow, save, vertical_gradient, vignette
)

W, H = 1920, 1080
IMPACT = "/System/Library/Fonts/Supplemental/Impact.ttf"
ROUNDED = "/System/Library/Fonts/Supplemental/Arial Rounded Bold.ttf"
CONDENSED = "/System/Library/Fonts/Supplemental/DIN Condensed Bold.ttf"


def _perspective_quad(top_y, bottom_y, top_half, bottom_half, cx=W / 2):
    return [(cx - top_half, top_y), (cx + top_half, top_y),
            (cx + bottom_half, bottom_y), (cx - bottom_half, bottom_y)]


def _crowd(draw, top, bottom, seed, density=1.0):
    rng = random.Random(seed)
    rows = 9
    for r in range(rows):
        y = top + (bottom - top) * (r / rows) ** 1.25
        size = 10 + r * 2
        alpha = 40 + r * 12
        for _ in range(int(34 * density)):
            x = rng.uniform(-40, W + 40)
            tone = rng.choice(["#101827", "#16203a", "#1c2748", "#232f52", "#2a3760"])
            draw.ellipse([x - size, y - size * 0.7, x + size, y + size * 0.7],
                         fill=hex_rgb(tone) + (min(255, alpha + 60),))
            draw.ellipse([x - size * 0.5, y - size * 1.25, x + size * 0.5, y - size * 0.4],
                         fill=hex_rgb("#0c1220") + (min(255, alpha + 80),))


def _bokeh(img, seed, count=90):
    rng = random.Random(seed)
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(count):
        x = rng.uniform(0, W)
        y = rng.uniform(60, 560)
        r = rng.uniform(3, 13)
        colour = rng.choice([(255, 236, 180), (180, 220, 255), (255, 190, 140)])
        d.ellipse([x - r, y - r, x + r, y + r], fill=colour + (rng.randint(40, 110),))
    layer = layer.filter(ImageFilter.GaussianBlur(4))
    img.alpha_composite(layer)


def _spotlights(img, seed):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for i, x in enumerate([280, 760, 1160, 1640]):
        spread = 170 if i % 2 == 0 else 220
        d.polygon([(x - 40, 0), (x + 40, 0), (x + spread, 780), (x - spread, 780)],
                  fill=(255, 246, 214, 16))
    layer = layer.filter(ImageFilter.GaussianBlur(26))
    img.alpha_composite(layer)


def build_arena(name: str, canvas_colour: str, apron_colour: str, seed: int) -> Image.Image:
    img = vertical_gradient((W, H), "#070b16", "#182votes"[:7] if False else "#18223c").convert("RGBA")
    glow = radial_glow((W, H), "#33456f", "#070b16", centre=(W / 2, 520), radius=1100, power=2.2)
    img = Image.blend(img.convert("RGB"), glow, 0.55).convert("RGBA")

    crowd = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    _crowd(ImageDraw.Draw(crowd), 60, 520, seed)
    img.alpha_composite(crowd.filter(ImageFilter.GaussianBlur(3)))
    _bokeh(img, seed + 1)
    _spotlights(img, seed)

    d = ImageDraw.Draw(img)
    # Raised slap stage: apron, floor and the two marked standing boxes. There
    # are no ropes or corner posts; fighters face each other across the table.
    apron = _perspective_quad(660, H, 760, 1120)
    d.polygon(apron, fill=hex_rgb(apron_colour) + (255,))
    d.polygon([(W / 2 - 740, 660), (W / 2 + 740, 660),
               (W / 2 + 760, 700), (W / 2 - 760, 700)],
              fill=mix(apron_colour, "#ffffff", 0.18) + (255,))

    stage = _perspective_quad(700, 1010, 700, 980)
    d.polygon(stage, fill=hex_rgb(canvas_colour) + (255,))
    rim = mix(canvas_colour, "#ffffff", 0.22)
    d.line(stage + [stage[0]], fill=rim + (255,), width=6, joint="curve")

    # The striker and defender boxes painted on the stage.
    for cx, colour in ((W / 2 - 460, Palette.BLUE), (W / 2 + 460, Palette.RED)):
        box = _perspective_quad(880, 1005, 150, 190, cx=cx)
        box_layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        box_draw = ImageDraw.Draw(box_layer)
        box_draw.polygon(box, fill=hex_rgb(colour) + (70,))
        box_draw.line(box + [box[0]], fill=(240, 244, 255, 200), width=4, joint="curve")
        img.alpha_composite(box_layer)

    # Front apron signage band.
    font = ImageFont.truetype(CONDENSED, 42)
    d.text((W / 2, 1046), "SUPER SLAPPER  ·  ALL THE JOHNSONS",
           font=font, fill=(255, 255, 255, 120), anchor="mm")

    img = img.convert("RGB")
    img = vignette(img, 0.45)
    return img


def build_stage_front() -> Image.Image:
    """Foreground slap table, the padded stand the two fighters face across."""
    img = Image.new("RGBA", (W, 340), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    cx = W / 2
    top, shoulder = 24, 82
    half_top, half_bottom = 225, 270

    # Table top, in perspective.
    tabletop = [(cx - half_top, top), (cx + half_top, top),
                (cx + half_bottom - 30, shoulder), (cx - half_bottom + 30, shoulder)]
    d.polygon(tabletop, fill=(38, 48, 74, 255), outline=(20, 24, 33, 255), width=5)
    inner = [(cx - half_top + 26, top + 18), (cx + half_top - 26, top + 18),
             (cx + half_bottom - 58, shoulder - 16), (cx - half_bottom + 58, shoulder - 16)]
    d.polygon(inner, fill=(58, 72, 106, 255))

    # Padded front panel.
    front = [cx - half_bottom, shoulder - 4, cx + half_bottom, shoulder - 4,
             cx + half_bottom, 250, cx - half_bottom, 250]
    d.polygon(front, fill=(24, 32, 52, 255), outline=(20, 24, 33, 255), width=5)
    d.line([(cx - half_bottom + 8, 158), (cx + half_bottom - 8, 158)],
           fill=(58, 72, 106, 255), width=4)
    d.line([(cx - half_bottom + 8, 196), (cx + half_bottom - 8, 196)],
           fill=(58, 72, 106, 255), width=4)

    gold = hex_rgb(Palette.GOLD)
    d.rectangle([cx - half_bottom + 10, shoulder, cx + half_bottom - 10, shoulder + 6],
                fill=gold + (255,))
    d.text((cx, 218), "SUPER SLAPPER", font=ImageFont.truetype(CONDENSED, 40),
           fill=gold + (220,), anchor="mm")

    # Chalk mark on the table top.
    d.line([(cx - 70, top + 34), (cx + 70, top + 34)], fill=(240, 244, 255, 120), width=4)

    # Stage lip below the fighters' feet.
    d.rectangle([0, 300, W, 340], fill=(18, 24, 40, 255))
    d.line([(0, 300), (W, 300)], fill=gold + (255,), width=5)
    return img


def build_ring_logo(size: int) -> Image.Image:
    """Bold circular promotion mark used on the canvas, posters and icons."""
    s = 512
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([8, 8, s - 8, s - 8], fill=hex_rgb(Palette.GOLD_DARK) + (255,))
    d.ellipse([20, 20, s - 20, s - 20], fill=hex_rgb(Palette.GOLD) + (255,))
    d.ellipse([38, 38, s - 38, s - 38], fill=(18, 22, 33, 255))
    font = ImageFont.truetype(IMPACT, 132)
    d.text((s / 2, 200), "SUPER", font=font, fill=hex_rgb(Palette.GOLD) + (255,), anchor="mm")
    d.text((s / 2, 300), "SLAPPER", font=font, fill=(247, 249, 255, 255), anchor="mm")
    font2 = ImageFont.truetype(CONDENSED, 54)
    d.text((s / 2, 388), "WORLD SLAP AUTHORITY", font=font2, fill=(180, 196, 230, 255), anchor="mm")
    return img.resize((size, size), Image.LANCZOS)


def build_logo(width: int = 1400) -> Image.Image:
    """Title-screen logo with a slap burst behind it."""
    h = int(width * 0.42)
    img = Image.new("RGBA", (width, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Burst.
    cx, cy = width / 2, h * 0.46
    for i in range(26):
        a = 2 * math.pi * i / 26
        r0, r1 = width * 0.16, width * (0.34 if i % 2 == 0 else 0.28)
        d.polygon([(cx + math.cos(a - 0.05) * r0, cy + math.sin(a - 0.05) * r0 * 0.6),
                   (cx + math.cos(a) * r1, cy + math.sin(a) * r1 * 0.6),
                   (cx + math.cos(a + 0.05) * r0, cy + math.sin(a + 0.05) * r0 * 0.6)],
                  fill=(240, 196, 60, 46))

    text = "SUPER SLAPPER"
    # Impact is wide: size the face so the whole name fits the canvas instead of
    # letting the outer letters run off the edge.
    font_size = int(width * 0.30)
    font_main = ImageFont.truetype(IMPACT, font_size)
    while font_size > 12 and d.textlength(text, font=font_main) > width * 0.94:
        font_size -= 2
        font_main = ImageFont.truetype(IMPACT, font_size)

    # Outline via repeated offset draws.
    for dx in range(-8, 9, 4):
        for dy in range(-8, 9, 4):
            d.text((cx + dx, cy + dy), text, font=font_main, fill=(14, 18, 28, 255), anchor="mm")

    # Gold gradient fill. The gradient is an RGBA layer masked by the text, so
    # the letters keep their alpha instead of turning into a flat white wash.
    mask = Image.new("L", (width, h), 0)
    ImageDraw.Draw(mask).text((cx, cy), text, font=font_main, fill=255, anchor="mm")
    grad = vertical_gradient((width, h), Palette.GOLD_LIGHT, Palette.GOLD_DARK).convert("RGBA")
    grad.putalpha(mask)
    img.alpha_composite(grad)

    # No baked-in tagline: the title screen draws its own, clear of the fighters.
    bbox = img.getbbox()
    return img.crop(bbox) if bbox else img


def build_menu_background(seed: int = 3) -> Image.Image:
    img = vertical_gradient((W, H), "#20365e", "#0a1020").convert("RGBA")
    glow = radial_glow((W, H), "#5b83c8", "#0a1020", centre=(W * 0.30, H * 0.38), radius=1150, power=1.9)
    img = Image.blend(img.convert("RGB"), glow, 0.82).convert("RGBA")

    # A giant open hand sweeping across the right of the frame.
    from fighters import FighterPainter, Kit
    painter = FighterPainter(Kit("menu", Palette.PLAYER, Palette.SKINS[1], "#241a12", "stripe"))
    hand = painter._hand((105, 111), spread=1.1, scale=1.4).resize((1500, 1500), Image.LANCZOS)
    hand.putalpha(hand.getchannel("A").point(lambda a: int(a * 0.22)))
    img.alpha_composite(hand, (W - 1180, -140))

    crowd = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    _crowd(ImageDraw.Draw(crowd), 680, H, seed, density=0.6)
    img.alpha_composite(crowd.filter(ImageFilter.GaussianBlur(6)))
    _bokeh(img, seed + 9, 70)

    # Clean stage lighting: no sanctioning mark and no rope-like bands across
    # the frame, so the title never reads as a boxing ring.
    _spotlights(img, seed + 3)

    return vignette(img.convert("RGB"), 0.28)


def build_poster(left_kit: str, right_kit: str) -> Image.Image:
    """Versus poster for the fight night screen."""
    from fighters import FighterPainter, Kit
    kits = {
        "player": Kit("player", Palette.PLAYER, Palette.SKINS[1], "#241a12", "stripe"),
        "rival": Kit("rival", Palette.RIVAL, Palette.SKINS[0], "#3a2410", "solid"),
        "champion": Kit("champion", Palette.CHAMPION, Palette.SKINS[4], "#1c1410", "stripe"),
    }
    img = vertical_gradient((1200, 900), "#111a30", "#05070d").convert("RGBA")
    d = ImageDraw.Draw(img)
    d.polygon([(600, 0), (760, 0), (520, 900), (360, 900)], fill=(255, 255, 255, 10))
    lp = FighterPainter(kits.get(left_kit, kits["player"]))
    rp = FighterPainter(kits.get(right_kit, kits["rival"]))
    left = lp.draw("taunt")
    right = rp.draw("windup_b").transpose(Image.FLIP_LEFT_RIGHT)
    img.alpha_composite(left, (10, 200))
    img.alpha_composite(right, (700, 200))
    font = ImageFont.truetype(IMPACT, 150)
    d.text((600, 90), "VS", font=font, fill=hex_rgb(Palette.GOLD) + (255,), anchor="mm")
    return vignette(img.convert("RGB"), 0.35)


def build_all(out_dir: str) -> None:
    save(build_logo(1500), os.path.join(out_dir, "logo.png"))
    save(build_ring_logo(512), os.path.join(out_dir, "ring_logo.png"))
    save(build_menu_background(), os.path.join(out_dir, "menu_bg.png"))
    save(build_stage_front(), os.path.join(out_dir, "stage_front.png"))
    save(build_poster("player", "rival"), os.path.join(out_dir, "poster.png"))

    venues = [
        ("arena_world", "#243a6b", "#3b1f2b", 11),
        ("arena_hall", "#2a3350", "#232733", 22),
        ("arena_outdoor", "#1d3350", "#1a2b20", 33),
    ]
    for name, canvas, apron, seed in venues:
        save(build_arena(name, canvas, apron, seed), os.path.join(out_dir, f"{name}.png"))

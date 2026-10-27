"""Interface art: panels, buttons, bars, speech bubbles, belts and icons."""
from __future__ import annotations

import math
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

from common import (
    Palette, hex_rgb, mix, rounded_rect_layer, save, vertical_gradient
)

IMPACT = "/System/Library/Fonts/Supplemental/Impact.ttf"
ROUNDED = "/System/Library/Fonts/Supplemental/Arial Rounded Bold.ttf"
CONDENSED = "/System/Library/Fonts/Supplemental/DIN Condensed Bold.ttf"


def _nine_slice(size, radius, fill, outline, width, inner_glow=None):
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    layer = rounded_rect_layer(size, (0, 0, size[0] - 1, size[1] - 1), radius, fill, outline, width)
    img.alpha_composite(layer)
    if inner_glow:
        glow = rounded_rect_layer(size, (width + 2, width + 2, size[0] - width - 3, size[1] // 2),
                                  radius // 2, inner_glow, None, 0)
        glow.putalpha(glow.getchannel("A").point(lambda a: int(a * 0.35)))
        img.alpha_composite(glow)
    return img


def build_button(state: str) -> Image.Image:
    colours = {
        "normal": ("#2b3f77", Palette.GOLD, 5),
        "hover": ("#3d5aa8", Palette.GOLD_LIGHT, 5),
        "pressed": ("#1b2a52", Palette.GOLD_DARK, 5),
        "danger": ("#7a1f1c", "#ff9c8f", 5),
        "ghost": ("#141a2a", "#46527a", 4),
    }[state]
    img = _nine_slice((320, 120), 26, colours[0], colours[1], colours[2], inner_glow="#ffffff")
    return img


def build_panel() -> Image.Image:
    """A navy card with a gold border and a soft top highlight."""
    img = _nine_slice((320, 320), 30, "#1c2c4e", Palette.GOLD, 4, inner_glow="#3f5f9c")
    shade = vertical_gradient(img.size, "#2b4270", "#101a30").convert("RGBA")
    shade.putalpha(img.getchannel("A").point(lambda a: int(a * 0.55)))
    img.alpha_composite(shade)
    border = rounded_rect_layer(img.size, (0, 0, img.width - 1, img.height - 1), 30, None,
                                Palette.GOLD, 4)
    img.alpha_composite(border)
    return img


def build_bubble() -> Image.Image:
    """Rounded speech bubble body - nine-sliced, with the tail drawn separately."""
    img = _nine_slice((256, 200), 34, "#f7f9ff", "#111827", 6)
    return img


def build_bubble_tail(direction: int) -> Image.Image:
    img = Image.new("RGBA", (80, 60), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if direction < 0:
        pts = [(74, 6), (74, 54), (10, 30)]
    else:
        pts = [(6, 6), (6, 54), (70, 30)]
    d.polygon(pts, fill=hex_rgb("#f7f9ff") + (255,), outline=(17, 24, 39, 255))
    d.line(pts + [pts[0]], fill=(17, 24, 39, 255), width=6, joint="curve")
    # Cover the seam against the bubble.
    if direction < 0:
        d.rectangle([66, 8, 79, 52], fill=hex_rgb("#f7f9ff") + (255,))
    else:
        d.rectangle([1, 8, 14, 52], fill=hex_rgb("#f7f9ff") + (255,))
    return img


def build_bar_trough() -> Image.Image:
    img = Image.new("RGBA", (64, 40), (0, 0, 0, 0))
    img.alpha_composite(_nine_slice((64, 40), 16, "#0c1220", "#000000", 3))
    return img


def build_bar_fill(name: str, top: str, bottom: str) -> Image.Image:
    img = vertical_gradient((32, 128), top, bottom)
    img = img.resize((32, 128)).convert("RGBA")
    # Soft top highlight for a glossy, Wii-like finish.
    gloss = Image.new("RGBA", (32, 128), (255, 255, 255, 0))
    d = ImageDraw.Draw(gloss)
    d.rectangle([2, 2, 30, 34], fill=(255, 255, 255, 60))
    img.alpha_composite(gloss)
    return img


def build_power_bar() -> Image.Image:
    """Spring-loaded power meter fill, low (green) to high (red) from bottom up."""
    stops = [(0.0, "#43d17a"), (0.45, "#f2d33d"), (0.75, "#f2903d"), (1.0, "#e63329")]
    h = 256
    img = Image.new("RGBA", (32, h))
    px = img.load()
    for y in range(h):
        t = 1 - y / (h - 1)
        for i in range(len(stops) - 1):
            a, b = stops[i], stops[i + 1]
            if a[0] <= t <= b[0]:
                local = (t - a[0]) / (b[0] - a[0])
                px_col = mix(a[1], b[1], local)
                break
        else:
            px_col = hex_rgb(stops[-1][1])
        for x in range(32):
            px[x, y] = px_col + (255,)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, 31, 40], fill=None)
    gloss = Image.new("RGBA", (32, h), (255, 255, 255, 0))
    ImageDraw.Draw(gloss).rectangle([3, 4, 12, h - 6], fill=(255, 255, 255, 45))
    img.alpha_composite(gloss)
    return img


def build_needle() -> Image.Image:
    img = Image.new("RGBA", (28, 120), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(0, 60), (14, 10), (28, 60), (14, 110)], fill=(255, 255, 255, 240),
              outline=(20, 26, 40, 255))
    d.polygon([(4, 60), (14, 22), (24, 60), (14, 98)], fill=hex_rgb(Palette.GOLD) + (255,))
    return img


def build_pip(active: bool) -> Image.Image:
    img = Image.new("RGBA", (48, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if active:
        d.ellipse([2, 2, 46, 46], fill=hex_rgb(Palette.GOLD) + (255,), outline=(17, 24, 39, 255), width=3)
        d.ellipse([12, 12, 36, 36], fill=(255, 255, 255, 200))
    else:
        d.ellipse([2, 2, 46, 46], fill=(24, 32, 52, 220), outline=(90, 104, 140, 255), width=3)
    return img


def build_arrow(up: bool) -> Image.Image:
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if up:
        pts = [(32, 8), (58, 46), (32, 34), (6, 46)]
        colour = "#49d17a"
    else:
        pts = [(32, 56), (58, 18), (32, 30), (6, 18)]
        colour = "#e3554a"
    d.polygon(pts, fill=hex_rgb(colour) + (255,), outline=(17, 24, 39, 255))
    return img


def build_belt() -> Image.Image:
    """A championship belt: leather strap, gold plates and a gem centre."""
    w, h = 1024, 420
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    strap = [(20, 150), (w - 20, 150), (w - 60, 300), (60, 300)]
    d.polygon(strap, fill=(58, 30, 22, 255), outline=(20, 14, 10, 255))
    for i in range(6):
        x = 100 + i * 150
        d.ellipse([x, 176, x + 60, 236], fill=(74, 40, 28, 255), outline=(30, 20, 14, 255))
    d.polygon([(60, 300), (w - 60, 300), (w - 40, 330), (40, 330)], fill=(44, 24, 18, 255))

    # Gold plates.
    for cx, cy, r in [(140, 210, 78), (w - 140, 210, 78)]:
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=hex_rgb(Palette.GOLD_DARK) + (255,),
                  outline=(20, 14, 10, 255), width=6)
        d.ellipse([cx - r + 12, cy - r + 12, cx + r - 12, cy + r - 12],
                  fill=hex_rgb(Palette.GOLD) + (255,))

    cx, cy = w / 2, 200
    r = 150
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=hex_rgb(Palette.GOLD_DARK) + (255,),
              outline=(20, 14, 10, 255), width=8)
    d.ellipse([cx - r + 16, cy - r + 16, cx + r - 16, cy + r - 16], fill=hex_rgb(Palette.GOLD) + (255,))
    d.ellipse([cx - r + 34, cy - r + 34, cx + r - 34, cy + r - 34], fill=(24, 30, 46, 255))

    font = ImageFont.truetype(IMPACT, 62)
    d.text((cx, cy - 26), "SUPER", font=font, fill=hex_rgb(Palette.GOLD) + (255,), anchor="mm")
    font2 = ImageFont.truetype(IMPACT, 74)
    d.text((cx, cy + 40), "SLAPPER", font=font2, fill=(247, 249, 255, 255), anchor="mm")

    # Central gem.
    d.polygon([(cx, cy - 108), (cx + 26, cy - 84), (cx, cy - 60), (cx - 26, cy - 84)],
              fill=(220, 60, 70, 255), outline=(255, 220, 220, 255))
    return img


def build_trophy() -> Image.Image:
    img = Image.new("RGBA", (420, 560), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    gold, dark = hex_rgb(Palette.GOLD), hex_rgb(Palette.GOLD_DARK)
    d.polygon([(90, 470), (330, 470), (300, 520), (120, 520)], fill=dark)
    d.rectangle([180, 380, 240, 470], fill=dark)
    d.pieslice([110, 120, 310, 380], 0, 180, fill=gold, outline=(20, 14, 10, 255), width=6)
    d.rectangle([110, 120, 310, 160], fill=dark)
    for x in (78, 300):
        d.arc([x - 60, 160, x + 60, 300], 0, 360, fill=gold, width=22)
    d.ellipse([176, 60, 244, 128], fill=gold, outline=(20, 14, 10, 255), width=5)
    d.text((210, 300), "SLAP", font=ImageFont.truetype(IMPACT, 54), fill=(24, 30, 46, 255), anchor="mm")
    return img


def build_medal() -> Image.Image:
    img = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(88, 10), (168, 10), (196, 120), (60, 120)], fill=(200, 45, 40, 255),
              outline=(30, 16, 16, 255))
    d.polygon([(96, 16), (150, 16), (150, 110), (96, 110)], fill=(230, 220, 200, 255))
    d.ellipse([48, 96, 208, 256], fill=hex_rgb(Palette.GOLD) + (255,), outline=(20, 14, 10, 255), width=6)
    d.text((128, 176), "1", font=ImageFont.truetype(IMPACT, 96), fill=(40, 30, 10, 255), anchor="mm")
    return img


def build_icon(name: str, size: int = 128) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = size
    ink = (20, 24, 33, 255)

    def disc(colour):
        d.ellipse([4, 4, s - 4, s - 4], fill=hex_rgb(colour) + (255,))

    if name == "icon_weights":
        disc("#3a4d7a")
        d.rounded_rectangle([s * 0.14, s * 0.44, s * 0.86, s * 0.56], radius=8, fill=(214, 220, 235, 255))
        for x in (0.10, 0.24, 0.68, 0.82):
            d.rounded_rectangle([s * x, s * 0.28, s * (x + 0.12), s * 0.72], radius=8,
                                fill=(60, 70, 96, 255), outline=ink, width=3)
    elif name == "icon_run":
        disc("#2f6fe0")
        d.ellipse([s * 0.40, s * 0.10, s * 0.62, s * 0.32], fill=(245, 215, 180, 255), outline=ink, width=3)
        d.line([(s * 0.48, s * 0.36), (s * 0.40, s * 0.60)], fill=ink, width=14)
        d.line([(s * 0.40, s * 0.60), (s * 0.22, s * 0.86)], fill=ink, width=12)
        d.line([(s * 0.40, s * 0.60), (s * 0.66, s * 0.80)], fill=ink, width=12)
        d.line([(s * 0.46, s * 0.40), (s * 0.74, s * 0.46)], fill=ink, width=12)
        d.line([(s * 0.46, s * 0.42), (s * 0.24, s * 0.52)], fill=ink, width=12)
    elif name == "icon_target":
        disc("#c8402f")
        for r, colour in ((0.42, (250, 250, 255)), (0.30, (220, 60, 50)), (0.18, (250, 250, 255))):
            d.ellipse([s * (0.5 - r), s * (0.5 - r), s * (0.5 + r), s * (0.5 + r)],
                      fill=colour + (255,), outline=ink, width=2)
        d.ellipse([s * 0.44, s * 0.44, s * 0.56, s * 0.56], fill=hex_rgb(Palette.GOLD) + (255,))
    elif name == "icon_hand":
        disc("#e08a2f")
        # Simplified open palm.
        d.ellipse([s * 0.32, s * 0.46, s * 0.74, s * 0.86], fill=(245, 215, 180, 255), outline=ink, width=3)
        for i, x in enumerate((0.34, 0.45, 0.56, 0.67)):
            d.rounded_rectangle([s * x, s * (0.16 + abs(i - 1.5) * 0.03), s * (x + 0.10), s * 0.60],
                                radius=8, fill=(245, 215, 180, 255), outline=ink, width=3)
        d.ellipse([s * 0.18, s * 0.52, s * 0.36, s * 0.68], fill=(245, 215, 180, 255), outline=ink, width=3)
    elif name == "icon_reflex":
        disc("#7443c9")
        d.arc([s * 0.16, s * 0.16, s * 0.84, s * 0.84], 210, 330, fill=(255, 255, 255, 255), width=10)
        d.polygon([(s * 0.78, s * 0.20), (s * 0.92, s * 0.34), (s * 0.72, s * 0.38)],
                  fill=(255, 255, 255, 255), outline=ink)
        d.line([(s * 0.5, s * 0.42), (s * 0.5, s * 0.70)], fill=(255, 255, 255, 255), width=10)
        d.line([(s * 0.5, s * 0.70), (s * 0.34, s * 0.86)], fill=(255, 255, 255, 255), width=10)
        d.line([(s * 0.5, s * 0.70), (s * 0.68, s * 0.86)], fill=(255, 255, 255, 255), width=10)
    elif name == "icon_medkit":
        disc("#2c9e63")
        d.rounded_rectangle([s * 0.16, s * 0.32, s * 0.84, s * 0.80], radius=12,
                            fill=(240, 246, 255, 255), outline=ink, width=4)
        d.rectangle([s * 0.44, s * 0.40, s * 0.56, s * 0.72], fill=(215, 50, 45, 255))
        d.rectangle([s * 0.30, s * 0.50, s * 0.70, s * 0.62], fill=(215, 50, 45, 255))
    elif name == "icon_water":
        disc("#2f9fd0")
        d.polygon([(s * 0.5, s * 0.14), (s * 0.78, s * 0.58), (s * 0.70, s * 0.78),
                   (s * 0.30, s * 0.78), (s * 0.22, s * 0.58)],
                  fill=(230, 246, 255, 255), outline=ink, width=4)
        d.ellipse([s * 0.36, s * 0.50, s * 0.48, s * 0.62], fill=(190, 225, 255, 255))
    else:  # icon_video
        disc("#8a6a2a")
        d.rounded_rectangle([s * 0.14, s * 0.28, s * 0.86, s * 0.74], radius=12,
                            fill=(38, 44, 60, 255), outline=ink, width=4)
        d.polygon([(s * 0.42, s * 0.40), (s * 0.66, s * 0.51), (s * 0.42, s * 0.62)],
                  fill=(245, 245, 255, 255))
    return img


def build_stat_icon(name: str, size: int = 96) -> Image.Image:
    return build_icon(name, size)


def build_weigh_in_scale() -> Image.Image:
    img = Image.new("RGBA", (480, 300), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([20, 40, 460, 280], radius=28, fill=(38, 46, 66, 255), outline=(12, 16, 24, 255), width=6)
    d.ellipse([120, 80, 360, 268], fill=(240, 244, 252, 255), outline=(12, 16, 24, 255), width=6)
    for i in range(11):
        a = math.pi * (0.15 + 0.7 * i / 10)
        x = 240 + math.cos(a) * 105
        y = 220 - math.sin(a) * 105
        d.line([(240 + math.cos(a) * 88, 220 - math.sin(a) * 88), (x, y)], fill=(80, 90, 110, 255), width=4)
    d.line([(240, 220), (168, 150)], fill=(210, 50, 45, 255), width=8)
    d.ellipse([232, 212, 248, 228], fill=(210, 50, 45, 255))
    return img


def build_all(out_dir: str) -> None:
    for state in ("normal", "hover", "pressed", "danger", "ghost"):
        save(build_button(state), os.path.join(out_dir, f"button_{state}.png"))
    save(build_panel(), os.path.join(out_dir, "panel.png"))
    save(build_bubble(), os.path.join(out_dir, "bubble.png"))
    save(build_bubble_tail(-1), os.path.join(out_dir, "bubble_tail_left.png"))
    save(build_bubble_tail(1), os.path.join(out_dir, "bubble_tail_right.png"))
    save(build_bar_trough(), os.path.join(out_dir, "bar_trough.png"))
    save(build_bar_fill("health", "#ff6b6b", "#a11d1d"), os.path.join(out_dir, "bar_health.png"))
    save(build_bar_fill("stamina", "#7ee08a", "#1f7a3c"), os.path.join(out_dir, "bar_stamina.png"))
    save(build_bar_fill("damage", "#ffd166", "#b8860b"), os.path.join(out_dir, "bar_damage.png"))
    save(build_power_bar(), os.path.join(out_dir, "bar_power.png"))
    save(build_needle(), os.path.join(out_dir, "needle.png"))
    save(build_pip(True), os.path.join(out_dir, "pip_on.png"))
    save(build_pip(False), os.path.join(out_dir, "pip_off.png"))
    save(build_arrow(True), os.path.join(out_dir, "arrow_up.png"))
    save(build_arrow(False), os.path.join(out_dir, "arrow_down.png"))
    save(build_belt(), os.path.join(out_dir, "belt.png"))
    save(build_trophy(), os.path.join(out_dir, "trophy.png"))
    save(build_medal(), os.path.join(out_dir, "medal.png"))
    save(build_weigh_in_scale(), os.path.join(out_dir, "scale.png"))

    icons = ["icon_weights", "icon_run", "icon_target", "icon_hand", "icon_reflex",
             "icon_medkit", "icon_water", "icon_video"]
    for icon in icons:
        save(build_icon(icon, 128), os.path.join(out_dir, f"{icon}.png"))
    for stat in ("Power", "Aim", "Chin", "Endurance", "Reflex", "Mass"):
        save(build_icon("icon_hand" if stat == "Power" else "icon_target", 96),
             os.path.join(out_dir, f"stat_{stat.lower()}.png"))

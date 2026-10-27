"""Shared drawing helpers for the SuperSlapper artwork generator.

Everything is drawn into a supersampled buffer and downscaled with LANCZOS, so
curves come out clean without any per-pixel Python loops. Outlines are painted
by stroking the shape boundary before filling it, which is far faster than
dilating a mask (and the process stays in C inside Pillow).
"""
from __future__ import annotations

import math
import os
from PIL import Image, ImageDraw, ImageFilter

SS = 2  # supersampling factor


class Palette:
    PLAYER = dict(main="#2f6fe0", dark="#17418f", trim="#8ec2ff", trunks="#12305f")
    RIVAL = dict(main="#e0392e", dark="#8d1a14", trim="#ffab9c", trunks="#5c1410")
    GREEN = dict(main="#23a86b", dark="#0f5c39", trim="#9bf0c4", trunks="#0b3d26")
    CHAMPION = dict(main="#f2c53d", dark="#9a7408", trim="#fff2b8", trunks="#4a3606")

    SKINS = ["#f0c091", "#d99b66", "#b5744a", "#8a5230", "#63381f", "#f7d3ab"]

    INK = "#141821"
    INK_SOFT = "#20283a"
    GOLD = "#f5c542"
    GOLD_DARK = "#a87c12"
    GOLD_LIGHT = "#ffe89a"
    BG_DEEP = "#0a0f1e"
    BG_MID = "#152140"
    CANVAS = "#2b3f77"
    WHITE = "#f7f9ff"
    RED = "#e0392e"
    BLUE = "#2f6fe0"
    GREY = "#8a92a6"


def hex_rgb(value) -> tuple[int, int, int]:
    """Accepts '#rrggbb' or an (r, g, b) tuple and always returns a tuple."""
    if isinstance(value, (tuple, list)):
        return tuple(int(c) for c in value[:3])
    value = value.lstrip("#")
    if len(value) == 3:
        value = "".join(c * 2 for c in value)
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def rgba(value, alpha=255) -> tuple[int, int, int, int]:
    r, g, b = hex_rgb(value)
    return (r, g, b, alpha)


def mix(a, b, t: float) -> tuple[int, int, int]:
    ca, cb = hex_rgb(a), hex_rgb(b)
    return tuple(int(ca[i] + (cb[i] - ca[i]) * t) for i in range(3))


def vertical_gradient(size, top, bottom) -> Image.Image:
    w, h = size
    img = Image.new("RGB", (1, h))
    px = img.load()
    for y in range(h):
        px[0, y] = mix(top, bottom, y / max(1, h - 1))
    return img.resize((w, h), Image.BILINEAR)


def radial_glow(size, inner, outer, centre=None, radius=None, power=1.6) -> Image.Image:
    """Radial gradient computed small and upscaled - visually identical, 100x faster."""
    w, h = size
    cx, cy = centre if centre else (w / 2, h / 2)
    radius = radius or max(w, h) * 0.7
    sw, sh = max(2, w // 10), max(2, h // 10)
    small = Image.new("RGB", (sw, sh), hex_rgb(outer))
    px = small.load()
    ci, co = hex_rgb(inner), hex_rgb(outer)
    for y in range(sh):
        dy = (y * 10 - cy) / radius
        for x in range(sw):
            dx = (x * 10 - cx) / radius
            d = min(1.0, math.hypot(dx, dy))
            t = (1.0 - d) ** power
            px[x, y] = tuple(int(co[i] + (ci[i] - co[i]) * t) for i in range(3))
    return small.resize((w, h), Image.BICUBIC)


def new_layer(size) -> tuple[Image.Image, ImageDraw.ImageDraw]:
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def _big(size):
    return (size[0] * SS, size[1] * SS), Image.new("RGBA", (size[0] * SS, size[1] * SS), (0, 0, 0, 0))


def shape_layer(size, pts, fill, outline=None, width=3, closed=True) -> Image.Image:
    """Fill a polygon and stroke its boundary with a cartoon outline."""
    big_size, img = _big(size)
    d = ImageDraw.Draw(img)
    scaled = [(x * SS, y * SS) for x, y in pts]
    if outline and width > 0:
        outline_pts = scaled + ([scaled[0]] if closed else [])
        d.line(outline_pts, fill=rgba(outline), width=max(1, int(width * 2 * SS)), joint="curve")
    if fill is not None:
        d.polygon(scaled, fill=rgba(fill))
    return img.resize(size, Image.LANCZOS)


def ellipse_layer(size, centre, rx, ry, fill, outline=None, width=3, rotation=0.0) -> Image.Image:
    big_size, img = _big(size)
    d = ImageDraw.Draw(img)
    if rotation:
        pts = ellipse_fn(centre[0], centre[1], rx, ry, rotation)
        return shape_layer(size, pts, fill, outline, width)
    box = [(centre[0] - rx) * SS, (centre[1] - ry) * SS,
           (centre[0] + rx) * SS, (centre[1] + ry) * SS]
    if fill is not None:
        d.ellipse(box, fill=rgba(fill))
    if outline and width > 0:
        d.ellipse(box, outline=rgba(outline), width=max(1, int(width * SS)))
    return img.resize(size, Image.LANCZOS)


def rounded_rect_layer(size, box, radius, fill, outline=None, width=3) -> Image.Image:
    big_size, img = _big(size)
    d = ImageDraw.Draw(img)
    scaled = [box[0] * SS, box[1] * SS, box[2] * SS, box[3] * SS]
    if fill is not None:
        d.rounded_rectangle(scaled, radius=radius * SS, fill=rgba(fill))
    if outline and width > 0:
        d.rounded_rectangle(scaled, radius=radius * SS, outline=rgba(outline),
                            width=max(1, int(width * SS)))
    return img.resize(size, Image.LANCZOS)


def paste_outlined(base: Image.Image, size, pts, fill, outline=None, width=3):
    base.alpha_composite(shape_layer(size, pts, fill, outline, width))
    return base


def ellipse_fn(cx, cy, rx, ry, rotation=0.0):
    pts = []
    steps = 64
    for i in range(steps):
        t = 2 * math.pi * i / steps
        x = rx * math.cos(t)
        y = ry * math.sin(t)
        if rotation:
            c, s = math.cos(rotation), math.sin(rotation)
            x, y = x * c - y * s, x * s + y * c
        pts.append((cx + x, cy + y))
    return pts


def capsule(p0, p1, r0, r1, steps=14):
    """Tapered capsule between two points - the workhorse for limbs."""
    x0, y0 = p0
    x1, y1 = p1
    ang = math.atan2(y1 - y0, x1 - x0)
    pts = []
    for i in range(steps + 1):
        a = ang + math.pi / 2 + math.pi * i / steps
        pts.append((x0 + r0 * math.cos(a), y0 + r0 * math.sin(a)))
    for i in range(steps + 1):
        a = ang - math.pi / 2 + math.pi * i / steps
        pts.append((x1 + r1 * math.cos(a), y1 + r1 * math.sin(a)))
    return pts


def poly_layer(size, pts_list, fill, outline=None, width=3):
    return shape_layer(size, pts_list, fill, outline, width)


def vignette(img: Image.Image, strength=0.55) -> Image.Image:
    w, h = img.size
    mask = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(mask)
    d.ellipse([-w * 0.25, -h * 0.3, w * 1.25, h * 1.3], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(max(w, h) // 12))
    dark = Image.new("RGB", (w, h), (0, 0, 0))
    return Image.composite(img, Image.blend(img, dark, strength), mask)


def save(img: Image.Image, path: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if img.mode not in ("RGB", "RGBA"):
        img = img.convert("RGBA")
    img.save(path, optimize=True)


def ensure_dir(path: str) -> str:
    os.makedirs(path, exist_ok=True)
    return path

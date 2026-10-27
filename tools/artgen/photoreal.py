"""Semi-photoreal slap-landing still for the title screen.

The sprite art is flat cartoon with ink outlines; this module paints the
impact instead, with no outlines at all. Every form gets a directional
gradient, a lit rim and an occlusion crescent, then the finished frame is
graded with bloom, grain, a split tone and a vignette - the things a camera
lens leaves behind. The subject is a tight crop on skin, which is what makes
it read as a photograph rather than as the cartoon it grew out of.

Produces Content/art/hero_slap.png.
"""
from __future__ import annotations

import math
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps

from common import ellipse_fn, hex_rgb, mix

SS = 2

# Key light: high and slightly in front of the subject.
KEY = (0.34, -0.94)


def _ramp(size, dx, dy, lo=0.0, hi=1.0) -> Image.Image:
    """A linear brightness ramp running along (dx, dy), generated tiny."""
    w, h = max(1, size[0]), max(1, size[1])
    small = Image.new("L", (48, 48))
    px = small.load()
    for y in range(48):
        fy = y / 47.0 - 0.5
        for x in range(48):
            fx = x / 47.0 - 0.5
            t = (fx * dx + fy * dy) * 1.35
            v = max(0.0, min(1.0, 0.5 + t * 0.62))
            px[x, y] = int(255 * (lo + (hi - lo) * v))
    return small.resize((w, h), Image.BICUBIC)


def _blur_mask(mask, radius):
    return mask.filter(ImageFilter.GaussianBlur(max(0.3, radius)))


def _bbox(pts, pad, bounds=(1200, 640)):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    x0 = max(0, int((min(xs) - pad) * SS))
    y0 = max(0, int((min(ys) - pad) * SS))
    x1 = min(bounds[0] * SS, int((max(xs) + pad) * SS) + 1)
    y1 = min(bounds[1] * SS, int((max(ys) + pad) * SS) + 1)
    return x0, y0, x1, y1


def paint_form(canvas, pts, base, light=KEY, contrast=0.60, rim=0.55, core=0.42,
               blur=0.55, pad=8, alpha=255, contour=0.0):
    """A shaded solid: gradient body, lit rim, occlusion on the far edge.

    `contour` lays a soft dark edge all round the shape - enough to keep limbs
    separate on a dark stage without the hard ink line of the sprite art.
    """
    bounds = (canvas.width // SS, canvas.height // SS)
    box = _bbox(pts, pad, bounds)
    if box[2] - box[0] < 1 or box[3] - box[1] < 1:
        return
    x0, y0, x1, y1 = box
    mask = Image.new("L", (x1 - x0, y1 - y0), 0)
    ImageDraw.Draw(mask).polygon(
        [((p[0] * SS) - x0, (p[1] * SS) - y0) for p in pts], fill=255)
    mask = _blur_mask(mask, blur)
    w, h = mask.size

    dark = mix(base, "#2b1206", contrast * 0.8)
    lite = mix(base, "#fff3e4", contrast)
    body = ImageOps.colorize(_ramp((w, h), *light), black=dark, white=lite).convert("RGB")

    if rim > 0:
        shifted = ImageChops.offset(mask, int(-light[0] * 3.4 * SS), int(-light[1] * 3.4 * SS))
        edge = ImageChops.subtract(mask, shifted).filter(ImageFilter.GaussianBlur(2.2 * SS))
        body.paste(mix(lite, "#ffffff", 0.30), (0, 0),
                   edge.point(lambda v: int(min(255, v * 1.9) * rim)))

    if core > 0:
        shifted = ImageChops.offset(mask, int(light[0] * 8 * SS), int(light[1] * 8 * SS))
        edge = ImageChops.subtract(mask, shifted).filter(ImageFilter.GaussianBlur(4.5 * SS))
        body.paste(mix(dark, "#000000", 0.30), (0, 0),
                   edge.point(lambda v: int(v * core)))

    if contour > 0:
        edge = ImageChops.subtract(mask, mask.filter(ImageFilter.MinFilter(5)))
        edge = edge.filter(ImageFilter.GaussianBlur(1.2 * SS))
        body.paste(mix(dark, "#120a10", 0.55), (0, 0),
                   edge.point(lambda v: int(min(255, v * 1.4) * contour)))

    part = body.convert("RGBA")
    if alpha < 255:
        mask = mask.point(lambda v: int(v * alpha / 255))
    part.putalpha(mask)
    canvas.alpha_composite(part, (x0, y0))


def paint_wash(canvas, pts, colour, alpha=70, blur=6, pad=20, mask_alpha=255):
    """A soft bloom or grime patch with no hard edge at all."""
    bounds = (canvas.width // SS, canvas.height // SS)
    box = _bbox(pts, pad, bounds)
    if box[2] - box[0] < 1 or box[3] - box[1] < 1:
        return
    x0, y0, x1, y1 = box
    mask = Image.new("L", (x1 - x0, y1 - y0), 0)
    ImageDraw.Draw(mask).polygon(
        [((p[0] * SS) - x0, (p[1] * SS) - y0) for p in pts], fill=255)
    mask = _blur_mask(mask, blur).point(lambda v: int(v * alpha / 255))
    if mask_alpha < 255:
        mask = mask.point(lambda v: int(v * mask_alpha / 255))
    layer = Image.new("RGBA", mask.size, hex_rgb(colour) + (255,))
    layer.putalpha(mask)
    canvas.alpha_composite(layer, (x0, y0))


def paint_spec(canvas, centre, rx, ry, strength=150, colour="#fff6ea", rot=0.0):
    put_spec(canvas, centre, rx, ry, strength, colour, rot)


def put_spec(canvas, centre, rx, ry, strength=150, colour="#fff6ea", rot=0.0):
    pts = ellipse_fn(centre[0], centre[1], rx, ry, rot)
    bounds = (canvas.width // SS, canvas.height // SS)
    box = _bbox(pts, 14, bounds)
    if box[2] - box[0] < 1 or box[3] - box[1] < 1:
        return
    x0, y0, x1, y1 = box
    mask = Image.new("L", (x1 - x0, y1 - y0), 0)
    ImageDraw.Draw(mask).polygon(
        [((p[0] * SS) - x0, (p[1] * SS) - y0) for p in pts], fill=255)
    mask = _blur_mask(mask, 2.4).point(lambda v: int(v * strength / 255))
    layer = Image.new("RGBA", mask.size, hex_rgb(colour) + (255,))
    layer.putalpha(mask)
    canvas.alpha_composite(layer, (x0, y0))




def capsule_pts(p0, p1, r0, r1, steps=14):
    from common import capsule
    return capsule(p0, p1, r0, r1, steps)



# The slap landing used as the title illustration.
#
# A tight crop on the impact: the defender's face fills the right of the frame,
# the striker's open hand drives in from the lower left. Faces at this size are
# skin, light and texture, which is what makes the still read as a photograph
# rather than as the cartoon sprite it grew out of.
HERO_W, HERO_H = 1200, 640
HERO_CANVAS = (HERO_W * SS, HERO_H * SS)

HERO_SKIN = "#c98d67"
HERO_HAND = "#c2815a"


def _spline(points, samples=10, closed=True):
    """Catmull-Rom through the control points, so outlines curve like tissue."""
    pts = list(points)
    if closed:
        pts = [pts[-1]] + pts + [pts[0], pts[1]]
    else:
        pts = [pts[0]] + pts + [pts[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        for s in range(samples):
            t = s / samples
            t2, t3 = t * t, t * t * t
            out.append((
                0.5 * (2 * p1[0] + (-p0[0] + p2[0]) * t
                       + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2
                       + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3),
                0.5 * (2 * p1[1] + (-p0[1] + p2[1]) * t
                       + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2
                       + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3),
            ))
    return out


def _bokeh(size, rng):
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    palette = [(255, 198, 118), (255, 228, 174), (128, 176, 255), (255, 132, 96), (222, 238, 255)]
    for _ in range(120):
        x = rng.uniform(-60, size[0] + 60)
        y = rng.uniform(-40, size[1] * 0.7)
        r = rng.uniform(10, 52)
        colour = palette[rng.randrange(len(palette))]
        d.ellipse([x - r, y - r, x + r, y + r], fill=colour + (rng.randint(8, 40),))
    return layer.filter(ImageFilter.GaussianBlur(22))


def _hero_backdrop():
    from common import radial_glow, vertical_gradient

    base = vertical_gradient(HERO_CANVAS, "#05070c", "#121a2a").convert("RGBA")

    # Hard warm spot behind the head: the light that rims the shoulder.
    glow = radial_glow(HERO_CANVAS, "#a8703c", "#060810",
                       centre=(1760, 210), radius=1150, power=2.2).convert("RGBA")
    glow.putalpha(185)
    base.alpha_composite(glow)

    rng = random.Random(7)
    crowd = Image.new("RGBA", HERO_CANVAS, (0, 0, 0, 0))
    d = ImageDraw.Draw(crowd)
    for _ in range(1000):
        x = rng.uniform(-40, HERO_CANVAS[0] + 40)
        y = rng.uniform(0, HERO_CANVAS[1] * 0.62)
        w = rng.uniform(22, 54)
        h = rng.uniform(28, 74)
        shade = rng.randint(8, 32)
        d.ellipse([x, y, x + w, y + h], fill=(shade + 9, shade + 6, shade + 14, 190))
    base.alpha_composite(crowd.filter(ImageFilter.GaussianBlur(16)))

    base.alpha_composite(_bokeh(HERO_CANVAS, rng))

    fog = Image.new("RGBA", HERO_CANVAS, (0, 0, 0, 0))
    ImageDraw.Draw(fog).rectangle([0, 380, HERO_CANVAS[0], 900], fill=(200, 180, 152, 26))
    base.alpha_composite(fog.filter(ImageFilter.GaussianBlur(90)))
    return base


def _edge_rim(canvas, pts, colour="#ffcf9a", width=7, strength=200, side=(1, -0.4)):
    """A rim light along one side of a silhouette."""
    bounds = (canvas.width // SS, canvas.height // SS)
    box = _bbox(pts, 14, bounds)
    if box[2] - box[0] < 1 or box[3] - box[1] < 1:
        return
    x0, y0, x1, y1 = box
    mask = Image.new("L", (x1 - x0, y1 - y0), 0)
    ImageDraw.Draw(mask).polygon([((p[0] * SS) - x0, (p[1] * SS) - y0) for p in pts], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(1.0 * SS))
    shifted = ImageChops.offset(mask, -int(side[0] * width * SS), -int(side[1] * width * SS))
    edge = ImageChops.subtract(mask, shifted).filter(ImageFilter.GaussianBlur(3.0 * SS))
    edge = edge.point(lambda v: int(min(255, v * 1.6) * strength / 255))
    layer = Image.new("RGBA", mask.size, hex_rgb(colour) + (255,))
    layer.putalpha(edge)
    canvas.alpha_composite(layer, (x0, y0))


def _texture(canvas, pts, base, amount=26, grain=26):
    """Pores, freckles, the unevenness that reads as skin."""
    bounds = (canvas.width // SS, canvas.height // SS)
    box = _bbox(pts, 6, bounds)
    if box[2] - box[0] < 1 or box[3] - box[1] < 1:
        return
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).polygon([((p[0] * SS) - x0, (p[1] * SS) - y0) for p in pts], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(2.0 * SS))
    noise = Image.effect_noise((w, h), grain).filter(ImageFilter.GaussianBlur(0.8 * SS))
    shade = ImageOps.colorize(noise, black=mix(base, "#3a1608", 0.6),
                              white=mix(base, "#fff4e8", 0.5)).convert("RGB")
    layer = shade.convert("RGBA")
    layer.putalpha(mask.point(lambda v: int(v * amount / 255)))
    canvas.alpha_composite(layer, (x0, y0))


def _stubble(canvas, pts, colour="#2a1a14", amount=90):
    """Clipped beard shadow: grain that only lives on the jaw."""
    bounds = (canvas.width // SS, canvas.height // SS)
    box = _bbox(pts, 6, bounds)
    if box[2] - box[0] < 1 or box[3] - box[1] < 1:
        return
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).polygon([((p[0] * SS) - x0, (p[1] * SS) - y0) for p in pts], fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(3.0 * SS))
    specks = Image.effect_noise((w, h), 70).point(lambda v: 255 if v > 146 else 0)
    mask = ImageChops.multiply(mask, specks.filter(ImageFilter.GaussianBlur(0.7 * SS)))
    mask = mask.point(lambda v: int(v * amount / 255))
    layer = Image.new("RGBA", (w, h), hex_rgb(colour) + (255,))
    layer.putalpha(mask)
    canvas.alpha_composite(layer, (x0, y0))


def _hero_face(canvas):
    face = _spline([
        (700, 78), (676, 190), (694, 318), (722, 448), (714, 520),
        (800, 580), (946, 592), (1070, 646), (1210, 662), (1240, 320),
        (1104, 104), (900, 26),
    ])
    # Skull and jaw volume: one soft mass, then the modelling on top.
    paint_form(canvas, face, HERO_SKIN, light=(-0.42, -0.91), contrast=0.95,
               rim=0.55, core=0.75, blur=3.0, pad=20)

    # Temple falls away, cheekbone catches the key, jaw sits in its own shadow.
    paint_wash(canvas, ellipse_fn(940, 150, 260, 190), "#3f1808", alpha=140, blur=48, pad=60)
    paint_wash(canvas, ellipse_fn(770, 330, 120, 150), "#ffe3c4", alpha=62, blur=44, pad=60)
    paint_wash(canvas, ellipse_fn(900, 566, 280, 96), "#300e04", alpha=150, blur=36, pad=60)
    paint_wash(canvas, ellipse_fn(1140, 470, 160, 230), "#4a1c0c", alpha=125, blur=52, pad=60)
    paint_wash(canvas, ellipse_fn(1040, 620, 240, 90), "#2a0c04", alpha=140, blur=40, pad=60)
    put_spec(canvas, (742, 322), 40, 62, strength=120, rot=-0.25)

    # Ear: a curved shell with a shadowed bowl, cropped by the frame edge.
    ear = _spline([(1032, 224), (1112, 214), (1168, 300), (1150, 392),
                   (1078, 424), (1026, 356), (1030, 276)], samples=8)
    paint_form(canvas, ear, mix(HERO_SKIN, "#c0603c", 0.22), light=(-0.2, -0.95),
               contrast=0.8, rim=0.35, core=0.5, blur=2.4, pad=24)
    bowl = _spline([(1052, 246), (1104, 240), (1138, 312), (1122, 372),
                    (1072, 392), (1042, 330)], samples=8)
    paint_wash(canvas, bowl, "#4a1a0e", alpha=120, blur=13, pad=30)
    paint_wash(canvas, _spline([(1042, 232), (1084, 224), (1126, 262), (1096, 276),
                                (1056, 268)], samples=8), "#f0b48c", alpha=70, blur=10, pad=26)

    # Hair over the skull and down the back of the head.
    hair = _spline([(806, 40), (900, 6), (1050, 20), (1180, 96), (1246, 260),
                    (1252, 470), (1194, 470), (1156, 300), (1080, 176),
                    (930, 108), (812, 112)])
    paint_form(canvas, hair, "#1a100c", light=(-0.4, -0.92), contrast=0.55, rim=0.25, blur=3.0, pad=26)
    for i in range(5):
        x = 846 + i * 52
        paint_wash(canvas, _spline([(x, 104 - i * 5), (x + 38, 76 - i * 5), (x + 84, 54 - i * 4)],
                                   samples=6, closed=False), "#6a4630", alpha=34, blur=5, pad=18)

    _hero_eye(canvas)

    # Mouth. The hand lands above it, so the lips stay readable.
    paint_wash(canvas, capsule_pts((704, 466), (818, 452), 16, 12), "#5a1a14", alpha=215, blur=6, pad=30)
    lower = _spline([(718, 498), (766, 492), (808, 502), (788, 522), (740, 520)], samples=8)
    paint_form(canvas, lower, "#8e3d36", light=(-0.35, -0.94), contrast=0.6, rim=0.25,
               core=0.45, blur=2.6, pad=26)
    upper = _spline([(712, 472), (760, 458), (804, 464), (776, 478), (730, 480)], samples=8)
    paint_form(canvas, upper, "#6e2b24", light=(-0.3, -0.95), contrast=0.6, rim=0.2,
               core=0.45, blur=2.2, pad=24)
    put_spec(canvas, (764, 500), 24, 6, strength=70, rot=-0.1)
    paint_wash(canvas, _spline([(730, 548), (800, 546), (830, 586), (752, 594)], samples=8),
               "#3a1408", alpha=95, blur=20, pad=36)

    _texture(canvas, face, HERO_SKIN, amount=30, grain=24)
    _stubble(canvas, _spline([(836, 470), (1000, 470), (1080, 540), (980, 600),
                              (860, 584), (820, 520)], samples=8), amount=100)

    # Warm rim from the spot behind, down the back of the head and shoulder.
    _edge_rim(canvas, face, colour="#ffc98c", width=9, strength=185, side=(-1, 0.35))


def _hero_eye(canvas):
    """A squinting eye above the blow: enough of a face to read as one."""
    ex, ey = 806, 258

    # Socket, brow bone, and the shadow the brow casts.
    paint_wash(canvas, ellipse_fn(ex + 4, ey - 6, 88, 52, -0.18), "#4a1c10", alpha=115, blur=26, pad=44)
    brow = _spline([(718, 222), (792, 198), (872, 214), (884, 238), (786, 232), (726, 246)],
                   samples=8)
    paint_form(canvas, brow, "#2a1a12", light=(-0.4, -0.9), contrast=0.6, rim=0.3, core=0.4,
               blur=2.6, pad=22)
    paint_wash(canvas, ellipse_fn(ex, ey + 6, 74, 22, -0.18), "#4a1c10", alpha=90, blur=14, pad=30)

    # Sclera, iris, pupil - all squeezed down to a slit under the lid.
    eye = _spline([(748, 262), (790, 248), (846, 256), (860, 278), (800, 284), (750, 280)],
                  samples=8)
    paint_form(canvas, eye, "#e8ddd0", light=(-0.3, -0.95), contrast=0.35, rim=0, core=0.3,
               blur=2.0, pad=18)
    iris = ellipse_fn(806, 270, 22, 22)
    paint_form(canvas, iris, "#3d2414", light=(-0.3, -0.95), contrast=0.4, rim=0.2, core=0.4,
               blur=1.6, pad=14)
    paint_wash(canvas, ellipse_fn(806, 272, 9, 10), "#0d0806", alpha=245, blur=2.4, pad=12)
    put_spec(canvas, (812, 262), 5, 5, strength=240)

    # Heavy upper lid and wet lower lid.
    paint_wash(canvas, _spline([(742, 258), (796, 244), (854, 254), (860, 266), (792, 258),
                                (746, 272)], samples=8), "#2a1208", alpha=225, blur=4, pad=20)
    paint_wash(canvas, capsule_pts((752, 284), (846, 282), 5, 3), "#a85a44", alpha=110,
               blur=4, pad=16)

    # Impact squint creases at the outer corner.
    for i in range(3):
        paint_wash(canvas, capsule_pts((864 + i * 6, 248 + i * 12), (900 + i * 8, 240 + i * 16),
                                       4, 2), "#5a2414", alpha=95, blur=4, pad=16)


def _hero_hand(canvas):
    # Forearm, entering from the lower left and leaving the frame.
    arm = capsule_pts((60, 760), (320, 520), 136, 92)
    paint_form(canvas, arm, HERO_HAND, light=(-0.5, -0.86), contrast=0.9, rim=0.5,
               core=0.6, blur=3.0, pad=30)

    # Back of the hand, angled at the cheek.
    palm = _spline([(292, 448), (398, 336), (556, 330), (638, 372),
                    (556, 492), (398, 534)])
    paint_form(canvas, palm, HERO_HAND, light=(-0.5, -0.86), contrast=0.9, rim=0.5,
               core=0.55, blur=3.0, pad=26)

    # Knuckles, then the tendons running back to the wrist.
    for i in range(4):
        put_spec(canvas, (462 + i * 42, 352 + i * 34), 22, 15, strength=100, rot=-0.2)
    for i in range(3):
        paint_wash(canvas, capsule_pts((372, 398 + i * 38), (534, 372 + i * 34), 9, 6),
                   "#6a3420", alpha=60, blur=10, pad=26)
    paint_wash(canvas, capsule_pts((330, 470), (520, 500), 30, 22), "#6a3420", alpha=55,
               blur=18, pad=34)

    fingers = [
        ((470, 340), (592, 296), (684, 302), 38, 31),
        ((516, 366), (648, 336), (742, 358), 40, 33),
        ((544, 402), (664, 404), (734, 446), 38, 31),
        ((556, 442), (638, 476), (688, 524), 32, 26),
    ]
    for root, mid, tip, w0, w1 in fingers:
        paint_form(canvas, capsule_pts(root, mid, w0, w1), HERO_HAND, light=(-0.5, -0.86),
                   contrast=0.85, rim=0.5, core=0.5, blur=2.2, pad=22)
        paint_form(canvas, capsule_pts(mid, tip, w1, w1 * 0.86), HERO_HAND, light=(-0.5, -0.86),
                   contrast=0.85, rim=0.5, core=0.5, blur=2.2, pad=22)
        paint_wash(canvas, ellipse_fn(mid[0] - 8, mid[1] + 6, 12, 10), "#6a3420", alpha=60,
                   blur=8, pad=20)
        put_spec(canvas, (mid[0] + 12, mid[1] - 14), 26, 13, strength=70, rot=-0.25)

    thumb = capsule_pts((318, 520), (474, 596), 48, 36)
    paint_form(canvas, thumb, mix(HERO_HAND, "#ffffff", 0.07), light=(-0.5, -0.86),
               contrast=0.85, rim=0.5, core=0.5, blur=2.6, pad=26)

    _texture(canvas, palm, HERO_HAND, amount=22, grain=22)


def _hero_contact(canvas, strength=1.0, seed=21):
    """The cheek gives way under the heel of the hand."""
    cx, cy = 748, 386
    if strength <= 0:
        return

    # Ambient occlusion: the hand sits on the face, so the skin dims under it.
    for y0, y1 in ((300, 330), (340, 372), (392, 424), (438, 478)):
        paint_wash(canvas, capsule_pts((cx - 20, y0), (cx + 30, y1), 20, 16), "#3a1408",
                   alpha=105, blur=14, pad=30)

    # Compressed skin: a pale, hot patch with the finger ridges pressed into it.
    paint_wash(canvas, ellipse_fn(cx + 26, cy + 6, 78, 96, -0.2), "#ffdcb4",
               alpha=int(95 * strength), blur=26, pad=50)
    paint_wash(canvas, ellipse_fn(cx + 60, cy + 30, 60, 84, -0.2), "#d8503a",
               alpha=int(95 * strength), blur=30, pad=50)
    for i in range(4):
        paint_wash(canvas, capsule_pts((cx + 30, cy - 60 + i * 40), (cx + 54, cy - 46 + i * 40), 9, 5),
                   "#c0724f", alpha=int(90 * strength), blur=9, pad=24)

    # The flash itself, reflected off the contact patch.
    flash = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(flash).ellipse([(cx - 30) * SS, (cy - 30) * SS, (cx + 34) * SS, (cy + 34) * SS],
                                  fill=(255, 246, 224, int(150 * strength)))
    canvas.alpha_composite(flash.filter(ImageFilter.GaussianBlur(26 * SS)))

    rng = random.Random(seed)
    spray = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(spray)
    for _ in range(int(110 * strength)):
        a = rng.uniform(-1.9, 0.15)
        dist = rng.uniform(20, 330) ** 1.15 / 3.0
        x = (cx + math.cos(a) * dist) * SS
        y = (cy + math.sin(a) * dist * 0.82) * SS
        r = rng.uniform(1.4, 4.6) * SS * (1.4 - dist / 240)
        if r < 0.6:
            continue
        sd.ellipse([x - r, y - r, x + r, y + r], fill=(222, 238, 255, rng.randint(55, 150)))
        if r > 2.4 * SS * 0.6:
            sd.line([(x - r * 3, y + r * 2), (x - r * 6, y + r * 4)],
                    fill=(222, 238, 255, rng.randint(30, 80)), width=max(1, int(r * 0.5)))
    canvas.alpha_composite(spray.filter(ImageFilter.GaussianBlur(1.2 * SS)))

    streaks = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    strd = ImageDraw.Draw(streaks)
    for _ in range(int(26 * strength)):
        a = rng.uniform(-1.4, 0.25)
        d0 = rng.uniform(30, 90)
        d1 = d0 + rng.uniform(90, 300)
        p0 = ((cx + math.cos(a) * d0) * SS, (cy + math.sin(a) * d0 * 0.8) * SS)
        p1 = ((cx + math.cos(a) * d1) * SS, (cy + math.sin(a) * d1 * 0.9) * SS)
        strd.line([p0, p1], fill=(255, 246, 232, 26), width=int(rng.uniform(3, 8) * SS))
    canvas.alpha_composite(streaks.filter(ImageFilter.GaussianBlur(10 * SS)))


def _hero_finish(canvas, size=(HERO_W, HERO_H)):
    # One directional smear over the swinging hand: the slap is still moving.
    smear = canvas.filter(ImageFilter.GaussianBlur(1.0 * SS))
    smear_layer = smear.copy()
    smear_layer.putalpha(smear.getchannel("A").point(lambda v: int(v * 0.22)))
    canvas.alpha_composite(smear_layer)

    rgb = canvas.convert("RGB")

    bright = rgb.point(lambda v: max(0, int((v - 170) * 2.3)))
    rgb = ImageChops.screen(rgb, bright.filter(ImageFilter.GaussianBlur(46))
                            .point(lambda v: int(v * 0.7)))

    # Lifted so the still holds its own against the blue menu art behind it.
    rgb = rgb.point(lambda v: min(255, int(24 + 255 * (v / 255.0) ** 0.94 * 1.12)))
    rgb = rgb.point(lambda v: max(0, min(255, int((v - 128) * 1.18 + 128))))

    # Split tone: warm highlights, cool shadows.
    warm = Image.new("RGB", rgb.size, (255, 216, 168))
    cool = Image.new("RGB", rgb.size, (48, 68, 104))
    rgb = Image.blend(rgb, ImageChops.multiply(rgb, warm), 0.12)
    rgb = Image.blend(rgb, ImageChops.screen(rgb, cool.point(lambda v: int(v * 0.42))), 0.10)

    # Chromatic fringe at the edges, as a fast lens leaves it.
    r, g, b = rgb.split()
    r = r.point(lambda v: min(255, int(v * 1.04)))
    b = b.point(lambda v: min(255, int(v * 0.97)))
    rgb = Image.merge("RGB", (r, g, b))

    noise = Image.effect_noise(rgb.size, 12).convert("RGB")
    rgb = Image.blend(rgb, ImageChops.overlay(rgb, noise), 0.11)

    from common import vignette
    rgb = vignette(rgb, 0.42)
    return rgb.resize(size, Image.LANCZOS)


def _feather(img: Image.Image, left: int = 470, top: int = 300) -> Image.Image:
    """Fade the top and left edges so the still melts into the menu art."""
    w, h = img.size
    hgrad = Image.new("L", (w, 1))
    for x in range(w):
        hgrad.putpixel((x, 0), int(255 * min(1.0, x / left) ** 2.1))
    vgrad = Image.new("L", (1, h))
    for y in range(h):
        vgrad.putpixel((0, y), int(255 * min(1.0, y / top) ** 2.1))
    mask = ImageChops.multiply(hgrad.resize((w, h), Image.BILINEAR),
                               vgrad.resize((w, h), Image.BILINEAR))
    out = img.copy()
    out.putalpha(ImageChops.multiply(img.getchannel("A"), mask))
    return out


def _layers():
    """Backdrop, face and hand as separate plates, so they can be animated."""
    backdrop = _hero_backdrop()
    face = Image.new("RGBA", HERO_CANVAS, (0, 0, 0, 0))
    _hero_face(face)
    hand = Image.new("RGBA", HERO_CANVAS, (0, 0, 0, 0))
    _hero_hand(hand)
    return backdrop, face, hand


def build_hero() -> Image.Image:
    backdrop, face, hand = _layers()
    canvas = backdrop.copy()
    canvas.alpha_composite(face)
    canvas.alpha_composite(hand)
    _hero_contact(canvas)
    return _feather(_hero_finish(canvas).convert("RGBA"))


# ---------------------------------------------------------------- replay
# A slap landing in motion: the hand drives in, the cheek compresses, the head
# snaps back, and - for a knockdown - the head keeps going down out of frame.
REPLAY_W, REPLAY_H = 960, 512
REPLAY_COLS = 4

FACE_PIVOT = (980, 640)
HAND_PIVOT = (330, 520)

# hand_dx, hand_angle, face_angle, face_dx, face_dy, contact, dark
REPLAY_HIT = [
    (-780, -0.12, 0.00, 0, 0, 0.00, 0.00),
    (-560, -0.10, 0.00, 0, 0, 0.00, 0.00),
    (-340, -0.06, 0.00, 0, 0, 0.00, 0.00),
    (-170, -0.02, 0.00, 0, 0, 0.10, 0.00),
    (-50, 0.00, -0.05, 8, 3, 0.85, 0.00),
    (12, 0.03, -0.12, 28, 12, 1.00, 0.00),
    (36, 0.05, -0.16, 46, 20, 0.70, 0.02),
    (48, 0.07, -0.18, 54, 24, 0.40, 0.05),
]

REPLAY_KO = REPLAY_HIT[:6] + [
    (36, 0.05, -0.22, 60, 34, 0.55, 0.06),
    (26, 0.04, -0.34, 96, 108, 0.30, 0.14),
    (10, 0.02, -0.52, 150, 250, 0.10, 0.30),
    (0, 0.00, -0.66, 190, 380, 0.00, 0.48),
]


def _plate(layer, angle=0.0, pivot=(0, 0), offset=(0, 0)):
    out = layer
    if abs(angle) > 1e-6:
        out = out.rotate(angle, center=(pivot[0] * SS, pivot[1] * SS), resample=Image.BICUBIC)
    dx, dy = int(offset[0] * SS), int(offset[1] * SS)
    if dx or dy:
        moved = Image.new("RGBA", layer.size, (0, 0, 0, 0))
        moved.paste(out, (dx, dy), out)
        out = moved
    return out


def build_replay(knockdown: bool = False) -> Image.Image:
    backdrop, face, hand = _layers()
    plan = REPLAY_KO if knockdown else REPLAY_HIT
    rows = (len(plan) + REPLAY_COLS - 1) // REPLAY_COLS
    strip = Image.new("RGBA", (REPLAY_W * REPLAY_COLS, REPLAY_H * rows), (0, 0, 0, 0))

    for index, (hx, ha, fa, fx, fy, contact, dark) in enumerate(plan):
        canvas = backdrop.copy()
        canvas.alpha_composite(_plate(face, fa, FACE_PIVOT, (fx, fy)))
        canvas.alpha_composite(_plate(hand, ha, HAND_PIVOT, (hx, 0)))
        _hero_contact(canvas, contact, seed=40 + index)
        frame = _hero_finish(canvas, (REPLAY_W, REPLAY_H)).convert("RGBA")
        if dark > 0:
            shade = Image.new("RGBA", frame.size, (4, 5, 9, int(255 * dark)))
            frame = Image.alpha_composite(frame, shade)
        strip.alpha_composite(frame, ((index % REPLAY_COLS) * REPLAY_W,
                                      (index // REPLAY_COLS) * REPLAY_H))
    return strip

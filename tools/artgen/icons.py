"""Application icons and launch screens for every platform head.

The icon has one job: be readable at 40 px on a home screen. So it is a single
bold mark - an open hand, ink-outlined, mid-slap - on the game's navy, with a
gold burst behind it. No wordmark and no thin detail, because both turn to mud
at the small sizes iOS keeps in the icon cache.

iOS also gets a launch screen built from the wordmark, which is the only place
the name is big enough to read.
"""
from __future__ import annotations

import json
import math
import os
import shutil
import subprocess

from PIL import Image, ImageChops, ImageDraw, ImageFilter

from common import hex_rgb, radial_glow, vertical_gradient
from arena import build_logo

IMPACT = "/System/Library/Fonts/Supplemental/Impact.ttf"

INK = "#12151f"

# Launch screen background. Matches the game's darkest backdrop so the hand-off
# from the OS splash to the title screen has no visible step.
SPLASH_BG = "#0d1526"


def _capsules(size: float):
    """Capsule list for a spread open hand inside a size x size box.

    The fingers fan out from the palm so the gaps between the tips stay at least
    a third of a finger wide. Anything tighter and the mark reads as a mitten
    once it is downscaled to a home-screen icon.
    """
    shapes = [
        # Palm, a fat stadium under the finger roots.
        ((0.500, 0.600), (0.500, 0.780), 0.380),
        # Index, middle, ring, little.
        ((0.378, 0.640), (0.310, 0.300), 0.104),
        ((0.468, 0.615), (0.452, 0.228), 0.110),
        ((0.556, 0.620), (0.604, 0.258), 0.104),
        ((0.640, 0.655), (0.712, 0.372), 0.090),
        # Thumb, out to the left and low.
        ((0.352, 0.730), (0.152, 0.610), 0.132),
    ]
    return [((a[0] * size, a[1] * size), (b[0] * size, b[1] * size), w * size)
            for a, b, w in shapes]


def _paint_capsules(draw: ImageDraw.ImageDraw, caps, grow: float = 0.0) -> None:
    for (x0, y0), (x1, y1), w in caps:
        w += grow
        r = w / 2
        draw.line([(x0, y0), (x1, y1)], fill=255, width=max(1, int(round(w))))
        for x, y in ((x0, y0), (x1, y1)):
            draw.ellipse([x - r, y - r, x + r, y + r], fill=255)


def _fit_hand(size: int, scale: float, rotation: float = -7.0) -> tuple[Image.Image, Image.Image]:
    """Hand masks trimmed to their own bounds, so the mark is optically centred."""
    ss = 3
    big = size * ss
    ink = Image.new("L", (big, big), 0)
    _paint_capsules(ImageDraw.Draw(ink), _capsules(big), grow=0.016 * size * 2 * ss)
    body = Image.new("L", (big, big), 0)
    _paint_capsules(ImageDraw.Draw(body), _capsules(big))

    if rotation:
        centre = (0.48 * big, 0.56 * big)
        ink = ink.rotate(rotation, resample=Image.BICUBIC, center=centre)
        body = body.rotate(rotation, resample=Image.BICUBIC, center=centre)

    box = ink.getbbox()
    ink = ink.crop(box)
    body = body.crop(box)

    target = int(size * scale)
    ratio = target / max(ink.width, ink.height)
    dims = (max(1, round(ink.width * ratio)), max(1, round(ink.height * ratio)))
    ink = ink.resize(dims, Image.LANCZOS)
    body = body.resize(dims, Image.LANCZOS)

    canvas_ink = Image.new("L", (size, size), 0)
    canvas_body = Image.new("L", (size, size), 0)
    pos = ((size - dims[0]) // 2, (size - dims[1]) // 2)
    canvas_ink.paste(ink, pos)
    canvas_body.paste(body, pos)
    return canvas_ink, canvas_body


def _background(size: int, burst: bool = True) -> Image.Image:
    base = radial_glow((size, size), "#28406f", "#080d1a",
                       centre=(size * 0.50, size * 0.42),
                       radius=size * 0.78, power=1.7)
    img = base.convert("RGB")

    if burst:
        layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        cx, cy = size * 0.50, size * 0.52
        rays = 16
        for i in range(rays):
            a = 2 * math.pi * i / rays
            half = (2 * math.pi / rays) * 0.30
            r0, r1 = size * 0.20, size * (0.70 if i % 2 == 0 else 0.56)
            d.polygon([(cx + math.cos(a - half) * r0, cy + math.sin(a - half) * r0),
                       (cx + math.cos(a) * r1, cy + math.sin(a) * r1),
                       (cx + math.cos(a + half) * r0, cy + math.sin(a + half) * r0)],
                      fill=(245, 197, 66, 58))
        img = Image.alpha_composite(img.convert("RGBA"), layer).convert("RGB")

    # Warm pool of light directly behind the hand so the gold reads as lit.
    glow = radial_glow((size, size), "#a5762a", "#080d1a",
                       centre=(size * 0.50, size * 0.58),
                       radius=size * 0.52, power=2.4).convert("RGBA")
    glow.putalpha(150)
    img = Image.alpha_composite(img.convert("RGBA"), glow).convert("RGB")
    return img


def _compose_hand(img: Image.Image, ink: Image.Image, body: Image.Image) -> Image.Image:
    size = img.size[0]
    img = img.convert("RGBA")

    # Drop shadow: the ink silhouette, blurred and nudged down, so the hand sits
    # on the burst rather than floating in front of it.
    shadow = ink.filter(ImageFilter.GaussianBlur(size * 0.022))
    shadow_layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    shadow_layer.paste((4, 6, 12, 140), (0, 0), shadow)
    shadow_layer = shadow_layer.transform(img.size, Image.AFFINE,
                                          (1, 0, 0, 0, 1, -size * 0.02),
                                          resample=Image.BILINEAR)
    img = Image.alpha_composite(img, shadow_layer)

    ink_layer = Image.new("RGBA", img.size, hex_rgb(INK) + (255,))
    ink_layer.putalpha(ink)
    img = Image.alpha_composite(img, ink_layer)

    grad = vertical_gradient(img.size, "#fff3cf", "#dfa03a").convert("RGBA")
    body_layer = grad.copy()
    body_layer.putalpha(body)
    img = Image.alpha_composite(img, body_layer)

    # Gloss: a broad light wash from the upper left, clipped to the hand.
    gloss = Image.new("RGBA", img.size, (255, 252, 240, 255))
    gloss.putalpha(body)
    gloss.putalpha(Image.eval(gloss.getchannel("A"), lambda v: int(v * 0.16)))
    img = Image.alpha_composite(img, gloss)

    # Occlusion along the bottom edge, so the hand has weight.
    edge = ImageChops.subtract(body, ImageChops.offset(body, 0, -int(size * 0.035)))
    edge = edge.filter(ImageFilter.GaussianBlur(size * 0.02))
    dark = Image.new("RGBA", img.size, (120, 62, 8, 255))
    dark.putalpha(edge.point(lambda v: int(v * 0.55)))
    return Image.alpha_composite(img, dark).convert("RGB")


def _mark(size: int, scale: float = 0.94) -> Image.Image:
    """The hand alone on transparency, for adaptive icons and splash art."""
    ink, body = _fit_hand(size, scale)
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ink_layer = Image.new("RGBA", (size, size), hex_rgb(INK) + (255,))
    ink_layer.putalpha(ink)
    layer = Image.alpha_composite(layer, ink_layer)
    grad = vertical_gradient((size, size), "#fff3cf", "#dfa03a").convert("RGBA")
    grad.putalpha(body)
    return Image.alpha_composite(layer, grad)


def _master(size: int = 1024) -> Image.Image:
    """Full-bleed opaque icon. iOS masks the corners itself, so none are baked in."""
    ink, body = _fit_hand(size, 0.78)
    return _compose_hand(_background(size), ink, body)


def _mac_master(master: Image.Image, size: int = 1024) -> Image.Image:
    """macOS keeps its rounded square with a margin around the artwork."""
    art = int(size * 0.82)
    body = master.resize((art, art), Image.LANCZOS).convert("RGBA")
    mask = Image.new("L", (art, art), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, art - 1, art - 1],
                                           radius=int(art * 0.225), fill=255)
    body.putalpha(mask)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    pad = (size - art) // 2
    out.alpha_composite(body, (pad, pad))
    return out


def _write_ico(master: Image.Image, path: str) -> None:
    sizes = [16, 24, 32, 48, 64, 128, 256]
    images = [master.resize((s, s), Image.LANCZOS) for s in sizes]
    images[-1].save(path, format="ICO", sizes=[(s, s) for s in sizes], append_images=images[:-1])


def _write_icns(mac: Image.Image, path: str) -> None:
    iconset = os.path.join(os.path.dirname(path), "SuperSlapper.iconset")
    if os.path.isdir(iconset):
        shutil.rmtree(iconset)
    os.makedirs(iconset)
    specs = [(16, "icon_16x16"), (32, "icon_16x16@2x"), (32, "icon_32x32"), (64, "icon_32x32@2x"),
             (128, "icon_128x128"), (256, "icon_128x128@2x"), (256, "icon_256x256"),
             (512, "icon_256x256@2x"), (512, "icon_512x512"), (1024, "icon_512x512@2x")]
    for px, name in specs:
        mac.resize((px, px), Image.LANCZOS).save(os.path.join(iconset, name + ".png"))
    try:
        subprocess.run(["iconutil", "-c", "icns", iconset, "-o", path], check=True,
                       capture_output=True)
    except Exception as exc:  # pragma: no cover - only on macOS
        print("iconutil failed:", exc)
    finally:
        shutil.rmtree(iconset, ignore_errors=True)


def _android(master: Image.Image, root: str) -> None:
    densities = {"mdpi": 48, "hdpi": 72, "xhdpi": 96, "xxhdpi": 144, "xxxhdpi": 192}
    for density, px in densities.items():
        folder = os.path.join(root, f"mipmap-{density}")
        os.makedirs(folder, exist_ok=True)
        master.resize((px, px), Image.LANCZOS).save(os.path.join(folder, "ic_launcher.png"))
        # Adaptive foreground is a 108dp layer whose safe zone is the middle
        # 66dp, so the mark is drawn at roughly 60% of the canvas.
        fg_px = int(round(px * 2.25))
        fg = Image.new("RGBA", (fg_px, fg_px), (0, 0, 0, 0))
        mark = _mark(int(fg_px * 0.60))
        fg.alpha_composite(mark, ((fg_px - mark.width) // 2, (fg_px - mark.height) // 2))
        fg.save(os.path.join(folder, "ic_launcher_foreground.png"))

    anydpi = os.path.join(root, "mipmap-anydpi-v26")
    os.makedirs(anydpi, exist_ok=True)
    with open(os.path.join(anydpi, "ic_launcher.xml"), "w", encoding="utf-8") as f:
        f.write('<?xml version="1.0" encoding="utf-8"?>\n'
                '<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">\n'
                '    <background android:drawable="@color/ic_launcher_background" />\n'
                '    <foreground android:drawable="@mipmap/ic_launcher_foreground" />\n'
                '</adaptive-icon>\n')

    values = os.path.join(root, "values")
    os.makedirs(values, exist_ok=True)
    with open(os.path.join(values, "colors.xml"), "w", encoding="utf-8") as f:
        f.write('<?xml version="1.0" encoding="utf-8"?>\n<resources>\n'
                '    <color name="ic_launcher_background">#152140</color>\n'
                f'    <color name="splash_background">{SPLASH_BG}</color>\n'
                '</resources>\n')


def _android_splash(root: str) -> None:
    folder = os.path.join(root, "drawable-nodpi")
    os.makedirs(folder, exist_ok=True)
    # Android 12 crops the splash icon to a 192dp circle, so this is the mark
    # alone on transparency - a wordmark would be cut off at both ends.
    _mark(768, scale=0.86).save(os.path.join(folder, "splash_mark.png"), optimize=True)


def _ios(master: Image.Image, root: str) -> None:
    os.makedirs(root, exist_ok=True)
    # Single-size app icon (iOS 18+): one 1024 master that the system derives
    # every other size from. The old per-size rack leaves unassigned children in
    # actool once the legacy 1x slots are gone, so it is simply not needed.
    for stale in os.listdir(root):
        if stale.startswith("icon_") and stale.endswith(".png"):
            os.remove(os.path.join(root, stale))

    filename = "icon_1024x1024_1x.png"
    # App Store validation rejects an alpha channel, and iOS draws its own
    # corners, so the master is written as opaque RGB.
    master.convert("RGB").save(os.path.join(root, filename), optimize=True)

    entry = {
        "filename": filename,
        "idiom": "universal",
        "platform": "ios",
        "size": "1024x1024",
    }
    with open(os.path.join(root, "Contents.json"), "w", encoding="utf-8") as f:
        json.dump({"images": [entry], "info": {"version": 1, "author": "xcode"}}, f, indent=2)


def _ios_splash(assets: str) -> None:
    """Launch-screen image set, referenced by LaunchScreen.storyboard."""
    logo = build_logo(1800)
    set_dir = os.path.join(assets, "LaunchLogo.imageset")
    os.makedirs(set_dir, exist_ok=True)
    images = []
    for scale, px in (("1x", 600), ("2x", 1200), ("3x", 1800)):
        name = f"launch_logo_{scale}.png"
        height = max(1, round(logo.height * px / logo.width))
        logo.resize((px, height), Image.LANCZOS).save(os.path.join(set_dir, name),
                                                      optimize=True)
        images.append({"filename": name, "idiom": "universal", "scale": scale})
    with open(os.path.join(set_dir, "Contents.json"), "w", encoding="utf-8") as f:
        json.dump({"images": images, "info": {"version": 1, "author": "xcode"}}, f, indent=2)


def _macos_appicon(mac: Image.Image, root: str) -> None:
    os.makedirs(root, exist_ok=True)
    specs = [(16, "1x"), (32, "2x"), (32, "1x"), (64, "2x"), (128, "1x"), (256, "2x"),
             (256, "1x"), (512, "2x"), (512, "1x"), (1024, "2x")]
    images = []
    for px, scale in specs:
        base = px // (2 if scale == "2x" else 1)
        filename = f"appicon_{base}x{base}{'@2x' if scale == '2x' else ''}.png"
        mac.resize((px, px), Image.LANCZOS).save(os.path.join(root, filename))
        images.append({
            "filename": filename,
            "idiom": "mac",
            "scale": scale,
            "size": f"{base}x{base}",
        })
    with open(os.path.join(root, "Contents.json"), "w", encoding="utf-8") as f:
        json.dump({"images": images, "info": {"version": 1, "author": "xcode"}}, f, indent=2)


def build_all(content_dir: str, android_res: str, ios_assets: str, mac_assets: str,
              windows_dir: str) -> None:
    master = _master(1024)
    mac = _mac_master(master)
    art = os.path.join(content_dir, "art")
    os.makedirs(art, exist_ok=True)
    master.save(os.path.join(art, "appicon_1024.png"), optimize=True)
    master.resize((512, 512), Image.LANCZOS).save(os.path.join(art, "appicon_512.png"),
                                                 optimize=True)
    master.resize((128, 128), Image.LANCZOS).save(os.path.join(art, "appicon_128.png"),
                                                 optimize=True)

    os.makedirs(windows_dir, exist_ok=True)
    _write_ico(master, os.path.join(windows_dir, "Icon.ico"))
    _write_icns(mac, os.path.join(mac_assets, "SuperSlapper.icns"))
    _android(master, android_res)
    _android_splash(android_res)
    _ios(master, os.path.join(ios_assets, "AppIcon.appiconset"))
    _ios_splash(ios_assets)
    _macos_appicon(mac, os.path.join(mac_assets, "AppIcon.appiconset"))

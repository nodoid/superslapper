"""Fighter sprite sheets in the soft-lit, painted style.

Same 8x2 layout and pose skeletons as fighters.py, so the fight engine drives
them unchanged. Anatomy is painted into the existing silhouette with layered
light, contour and rim; the face carries brow, cheekbone, nose and lips, and the
hands have knuckles and nails. Readable at fight scale (about 750 px tall).
"""
from __future__ import annotations

import math

from PIL import Image, ImageDraw, ImageFilter

from common import ellipse_fn, mix
from fighters import FRAME_H, FRAME_W, FRAMES, POSES, Kit, _rot
from photoreal import SS, _spline, capsule_pts, paint_form, paint_wash, put_spec

CANVAS = (FRAME_W * SS, FRAME_H * SS)
KEY_DIR = (0.42, -0.91)


def _lean(pose):
    lean = pose["lean"] * math.pi / 180.0
    if abs(lean) < 1e-6:
        return pose
    pivot = pose["hip"]
    lifted = dict(pose)
    for key in ("shoulder", "elbow", "hand", "head"):
        lifted[key] = _rot(pose[key], pivot, lean)
    lifted["head_tilt"] = pose["head_tilt"] + lean
    return lifted


class SoftFighter:
    def __init__(self, kit: Kit):
        self.kit = kit

    # ------------------------------------------------------------------ draw
    def frame(self, name: str) -> Image.Image:
        pose = _lean(POSES[name])
        c = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
        if pose["legs"] == "down":
            self._ko(c)
        else:
            self._floor(c)
            self._far_leg(c, pose)
            self._far_arm(c, pose)
            self._near_leg(c, pose)
            self._torso(c, pose)
            self._head(c, pose)
            self._near_arm(c, pose)
            self._sheen(c, pose)
        return c.resize((FRAME_W, FRAME_H), Image.LANCZOS)

    def _floor(self, c):
        paint_wash(c, ellipse_fn(216, 508, 96, 20), "#05070c", alpha=155, blur=9, pad=24)

    # ----------------------------------------------------------------- limbs
    def _limb(self, c, p0, p1, r0, r1, colour, rim=0.45, core=0.5, pad=14):
        paint_form(c, capsule_pts(p0, p1, r0, r1), colour, light=KEY_DIR,
                   contrast=0.72, rim=rim, core=core, blur=0.6, pad=pad, contour=0.55)

    def _near_leg(self, c, pose):
        crouch = pose["crouch"]
        hip = (pose["hip"][0] + 16, pose["hip"][1] + 4)
        knee = (hip[0] + 8, hip[1] + 62 - crouch * 0.4)
        ankle = (knee[0] + 4, 496 - crouch * 0.4)
        self._leg(c, hip, knee, ankle, self.kit.skin, self.kit.trunks)

    def _far_leg(self, c, pose):
        crouch = pose["crouch"]
        hip = (pose["hip"][0] - 24, pose["hip"][1] + 4)
        knee = (hip[0] - 26 - crouch * 0.3, hip[1] + 60 - crouch * 0.4)
        ankle = (knee[0] - 22, 496 - crouch * 0.4)
        self._leg(c, hip, knee, ankle, mix(self.kit.skin, "#1d0f08", 0.42), self.kit.dark)

    def _leg(self, c, hip, knee, ankle, skin, trunks):
        thigh = capsule_pts(hip, knee, 31, 24)
        paint_form(c, thigh, trunks, light=KEY_DIR, contrast=0.62, rim=0.3, core=0.5,
                   blur=0.6, pad=16, contour=0.5)
        self._limb(c, knee, ankle, 23, 15, skin)
        self._limb(c, ankle, (ankle[0] + 32, ankle[1] + 4), 16, 13, mix(skin, "#000000", 0.12))
        paint_wash(c, capsule_pts((hip[0] - 10, hip[1] + 14), (knee[0] - 4, knee[1] - 10), 9, 5),
                   mix(skin, "#2a1008", 0.45), alpha=70, blur=6, pad=14)
        paint_form(c, ellipse_fn(knee[0] + 2, knee[1] - 3, 15, 13), mix(skin, "#ffe6cc", 0.14),
                   light=KEY_DIR, contrast=0.5, rim=0.2, core=0.3, blur=1.1, pad=10)
        paint_wash(c, capsule_pts((knee[0] - 6, knee[1] + 10), (knee[0] + 12, knee[1] + 12), 4, 2.4),
                   mix(skin, "#2a1008", 0.5), alpha=100, blur=3, pad=10)
        paint_form(c, ellipse_fn(knee[0] - 6, knee[1] + 26, 13, 18), mix(skin, "#ffffff", 0.05),
                   light=KEY_DIR, contrast=0.55, rim=0.22, core=0.32, blur=1.2, pad=12)
        paint_wash(c, capsule_pts((knee[0] + 6, knee[1] + 28), (ankle[0] + 4, ankle[1] - 6), 5, 3),
                   mix(skin, "#2a1008", 0.42), alpha=80, blur=4, pad=12)
        put_spec(c, (knee[0] + 7, (knee[1] + ankle[1]) / 2), 5, 18, strength=90)
        for i in range(5):
            paint_form(c, ellipse_fn(ankle[0] + 22 + i * 2, ankle[1] + 8 - i * 1.2,
                                     4.2 - i * 0.4, 3.2 - i * 0.25), mix(skin, "#ffffff", 0.04),
                       light=KEY_DIR, contrast=0.45, rim=0.18, core=0.25, blur=0.4, pad=6)

    def _arm(self, c, shoulder, elbow, hand, skin, scale=1.0, spread=1.0):
        self._limb(c, shoulder, elbow, 25 * scale, 19 * scale, skin)
        self._limb(c, elbow, hand, 18 * scale, 13 * scale, skin)
        mid = ((shoulder[0] + elbow[0]) / 2, (shoulder[1] + elbow[1]) / 2)
        paint_form(c, ellipse_fn(mid[0] + 3 * scale, mid[1] - 2 * scale,
                                 11 * scale, 15 * scale), mix(skin, "#ffffff", 0.07),
                   light=KEY_DIR, contrast=0.5, rim=0.2, core=0.28, blur=1.2, pad=12)
        paint_wash(c, capsule_pts((mid[0] - 10 * scale, mid[1] - 6 * scale),
                                  (elbow[0] - 8 * scale, elbow[1] - 4 * scale),
                                  4.5 * scale, 2 * scale), mix(skin, "#20100a", 0.5),
                   alpha=90, blur=4, pad=12)
        paint_wash(c, capsule_pts((elbow[0] - 9 * scale, elbow[1] + 6 * scale),
                                  (elbow[0] + 9 * scale, elbow[1] + 9 * scale),
                                  3.6 * scale, 2 * scale), mix(skin, "#20100a", 0.5),
                   alpha=115, blur=3, pad=12)
        paint_form(c, ellipse_fn(elbow[0] + (hand[0] - elbow[0]) * 0.26,
                                 elbow[1] + (hand[1] - elbow[1]) * 0.26,
                                 10 * scale, 10 * scale), mix(skin, "#ffffff", 0.05),
                   light=KEY_DIR, contrast=0.5, rim=0.2, core=0.3, blur=1.1, pad=10)
        for offset in (-4, 0, 5):
            paint_wash(c, capsule_pts((hand[0] - 16 * scale, hand[1] - 4 * scale + offset),
                                      (hand[0] - 4 * scale, hand[1] + 1 * scale + offset),
                                      1.5 * scale, 1.1 * scale), mix(skin, "#ffd9bc", 0.18),
                       alpha=110, blur=2, pad=8)
        put_spec(c, (elbow[0] + 5 * scale, elbow[1] + (hand[1] - elbow[1]) * 0.4),
                 4 * scale, 13 * scale, strength=100)
        self._hand(c, hand, spread, scale)

    def _far_arm(self, c, pose):
        shoulder = (pose["shoulder"][0] - 50, pose["shoulder"][1] + 12)
        elbow = (shoulder[0] - 26, shoulder[1] + 56)
        hand = (elbow[0] - 16, elbow[1] + 46)
        self._arm(c, shoulder, elbow, hand, mix(self.kit.skin, "#2a1410", 0.40), 0.80, 0.5)

    def _near_arm(self, c, pose):
        self._arm(c, pose["shoulder"], pose["elbow"], pose["hand"], self.kit.skin, 1.0, 1.0)

    def _vest_strap(self, c, pose):
        """The tank top goes over the deltoid, so the kit reads as a vest."""
        sx, sy = pose["shoulder"]
        top = (sx - 8, sy)
        strap = _spline([(top[0] - 30, top[1] + 42), (top[0] - 4, top[1] + 26),
                         (sx + 16, sy - 2), (sx + 6, sy - 16),
                         (top[0] - 14, top[1] + 10), (top[0] - 34, top[1] + 26)], samples=6)
        paint_form(c, strap, self.kit.main, light=KEY_DIR, contrast=0.5, rim=0.35,
                   core=0.4, blur=0.7, pad=16, contour=0.5)
        paint_wash(c, _spline([(top[0] - 26, top[1] + 40), (top[0] + 2, top[1] + 24),
                               (sx + 10, sy - 2)], samples=6, closed=False),
                   mix(self.kit.main, "#000000", 0.5), alpha=80, blur=4, pad=16)

    def _hand(self, c, centre, spread=1.0, scale=1.0):
        cx, cy = centre
        skin = self.kit.skin
        lit = mix(skin, "#ffe3c8", 0.16)
        tone = mix(skin, "#000000", 0.02)

        palm = [(cx - 26 * scale, cy - 17 * scale), (cx + 6 * scale, cy - 25 * scale),
                (cx + 26 * scale, cy - 3 * scale), (cx + 19 * scale, cy + 19 * scale),
                (cx - 10 * scale, cy + 26 * scale), (cx - 29 * scale, cy + 10 * scale)]
        paint_form(c, _spline(palm, samples=6), tone, light=KEY_DIR, contrast=0.66,
                   rim=0.38, core=0.44, blur=0.5, pad=14, contour=0.48)
        paint_wash(c, ellipse_fn(cx - 13 * scale, cy + 9 * scale, 12 * scale, 9 * scale),
                   mix(tone, "#2a1008", 0.5), alpha=95, blur=5, pad=12)
        paint_wash(c, ellipse_fn(cx + 4 * scale, cy - 6 * scale, 11 * scale, 8 * scale),
                   mix(tone, "#2a1008", 0.35), alpha=60, blur=6, pad=12)

        base = (cx + 8 * scale, cy - 1 * scale)
        for i, ang in enumerate((-0.98, -0.44, 0.10, 0.64)):
            a = ang * spread
            length = (46 if i in (1, 2) else 38) * scale
            mid = (base[0] + math.cos(a) * length * 0.52,
                   base[1] + math.sin(a) * length * 0.48)
            tip = (base[0] + math.cos(a) * length, base[1] + math.sin(a) * length * 0.92)
            r0 = (8.4 - i * 0.5) * scale
            r1 = (7.2 - i * 0.45) * scale

            paint_form(c, capsule_pts(base, mid, r0, r1), tone, light=KEY_DIR, contrast=0.6,
                       rim=0.32, core=0.4, blur=0.45, pad=10, contour=0.42)
            paint_form(c, capsule_pts(mid, tip, r1, r1 * 0.78), tone, light=KEY_DIR,
                       contrast=0.6, rim=0.32, core=0.4, blur=0.45, pad=10, contour=0.42)
            paint_wash(c, ellipse_fn(mid[0], mid[1], 4.2 * scale, 3.2 * scale),
                       mix(tone, "#2a1008", 0.5), alpha=95, blur=2.4, pad=8)
            paint_wash(c, ellipse_fn(tip[0] + 1 * scale, tip[1] + 1 * scale,
                                     4 * scale, 3.2 * scale), mix(tone, "#c25a46", 0.45),
                       alpha=80, blur=2.4, pad=8)
            put_spec(c, (tip[0] - 1 * scale, tip[1] - 2 * scale), 2.2 * scale, 1.7 * scale,
                     strength=180)

        t0 = (cx - 8 * scale, cy + 4 * scale)
        t1 = (cx - 20 * scale, cy + 16 * scale)
        t2 = (cx - 27 * scale, cy + 26 * scale)
        paint_form(c, capsule_pts(t0, t1, 10 * scale, 8 * scale), lit, light=KEY_DIR,
                   contrast=0.6, rim=0.3, core=0.36, blur=0.5, pad=10, contour=0.4)
        paint_form(c, capsule_pts(t1, t2, 8 * scale, 6.4 * scale), lit, light=KEY_DIR,
                   contrast=0.6, rim=0.3, core=0.36, blur=0.5, pad=10, contour=0.4)
        paint_wash(c, ellipse_fn(t1[0], t1[1], 4 * scale, 3.2 * scale),
                   mix(tone, "#2a1008", 0.5), alpha=95, blur=2.2, pad=8)

        wrap = [(cx - 34 * scale, cy - 14 * scale), (cx - 14 * scale, cy - 20 * scale),
                (cx - 8 * scale, cy + 8 * scale), (cx - 28 * scale, cy + 16 * scale)]
        paint_form(c, _spline(wrap, samples=5), self.kit.trim, light=KEY_DIR, contrast=0.5,
                   rim=0.32, core=0.38, blur=0.5, pad=12, contour=0.45)
        paint_wash(c, capsule_pts((cx - 30 * scale, cy - 6 * scale),
                                  (cx - 12 * scale, cy + 2 * scale), 1.5, 1.1),
                   mix(self.kit.trim, "#000000", 0.55), alpha=110, blur=2, pad=8)

    # ----------------------------------------------------------------- torso
    def _torso(self, c, pose):
        hip = pose["hip"]
        shoulder = pose["shoulder"]
        top = (shoulder[0] - 8, shoulder[1])

        body = [
            (top[0] - 52, top[1] + 8), (top[0] - 66, top[1] + 46),
            (hip[0] - 44, hip[1] - 6), (hip[0] - 30, hip[1] + 16),
            (hip[0] + 30, hip[1] + 16), (hip[0] + 46, hip[1] - 6),
            (top[0] + 58, top[1] + 46), (top[0] + 44, top[1] + 4),
        ]
        paint_form(c, body, self.kit.skin, light=KEY_DIR, contrast=0.7, rim=0.4, core=0.5,
                   blur=0.8, pad=12, contour=0.5)

        # Clavicles, upper pec shelf and the sternum groove.
        paint_form(c, capsule_pts((top[0] - 6, top[1] + 14), (top[0] + 40, top[1] + 18), 4.4, 3.6),
                   mix(self.kit.skin, "#ffe6cc", 0.24), light=KEY_DIR, contrast=0.5, rim=0.2,
                   core=0.3, blur=0.5, pad=10)
        paint_form(c, capsule_pts((top[0] - 4, top[1] + 14), (top[0] - 38, top[1] + 20), 4, 3.2),
                   mix(self.kit.skin, "#ffe6cc", 0.18), light=KEY_DIR, contrast=0.5, rim=0.2,
                   core=0.3, blur=0.5, pad=10)
        paint_wash(c, capsule_pts((top[0] - 2, top[1] + 12), (top[0] + 4, top[1] + 50), 4.5, 3),
                   mix(self.kit.skin, "#2a1008", 0.5), alpha=100, blur=5, pad=12)
        for side in (1, -1):
            paint_wash(c, ellipse_fn(top[0] + side * 26, top[1] + 40, 22, 12),
                       mix(self.kit.skin, "#2a1008", 0.45), alpha=70, blur=6, pad=14)

        vest = [
            (top[0] - 52, top[1] + 14), (top[0] - 65, top[1] + 62),
            (hip[0] - 42, hip[1]), (hip[0] + 42, hip[1]),
            (top[0] + 55, top[1] + 64), (top[0] + 48, top[1] + 8),
            (top[0] + 18, top[1] + 6), (top[0] - 20, top[1] + 8),
        ]
        paint_form(c, vest, self.kit.main, light=KEY_DIR, contrast=0.55, rim=0.4, core=0.45,
                   blur=0.8, pad=12, contour=0.5)

        # Fabric: chest seam, waist folds, hem shadow, shoulder sheen.
        paint_wash(c, capsule_pts((top[0] + 2, top[1] + 16), (hip[0] - 4, hip[1] - 6), 2.2, 1.6),
                   mix(self.kit.main, "#000000", 0.5), alpha=110, blur=3, pad=10)
        for i, fy in enumerate((0.30, 0.48, 0.66, 0.84)):
            y = top[1] + (hip[1] - top[1]) * fy
            x = top[0] + (hip[0] - top[0]) * fy
            paint_wash(c, _spline([(x - 40, y), (x - 4, y + 6 + i), (x + 38, y - 2)],
                                  samples=6, closed=False),
                       mix(self.kit.main, "#000000", 0.5), alpha=70 - i * 5, blur=4, pad=12)
            paint_wash(c, _spline([(x - 32, y + 10), (x - 4, y + 15 + i), (x + 30, y + 7)],
                                  samples=6, closed=False),
                       mix(self.kit.main, "#ffffff", 0.45), alpha=38, blur=4, pad=12)
        paint_wash(c, capsule_pts((hip[0] - 40, hip[1]), (hip[0] + 40, hip[1] + 2), 6, 3.5),
                   mix(self.kit.main, "#000000", 0.5), alpha=120, blur=4, pad=12)
        paint_wash(c, capsule_pts((hip[0] - 28, hip[1] - 62), (hip[0] - 12, hip[1] - 6), 4, 3),
                   mix(self.kit.main, "#000000", 0.5), alpha=85, blur=4, pad=14)
        put_spec(c, (hip[0] - 4, hip[1] - 86), 14, 26, strength=60,
                 colour=mix(self.kit.main, "#ffffff", 0.6))

        trunks = [(hip[0] - 46, hip[1] - 2), (hip[0] + 46, hip[1] - 2),
                  (hip[0] + 40, hip[1] + 54), (hip[0] - 40, hip[1] + 54)]
        paint_form(c, trunks, self.kit.trunks, light=KEY_DIR, contrast=0.5, rim=0.3, core=0.4,
                   blur=0.7, pad=12, contour=0.5)
        if self.kit.trunks_pattern == "stripe":
            paint_wash(c, [(hip[0] - 46, hip[1] + 12), (hip[0] + 46, hip[1] + 12),
                           (hip[0] + 45, hip[1] + 24), (hip[0] - 45, hip[1] + 24)],
                       self.kit.trim, alpha=230, blur=1.0, pad=8)
        band = [(hip[0] - 44, hip[1] + 44), (hip[0] + 44, hip[1] + 44),
                (hip[0] + 41, hip[1] + 54), (hip[0] - 41, hip[1] + 54)]
        paint_form(c, band, mix(self.kit.trunks, "#ffffff", 0.30), light=KEY_DIR,
                   contrast=0.4, rim=0.25, core=0.3, blur=0.5, pad=10, contour=0.35)
        for i in range(2):
            paint_wash(c, _spline([(hip[0] - 36, hip[1] + 14 + i * 12),
                                   (hip[0] - 2, hip[1] + 19 + i * 12),
                                   (hip[0] + 32, hip[1] + 12 + i * 12)], samples=6, closed=False),
                       mix(self.kit.trunks, "#000000", 0.5), alpha=70, blur=4, pad=12)

    # ------------------------------------------------------------------ head
    def _head(self, c, pose):
        cx, cy = pose["head"]
        tilt = pose["head_tilt"]
        r = 74
        skin = self.kit.skin

        if pose.get("neck", True):
            neck = capsule_pts((cx - 2, cy + r - 10), (cx + 8, cy + r + 32), 28, 32)
            paint_form(c, neck, mix(skin, "#000000", 0.14), light=KEY_DIR, contrast=0.6, rim=0.2,
                       core=0.5, blur=0.7, pad=14, contour=0.45)
            paint_wash(c, ellipse_fn(cx + 6, cy + r + 4, 22, 11), "#3c1c10", alpha=100,
                       blur=6, pad=18)
            paint_wash(c, capsule_pts((cx - 16, cy + r - 6), (cx - 6, cy + r + 34), 4, 3),
                       "#ffe0c0", alpha=70, blur=4, pad=12)

        skull = []
        for i in range(64):
            t = 2 * math.pi * i / 64
            ux, uy = math.cos(t), math.sin(t)
            # Narrower than a ball, with a jaw that tapers to the chin.
            ux *= 0.86
            if uy > 0:
                ux *= 0.94
                uy *= 1.06
            if tilt:
                ca, sa = math.cos(tilt), math.sin(tilt)
                ux, uy = ux * ca - uy * sa, ux * sa + uy * ca
            skull.append((cx + ux * r, cy + uy * r * 0.98))
        paint_form(c, skull, skin, light=(0.42, -0.9), contrast=0.78, rim=0.6, core=0.55,
                   blur=0.8, pad=14, contour=0.55)

        # Cheekbone, jaw and temple.
        paint_wash(c, ellipse_fn(cx + 30, cy + 22, 24, 28, tilt), "#8f3f22", alpha=55, blur=9, pad=22)
        paint_wash(c, ellipse_fn(cx + 4, cy + 60, 38, 15, tilt), "#3d1a0c", alpha=80, blur=8, pad=22)
        paint_wash(c, ellipse_fn(cx + 34, cy - 28, 16, 12, tilt), "#6b3220", alpha=55, blur=8, pad=18)
        paint_form(c, ellipse_fn(cx + 34, cy + 14, 15, 11, tilt), "#ffe6cc",
                   light=KEY_DIR, contrast=0.4, rim=0, core=0.2, blur=1.5, pad=12)
        put_spec(c, (cx + 32, cy - 4), 13, 10, strength=100, rot=tilt)

        ear = ellipse_fn(cx - 42, cy + 6, 14, 18, tilt)
        paint_form(c, ear, mix(skin, "#c0603c", 0.2), light=(0.2, -0.95), contrast=0.6,
                   rim=0.25, core=0.4, blur=0.6, pad=14, contour=0.45)
        paint_wash(c, ellipse_fn(cx - 42, cy + 8, 6, 9, tilt), "#5a2414", alpha=100, blur=3, pad=12)
        paint_wash(c, ellipse_fn(cx - 40, cy + 16, 5, 5, tilt), "#c0654a", alpha=80, blur=3, pad=10)

        hair = []
        for i in range(40):
            t = math.pi + math.pi * i / 39
            ux, uy = math.cos(t), math.sin(t)
            if tilt:
                ca, sa = math.cos(tilt), math.sin(tilt)
                ux, uy = ux * ca - uy * sa, ux * sa + uy * ca
            hair.append((cx + ux * r * 0.99, cy + uy * r * 1.02 - 2))
        hair += [(cx + 60, cy - 8), (cx + 28, cy - 28), (cx - 22, cy - 16), (cx - 58, cy - 28)]
        paint_form(c, hair, self.kit.hair, light=(0.3, -0.95), contrast=0.5, rim=0.35,
                   core=0.4, blur=0.8, pad=14, contour=0.5)
        for i in range(6):
            x0 = cx - 48 + i * 13
            paint_wash(c, _spline([(x0, cy - 62 + abs(i - 3) * 3), (x0 + 7, cy - 44),
                                   (x0 + 9, cy - 30)], samples=5, closed=False),
                       mix(self.kit.hair, "#ffffff", 0.22), alpha=55, blur=3, pad=10)

        if self.kit.beard:
            paint_wash(c, ellipse_fn(cx + 16, cy + 50, 40, 24, tilt), mix(self.kit.hair, "#000000", 0.2),
                       alpha=70, blur=9, pad=20)

        self._face(c, pose, cx, cy, tilt)

    def _face(self, c, pose, cx, cy, tilt):
        skin = self.kit.skin
        hurt = pose["expression"] in ("hurt", "ko")
        angry = pose["brows"] == "angry"
        happy = pose["brows"] == "happy"

        eye_x, eye_y = cx + 32, cy - 6

        paint_wash(c, ellipse_fn(eye_x + 1, eye_y - 1, 19, 14, tilt), "#4a1e10",
                   alpha=85, blur=7, pad=16)

        if hurt:
            # One squeezed line with crow's feet; no watering, no tears.
            paint_wash(c, capsule_pts((eye_x - 13, eye_y + 2), (eye_x + 12, eye_y - 3), 3, 2),
                       "#241006", alpha=225, blur=2.2, pad=12)
            for i in range(2):
                paint_wash(c, capsule_pts((eye_x + 12, eye_y - 4 - i * 4),
                                          (eye_x + 21, eye_y - 8 - i * 5), 2, 1.4),
                           "#3d1a10", alpha=110, blur=2.4, pad=10)
        else:
            paint_form(c, ellipse_fn(eye_x, eye_y, 10.5, 8, tilt), "#efe6dc",
                       light=(0.3, -0.95), contrast=0.2, rim=0, core=0.25, blur=0.6, pad=10,
                       contour=0.3)
            paint_form(c, ellipse_fn(eye_x + 3, eye_y, 5.8, 6.6, tilt), "#4a2a16",
                       light=(0.3, -0.95), contrast=0.3, rim=0.2, core=0.3, blur=0.5, pad=8)
            paint_wash(c, ellipse_fn(eye_x + 3, eye_y, 2.7, 3.3, tilt), "#100a06", alpha=238,
                       blur=1.2, pad=8)
            put_spec(c, (eye_x + 5, eye_y - 2), 1.9, 1.9, strength=225)
            paint_wash(c, capsule_pts((eye_x - 4, eye_y + 8), (eye_x + 8, eye_y + 8), 2.2, 1.6),
                       "#5a2e1a", alpha=80, blur=2.4, pad=10)

        # Upper lid line and lashes.
        lid_y = eye_y - (0 if hurt else 7)
        paint_wash(c, capsule_pts((eye_x - 12, lid_y + 1), (eye_x, lid_y - 2),
                                  2.4, 1.8), "#241006", alpha=210, blur=2.2, pad=12)
        paint_wash(c, capsule_pts((eye_x, lid_y - 2), (eye_x + 11, lid_y + 1),
                                  2.2, 1.6), "#241006", alpha=190, blur=2.2, pad=12)

        # Brow: a thick angled mass with hair strokes over it.
        brow_y = eye_y - (13 if angry else 17)
        brow_lift = 10 if angry else 0
        paint_wash(c, capsule_pts((eye_x - 15, brow_y + (6 if angry else 0)),
                                  (eye_x + 13, brow_y - 4 + brow_lift),
                                  5.2, 3.2), mix(self.kit.hair, "#000000", 0.15),
                   alpha=215, blur=2.6, pad=12)
        for i in range(4):
            paint_wash(c, capsule_pts((eye_x - 12 + i * 7, brow_y + 3),
                                      (eye_x - 8 + i * 7, brow_y - 5 - i), 1.6, 1.1),
                       mix(self.kit.hair, "#ffffff", 0.18), alpha=80, blur=1.8, pad=8)
        if hurt:
            paint_wash(c, capsule_pts((eye_x - 14, brow_y + 6), (eye_x + 13, brow_y + 12),
                                      4.4, 2.6), "#241006", alpha=140, blur=2.6, pad=12)
        elif happy:
            paint_wash(c, ellipse_fn(eye_x + 4, eye_y + 7, 9, 4, tilt), "#c0654a",
                       alpha=70, blur=3, pad=10)

        # Nose: bridge, tip, wings and nostrils.
        paint_form(c, _spline([(cx + 38, cy - 22), (cx + 47, cy - 4),
                               (cx + 53, cy + 16), (cx + 63, cy + 22),
                               (cx + 55, cy + 29), (cx + 43, cy + 16)], samples=6),
                   mix(skin, "#ffffff", 0.06), light=KEY_DIR, contrast=0.55, rim=0.3,
                   core=0.35, blur=0.9, pad=12, contour=0.25)
        paint_wash(c, ellipse_fn(cx + 55, cy + 26, 9, 5, tilt), "#3a1608", alpha=125,
                   blur=3, pad=10)
        for side in (1, -1):
            paint_wash(c, ellipse_fn(cx + 54 + side * 7, cy + 25, 4, 3, tilt),
                       "#241008", alpha=200, blur=2, pad=8)
        put_spec(c, (cx + 57, cy + 9), 5, 9, strength=105, rot=tilt)

        # Mouth: upper lip, then teeth or a dark opening.
        mx, my = cx + 44, cy + 46
        paint_wash(c, capsule_pts((mx - 13, my), (mx + 12, my - 1), 2.6, 1.8),
                   "#8e3f2a", alpha=185, blur=2.4, pad=12)

        if pose["mouth"] in ("hurt", "open"):
            paint_wash(c, ellipse_fn(mx + 1, my + 8, 14, 10, tilt), "#3d0f10", alpha=245,
                       blur=2.6, pad=14)
            paint_wash(c, capsule_pts((mx - 9, my + 2), (mx + 11, my + 1), 2.6, 1.8),
                       "#f2e6d4", alpha=225, blur=1.2, pad=10)
            paint_form(c, ellipse_fn(mx + 2, my + 12, 8, 5, tilt), "#9a4136", light=(0.3, -0.95),
                       contrast=0.5, rim=0.15, core=0.2, blur=0.7, pad=10)
        elif pose["mouth"] == "grit":
            paint_wash(c, ellipse_fn(mx, my + 5, 12, 7, tilt), "#33100c", alpha=235, blur=2, pad=12)
            paint_wash(c, capsule_pts((mx - 9, my + 2), (mx + 10, my + 2), 3.6, 2.8),
                       "#f4ecdc", alpha=238, blur=1.2, pad=10)
            for i in range(4):
                paint_wash(c, capsule_pts((mx - 6 + i * 5, my), (mx - 6 + i * 5, my + 5),
                                          0.8, 0.8), "#8d7a62", alpha=140, blur=1, pad=6)
        else:
            paint_wash(c, capsule_pts((mx - 10, my + 5), (mx + 12, my + 3), 2.4, 1.8),
                       "#3d1a10", alpha=190, blur=2, pad=10)
            paint_wash(c, capsule_pts((mx - 4, my + 12), (mx + 9, my + 11), 3, 2.2),
                       "#b4564a", alpha=110, blur=3, pad=10)

        paint_wash(c, ellipse_fn(mx + 2, my + 22, 10, 4, tilt), "#5a2a18", alpha=85, blur=4, pad=10)
        paint_wash(c, ellipse_fn(cx + 44, cy + 30, 10, 12, tilt), "#8f3f22", alpha=45, blur=6, pad=14)

        if hurt:
            paint_wash(c, ellipse_fn(cx + 46, cy + 26, 21, 17, tilt), "#d0492f", alpha=95,
                       blur=8, pad=20)

    # --------------------------------------------------------------- effects
    def _sheen(self, c, pose):
        """Sweat where the work is: shoulder, brow, chest. Never by the eyes."""
        shoulder = pose["shoulder"]
        head = pose["head"]
        for i in range(4):
            put_spec(c, (shoulder[0] - 10 + i * 11, shoulder[1] - 12 + (i % 2) * 5),
                     2.1, 1.5, strength=110)
        for i in range(3):
            put_spec(c, (head[0] - 26 + i * 13, head[1] - 56 + (i % 2) * 5),
                     1.3, 1.7, strength=90)

    def _ko(self, c):
        """Flat on the canvas: head left, hips right, one knee up, one arm out."""
        skin = self.kit.skin
        paint_wash(c, ellipse_fn(232, 486, 168, 26), "#04060a", alpha=165, blur=12, pad=30)

        # Far leg bent up, knee in the air.
        self._leg(c, (286, 448), (330, 410), (356, 452),
                  mix(skin, "#1d0f08", 0.32), self.kit.dark)

        # Arm thrown back past the head.
        self._arm(c, (146, 442), (108, 404), (86, 384),
                  mix(skin, "#2a1410", 0.18), 0.8, 0.7)

        # Ribcage and waist lying across the canvas.
        torso = _spline([(116, 428), (152, 414), (222, 420), (286, 436),
                         (300, 466), (264, 486), (186, 486), (128, 466)], samples=6)
        paint_form(c, torso, skin, light=(0.34, -0.94), contrast=0.76, rim=0.42,
                   core=0.52, blur=0.7, pad=16, contour=0.5)
        paint_wash(c, ellipse_fn(196, 476, 72, 12), mix(skin, "#2a1008", 0.5), alpha=95,
                   blur=8, pad=18)
        paint_wash(c, capsule_pts((150, 442), (280, 452), 6, 4), mix(skin, "#2a1008", 0.4),
                   alpha=60, blur=6, pad=14)

        vest = _spline([(132, 424), (158, 412), (228, 418), (288, 434),
                        (292, 462), (244, 478), (176, 476), (126, 458)], samples=6)
        paint_form(c, vest, self.kit.main, light=(0.34, -0.94), contrast=0.56, rim=0.4,
                   core=0.42, blur=0.8, pad=14, contour=0.5)
        paint_wash(c, _spline([(146, 436), (206, 442), (272, 436)], samples=6, closed=False),
                   mix(self.kit.main, "#000000", 0.45), alpha=70, blur=5, pad=14)
        paint_wash(c, _spline([(146, 448), (206, 454), (272, 448)], samples=6, closed=False),
                   mix(self.kit.main, "#ffffff", 0.4), alpha=40, blur=4, pad=12)

        trunks = _spline([(268, 432), (310, 440), (316, 468), (288, 484), (262, 468)],
                         samples=6)
        paint_form(c, trunks, self.kit.trunks, light=(0.34, -0.94), contrast=0.52, rim=0.32,
                   core=0.4, blur=0.7, pad=12, contour=0.45)
        paint_wash(c, capsule_pts((268, 438), (306, 444), 3, 2),
                   mix(self.kit.trunks, "#000000", 0.5), alpha=150, blur=2.5, pad=10)

        # Near leg stretched out to the right.
        self._leg(c, (300, 462), (352, 480), (402, 470), skin, self.kit.trunks)

        # Near arm resting across the chest.
        self._arm(c, (158, 462), (212, 486), (256, 470), skin, 0.86, 0.75)

        # Head last, so the hair and jaw sit over the shoulders.
        self._head(c, {"head": (88, 428), "head_tilt": -0.20, "expression": "ko",
                       "mouth": "hurt", "brows": "hurt", "neck": False})
        put_spec(c, (196, 428), 26, 7, strength=45)
        put_spec(c, (250, 456), 18, 6, strength=40)


def build_sheet(kit: Kit) -> Image.Image:
    painter = SoftFighter(kit)
    sheet = Image.new("RGBA", (FRAME_W * 8, FRAME_H * 2), (0, 0, 0, 0))
    for index, name in enumerate(FRAMES):
        sheet.alpha_composite(painter.frame(name), ((index % 8) * FRAME_W, (index // 8) * FRAME_H))
    return sheet


def build_all(out_dir: str) -> dict[str, str]:
    from common import Palette
    import os

    os.makedirs(out_dir, exist_ok=True)
    kits = {
        "player": Kit("player", Palette.PLAYER, Palette.SKINS[1], "#241a12", "stripe"),
        "rival": Kit("rival", Palette.RIVAL, Palette.SKINS[0], "#3a2410", "solid", beard=True),
        "green": Kit("green", Palette.GREEN, Palette.SKINS[3], "#140f0b", "solid"),
        "champion": Kit("champion", Palette.CHAMPION, Palette.SKINS[4], "#1c1410", "stripe", beard=True),
    }
    paths = {}
    for name, kit in kits.items():
        path = os.path.join(out_dir, f"fighter_{name}.png")
        build_sheet(kit).save(path, optimize=True)
        paths[name] = path
    return paths

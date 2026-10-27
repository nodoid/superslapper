"""Character art: the slap fighters, referee and impact effects."""
from __future__ import annotations

import math

from PIL import Image, ImageDraw, ImageFilter

from common import (
    Palette, capsule, ellipse_fn, hex_rgb, mix, new_layer, poly_layer, shape_layer
)

FRAME_W, FRAME_H = 480, 540
COLS, ROWS = 8, 2

FRAMES = [
    "idle_a", "idle_b", "windup_a", "windup_b", "strike_a", "strike_b", "strike_c", "follow",
    "hit", "stagger", "ko", "victory", "brace", "taunt", "tired", "blank",
]


def _pose(**kw):
    base = dict(
        hip=(196, 356), shoulder=(238, 248), elbow=(292, 300), hand=(344, 336),
        head=(198, 152), head_tilt=0.0, expression="idle",
        legs="stand", lean=0.0, sway=0.0, speed=0.0, mouth="line",
        brows="neutral", crouch=0.0, arm_up=False,
    )
    base.update(kw)
    return base


POSES = {
    "idle_a": _pose(),
    "idle_b": _pose(hip=(196, 352), shoulder=(238, 244), head=(198, 148),
                    elbow=(290, 296), hand=(340, 330), expression="angry"),
    "windup_a": _pose(shoulder=(232, 250), elbow=(268, 214), hand=(292, 158),
                      lean=-4, mouth="line", expression="angry", brows="angry"),
    "windup_b": _pose(shoulder=(228, 252), elbow=(250, 200), hand=(246, 132),
                      hip=(190, 358), head=(190, 152), lean=-8, mouth="grit",
                      expression="angry", brows="angry", crouch=6),
    "strike_a": _pose(shoulder=(240, 246), elbow=(300, 250), hand=(348, 208),
                      lean=6, mouth="open", expression="angry", brows="angry", speed=1),
    "strike_b": _pose(shoulder=(246, 246), elbow=(312, 266), hand=(372, 244),
                      lean=10, mouth="open", expression="angry", brows="angry", speed=2,
                      head=(204, 154)),
    "strike_c": _pose(shoulder=(250, 250), elbow=(318, 286), hand=(388, 292),
                      lean=12, mouth="open", expression="angry", brows="angry", speed=3,
                      head=(208, 156), hip=(200, 358)),
    "follow": _pose(shoulder=(246, 256), elbow=(310, 316), hand=(360, 356),
                    lean=10, mouth="grit", expression="angry", brows="angry", speed=1,
                    head=(204, 158)),
    # Taking a slap: the arm has to fall away behind the body. An arm still
    # reaching forward reads as the fighter throwing a slap of their own, which
    # made both fighters look like they were slapping at the same instant.
    "hit": _pose(shoulder=(228, 258), elbow=(198, 322), hand=(188, 378),
                 head=(166, 144), head_tilt=-0.36, expression="hurt", mouth="hurt",
                 brows="hurt", lean=-14, legs="stagger"),
    "stagger": _pose(shoulder=(222, 264), elbow=(190, 330), hand=(178, 392),
                     head=(156, 156), head_tilt=-0.26, expression="hurt", mouth="hurt",
                     brows="hurt", lean=-20, legs="stagger", crouch=10),
    "ko": _pose(legs="down", expression="ko", mouth="hurt"),
    "victory": _pose(shoulder=(238, 240), elbow=(250, 170), hand=(232, 108),
                     head=(198, 150), expression="happy", mouth="open", brows="happy",
                     lean=-4, arm_up=True),
    "brace": _pose(shoulder=(234, 248), elbow=(280, 230), hand=(300, 176),
                   head=(194, 156), expression="angry", mouth="grit", brows="angry",
                   crouch=10, lean=4),
    "taunt": _pose(shoulder=(236, 248), elbow=(276, 214), hand=(318, 196),
                   head=(198, 148), expression="happy", mouth="open", brows="happy",
                   lean=-2),
    "tired": _pose(shoulder=(228, 262), elbow=(266, 316), hand=(286, 372),
                   head=(190, 164), expression="hurt", mouth="open", brows="hurt",
                   crouch=14, lean=-6),
    "blank": _pose(),
}


def _rot(pt, origin, angle):
    x, y = pt[0] - origin[0], pt[1] - origin[1]
    c, s = math.cos(angle), math.sin(angle)
    return (origin[0] + x * c - y * s, origin[1] + x * s + y * c)


class Kit:
    def __init__(self, name, colours, skin, hair, trunks_pattern="solid", beard=False):
        self.name = name
        self.main = colours["main"]
        self.dark = colours["dark"]
        self.trim = colours["trim"]
        self.trunks = colours["trunks"]
        self.skin = skin
        self.skin_dark = mix(skin, "#000000", 0.28)
        self.skin_light = mix(skin, "#ffffff", 0.35)
        self.hair = hair
        self.trunks_pattern = trunks_pattern
        self.beard = beard


class FighterPainter:
    def __init__(self, kit: Kit):
        self.kit = kit

    def draw(self, name: str) -> Image.Image:
        pose = self._lean_upper_body(POSES[name])
        img = Image.new("RGBA", (FRAME_W, FRAME_H), (0, 0, 0, 0))
        if pose["legs"] == "down":
            self._draw_ko(img)
            return img

        self._draw_shadow(img, pose)
        self._draw_far_leg(img, pose)
        self._draw_far_arm(img, pose)
        self._draw_near_leg(img, pose)
        self._draw_torso(img, pose)
        self._draw_head(img, pose)
        self._draw_near_arm(img, pose)
        if pose["speed"]:
            self._draw_speed_lines(img, pose)
        return img

    # ---------------------------------------------------------------- pieces
    @staticmethod
    def _lean_upper_body(pose):
        """Rotate the whole upper body about the hip so the lean reads as one motion."""
        lean = pose["lean"] * math.pi / 180.0
        if abs(lean) < 1e-6:
            return pose

        pivot = pose["hip"]
        lifted = dict(pose)
        for key in ("shoulder", "elbow", "hand", "head"):
            lifted[key] = _rot(pose[key], pivot, lean)
        lifted["head_tilt"] = pose["head_tilt"] + lean
        return lifted

    def _draw_shadow(self, img, pose):
        layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        d.ellipse([130, 488, 330, 532], fill=(0, 0, 0, 90))
        img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(6)))

    def _leg(self, img, hip, knee, ankle, fill):
        thigh = capsule(hip, knee, 30, 24)
        shin = capsule(knee, ankle, 24, 17)
        boot = capsule(ankle, (ankle[0] + 30, ankle[1] + 4), 19, 16)
        img.alpha_composite(poly_layer(img.size, thigh, fill, Palette.INK, 3))
        img.alpha_composite(poly_layer(img.size, shin, self.kit.skin, Palette.INK, 3))
        img.alpha_composite(poly_layer(img.size, boot, self.kit.trim, Palette.INK, 3))

    def _draw_near_leg(self, img, pose):
        crouch = pose["crouch"]
        hip = (pose["hip"][0] + 16, pose["hip"][1] + 4)
        knee = (hip[0] + 8, hip[1] + 62 - crouch * 0.4)
        ankle = (knee[0] + 4, 496 - crouch * 0.4)
        self._leg(img, hip, knee, ankle, self.kit.dark)

    def _draw_far_leg(self, img, pose):
        crouch = pose["crouch"]
        hip = (pose["hip"][0] - 24, pose["hip"][1] + 4)
        knee = (hip[0] - 26 - crouch * 0.3, hip[1] + 60 - crouch * 0.4)
        ankle = (knee[0] - 22, 496 - crouch * 0.4)
        self._leg(img, hip, knee, ankle, mix(self.kit.dark, "#000000", 0.35))

    def _draw_far_arm(self, img, pose):
        shoulder = (pose["shoulder"][0] - 52, pose["shoulder"][1] + 12)
        elbow = (shoulder[0] - 26, shoulder[1] + 56)
        hand = (elbow[0] - 16, elbow[1] + 46)
        img.alpha_composite(poly_layer(img.size, capsule(shoulder, elbow, 24, 20),
                                       mix(self.kit.main, "#000000", 0.3), Palette.INK, 3))
        img.alpha_composite(poly_layer(img.size, capsule(elbow, hand, 20, 16),
                                       mix(self.kit.skin, "#000000", 0.18), Palette.INK, 3))
        self._paste_hand(img, hand, spread=0.55, scale=0.8)

    def _draw_torso(self, img, pose):
        hip = pose["hip"]
        shoulder = pose["shoulder"]
        lean = pose["lean"] * math.pi / 180
        shoulder = _rot((shoulder[0], shoulder[1] + 40), hip, lean)
        shoulder = (shoulder[0], shoulder[1] - 40)

        top = (shoulder[0] - 8, shoulder[1])
        pts = [
            (top[0] - 52, top[1] + 8),
            (top[0] - 66, top[1] + 46),
            (hip[0] - 44, hip[1] - 6),
            (hip[0] - 30, hip[1] + 16),
            (hip[0] + 30, hip[1] + 16),
            (hip[0] + 46, hip[1] - 6),
            (top[0] + 62, top[1] + 50),
            (top[0] + 48, top[1] + 6),
        ]
        img.alpha_composite(poly_layer(img.size, pts, self.kit.skin, Palette.INK, 3))

        # Tank top over the torso.
        top_pts = [
            (top[0] - 50, top[1] + 26),
            (top[0] - 62, top[1] + 60),
            (hip[0] - 42, hip[1]),
            (hip[0] + 42, hip[1]),
            (top[0] + 58, top[1] + 64),
            (top[0] + 44, top[1] + 24),
            (top[0] + 18, top[1] + 46),
            (top[0] - 20, top[1] + 46),
        ]
        img.alpha_composite(poly_layer(img.size, top_pts, self.kit.main, Palette.INK, 3))

        # Trunks.
        trunk_pts = [
            (hip[0] - 46, hip[1] - 2),
            (hip[0] + 46, hip[1] - 2),
            (hip[0] + 40, hip[1] + 54),
            (hip[0] - 40, hip[1] + 54),
        ]
        img.alpha_composite(poly_layer(img.size, trunk_pts, self.kit.trunks, Palette.INK, 3))

        if self.kit.trunks_pattern == "stripe":
            stripe = [(hip[0] - 46, hip[1] + 10), (hip[0] + 46, hip[1] + 10),
                      (hip[0] + 45, hip[1] + 22), (hip[0] - 45, hip[1] + 22)]
            img.alpha_composite(poly_layer(img.size, stripe, self.kit.trim, None, 0))

        # Waistband highlight.
        band = [(hip[0] - 44, hip[1] + 44), (hip[0] + 44, hip[1] + 44),
                (hip[0] + 41, hip[1] + 54), (hip[0] - 41, hip[1] + 54)]
        img.alpha_composite(poly_layer(img.size, band, mix(self.kit.trunks, "#ffffff", 0.35), None, 0))

    def _draw_head(self, img, pose):
        cx, cy = pose["head"]
        tilt = pose["head_tilt"]
        r = 74
        layer = Image.new("RGBA", img.size, (0, 0, 0, 0))

        # Neck.
        img.alpha_composite(poly_layer(img.size, capsule((cx, cy + r - 6), (cx + 6, cy + r + 34), 30, 34),
                                       self.kit.skin_dark, Palette.INK, 3))

        # A single silhouette: round skull, heavier jaw at the front (facing +x).
        pts = []
        for i in range(72):
            t = 2 * math.pi * i / 72
            ux, uy = math.cos(t), math.sin(t)
            if uy > 0 and ux > 0:
                ux *= 1.04
                uy *= 1.12
            elif uy > 0:
                uy *= 0.96
                ux *= 0.92
            if tilt:
                c, s2 = math.cos(tilt), math.sin(tilt)
                ux, uy = ux * c - uy * s2, ux * s2 + uy * c
            pts.append((cx + ux * r * 0.94, cy + uy * r))
        layer.alpha_composite(shape_layer(img.size, pts, self.kit.skin, Palette.INK, 3))

        # Ear.
        ear_pts = ellipse_fn(cx - 42, cy + 6, 15, 20, tilt)
        layer.alpha_composite(poly_layer(img.size, ear_pts, self.kit.skin_dark, Palette.INK, 3))

        # Hair.
        hair_pts = ellipse_fn(cx - 2, cy - 30, r * 0.9, 44, tilt)
        hair_cut = [(cx - 76, cy - 40), (cx + 78, cy - 34), (cx + 66, cy - 8),
                    (cx + 30, cy - 26), (cx - 20, cy - 14), (cx - 62, cy - 26)]
        hair_layer, hdraw = new_layer(img.size)
        hdraw.polygon([(x, y) for x, y in hair_pts], fill=hex_rgb(self.kit.hair) + (255,))
        hdraw.polygon([(x, y) for x, y in hair_cut], fill=(0, 0, 0, 0))
        layer.alpha_composite(hair_layer)

        if self.kit.beard:
            beard = ellipse_fn(cx + 16, cy + 52, 46, 26, tilt)
            layer.alpha_composite(poly_layer(img.size, beard, self.kit.hair, None, 0))

        d = ImageDraw.Draw(layer)
        # Eye (side-on, facing right) with brow.
        eye_x, eye_y = cx + 34, cy - 4
        if pose["expression"] in ("ko", "hurt"):
            d.line([(eye_x - 12, eye_y), (eye_x + 10, eye_y)], fill=Palette.INK, width=5)
        else:
            d.ellipse([eye_x - 12, eye_y - 12, eye_x + 12, eye_y + 12], fill=Palette.WHITE,
                      outline=Palette.INK, width=3)
            d.ellipse([eye_x + 1, eye_y - 6, eye_x + 11, eye_y + 6], fill="#2b1d12")

        brow_lift = -14 if pose["brows"] == "angry" else -18
        if pose["brows"] == "hurt":
            d.line([(eye_x - 16, eye_y + brow_lift - 2), (eye_x + 14, eye_y + brow_lift + 8)],
                   fill=Palette.INK, width=8)
        elif pose["brows"] == "happy":
            d.arc([eye_x - 16, eye_y - 40, eye_x + 16, eye_y - 6], 200, 340, fill=Palette.INK, width=6)
        else:
            d.line([(eye_x - 16, eye_y + brow_lift), (eye_x + 14, eye_y + brow_lift - 6)],
                   fill=Palette.INK, width=8)

        # Nose.
        d.polygon([(cx + 52, cy + 4), (cx + 70, cy + 26), (cx + 48, cy + 28)],
                  fill=self.kit.skin_dark, outline=Palette.INK)

        # Mouth.
        mx, my = cx + 44, cy + 52
        if pose["mouth"] == "open":
            d.ellipse([mx - 12, my - 12, mx + 20, my + 18], fill="#5a1a1a", outline=Palette.INK, width=3)
            d.ellipse([mx - 2, my + 2, mx + 12, my + 14], fill="#c2554f")
        elif pose["mouth"] == "hurt":
            d.chord([mx - 14, my - 6, mx + 22, my + 26], 0, 180, fill="#5a1a1a", outline=Palette.INK, width=3)
        elif pose["mouth"] == "grit":
            d.rectangle([mx - 10, my, mx + 20, my + 14], fill=Palette.WHITE, outline=Palette.INK, width=3)
            for i in range(3):
                d.line([(mx - 10 + i * 10, my), (mx - 10 + i * 10, my + 14)], fill=Palette.INK, width=2)
        else:
            d.line([(mx - 8, my + 4), (mx + 18, my + 2)], fill=Palette.INK, width=5)

        # Cheek flush and jaw shadow.
        d.arc([cx + 6, cy + 10, cx + 66, cy + 62], 20, 120, fill=mix(self.kit.skin, "#e06a5a", 0.45), width=4)

        img.alpha_composite(layer)

    HAND_BOX = 210

    def _hand(self, centre, spread=1.0, scale=1.0, facing=1):
        """Open palm - four fingers and a thumb. This is the slap."""
        size = (self.HAND_BOX, self.HAND_BOX)
        img = Image.new("RGBA", size, (0, 0, 0, 0))
        cx, cy = size[0] / 2, size[1] / 2 + 6
        fingers = []
        base_angles = [-0.95, -0.35, 0.25, 0.8]
        for i, ang in enumerate(base_angles):
            a = ang * spread
            length = (52 if i in (1, 2) else 44) * scale
            tip = (cx + math.cos(a) * length * facing, cy + math.sin(a) * length * 0.9)
            fingers.append(capsule((cx, cy), tip, 13 * scale, 11 * scale))

        palm = ellipse_fn(cx, cy, 34 * scale, 30 * scale)
        img.alpha_composite(poly_layer(size, palm, self.kit.skin, Palette.INK, 3))
        for pts in fingers:
            img.alpha_composite(poly_layer(size, pts, self.kit.skin_light, Palette.INK, 3))
        thumb = capsule((cx - 6 * facing, cy - 4), (cx - 28 * facing, cy + 26), 14 * scale, 10 * scale)
        img.alpha_composite(poly_layer(size, thumb, self.kit.skin_light, Palette.INK, 3))

        # Wrist wrap.
        wrap = capsule((cx - 22 * facing, cy - 6), (cx - 40 * facing, cy + 8), 18 * scale, 16 * scale)
        img.alpha_composite(poly_layer(size, wrap, self.kit.trim, Palette.INK, 3))
        return img

    def _paste_hand(self, img, hand_pos, spread=1.0, scale=1.0, facing=1):
        glyph = self._hand(hand_pos, spread=spread, scale=scale, facing=facing)
        half = self.HAND_BOX / 2
        img.alpha_composite(glyph, (int(hand_pos[0] - half), int(hand_pos[1] - half - 6)))

    def _draw_near_arm(self, img, pose):
        shoulder = pose["shoulder"]
        elbow = pose["elbow"]
        hand = pose["hand"]
        img.alpha_composite(poly_layer(img.size, capsule(shoulder, elbow, 27, 21),
                                       self.kit.main, Palette.INK, 3))
        img.alpha_composite(poly_layer(img.size, capsule(elbow, hand, 21, 16),
                                       self.kit.skin, Palette.INK, 3))
        self._paste_hand(img, hand, spread=1.0, scale=1.0)

    def _draw_speed_lines(self, img, pose):
        layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        hx, hy = pose["hand"]
        for i in range(3):
            offset = 26 + i * 16
            d.arc([hx - 150 - offset, hy - 150, hx + 40, hy + 120],
                  300, 360, fill=(255, 255, 255, 150 - i * 35), width=4)
        img.alpha_composite(layer)

    def _draw_ko(self, img):
        layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        d.ellipse([40, 430, 360, 500], fill=(0, 0, 0, 110))
        img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(8)))

        # Body lying on the canvas, head towards the left.
        img.alpha_composite(poly_layer(img.size, capsule((110, 458), (240, 452), 30, 26),
                                       self.kit.skin, Palette.INK, 3))
        img.alpha_composite(poly_layer(img.size, capsule((150, 452), (300, 448), 34, 30),
                                       self.kit.main, Palette.INK, 3))
        # Legs.
        img.alpha_composite(poly_layer(img.size, capsule((292, 452), (352, 470), 26, 18),
                                       self.kit.dark, Palette.INK, 3))
        img.alpha_composite(poly_layer(img.size, capsule((296, 462), (350, 486), 22, 16),
                                       self.kit.skin, Palette.INK, 3))
        # Head.
        head = ellipse_fn(96, 448, 60, 56)
        img.alpha_composite(poly_layer(img.size, head, self.kit.skin, Palette.INK, 3))
        arm = capsule((180, 440), (110, 396), 18, 14)
        img.alpha_composite(poly_layer(img.size, arm, self.kit.skin_light, Palette.INK, 3))

        d = ImageDraw.Draw(img)
        d.line([(70, 442), (88, 442)], fill=Palette.INK, width=4)
        d.line([(100, 442), (118, 442)], fill=Palette.INK, width=4)
        d.chord([72, 462, 108, 484], 0, 180, fill="#5a1a1a", outline=Palette.INK, width=2)


def build_sheet(kit: Kit) -> Image.Image:
    painter = FighterPainter(kit)
    sheet = Image.new("RGBA", (FRAME_W * COLS, FRAME_H * ROWS), (0, 0, 0, 0))
    for index, name in enumerate(FRAMES):
        frame = painter.draw(name)
        sheet.alpha_composite(frame, ((index % COLS) * FRAME_W, (index // COLS) * FRAME_H))
    return sheet


REFEREE_FRAMES = 4
REF_W, REF_H = 340, 500


def build_referee() -> Image.Image:
    """A referee in black and white stripes: idle, point, count, hand raised."""
    sheet = Image.new("RGBA", (REF_W * REFEREE_FRAMES, REF_H), (0, 0, 0, 0))
    for i in range(REFEREE_FRAMES):
        img = Image.new("RGBA", (REF_W, REF_H), (0, 0, 0, 0))
        cx = 170
        # Legs
        img.alpha_composite(poly_layer(img.size, capsule((150, 380), (136, 480), 22, 16), "#20242e", Palette.INK, 3))
        img.alpha_composite(poly_layer(img.size, capsule((190, 380), (206, 480), 22, 16), "#20242e", Palette.INK, 3))
        # Torso
        torso = [(112, 250), (228, 250), (238, 386), (102, 386)]
        img.alpha_composite(poly_layer(img.size, torso, "#f2f4f8", Palette.INK, 3))
        for x in (128, 154, 180, 206):
            img.alpha_composite(poly_layer(img.size, [(x, 252), (x + 10, 252), (x + 10, 386), (x, 386)],
                                           "#20242e", None, 0))
        # Head
        img.alpha_composite(poly_layer(img.size, ellipse_fn(cx, 190, 52, 56), "#e8b98f", Palette.INK, 3))
        img.alpha_composite(poly_layer(img.size, ellipse_fn(cx, 150, 50, 30), "#2b2b33", None, 0))
        d = ImageDraw.Draw(img)
        d.ellipse([cx - 22, 182, cx - 8, 196], fill=Palette.INK)
        d.ellipse([cx + 8, 182, cx + 22, 196], fill=Palette.INK)
        d.line([(cx - 14, 222), (cx + 14, 222)], fill=Palette.INK, width=4)
        # Arms per frame
        if i == 0:
            img.alpha_composite(poly_layer(img.size, capsule((116, 266), (86, 340), 16, 13), "#e8b98f", Palette.INK, 3))
            img.alpha_composite(poly_layer(img.size, capsule((224, 266), (254, 340), 16, 13), "#e8b98f", Palette.INK, 3))
        elif i == 1:  # point
            img.alpha_composite(poly_layer(img.size, capsule((116, 266), (86, 340), 16, 13), "#e8b98f", Palette.INK, 3))
            img.alpha_composite(poly_layer(img.size, capsule((224, 266), (300, 240), 16, 13), "#e8b98f", Palette.INK, 3))
            d.line([(300, 240), (330, 234)], fill=Palette.INK, width=7)
        elif i == 2:  # counting, arm sweeping down
            img.alpha_composite(poly_layer(img.size, capsule((116, 266), (86, 340), 16, 13), "#e8b98f", Palette.INK, 3))
            img.alpha_composite(poly_layer(img.size, capsule((224, 266), (268, 320), 16, 13), "#e8b98f", Palette.INK, 3))
        else:  # hand raised
            img.alpha_composite(poly_layer(img.size, capsule((116, 266), (78, 330), 16, 13), "#e8b98f", Palette.INK, 3))
            img.alpha_composite(poly_layer(img.size, capsule((224, 266), (250, 140), 16, 14), "#e8b98f", Palette.INK, 3))
            d.ellipse([236, 108, 272, 146], fill="#e8b98f", outline=Palette.INK, width=3)

        sheet.alpha_composite(img, (i * REF_W, 0))
    return sheet


IMPACT_FRAMES = 5


def build_impact() -> Image.Image:
    """A cartoon slap burst that scales out over five frames."""
    size = 300
    sheet = Image.new("RGBA", (size * IMPACT_FRAMES, size), (0, 0, 0, 0))
    for i in range(IMPACT_FRAMES):
        img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        progress = (i + 1) / IMPACT_FRAMES
        radius = 30 + progress * 90
        alpha = int(255 * (1 - progress * 0.75))
        points = []
        for k in range(16):
            a = 2 * math.pi * k / 16
            r = radius * (1.0 if k % 2 == 0 else 0.55)
            points.append((size / 2 + math.cos(a) * r, size / 2 + math.sin(a) * r))
        d.polygon(points, fill=(255, 245, 200, alpha))
        d.polygon([(size / 2 + (x - size / 2) * 0.62, size / 2 + (y - size / 2) * 0.62) for x, y in points],
                  fill=(255, 255, 255, alpha))
        if i < 2:
            d.ellipse([size / 2 - 34, size / 2 - 34, size / 2 + 34, size / 2 + 34],
                      fill=(255, 255, 255, 255))
        sheet.alpha_composite(img, (i * size, 0))
    return sheet


def build_stars() -> Image.Image:
    """Dizzy stars for a stunned fighter."""
    size = 128
    sheet = Image.new("RGBA", (size * 4, size), (0, 0, 0, 0))
    for i in range(4):
        img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        rotation = i * math.pi / 8
        points = []
        for k in range(10):
            a = rotation + 2 * math.pi * k / 10
            r = 44 if k % 2 == 0 else 20
            points.append((size / 2 + math.cos(a) * r, size / 2 + math.sin(a) * r))
        d.polygon(points, fill=(255, 226, 92, 255), outline=(255, 255, 255, 255))
        sheet.alpha_composite(img, (i * size, 0))
    return sheet


def build_hand_closeup() -> Image.Image:
    """A huge open hand, drawn when a slap comes at the camera."""
    kit = Kit("closeup", Palette.RIVAL, Palette.SKINS[0], "#2b1d12")
    painter = FighterPainter(kit)
    hand = painter._hand((130, 130), spread=1.15, scale=1.5, facing=1)
    canvas = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    canvas.alpha_composite(hand.resize((512, 512), Image.LANCZOS))
    return canvas



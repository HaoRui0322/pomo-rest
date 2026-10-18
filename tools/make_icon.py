# -*- coding: utf-8 -*-
"""Generate the PomoRest application icon (rounded card + tomato)."""
import os
from PIL import Image, ImageDraw

SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]

BG = (20, 22, 30, 255)
CARD = (24, 27, 36, 255)
TOMATO = (255, 107, 87, 255)
TOMATO_DARK = (224, 82, 66, 255)
LEAF = (79, 209, 181, 255)
HILITE = (255, 160, 145, 255)


def draw_master(size=256):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = size

    # rounded square background
    r = int(s * 0.22)
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=r, fill=BG)

    # subtle inner card
    pad = int(s * 0.06)
    r2 = int(s * 0.18)
    d.rounded_rectangle([pad, pad, s - 1 - pad, s - 1 - pad], radius=r2, fill=CARD)

    # tomato body
    cx = s / 2
    cy = s * 0.56
    rad = s * 0.30
    box = [cx - rad, cy - rad, cx + rad, cy + rad]
    d.ellipse(box, fill=TOMATO)

    # highlight
    hl = int(s * 0.06)
    d.ellipse([cx - rad * 0.55, cy - rad * 0.62, cx - rad * 0.18, cy - rad * 0.25], fill=HILITE)

    # leaf
    leaf_w = s * 0.30
    leaf_h = s * 0.16
    leaf_top = cy - rad - leaf_h * 0.5
    d.polygon([
        (cx - leaf_w * 0.5, leaf_top + leaf_h),
        (cx, leaf_top),
        (cx + leaf_w * 0.5, leaf_top + leaf_h),
    ], fill=LEAF)

    return img


def main():
    out_dir = os.path.join(os.path.dirname(__file__), "..", "src", "Assets")
    out_dir = os.path.abspath(out_dir)
    os.makedirs(out_dir, exist_ok=True)
    master = draw_master(256)
    master.save(os.path.join(out_dir, "app.ico"), sizes=[(x, x) for x in SIZES])
    # keep a PNG preview for docs if useful
    master.save(os.path.join(out_dir, "app-preview.png"))
    print("icon written to", os.path.join(out_dir, "app.ico"))


if __name__ == "__main__":
    main()

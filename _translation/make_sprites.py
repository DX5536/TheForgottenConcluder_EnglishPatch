# -*- coding: utf-8 -*-
"""Render English replacements for the game's vertical brush-art labels.

Each output PNG matches its sprite's exact pixel size so the plugin can swap it
in without touching layout. Text is drawn horizontally at high resolution then
rotated 90 deg CCW, which keeps the brush stroke shapes clean.

FONT choices:
  MaShanZheng - a real Chinese brush face; its Latin glyphs carry proper
                brush weight, so it sits naturally beside the game's calligraphy
  WaterBrush  - Latin script face, lighter and more cursive
"""
import os, sys
from PIL import Image, ImageDraw, ImageFont

SP = os.environ.get("TFC_SP", os.path.dirname(os.path.abspath(__file__)))

FONTS = {
    "msz": os.path.join(SP, "MaShanZheng-Regular.ttf"),
    "wb":  os.path.join(SP, "WaterBrush-Regular.ttf"),
}

INK = (48, 40, 34, 255)   # deep ink, slightly warm
SS = 4                    # supersample factor for smooth strokes

# Selected states use an 'a' suffix on the sprite name (UI_Wune_P1 -> UI_Wune_P1a).
SELECTED_GOLD = (222, 168, 44)      # the gold used by the selected vertical strips

# Per-element colours of the wuxing ring when selected.
RING_COLOURS = {
    "UI_Wune_P1": (76, 175, 80),     # Swift  - green
    "UI_Wune_P2": (139, 84, 40),     # Fierce - brown
    "UI_Wune_P3": (228, 182, 40),    # Divine - yellow
    "UI_Wune_P4": (140, 82, 200),    # Demon  - purple
    "UI_Wune_P5": (52, 122, 200),    # Soul   - blue
}

WHITE = (255, 255, 255, 255)

# sprite name -> (width, height, english)   [vertical strips, text rotated]
LABELS = {
    "UI_TmTitle_01": (57, 123, "Origin"),
    "UI_TmTitle_02": (57, 123, "Outer"),
    "UI_TmTitle_03": (57, 123, "Inner"),
    "UI_TmTitle_04": (71, 137, "Mastery"),
    "UI_TmTitle_05": (71, 137, "Gear"),
    "UI_TmTitle_06": (71, 137, "Wards"),
}

# The wuxing ring: square tiles, so English sits upright and reads normally.
# P1..P5 order is a guess (clockwise from the top); the words are trivial to
# re-map by renaming files if the positions come out shuffled.
RING = {
    "UI_Wune_P1": (79, 79, "Swift"),    # top
    "UI_Wune_P2": (79, 79, "Fierce"),   # right
    "UI_Wune_P3": (79, 79, "Divine"),   # lower right
    "UI_Wune_P4": (79, 79, "Demon"),    # lower left
    "UI_Wune_P5": (79, 79, "Soul"),     # left
}


def embolden(img, strength):
    """Fake extra weight by compositing the glyph over itself at 1px offsets."""
    if strength <= 0:
        return img
    out = img.copy()
    for dx, dy in ((strength, 0), (-strength, 0), (0, strength), (0, -strength),
                   (strength, strength), (-strength, -strength)):
        shifted = Image.new("RGBA", img.size, (0, 0, 0, 0))
        shifted.paste(img, (dx, dy), img)
        out = Image.alpha_composite(out, shifted)
    return out


def render(width, height, text, font_path, bold=2, fill=INK, outline=None):
    # The label reads vertically, so lay it out along the LONG axis then rotate.
    long_side, short_side = height, width
    margin = int(long_side * 0.06)
    avail_len = long_side - margin * 2
    avail_thick = int(short_side * 0.86)

    size = 8
    for cand in range(8, 240):
        f = ImageFont.truetype(font_path, cand * SS)
        box = f.getbbox(text)
        w = (box[2] - box[0]) / SS
        h = (box[3] - box[1]) / SS
        if w <= avail_len and h <= avail_thick:
            size = cand
        else:
            break

    f = ImageFont.truetype(font_path, size * SS)
    box = f.getbbox(text)
    tw, th = box[2] - box[0], box[3] - box[1]

    strip = Image.new("RGBA", (long_side * SS, short_side * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(strip)
    x = (long_side * SS - tw) // 2 - box[0]
    y = (short_side * SS - th) // 2 - box[1]
    stroke = (2 * SS) if outline else 0
    d.text((x, y), text, font=f, fill=fill,
           stroke_width=stroke, stroke_fill=outline)

    if not outline:
        # embolden() smears shifted copies of the glyph over itself, which would
        # drag the white stroke across the fill - the stroke already adds weight.
        strip = embolden(strip, bold * SS // 2)
    strip = strip.resize((long_side, short_side), Image.LANCZOS)
    return strip.rotate(90, expand=True)   # bottom-to-top, like the original columns


def render_square(size_wh, text, font_path, bold=2, fill=INK, outline=None):
    """Upright text for square tiles (the wuxing ring).

    `outline` draws a stroke behind the glyphs (white, for readability against
    the busy ink-wash flames), matching the look of the central "Soul" label.
    """
    w, h = size_wh
    avail_w = int(w * 0.90)
    avail_h = int(h * 0.62)

    best = 8
    for cand in range(8, 200):
        f = ImageFont.truetype(font_path, cand * SS)
        box = f.getbbox(text)
        if (box[2] - box[0]) / SS <= avail_w and (box[3] - box[1]) / SS <= avail_h:
            best = cand
        else:
            break

    f = ImageFont.truetype(font_path, best * SS)
    box = f.getbbox(text)
    tw, th = box[2] - box[0], box[3] - box[1]
    img = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    x = (w * SS - tw) // 2 - box[0]
    y = (h * SS - th) // 2 - box[1]
    stroke = (2 * SS) if outline else 0    # ~2px outline at final size
    d.text((x, y), text, font=f, fill=fill,
           stroke_width=stroke, stroke_fill=outline)
    if not outline:
        # embolden() smears shifted copies over the original, which would let the
        # white stroke bleed across the fill - the stroke already adds weight.
        img = embolden(img, bold * SS // 2)
    return img.resize((w, h), Image.LANCZOS)


def recolour(img, rgb):
    """Recolour the ink while keeping the brush alpha, then add a soft glow."""
    r, g, b = rgb
    px = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            a = px[x, y][3]
            if a:
                px[x, y] = (r, g, b, a)
    return img


def build(which, outdir, bold):
    font_path = FONTS[which]
    os.makedirs(outdir, exist_ok=True)
    made = []
    for sprite, (w, h, text) in LABELS.items():
        img = render(w, h, text, font_path, bold, fill=INK, outline=WHITE)
        if img.size != (w, h):
            img = img.resize((w, h), Image.LANCZOS)
        img.save(os.path.join(outdir, sprite + ".png"))
        made.append((sprite, w, h, text))
        # Selected state ('a' suffix): gold ink, rendered fresh rather than
        # recoloured - recolour() repaints every opaque pixel and would swallow
        # the white outline along with the fill.
        sel = render(w, h, text, font_path, bold,
                     fill=SELECTED_GOLD + (255,), outline=WHITE)
        if sel.size != (w, h):
            sel = sel.resize((w, h), Image.LANCZOS)
        sel.save(os.path.join(outdir, sprite + "a.png"))
        made.append((sprite + "a", w, h, text + " [gold]"))

    for sprite, (w, h, text) in RING.items():
        # both states carry a white outline for readability against the ink flames;
        # each state is rendered fresh because recolouring would wipe the outline
        img = render_square((w, h), text, font_path, bold, fill=INK, outline=WHITE)
        img.save(os.path.join(outdir, sprite + ".png"))
        made.append((sprite, w, h, text))
        sel = render_square((w, h), text, font_path, bold,
                            fill=RING_COLOURS[sprite] + (255,), outline=WHITE)
        sel.save(os.path.join(outdir, sprite + "a.png"))
        made.append((sprite + "a", w, h, text + " [selected]"))
    return made


def preview(outdir, path, bg=(237, 231, 219, 255)):
    ims = [Image.open(os.path.join(outdir, n + ".png")) for n in LABELS]
    pad = 18
    W = sum(i.width for i in ims) + pad * (len(ims) + 1)
    H = max(i.height for i in ims) + pad * 2
    sheet = Image.new("RGBA", (W, H), bg)
    x = pad
    for i in ims:
        sheet.paste(i, (x, (H - i.height) // 2), i)
        x += i.width + pad
    sheet = sheet.resize((W * 2, H * 2), Image.LANCZOS)
    sheet.convert("RGB").save(path)


if __name__ == "__main__":
    which = sys.argv[1] if len(sys.argv) > 1 else "msz"
    bold = int(sys.argv[2]) if len(sys.argv) > 2 else 2
    outdir = os.path.join(SP, "TextFit_Sprites")
    made = build(which, outdir, bold)
    for s, w, h, t in made:
        print(f"  {s:<16} {w}x{h}  '{t}'")
    preview(outdir, os.path.join(SP, "sprite_preview.png"))
    print(f"\nfont={which} bold={bold} -> {len(made)} sprites + preview")

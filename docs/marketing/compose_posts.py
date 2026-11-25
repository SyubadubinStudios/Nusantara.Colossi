"""Compose social posts for Nusantara Colossi from Rodin concept art and in-game screenshots.

Run from anywhere: `python docs/marketing/compose_posts.py` (needs Pillow). Writes docs/marketing/posts/{id,en}/.
"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))).replace("\\", "/")
ART = f"{ROOT}/docs/marketing/art"
OUT = f"{ROOT}/docs/marketing/posts"
FONTS = f"{ROOT}/src/NusantaraColossi.Game/Assets/Fonts"

ANDESITE = (22, 27, 25)
PRADA = (201, 162, 75)
PRADA_BRIGHT = (235, 200, 114)
NADI = (63, 214, 207)
KAPUR = (233, 226, 207)
KAPUR_MUTED = (163, 157, 139)

CREDIT = {"id": "Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios",
          "en": "Made by Ariana Mischa Fadhila of Syubadubin Studios"}
TAG = {"art": {"id": "CONCEPT ART", "en": "CONCEPT ART"},
       "game": {"id": "TANGKAPAN LAYAR GAME", "en": "IN-GAME SCREENSHOT"}}

SIZES = {"16x9": (1920, 1080), "4x5": (1080, 1350), "9x16": (1080, 1920), "1x1": (1080, 1080)}

# (slug, source, kind, formats, focus x/y 0..1, headline {id,en}, sub {id,en})
POSTS = [
    ("01-keyart-bromo", f"{ART}/keyart-bromo-raw.png", "art", ["16x9", "4x5"], (.55, .45),
     {"id": "Panjat sang Penjaga.", "en": "Climb the guardians."},
     {"id": "Delapan raksasa purba bangkit di seluruh Nusantara.", "en": "Eight ancient giants wake across the archipelago."}),
    ("02-jakarta-tower-titan", f"{ART}/jakarta-tower-titan-raw.png", "art", ["4x5"], (.5, .4),
     {"id": "Salah satu menara itu bernapas.", "en": "One of the towers is breathing."},
     {"id": "Gedung Titan · Jakarta Hyper City", "en": "Tower Titan · Jakarta Hyper City"}),
    ("03-ijen-warden", f"{ART}/ijen-warden-raw.png", "art", ["9x16", "4x5"], (.5, .4),
     {"id": "Api biru. Gas beracun.", "en": "Blue fire. Toxic gas."},
     {"id": "Penjaga Api Biru · Kawah Ijen", "en": "Blue Fire Warden · Ijen Crater"}),
    ("04-banda-majapahit-ship", f"{ART}/banda-majapahit-ship-raw.png", "art", ["16x9", "4x5"], (.55, .4),
     {"id": "Armada itu tak pernah benar-benar karam.", "en": "The fleet never truly sank."},
     {"id": "Kapal Majapahit · Laut Banda", "en": "Majapahit Ship · Banda Sea"}),
    ("05-arka-garuda", f"{ART}/arka-garuda-raw.png", "art", ["4x5", "9x16"], (.5, .35),
     {"id": "Arka & GARUDA", "en": "Arka & GARUDA"},
     {"id": "Mantan insinyur AI. Pilot drone. Pemanjat.", "en": "Former AI engineer. Drone pilot. Climber."}),
    ("06-coop-leviathan", f"{ART}/coop-leviathan-raw.png", "art", ["4x5", "1x1"], (.5, .4),
     {"id": "Berburu bersama.", "en": "Hunt together."},
     {"id": "Co-op 2–4 pemain lewat LAN atau IP langsung.", "en": "2–4 player co-op over LAN or direct IP."}),
    ("07-ingame-bromo", f"{ROOT}/docs/images/hunt-bromo{{suffix}}.png", "game", ["16x9"], (.5, .5),
     {"id": "Langsung dari dalam game.", "en": "Straight from the game."},
     {"id": "Windows · macOS · Linux · Android", "en": "Windows · macOS · Linux · Android"}),
]


def font(name, size):
    return ImageFont.truetype(f"{FONTS}/{name}", size)


def cover(img, size, focus):
    w, h = size
    s = max(w / img.width, h / img.height)
    img = img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)
    x = min(max(0, round(img.width * focus[0] - w / 2)), img.width - w)
    y = min(max(0, round(img.height * focus[1] - h / 2)), img.height - h)
    return img.crop((x, y, x + w, y + h))


def shade(img, start, strength=.88):
    """Darken toward the bottom from `start` (0..1) so the type reads on any art."""
    w, h = img.size
    grad = Image.new("L", (1, h))
    for y in range(h):
        t = max(0.0, (y / h - start) / (1 - start))
        grad.putpixel((0, y), int(255 * min(1, t * t * (3 - 2 * t)) * strength))
    top = Image.new("L", (1, h))
    for y in range(h):
        t = max(0.0, 1 - y / (h * .16))
        top.putpixel((0, y), int(255 * t * .55))
    mask = Image.composite(grad, top, grad.point(lambda v: 255 if v > 0 else 0)).resize((w, h))
    dark = Image.new("RGB", (w, h), ANDESITE)
    return Image.composite(dark, img, mask)


def spaced(draw, xy, text, fnt, fill, spacing):
    x, y = xy
    for ch in text:
        draw.text((x, y), ch, font=fnt, fill=fill)
        x += draw.textlength(ch, font=fnt) + spacing
    return x


def spaced_width(draw, text, fnt, spacing):
    return sum(draw.textlength(c, font=fnt) + spacing for c in text) - spacing


def wrap(draw, text, fnt, width):
    lines, line = [], ""
    for word in text.split():
        probe = (line + " " + word).strip()
        if draw.textlength(probe, font=fnt) <= width or not line:
            line = probe
        else:
            lines.append(line)
            line = word
    lines.append(line)
    return lines


def compose(src, kind, fmt, focus, headline, sub, lang):
    src_img = Image.open(src).convert("RGB")
    size = SIZES[fmt]
    if kind == "game":  # never upscale real screenshots
        size = (src_img.width, round(src_img.width * size[1] / size[0]))
    w, h = size
    img = cover(src_img, size, focus)
    tall = h > w
    img = shade(img, .42 if tall else .38)
    d = ImageDraw.Draw(img)
    u = w / 1080 if tall or fmt == "1x1" else h / 1080  # scale unit
    margin = round(64 * u)

    # Eyebrow and honesty tag.
    mono = font("JetBrainsMono.ttf", round(20 * u))
    if kind != "game":  # the HUD already fills the top of a screenshot
        spaced(d, (margin, margin), "GARUDA NET · 2045 · NUSANTARA", mono, NADI, 4 * u)
    tag = TAG[kind][lang]
    tw = spaced_width(d, tag, font("JetBrainsMono.ttf", round(16 * u)), 3 * u)
    tx, ty, pad = w - margin - tw, margin + 2 * u, 12 * u
    pill = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(pill).rounded_rectangle((tx - pad, ty - pad * .7, tx + tw + pad, ty + 16 * u + pad * .9), radius=4 * u, fill=ANDESITE + (170,), outline=PRADA + (140,), width=max(1, round(u)))
    img.paste(Image.alpha_composite(img.convert("RGBA"), pill).convert("RGB"))
    d = ImageDraw.Draw(img)
    spaced(d, (tx, ty), tag, font("JetBrainsMono.ttf", round(16 * u)), KAPUR, 3 * u)

    # Bottom block, laid out upward from the credit line.
    credit_f = font("HankenGrotesk.ttf", round(22 * u))
    y = h - margin - round(22 * u)
    d.text((margin, y), CREDIT[lang], font=credit_f, fill=KAPUR_MUTED)

    logo_big = font("MarcellusSC-Regular.ttf", round(40 * u))
    y -= round(70 * u)
    x = spaced(d, (margin, y), "NUSANTARA", logo_big, KAPUR, 3 * u)
    spaced(d, (x + 18 * u, y), "COLOSSI", logo_big, PRADA, 12 * u)

    y -= round(36 * u)
    d.rectangle((margin, y, margin + round(84 * u), y + max(2, round(3 * u))), fill=PRADA)

    sub_f = font("HankenGrotesk.ttf", round(30 * u))
    sub_lines = wrap(d, sub, sub_f, w - 2 * margin)
    y -= round(28 * u) + len(sub_lines) * round(42 * u)
    for i, line in enumerate(sub_lines):
        d.text((margin, y + i * round(42 * u)), line, font=sub_f, fill=KAPUR)

    head_f = font("MarcellusSC-Regular.ttf", round((76 if tall or fmt == "1x1" else 84) * u))
    head_lines = wrap(d, headline, head_f, w - 2 * margin)
    lh = round(head_f.size * 1.12)
    y -= round(22 * u) + len(head_lines) * lh
    for i, line in enumerate(head_lines):
        d.text((margin, y + i * lh), line, font=head_f, fill=PRADA_BRIGHT)
    return img


def main():
    made = []
    for slug, src, kind, formats, focus, head, sub in POSTS:
        for lang in ("id", "en"):
            path = src.replace("{suffix}", "-id" if lang == "id" else "")
            os.makedirs(f"{OUT}/{lang}", exist_ok=True)
            for fmt in formats:
                img = compose(path, kind, fmt, focus, head[lang], sub[lang], lang)
                out = f"{OUT}/{lang}/{slug}-{fmt}.jpg"
                img.save(out, quality=90, optimize=True, progressive=True)
                made.append(out)
    print(len(made), "posts")


if __name__ == "__main__":
    main()

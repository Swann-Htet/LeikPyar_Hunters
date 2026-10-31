"""
Builds the Burmese text pipeline for the Unity game.

Unity's TextMeshPro cannot shape Myanmar script (it does not reorder vowels, stack consonants or place marks),
so the shaping is done HERE, once, with HarfBuzz and a real Unicode Myanmar font (MM.ttf, Z06-Walone):

  1. every Burmese string in translations.json is cut into syllable clusters,
  2. every cluster is shaped with HarfBuzz and its final outline (all parts already in the right place)
     becomes ONE glyph of a new font, mapped to a private-use code point,
  3. the strings are rewritten as those code points.

Outputs
  Assets/FONTS/Burmese/BurmeseClusters.ttf       the generated font (import it into Unity)
  Assets/Resources/Burmese/BurmeseMap.json       English text -> encoded Burmese text (read by the game)
  Tools/burmese/preview.png                      a picture of every string, to check it by eye
  Tools/burmese/review.txt                       English | Burmese list for a native speaker to review

Run:  python -I build_burmese.py        (needs: pip install fonttools uharfbuzz skia-pathops pillow)
"""
import json
import os
import sys

import uharfbuzz as hb
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SRC_FONT = os.path.join(ROOT, "Assets", "FONTS", "Burmese", "MM.ttf")   # the horror Burmese font chosen for the game (Unicode Myanmar font)
OUT_FONT = os.path.join(ROOT, "Assets", "FONTS", "Burmese", "BurmeseClusters.ttf")
OUT_MAP = os.path.join(ROOT, "Assets", "Resources", "Burmese", "BurmeseMap.json")
OUT_PREVIEW = os.path.join(HERE, "preview.png")
OUT_REVIEW = os.path.join(HERE, "review.txt")

PUA_START = 0xE000

# ---------------------------------------------------------------- Myanmar syllables

BASE = set()
BASE.update(range(0x1000, 0x1028))          # consonants, independent vowels
BASE.update([0x1029, 0x102A, 0x103F])
BASE.update(range(0x1040, 0x104C))          # digits, punctuation
BASE.update(range(0x104C, 0x1050))          # symbols
BASE.update(range(0x1050, 0x1056))          # Pali / Sanskrit
VIRAMA = "္"
ASAT = "်"


def is_myanmar(ch):
    o = ord(ch)
    return 0x1000 <= o <= 0x109F or 0xA9E0 <= o <= 0xA9FF or 0xAA60 <= o <= 0xAA7F or ch in "‌‍"


def split_clusters(run):
    """Cuts one run of Myanmar characters into syllable clusters."""
    clusters = []
    cur = ""
    for i, c in enumerate(run):
        prev = run[i - 1] if i > 0 else ""
        nxt = run[i + 1] if i + 1 < len(run) else ""
        start_new = False
        if ord(c) in BASE:
            if prev == VIRAMA:
                start_new = False                    # the consonant under a stack
            elif nxt in (ASAT, VIRAMA) and cur:
                start_new = False                    # a final consonant (with asat) or the top of a stack
            else:
                start_new = True
        if start_new and cur:
            clusters.append(cur)
            cur = ""
        cur += c
    if cur:
        clusters.append(cur)
    return clusters


def split_text(text):
    """-> list of ('my', run) / ('other', chars)"""
    parts = []
    for ch in text:
        kind = "my" if is_myanmar(ch) else "other"
        if parts and parts[-1][0] == kind:
            parts[-1] = (kind, parts[-1][1] + ch)
        else:
            parts.append((kind, ch))
    return parts


# ---------------------------------------------------------------- shaping

src = TTFont(SRC_FONT)
glyph_set = src.getGlyphSet()
upem = src["head"].unitsPerEm

with open(SRC_FONT, "rb") as fh:
    hb_face = hb.Face(fh.read())
hb_font = hb.Font(hb_face)


def shape(cluster):
    """-> (list of (glyph name, x offset, y offset), total advance)"""
    buf = hb.Buffer()
    buf.add_str(cluster)
    buf.script = "Mymr"
    buf.language = "my"
    buf.direction = "ltr"
    hb.shape(hb_font, buf, {})
    pen_x = 0
    out = []
    for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
        name = src.getGlyphName(info.codepoint)
        out.append((name, pen_x + pos.x_offset, pos.y_offset))
        pen_x += pos.x_advance
    return out, pen_x


def build_glyph(shaped):
    pen = TTGlyphPen(None)
    for name, dx, dy in shaped:
        tp = TransformPen(pen, (1, 0, 0, 1, dx, dy))
        rec = DecomposingRecordingPen(glyph_set)
        glyph_set[name].draw(rec)
        rec.replay(tp)
    return pen.glyph()


# ---------------------------------------------------------------- read the translations

with open(os.path.join(HERE, "translations.json"), encoding="utf-8") as fh:
    table = json.load(fh)

cluster_code = {}      # cluster -> private use character
cluster_glyph = {}     # cluster -> (shaped list, advance)


def code_for(cluster):
    if cluster not in cluster_code:
        cluster_code[cluster] = chr(PUA_START + len(cluster_code))
        cluster_glyph[cluster] = shape(cluster)
    return cluster_code[cluster]


def encode(text):
    out = []
    for kind, chunk in split_text(text):
        if kind == "other":
            out.append(chunk)
        else:
            for cl in split_clusters(chunk):
                cl = cl.replace("‌", "").replace("‍", "")
                if cl:
                    out.append(code_for(cl))
    return "".join(out)


entries = []
for english, burmese in table.items():
    entries.append({"k": english, "v": encode(burmese)})

# ---------------------------------------------------------------- build the font

glyph_names = [".notdef", "space"]
glyphs = {}
metrics = {}

notdef_pen = TTGlyphPen(None)
glyphs[".notdef"] = notdef_pen.glyph()
metrics[".notdef"] = (int(upem * 0.5), 0)
glyphs["space"] = TTGlyphPen(None).glyph()
metrics["space"] = (int(upem * 0.25), 0)

cmap = {0x20: "space"}

for cluster, ch in cluster_code.items():
    shaped, advance = cluster_glyph[cluster]
    name = "uni%04X" % ord(ch)
    g = build_glyph(shaped)
    glyph_names.append(name)
    glyphs[name] = g
    metrics[name] = (max(0, int(advance)), 0)
    cmap[ord(ch)] = name

fb = FontBuilder(upem, isTTF=True)
fb.setupGlyphOrder(glyph_names)
fb.setupCharacterMap(cmap)
fb.setupGlyf(glyphs)

# real left side bearings from the outlines
glyf = fb.font["glyf"]
hmtx = {}
for name in glyph_names:
    adv = metrics[name][0]
    g = glyf[name]
    if g.numberOfContours == 0:
        hmtx[name] = (adv, 0)
    else:
        g.recalcBounds(glyf)
        hmtx[name] = (adv, g.xMin)
fb.setupHorizontalMetrics(hmtx)

hhea = src["hhea"]
os2 = src["OS/2"]
fb.setupHorizontalHeader(ascent=hhea.ascent, descent=hhea.descent, lineGap=hhea.lineGap)
fb.setupNameTable({
    "familyName": "BurmeseClusters",
    "styleName": "Regular",
    "uniqueFontIdentifier": "BurmeseClusters-Regular",
    "fullName": "BurmeseClusters Regular",
    "psName": "BurmeseClusters-Regular",
    "version": "Version 1.0",
    "copyright": "Glyph outlines from MM.ttf (Z06-Walone), Copyright (c) 2022 by ZinBo. All rights reserved.",
})
fb.setupOS2(
    sTypoAscender=os2.sTypoAscender, sTypoDescender=os2.sTypoDescender, sTypoLineGap=os2.sTypoLineGap,
    usWinAscent=os2.usWinAscent, usWinDescent=os2.usWinDescent, usWeightClass=400,
)
fb.setupPost()
fb.setupDummyDSIG() if hasattr(fb, "setupDummyDSIG") else None

os.makedirs(os.path.dirname(OUT_FONT), exist_ok=True)
tmp_font = OUT_FONT + ".tmp"
fb.save(tmp_font)

# remove overlapping contours (the SDF atlas of TextMeshPro needs clean outlines)
try:
    from fontTools.ttLib import removeOverlaps
    f = TTFont(tmp_font)
    removeOverlaps.removeOverlaps(f)
    f.save(OUT_FONT)
    print("overlaps removed")
except Exception as exc:  # skia-pathops missing
    print("WARNING: could not remove overlaps (" + str(exc) + "). pip install skia-pathops")
    os.replace(tmp_font, OUT_FONT)
if os.path.exists(tmp_font):
    os.remove(tmp_font)

# ---------------------------------------------------------------- the map for the game

os.makedirs(os.path.dirname(OUT_MAP), exist_ok=True)
with open(OUT_MAP, "w", encoding="utf-8") as fh:
    json.dump({"entries": entries, "chars": "".join(cluster_code.values())}, fh, ensure_ascii=False, indent=1)

with open(OUT_REVIEW, "w", encoding="utf-8") as fh:
    fh.write("English  |  Burmese (please have a native speaker check these)\n\n")
    for english, burmese in table.items():
        fh.write(english + "\n    " + burmese + "\n")

# ---------------------------------------------------------------- preview picture (to check by eye)

font_img = ImageFont.truetype(OUT_FONT, 44)
rows = [(k, encode(v)) for k, v in table.items() if not k.startswith("@")]
line_h = 100
img = Image.new("RGB", (1500, line_h * len(rows) + 20), (12, 8, 10))
draw = ImageDraw.Draw(img)
small = ImageFont.load_default()
for i, (k, enc) in enumerate(rows):
    y = 10 + i * line_h
    draw.text((10, y), k[:90], fill=(120, 120, 120), font=small)
    # draw with the cluster font; Latin parts are not in it, so they show as boxes (the game uses a fallback font)
    draw.text((10, y + 22), enc, fill=(230, 40, 60), font=font_img)
img.save(OUT_PREVIEW)

print("clusters:", len(cluster_code), "entries:", len(entries))
print("font:", OUT_FONT)
print("map :", OUT_MAP)

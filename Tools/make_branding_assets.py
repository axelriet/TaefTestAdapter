"""Generates the Test Adapter for TAEF branding assets.

Writes (relative to the repository root):
  TaefTestAdapter/Packaging/Resources/taef-logo.ico     VSIX icon (16..256 px)
  TaefTestAdapter/Packaging/Resources/taef-logo.png     NuGet icon (128 px)
  TaefTestAdapter/Packaging/Resources/Preview.png       VSIX preview image (200 px)
  TaefTestAdapter/VsPackage/Resources/taef-logo.ico     VS package icon (VSPackage.resx)
  TaefTestAdapter/Resources/Icons/Icon.ico, Icon_128.png  master copies of the template icon (not used by any project)
  TaefTestAdapter/Resources/Icons/toolbaricons.png      64x16 toolbar strip (see TaefTestAdapterPackage.vsct)
  TaefTestAdapter/ItemTemplates/Test/TAEF/Icon.ico, TaefTestAdapter/ProjectTemplates/Test/TAEF/Icon.ico

The logo and toolbar icons are drawn from geometric primitives / pixel grids (no third-party logos, no
font glyphs); only the preview image uses the Segoe UI and Consolas fonts of Windows for its text.

Usage: python make_branding_assets.py [<repository root> [<enlarged preview folder>]]
Requires Pillow (pip install pillow).
"""
import io
import os
import struct
import sys

from PIL import Image, ImageDraw, ImageFont

REPO = sys.argv[1] if len(sys.argv) > 1 else os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
PREVIEW_DIR = sys.argv[2] if len(sys.argv) > 2 else None

BLUE = (21, 101, 192, 255)        # logo background
BLUE_DARK = (13, 71, 161, 255)    # logo border
WHITE = (255, 255, 255, 255)
GREEN = (56, 138, 52, 255)        # VS image library "action green"
RED = (229, 20, 0, 255)           # VS image library "action red"
ACTION_BLUE = (0, 83, 156, 255)   # VS image library "action blue"
GRAY = (66, 66, 66, 255)          # VS image library outline gray (themeable)


# ---------------------------------------------------------------------------------------------
# Logo
# ---------------------------------------------------------------------------------------------

def _letters(draw, x0, y0, h, color):
    """Draws the word TAEF with rectangles/polygons; returns total width. Stroke = h/5."""
    s = h / 5.0          # stroke
    w = h * 0.72         # letter width
    gap = h * 0.16
    x = x0

    def rect(a, b, c, d):
        draw.rectangle([round(a), round(b), round(c) - 1, round(d) - 1], fill=color)

    # T
    rect(x, y0, x + w, y0 + s)
    rect(x + (w - s) / 2, y0, x + (w + s) / 2, y0 + h)
    x += w + gap
    # A (two slanted legs + bar)
    top_l, top_r = x + w / 2 - s * 0.55, x + w / 2 + s * 0.55
    draw.polygon([(x, y0 + h), (top_l, y0), (top_l + s * 1.0, y0), (x + s * 1.05, y0 + h)], fill=color)
    draw.polygon([(x + w, y0 + h), (top_r, y0), (top_r - s * 1.0, y0), (x + w - s * 1.05, y0 + h)], fill=color)
    rect(x + w * 0.22, y0 + h * 0.58, x + w * 0.78, y0 + h * 0.58 + s * 0.9)
    x += w + gap
    # E
    rect(x, y0, x + s, y0 + h)
    rect(x, y0, x + w * 0.9, y0 + s)
    rect(x, y0 + (h - s) / 2, x + w * 0.8, y0 + (h + s) / 2)
    rect(x, y0 + h - s, x + w * 0.9, y0 + h)
    x += w * 0.9 + gap
    # F
    rect(x, y0, x + s, y0 + h)
    rect(x, y0, x + w * 0.9, y0 + s)
    rect(x, y0 + (h - s) / 2, x + w * 0.8, y0 + (h + s) / 2)
    x += w * 0.9
    return x - x0


def _letters_width(h):
    w = h * 0.72
    gap = h * 0.16
    return w + gap + w + gap + w * 0.9 + gap + w * 0.9


def _check(draw, points, width, color):
    draw.line(points, fill=color, width=max(1, round(width)), joint="curve")
    r = width / 2.0
    for (px, py) in (points[0], points[-1]):
        draw.ellipse([px - r, py - r, px + r, py + r], fill=color)


def draw_logo(size):
    """Rounded blue square with a white check mark; from 48 px on with the TAEF wordmark."""
    ss = 8 if size <= 64 else 4
    S = size * ss
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    margin = 0 if size <= 16 else S * 0.03
    radius = S * 0.2
    d.rounded_rectangle([margin, margin, S - margin - 1, S - margin - 1], radius=radius, fill=BLUE_DARK)
    border = max(ss, S * 0.035)
    d.rounded_rectangle([margin + border, margin + border, S - margin - border - 1, S - margin - border - 1],
                        radius=max(1, radius - border), fill=BLUE)

    if size >= 48:
        # check mark in the upper part, wordmark below
        cw = S * 0.085
        _check(d, [(S * 0.30, S * 0.36), (S * 0.44, S * 0.50), (S * 0.71, S * 0.20)], cw, WHITE)
        h = S * 0.22
        total = _letters_width(h)
        _letters(d, (S - total) / 2, S * 0.62, h, WHITE)
    else:
        cw = S * (0.16 if size <= 16 else 0.13)
        _check(d, [(S * 0.24, S * 0.52), (S * 0.42, S * 0.70), (S * 0.77, S * 0.30)], cw, WHITE)

    return img.resize((size, size), Image.LANCZOS)


# ---------------------------------------------------------------------------------------------
# ICO writer (BMP frames for <= 128 px, PNG frame for 256 px, as Windows/VS icons do)
# ---------------------------------------------------------------------------------------------

def _bmp_frame(img):
    w, h = img.size
    px = img.load()
    header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, 0, 0, 0, 0, 0)
    xor = bytearray()
    for y in range(h - 1, -1, -1):
        for x in range(w):
            r, g, b, a = px[x, y]
            xor += bytes((b, g, r, a))
    row_bytes = ((w + 31) // 32) * 4
    andmask = bytearray()
    for y in range(h - 1, -1, -1):
        row = bytearray(row_bytes)
        for x in range(w):
            if px[x, y][3] == 0:
                row[x // 8] |= 0x80 >> (x % 8)
        andmask += row
    return header + bytes(xor) + bytes(andmask)


def write_ico(path, images, png_min_size=256):
    frames = []
    for img in images:
        if img.size[0] >= png_min_size:
            buf = io.BytesIO()
            img.save(buf, format="PNG", optimize=True)
            frames.append((img.size[0], buf.getvalue()))
        else:
            frames.append((img.size[0], _bmp_frame(img)))
    out = bytearray(struct.pack("<HHH", 0, 1, len(frames)))
    offset = 6 + 16 * len(frames)
    for size, data in frames:
        dim = 0 if size >= 256 else size
        out += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    for _, data in frames:
        out += data
    with open(path, "wb") as f:
        f.write(out)


# ---------------------------------------------------------------------------------------------
# Toolbar strip: 64x16, four 16x16 icons (order must match TaefTestAdapterPackage.vsct):
#   1 run tests in process (/inproc), 2 parallel test execution, 3 break on error, 4 print test output
# ---------------------------------------------------------------------------------------------

PALETTE = {".": (0, 0, 0, 0), "G": GRAY, "g": GREEN, "b": ACTION_BLUE, "r": RED, "w": WHITE}


def _pixel_icon(rows):
    assert len(rows) == 16 and all(len(r) == 16 for r in rows), rows
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            px[x, y] = PALETTE[c]
    return img


def icon_inproc():
    # a process window with a "run" triangle inside: run the tests inside TE.exe (/inproc)
    return _pixel_icon([
        "................",
        ".GGGGGGGGGGGGGG.",
        ".GGGGGGGGGGGGGG.",
        ".G............G.",
        ".G....g.......G.",
        ".G....gg......G.",
        ".G....ggg.....G.",
        ".G....gggg....G.",
        ".G....ggggg...G.",
        ".G....gggg....G.",
        ".G....ggg.....G.",
        ".G....gg......G.",
        ".G....g.......G.",
        ".G............G.",
        ".GGGGGGGGGGGGGG.",
        "................",
    ])


def icon_parallel():
    # three parallel arrows
    return _pixel_icon([
        "................",
        "..........b.....",
        ".GGGGGGGGGbb....",
        "..........b.....",
        "................",
        "................",
        "..........b.....",
        ".GGGGGGGGGbb....",
        "..........b.....",
        "................",
        "................",
        "..........b.....",
        ".GGGGGGGGGbb....",
        "..........b.....",
        "................",
        "................",
    ])


def icon_break_on_error():
    # a breakpoint with a failure cross, plus "pause" bars
    return _pixel_icon([
        "....rrrrr.......",
        "..rrrrrrrrr.....",
        ".rrrrrrrrrrr....",
        ".rrwwrrrwwrr....",
        "rrrrwwrwwrrrr...",
        "rrrrrwwwrrrrr...",
        "rrrrrrwrrrrrr...",
        "rrrrrwwwrrrrr...",
        "rrrrwwrwwr......",
        ".rrwwrrrwr.bb.bb",
        ".rrrrrrrrr.bb.bb",
        "..rrrrrrrr.bb.bb",
        "....rrrrr..bb.bb",
        "...........bb.bb",
        "...........bb.bb",
        "...........bb.bb",
    ])


def icon_print_output():
    # an output window with text lines
    return _pixel_icon([
        "................",
        "GGGGGGGGGGGGGGGG",
        "GGGGGGGGGGGGGGGG",
        "G..............G",
        "G..............G",
        "G.bbbbbbbbb....G",
        "G..............G",
        "G.bbbbbb.......G",
        "G..............G",
        "G.bbbbbbbbbbb..G",
        "G..............G",
        "G.bbbbbbb......G",
        "G..............G",
        "GGGGGGGGGGGGGGGG",
        "................",
        "................",
    ])


def toolbar_strip():
    strip = Image.new("RGBA", (64, 16), (0, 0, 0, 0))
    for i, icon in enumerate((icon_inproc(), icon_parallel(), icon_break_on_error(), icon_print_output())):
        strip.alpha_composite(icon, (16 * i, 0))
    return strip


# ---------------------------------------------------------------------------------------------
# VSIX preview image (200x200)
# ---------------------------------------------------------------------------------------------

def preview():
    W = H = 200
    ss = 4
    img = Image.new("RGBA", (W * ss, H * ss), (250, 250, 250, 255))
    d = ImageDraw.Draw(img)
    font_dir = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts")
    bold = ImageFont.truetype(os.path.join(font_dir, "segoeuib.ttf"), 17 * ss)
    regular = ImageFont.truetype(os.path.join(font_dir, "segoeui.ttf"), 9 * ss)
    mono = ImageFont.truetype(os.path.join(font_dir, "consola.ttf"), 8 * ss)

    logo = draw_logo(56 * ss)
    img.alpha_composite(logo, (12 * ss, 12 * ss))
    d.text((76 * ss, 16 * ss), "Test Adapter", font=bold, fill=(33, 33, 33, 255))
    d.text((76 * ss, 38 * ss), "for TAEF", font=bold, fill=(33, 33, 33, 255))

    # a stylised Test Explorer panel
    top = 82
    d.rectangle([8 * ss, top * ss, 192 * ss, 192 * ss], fill=WHITE, outline=(200, 200, 200, 255), width=ss)
    d.rectangle([8 * ss, top * ss, 192 * ss, (top + 14) * ss], fill=(236, 236, 236, 255))
    d.text((13 * ss, (top + 1.5) * ss), "Test Explorer", font=regular, fill=(60, 60, 60, 255))
    rows = [
        (0, "MathTests_taef.dll", None),
        (1, "Contoso::MathTests", None),
        (2, "Addition", GREEN),
        (2, "Division", RED),
        (2, "Rounding#metadataSet0", GREEN),
        (2, "Rounding#metadataSet1", GREEN),
        (2, "Parsing", (255, 170, 0, 255)),
    ]
    y = top + 18
    for indent, text, status in rows:
        x = 13 + indent * 9
        if status is not None:
            cx, cy, r = (x + 3.5) * ss, (y + 5) * ss, 3.5 * ss
            d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=status)
            x += 10
        else:
            d.polygon([((x) * ss, (y + 2.5) * ss), ((x + 6) * ss, (y + 2.5) * ss), ((x + 3) * ss, (y + 7) * ss)],
                      fill=(110, 110, 110, 255))
            x += 9
        d.text((x * ss, y * ss), text, font=mono, fill=(33, 33, 33, 255))
        y += 13
    return img.resize((W, H), Image.LANCZOS).convert("RGB")


def main():
    sizes = [16, 24, 32, 48, 64, 128, 256]
    logos = {s: draw_logo(s) for s in sizes}

    paths = {
        "packaging_ico": os.path.join(REPO, r"TaefTestAdapter\Packaging\Resources\taef-logo.ico"),
        "packaging_png": os.path.join(REPO, r"TaefTestAdapter\Packaging\Resources\taef-logo.png"),
        "preview": os.path.join(REPO, r"TaefTestAdapter\Packaging\Resources\Preview.png"),
        "vspackage_ico": os.path.join(REPO, r"TaefTestAdapter\VsPackage\Resources\taef-logo.ico"),
        "icon_ico": os.path.join(REPO, r"TaefTestAdapter\Resources\Icons\Icon.ico"),
        "icon_png": os.path.join(REPO, r"TaefTestAdapter\Resources\Icons\Icon_128.png"),
        "toolbar": os.path.join(REPO, r"TaefTestAdapter\Resources\Icons\toolbaricons.png"),
        "item_template_ico": os.path.join(REPO, r"TaefTestAdapter\ItemTemplates\Test\TAEF\Icon.ico"),
        "project_template_ico": os.path.join(REPO, r"TaefTestAdapter\ProjectTemplates\Test\TAEF\Icon.ico"),
    }

    all_sizes = [logos[s] for s in sizes]
    write_ico(paths["packaging_ico"], all_sizes)
    write_ico(paths["vspackage_ico"], all_sizes)
    write_ico(paths["icon_ico"], all_sizes)
    write_ico(paths["item_template_ico"], all_sizes)
    write_ico(paths["project_template_ico"], all_sizes)
    logos[128].save(paths["packaging_png"], optimize=True)
    logos[128].save(paths["icon_png"], optimize=True)
    toolbar_strip().save(paths["toolbar"], optimize=True)
    preview().save(paths["preview"], optimize=True)

    for k, p in paths.items():
        print(k, p, os.path.getsize(p))

    if not PREVIEW_DIR:
        return

    # enlarged previews for visual inspection
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    sheet = Image.new("RGBA", (16 + 24 + 32 + 48 + 64 + 128 + 256 + 8 * 10, 270), (255, 255, 255, 255))
    x = 10
    for s in sizes:
        sheet.alpha_composite(logos[s], (x, 5))
        x += s + 10
    sheet.save(os.path.join(PREVIEW_DIR, "logo_sheet.png"))
    for bg_name, bg in (("light", (245, 245, 245, 255)), ("dark", (37, 37, 38, 255))):
        big = Image.new("RGBA", (64, 16), bg)
        big.alpha_composite(toolbar_strip())
        big.resize((64 * 10, 16 * 10), Image.NEAREST).save(os.path.join(PREVIEW_DIR, f"toolbar_{bg_name}.png"))
    small = Image.new("RGBA", (16 * 3 + 40, 24), (255, 255, 255, 255))
    for i, s in enumerate((16, 24)):
        small.alpha_composite(logos[s], (4 + i * 30, 0))
    small.resize((small.size[0] * 8, small.size[1] * 8), Image.NEAREST).save(os.path.join(PREVIEW_DIR, "logo_small_x8.png"))


if __name__ == "__main__":
    main()

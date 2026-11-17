"""
Regenerates every FlowCommand icon and Store tile from one drawing, so they always match.

    pip install pillow
    python packaging/tools/make_assets.py

Writes:
  src/FlowCommand/Assets/FlowCommand.ico      application + tray icon
  packaging/Assets/*.png                      MSIX tiles (scale and target-size variants)
  packaging/store-listing/*.png               logo for the Microsoft Store listing

The drawing is a command-prompt chevron and cursor on the same deep-indigo used by the README banner.
"""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SS = 4  # supersampling factor

BG_TOP = (11, 18, 51)
BG_BOTTOM = (21, 28, 73)
ACCENT_A = (144, 164, 244)
ACCENT_B = (125, 211, 252)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def glyph_mask(size):
    """Chevron and cursor bar, drawn as a white-on-black mask at `size` pixels."""
    m = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(m)
    w = size * 0.115                                   # stroke width
    pts = [(size * 0.30, size * 0.30), (size * 0.52, size * 0.50), (size * 0.30, size * 0.70)]
    d.line(pts, fill=255, width=int(w), joint="curve")
    for x, y in (pts[0], pts[2]):                      # round the ends
        d.ellipse((x - w / 2, y - w / 2, x + w / 2, y + w / 2), fill=255)
    bar = (size * 0.58, size * 0.675, size * 0.74, size * 0.675 + w)
    d.rounded_rectangle(bar, radius=w / 2, fill=255)
    return m


def icon(size, plated=True, margin=0.0):
    """Square icon. `margin` leaves transparent space around the plate (0 = edge to edge)."""
    big = size * SS
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))

    inset = int(big * margin)
    plate_size = big - 2 * inset
    if plated:
        grad = Image.new("RGB", (1, plate_size))
        for y in range(plate_size):
            grad.putpixel((0, y), lerp(BG_TOP, BG_BOTTOM, y / max(1, plate_size - 1)))
        grad = grad.resize((plate_size, plate_size))
        mask = Image.new("L", (plate_size, plate_size), 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, plate_size - 1, plate_size - 1), radius=int(plate_size * 0.22), fill=255)
        img.paste(grad, (inset, inset), mask)

    gm = glyph_mask(plate_size)
    ac = Image.new("RGB", (plate_size, plate_size))
    for x in range(plate_size):
        c = lerp(ACCENT_A, ACCENT_B, x / max(1, plate_size - 1))
        ImageDraw.Draw(ac).line((x, 0, x, plate_size), fill=c)
    img.paste(ac, (inset, inset), gm)
    return img.resize((size, size), Image.LANCZOS)


def wide(w, h):
    big = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    for y in range(h * SS):
        d.line((0, y, w * SS, y), fill=lerp(BG_TOP, BG_BOTTOM, y / (h * SS - 1)) + (255,))
    ic = icon(int(h * 0.74), plated=False).resize((int(h * 0.74 * SS), int(h * 0.74 * SS)), Image.LANCZOS)
    big.paste(ic, (int(w * SS * 0.5 - ic.width / 2), int(h * SS * 0.13)), ic)
    return big.resize((w, h), Image.LANCZOS)


def main():
    out = ROOT / "packaging" / "Assets"
    out.mkdir(parents=True, exist_ok=True)

    # MSIX tiles. Windows picks the scale or target size that fits the display; resources.pri (makepri) indexes them.
    for scale in (100, 125, 150, 200, 400):
        f = scale / 100
        icon(round(44 * f), margin=0.0).save(out / f"Square44x44Logo.scale-{scale}.png")
        icon(round(150 * f), margin=0.12).save(out / f"Square150x150Logo.scale-{scale}.png")
        wide(round(310 * f), round(150 * f)).save(out / f"Wide310x150Logo.scale-{scale}.png")
        icon(round(50 * f), margin=0.0).save(out / f"StoreLogo.scale-{scale}.png")
    for t in (16, 24, 32, 48, 256):
        icon(t).save(out / f"Square44x44Logo.targetsize-{t}.png")

    # Application and tray icon (multi-size .ico)
    ico_dir = ROOT / "src" / "FlowCommand" / "Assets"
    ico_dir.mkdir(parents=True, exist_ok=True)
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    imgs = [icon(s) for s in sizes]
    imgs[-1].save(ico_dir / "FlowCommand.ico", format="ICO", sizes=[(s, s) for s in sizes], append_images=imgs[:-1])

    # Store listing logo (Partner Center asks for a 300x300 app tile icon)
    store = ROOT / "packaging" / "store-listing"
    store.mkdir(parents=True, exist_ok=True)
    icon(300, margin=0.0).save(store / "StoreListingIcon-300x300.png")
    print("assets written")


if __name__ == "__main__":
    main()

import io, math, struct, sys  # usage: python make-icon.py <out.ico> <preview.png>
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops

SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]

def params(size):
    # Per-size tuning: small frames get bigger letters, a thicker wave and less glow so they stay legible.
    t = min(1.0, max(0.0, (size - 16) / (64 - 16)))
    return dict(
        width=0.33 - 0.03 * t,          # letter width
        leg=0.112 - 0.040 * t,          # leg thickness
        top=0.29 + 0.03 * t, bottom=0.69 - 0.03 * t,
        wave=max(0.026, 1.35 / size),   # wave stroke
        glow=min(1.0, max(0.0, (t - 0.10) * 1.3)),  # glow strength
        spacing=0.98 - 0.18 * t,        # letter pitch as a fraction of the letter width
    )

def render(size):
    ss = 8 if size <= 64 else 4
    S = size * ss
    p = params(size)
    U = lambda v: v * S

    # background: deep navy with a soft centre light
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32) / S
    d = np.sqrt((xx - 0.5) ** 2 + (yy - 0.52) ** 2)
    k = np.clip(1 - d / 0.75, 0, 1)[..., None]
    bg = np.array([0, 10, 46], np.float32) * (1 - k) + np.array([2, 26, 92], np.float32) * k
    img = Image.fromarray(np.dstack([bg, np.full((S, S), 255, np.float32)]).astype(np.uint8), 'RGBA')

    t_s = min(1.0, max(0.0, (size - 24) / 40))
    w, leg, y0, y1 = p['width'], p['leg'], p['top'], p['bottom']
    tp = leg * 0.55
    centres = [0.5 - (w * p['spacing']), 0.5, 0.5 + (w * p['spacing'])]

    def letter_mask(dx=0.0, dy=0.0):
        m = Image.new('L', (S, S), 0); dr = ImageDraw.Draw(m)
        for cx in centres:
            cx += dx
            ya, yb = y0 + dy, y1 + dy
            dr.polygon([(U(cx - w / 2), U(yb)), (U(cx - w / 2 + leg), U(yb)), (U(cx + tp), U(ya)), (U(cx - tp), U(ya))], fill=255)
            dr.polygon([(U(cx + w / 2), U(yb)), (U(cx + w / 2 - leg), U(yb)), (U(cx - tp), U(ya)), (U(cx + tp), U(ya))], fill=255)
            by = yb - (yb - ya) * 0.36
            bh = leg * 0.62
            half = w / 2 - leg * 0.55 - (yb - by) * (w / 2 - tp) / (yb - ya) * 0.0
            inset = (by - ya) / (yb - ya)
            xl = cx - tp - (w / 2 - tp) * inset + leg * 0.35
            dr.rectangle([U(xl), U(by), U(2 * cx - xl), U(by + bh)], fill=255)
        return m

    # glow behind everything
    glow_r = max(1.0, S * 0.035)
    lm = letter_mask()
    halo = Image.new('RGBA', (S, S), (255, 120, 20, 0))
    halo.putalpha(lm.filter(ImageFilter.GaussianBlur(glow_r)).point(lambda v: int(v * 0.55 * p['glow'])))
    img = Image.alpha_composite(img, halo)

    # letters: shadow edge then gradient face
    shadow = Image.new('RGBA', (S, S), (120, 40, 0, 255))
    shade = letter_mask(0.010 * t_s, 0.014 * t_s)
    img.paste(shadow, (0, 0), shade)
    ys = np.linspace(0, 1, S, dtype=np.float32)[:, None]
    top_c, bot_c = np.array([255, 246, 205], np.float32), np.array([255, 150, 25], np.float32)
    grad = top_c * (1 - ys[..., None]) + bot_c * ys[..., None]
    gy = np.clip((ys - y0) / (y1 - y0), 0, 1)[..., None]
    grad = top_c * (1 - gy) + bot_c * gy
    face = Image.fromarray(np.dstack([np.broadcast_to(grad, (S, S, 3)), np.full((S, S), 255, np.float32)]).astype(np.uint8), 'RGBA')
    img.paste(face, (0, 0), lm)

    # wave
    pts = []
    n = 400
    for i in range(n + 1):
        x = i / n
        c = math.cos(math.pi * (x - 0.5) / 0.20)
        env = math.exp(-(((x - 0.5) / 0.34) ** 2))
        off = (0.25 * c if c > 0 else 0.10 * c) * env
        pts.append((U(x), U(0.53 - off)))
    wl = Image.new('L', (S, S), 0); ImageDraw.Draw(wl).line(pts, fill=255, width=max(1, int(U(p['wave']))), joint='curve')
    wglow = Image.new('RGBA', (S, S), (255, 110, 10, 0))
    wglow.putalpha(wl.filter(ImageFilter.GaussianBlur(max(1.0, S * 0.022))).point(lambda v: min(255, int(v * 1.6 * p['glow']))))
    img = Image.alpha_composite(img, wglow)
    outer = Image.new('RGBA', (S, S), (255, 120, 15, 255)); img.paste(outer, (0, 0), wl)
    cl = Image.new('L', (S, S), 0); ImageDraw.Draw(cl).line(pts, fill=255, width=max(1, int(U(p['wave']) * 0.42)), joint='curve')
    core = Image.new('RGBA', (S, S), (255, 238, 165, 255)); img.paste(core, (0, 0), cl)

    return img.resize((size, size), Image.LANCZOS)

def write_ico(frames, path):
    blobs = []
    for s in SIZES:
        b = io.BytesIO(); frames[s].save(b, 'PNG', optimize=True); blobs.append(b.getvalue())
    head = struct.pack('<HHH', 0, 1, len(SIZES)); off = 6 + 16 * len(SIZES); ent = b''
    for s, blob in zip(SIZES, blobs):
        ent += struct.pack('<BBBBHHII', s % 256, s % 256, 0, 0, 1, 32, len(blob), off); off += len(blob)
    open(path, 'wb').write(head + ent + b''.join(blobs))

if __name__ == '__main__':
    out_ico, out_sheet = sys.argv[1], sys.argv[2]
    frames = {s: render(s) for s in SIZES}
    write_ico(frames, out_ico)
    sheet = Image.new('RGBA', (sum(SIZES) + 20 * (len(SIZES) + 1), 280), (40, 40, 40, 255)); x = 20
    for s in SIZES:
        sheet.alpha_composite(frames[s], (x, 10)); x += s + 20
    sheet.save(out_sheet)
    # enlarged view of the small frames for review (nearest, 6x)
    big = Image.new('RGBA', (6 * (16 + 20 + 24 + 32) + 50, 6 * 32 + 20), (40, 40, 40, 255)); x = 10
    for s in [16, 20, 24, 32]:
        big.alpha_composite(frames[s].resize((s * 6, s * 6), Image.NEAREST), (x, 10)); x += s * 6 + 10
    big.save(out_sheet.replace('.png', '_small.png'))
    frames[256].save(out_sheet.replace('.png', '_256.png'))

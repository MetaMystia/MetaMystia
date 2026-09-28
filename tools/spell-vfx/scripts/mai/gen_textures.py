"""Procedural textures for Mai's spell card VFX.

All particle textures are white (tinted by particle color) unless noted.
Run: python scripts/mai/gen_textures.py
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

# scripts/<spell>/ -> 工程根目录
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'Assets', 'Spells', 'Mai', 'Textures')
SS = 4  # supersampling factor for vector-drawn textures

rng = random.Random(11001)
nrng = np.random.default_rng(11001)


def grid(w, h):
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    return (x + 0.5) / w * 2 - 1, (y + 0.5) / h * 2 - 1


def smoothstep_math(e0, e1, x):
    t = min(1.0, max(0.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def save_alpha(name, a, rgb=(255, 255, 255)):
    a = np.clip(a, 0, 1)
    h, w = a.shape
    img = np.zeros((h, w, 4), np.uint8)
    img[..., 0], img[..., 1], img[..., 2] = rgb
    img[..., 3] = (a * 255 + 0.5).astype(np.uint8)
    Image.fromarray(img, 'RGBA').save(os.path.join(OUT, name + '.png'))


def save_rgba(name, rgb, a):
    img = np.dstack([np.clip(rgb, 0, 255), np.clip(a, 0, 1) * 255]).astype(np.uint8)
    Image.fromarray(img, 'RGBA').save(os.path.join(OUT, name + '.png'))


def canvas(size):
    w, h = size
    im = Image.new('L', (w * SS, h * SS), 0)
    return im, ImageDraw.Draw(im)


def down(im, size):
    return np.asarray(im.resize(size, Image.LANCZOS), np.float32) / 255


def glow_of(a, radius):
    im = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), 'L')
    return np.asarray(im.filter(ImageFilter.GaussianBlur(radius)), np.float32) / 255


def value_noise(w, h, cells, seed):
    r = np.random.default_rng(seed)
    g = r.random((cells + 1, cells + 1)).astype(np.float32)
    g[-1, :] = g[0, :]
    g[:, -1] = g[:, 0]
    ys = np.linspace(0, cells, h, endpoint=False)
    xs = np.linspace(0, cells, w, endpoint=False)
    y0, x0 = ys.astype(int), xs.astype(int)
    fy, fx = ys - y0, xs - x0
    fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
    a = g[y0][:, x0]
    b = g[y0][:, x0 + 1]
    c = g[y0 + 1][:, x0]
    d = g[y0 + 1][:, x0 + 1]
    top = a + (b - a) * fx[None, :]
    bot = c + (d - c) * fx[None, :]
    return top + (bot - top) * fy[:, None]


def fbm(w, h, base, octaves, seed):
    total, amp, norm = np.zeros((h, w), np.float32), 1.0, 0.0
    for o in range(octaves):
        total += amp * value_noise(w, h, base * 2 ** o, seed + o * 17)
        norm += amp
        amp *= 0.5
    return total / norm


# ---------------------------------------------------------------- soft glows

def soft_dot():
    x, y = grid(64, 64)
    r = np.sqrt(x * x + y * y)
    a = np.exp(-(r / 0.42) ** 2 * 2.2) * smoothstep(1.0, 0.75, r)
    save_alpha('soft_dot', a)


def snow_dot():
    x, y = grid(32, 32)
    r = np.sqrt(x * x + y * y)
    core = smoothstep(0.42, 0.28, r)
    halo = np.exp(-(r / 0.55) ** 2 * 3) * 0.55
    save_alpha('snow_dot', np.maximum(core, halo) * smoothstep(1.0, 0.8, r))


def sparkle():
    n = 128
    x, y = grid(n, n)
    r = np.sqrt(x * x + y * y)

    def streak(u, v, width, length):
        return np.exp(-np.abs(u) / width) * np.clip(1 - np.abs(v) / length, 0, 1) ** 2.2

    main = np.maximum(streak(x, y, 0.035, 1.0), streak(y, x, 0.035, 1.0))
    d1, d2 = (x + y) / math.sqrt(2), (x - y) / math.sqrt(2)
    diag = np.maximum(streak(d1, d2, 0.03, 0.45), streak(d2, d1, 0.03, 0.45)) * 0.55
    core = np.exp(-(r / 0.16) ** 2 * 2.5)
    save_alpha('sparkle', np.clip(main + diag + core, 0, 1) * smoothstep(1.0, 0.9, r))


def ring():
    x, y = grid(256, 256)
    r = np.sqrt(x * x + y * y)
    edge = np.exp(-((r - 0.86) / 0.035) ** 2)
    inner = smoothstep(0.45, 0.86, r) ** 3 * 0.45 * (r < 0.86)
    save_alpha('ring', np.clip(edge + inner, 0, 1))


def streak_tex():
    w, h = 128, 32
    _, y = grid(w, h)
    a = np.exp(-(y / 0.42) ** 2 * 2.5)
    core = np.exp(-(y / 0.12) ** 2 * 2.5) * 0.5
    save_alpha('streak', np.clip(a + core, 0, 1))


def white():
    save_alpha('white', np.ones((4, 4), np.float32))


# ---------------------------------------------------------------- crystals

def draw_line(d, p0, p1, width):
    d.line([p0, p1], fill=255, width=max(1, int(width)))
    rr = width / 2
    for p in (p0, p1):
        d.ellipse([p[0] - rr, p[1] - rr, p[0] + rr, p[1] + rr], fill=255)


def snowflake():
    n = 128
    im, d = canvas((n, n))
    c = n * SS / 2
    R = n * SS * 0.45
    for k in range(6):
        ang = math.radians(90 + 60 * k)
        ux, uy = math.cos(ang), -math.sin(ang)
        tip = (c + ux * R, c + uy * R)
        draw_line(d, (c, c), tip, SS * 3.2)
        # dendrite side branches, 60 degrees off the arm
        for t, ln, wd in ((0.34, 0.26, 2.6), (0.56, 0.24, 2.3), (0.76, 0.16, 2.0)):
            bx, by = c + ux * R * t, c + uy * R * t
            for s in (-1, 1):
                ba = ang + s * math.radians(60)
                ex, ey = bx + math.cos(ba) * R * ln, by - math.sin(ba) * R * ln
                draw_line(d, (bx, by), (ex, ey), SS * wd)
        # small leaf at the tip
        for s in (-1, 1):
            ba = ang + s * math.radians(35)
            draw_line(d, tip, (tip[0] - math.cos(ba) * R * 0.1, tip[1] + math.sin(ba) * R * 0.1), SS * 1.8)
    hexr = R * 0.17
    pts = [(c + hexr * math.cos(math.radians(60 * i)), c - hexr * math.sin(math.radians(60 * i))) for i in range(6)]
    d.polygon(pts, fill=255)
    a = down(im, (n, n))
    save_alpha('snowflake', np.clip(a + glow_of(a, 3.0) * 0.5, 0, 1))


def crystal_shard():
    w, h = 48, 128
    S = SS
    top, bot = (w / 2, 2), (w / 2, h - 2)
    lt, rt = (w * 0.12, h * 0.26), (w * 0.88, h * 0.26)
    lb, rb = (w * 0.2, h * 0.8), (w * 0.8, h * 0.8)
    mid_t, mid_b = (w * 0.56, h * 0.08), (w * 0.52, h * 0.9)

    def poly(points, val):
        im = Image.new('L', (w * S, h * S), 0)
        ImageDraw.Draw(im).polygon([(p[0] * S, p[1] * S) for p in points], fill=val)
        return np.asarray(im.resize((w, h), Image.LANCZOS), np.float32) / 255

    shape = poly([top, rt, rb, bot, lb, lt], 255)
    left = poly([top, mid_t, mid_b, bot, lb, lt], 255)
    ridge = poly([mid_t, (w * 0.6, h * 0.1), (w * 0.55, h * 0.88), mid_b], 255)
    rgb = np.zeros((h, w, 3), np.float32)
    light = np.array([246, 252, 255], np.float32)
    dark = np.array([168, 212, 246], np.float32)
    shade = left[..., None] * light + (1 - left[..., None]) * dark
    rgb[:] = shade
    rgb = rgb * (1 - ridge[..., None]) + np.array([255, 255, 255]) * ridge[..., None]
    _, yy = grid(w, h)
    alpha = shape * (0.72 + 0.28 * np.abs(yy)) + ridge * 0.2
    edge = np.clip(shape - poly([(w / 2, 8), (w * 0.8, h * 0.28), (w * 0.73, h * 0.78), (w / 2, h - 9), (w * 0.27, h * 0.78), (w * 0.2, h * 0.28)], 255), 0, 1)
    rgb = rgb * (1 - edge[..., None] * 0.6) + np.array([255, 255, 255]) * edge[..., None] * 0.6
    alpha = np.clip(alpha + edge * 0.25, 0, 1)
    save_rgba('crystal_shard', rgb, alpha)


# ---------------------------------------------------------------- feather

def feather():
    """White angel feather, drawn as vector paths so the barbs stay crisp at 64x160."""
    w, h, S = 64, 160, 4
    W, H = w * S, h * S
    r = random.Random(4242)
    cx = W * 0.5

    def spine_y(t):
        return H * (0.94 - 0.88 * t)

    def spine_x(t):
        return cx + W * (0.06 * math.sin(t * 2.4) - 0.02 * t)

    def half_width(t, side):
        # narrow at the calamus, widest around 40 %, tapering to a soft tip
        tt = min(1.0, max(0.0, t))
        base = smoothstep_math(0.0, 0.22, tt) ** 0.85
        shape = math.sin(min(1.0, tt / 0.97) ** 0.62 * math.pi) ** 0.75
        return W * (0.42 if side < 0 else 0.30) * shape * base

    def edge(t, side):
        return spine_x(t) + half_width(t, side) * side

    alpha_im = Image.new('L', (W, H), 0)
    shade_im = Image.new('L', (W, H), 0)
    da = ImageDraw.Draw(alpha_im)
    ds = ImageDraw.Draw(shade_im)

    # vane silhouette: dense sampling of the two edges
    n = 260
    left = [(edge(i / n, -1), spine_y(i / n)) for i in range(n + 1)]
    right = [(edge(i / n, 1), spine_y(i / n)) for i in range(n + 1)][::-1]
    da.polygon(left + right, fill=255)

    # barbs: strokes from the quill out to the edge, swept toward the tip
    for i in range(96):
        t = 0.045 + 0.925 * (i / 95) ** 0.92
        for side in (-1, 1):
            tip_t = min(1.0, t + 0.055)
            p0 = (spine_x(t), spine_y(t))
            p1 = (edge(tip_t, side), spine_y(tip_t))
            wdt = max(1, int(S * (1.7 - 1.25 * t)))
            for d in (da, ds):
                d.line([p0, p1], fill=255 if d is da else 232, width=wdt)

    # shallow V-notches on the vane edges, the way a feather separates when preened
    for st, side, reach in ((0.19, -1, 0.30), (0.33, 1, 0.26), (0.52, -1, 0.20)):
        for k in range(3):
            t = st + 0.016 * k
            frac = 1.0 - reach * (1 - abs(k - 1) / 1.0)
            ex = spine_x(t) + half_width(t, side) * side * frac
            da.line([(edge(t, side), spine_y(t)), (ex, spine_y(t))], fill=0, width=max(1, int(S * 0.9)))

    # scattered barbicels so the vane does not read as flat
    for _ in range(300):
        x = r.uniform(cx - W * 0.42, cx + W * 0.30)
        y = r.uniform(H * 0.05, H * 0.9)
        ds.line([(x, y), (x + r.uniform(-6, 6) * S * 0.4, y - r.uniform(3, 9) * S * 0.4)], fill=120, width=1)

    # quill: tapered, extends past the vane at the base
    quill = [(spine_x(0.0), spine_y(0.0) + H * 0.06)]
    for i in range(30):
        t = i / 29 * 0.92
        quill.append((spine_x(t), spine_y(t)))
    for d in (da, ds):
        d.line(quill, fill=255 if d is da else 235, width=max(1, int(S * 2.4)))
    quill_tip = [(spine_x(0.0), spine_y(0.0) + H * 0.10), (spine_x(0.0), spine_y(0.0) + H * 0.02)]
    da.line(quill_tip, fill=255, width=max(1, int(S * 1.5)))
    ds.line(quill_tip, fill=215, width=max(1, int(S * 1.5)))

    alpha = down(alpha_im, (w, h))
    shade = down(shade_im, (w, h))
    alpha = np.clip(alpha * 1.06, 0, 1)
    # vane shade: darker toward the right edge, plus the quill highlight
    _, xx = grid(w, h)
    side_shade = np.clip((xx - 0.02) * 0.22, 0, 1)
    k = np.clip(shade * 0.85 + side_shade, 0, 1)
    rgb = np.array([253, 253, 255], np.float32)[None, None, :] * (1 - k[..., None]) \
        + np.array([176, 194, 224], np.float32)[None, None, :] * k[..., None]
    save_rgba('feather', rgb, alpha)


# ---------------------------------------------------------------- magic circle

def magic_circle():
    n = 512
    im, d = canvas((n, n))
    c = n * SS / 2
    U = n * SS / 2

    def circle(r, width):
        rr = r * U
        d.ellipse([c - rr, c - rr, c + rr, c + rr], outline=255, width=max(1, int(width * SS)))

    def pt(r, deg):
        a = math.radians(deg)
        return (c + math.cos(a) * r * U, c - math.sin(a) * r * U)

    circle(0.985, 2.0)
    circle(0.94, 4.5)
    circle(0.8, 2.0)
    # rune band between 0.8 and 0.94: tiny ice glyphs
    glyph_rng = random.Random(7)
    for i in range(48):
        deg = i * 7.5 + 3.75
        kind = glyph_rng.randrange(4)
        r0, r1 = 0.83, 0.91
        if kind == 0:
            draw_line(d, pt(r0, deg), pt(r1, deg), SS * 2)
        elif kind == 1:
            m = (r0 + r1) / 2
            q = [pt(m + 0.035, deg), pt(m, deg + 1.8), pt(m - 0.035, deg), pt(m, deg - 1.8)]
            d.polygon(q, outline=255, width=SS * 2)
        elif kind == 2:
            m = (r0 + r1) / 2
            draw_line(d, pt(r0, deg), pt(m, deg), SS * 2)
            draw_line(d, pt(m, deg), pt(r1, deg + 1.6), SS * 2)
            draw_line(d, pt(m, deg), pt(r1, deg - 1.6), SS * 2)
        else:
            m = (r0 + r1) / 2
            rr = 0.012 * U
            p = pt(m, deg)
            d.ellipse([p[0] - rr, p[1] - rr, p[0] + rr, p[1] + rr], outline=255, width=SS * 2)
    for i in range(72):
        deg = i * 5
        ln = 0.035 if i % 6 == 0 else 0.015
        draw_line(d, pt(0.94, deg), pt(0.94 + ln, deg), SS * (2.5 if i % 6 == 0 else 1.5))
    # hexagram
    R = 0.76
    for off in (90, 270):
        tri = [pt(R, off + 120 * k) for k in range(3)]
        d.polygon(tri, outline=255, width=int(SS * 3.2))
    # vertex nodes with little crystal marks
    for k in range(6):
        deg = 90 + 60 * k
        p = pt(R, deg)
        rr = 0.055 * U
        d.ellipse([p[0] - rr, p[1] - rr, p[0] + rr, p[1] + rr], fill=0, outline=255, width=SS * 3)
        for j in range(3):
            a = deg + 60 * j
            draw_line(d, pt(R, deg), (p[0] + math.cos(math.radians(a)) * rr * 0.7, p[1] - math.sin(math.radians(a)) * rr * 0.7), SS * 1.5)
            draw_line(d, pt(R, deg), (p[0] - math.cos(math.radians(a)) * rr * 0.7, p[1] + math.sin(math.radians(a)) * rr * 0.7), SS * 1.5)
    # inner hexagon and ring
    hexagon = [pt(0.42, 60 * k) for k in range(6)]
    d.polygon(hexagon, outline=255, width=int(SS * 2.5))
    circle(0.36, 2.0)
    circle(0.33, 1.2)
    # central snowflake
    for k in range(6):
        deg = 90 + 60 * k
        draw_line(d, pt(0.0, 0), pt(0.26, deg), SS * 3)
        for t, ln in ((0.12, 0.07), (0.19, 0.05)):
            base = pt(t, deg)
            for s in (-1, 1):
                a = math.radians(deg + s * 60)
                draw_line(d, base, (base[0] + math.cos(a) * ln * U, base[1] - math.sin(a) * ln * U), SS * 2)
    a = down(im, (n, n))
    save_alpha('magic_circle', np.clip(a + glow_of(a, 6) * 0.55, 0, 1))


# ---------------------------------------------------------------- mist

def mist():
    n, cell = 256, 128
    out = np.zeros((n, n), np.float32)
    for i in range(4):
        x, y = grid(cell, cell)
        r = np.sqrt(x * x + y * y)
        noise = fbm(cell, cell, 3, 5, 100 + i * 13)
        warp = fbm(cell, cell, 2, 3, 200 + i * 7)
        mask = smoothstep(1.0, 0.15, r + (warp - 0.5) * 0.5)
        a = np.clip((noise - 0.28) * 1.9, 0, 1) * mask
        out[(i // 2) * cell:(i // 2 + 1) * cell, (i % 2) * cell:(i % 2 + 1) * cell] = a
    save_alpha('mist', out)


# ---------------------------------------------------------------- frost vignette

def frost_vignette():
    """Frost creeping in from the screen border, drawn as fern-like ice ferns in
    a float buffer so strokes keep a soft crystalline falloff instead of reading
    as opaque sticks."""
    w, h, S = 1024, 576, 2
    W, H = w * S, h * S
    r = random.Random(1101)
    # Strokes are drawn into value-scaled layers and combined with ImageChops.lighter
    # (a per-pixel max), avoiding an O(W*H) numpy pass for every single stroke.
    buf = Image.new('L', (W, H), 0)
    d = ImageDraw.Draw(buf)

    def stroke(p0, p1, width, intensity):
        d.line([p0, p1], fill=int(255 * intensity), width=max(1, int(round(width))))
        rr = width / 2
        for p in (p0, p1):
            d.ellipse([p[0] - rr, p[1] - rr, p[0] + rr, p[1] + rr], fill=int(255 * intensity))

    def fern(x, y, ang, length, width, depth):
        if depth <= 0 or length < W * 0.004 or width < 0.35:
            return
        # spine: slightly wandering, tapering, drawn as short segments
        steps = max(3, int(length / max(2.0, W * 0.006)))
        px, py = x, y
        seg = length / steps
        for i in range(steps):
            t = (i + 1) / steps
            ang += r.uniform(-0.055, 0.055)
            nx, ny = px + math.cos(ang) * seg, py + math.sin(ang) * seg
            stroke((px, py), (nx, ny), width * (1.0 - 0.72 * t), 0.92 - 0.35 * t)
            # paired side branches, strictly shortening along the spine
            if i >= 1 and i % 3 == 0 and depth > 1:
                blen = length * r.uniform(0.20, 0.30) * (1.0 - 0.80 * t)
                for side in (-1, 1):
                    if r.random() < 0.30:
                        continue
                    ba = ang + side * math.radians(r.uniform(52, 66))
                    fern(nx, ny, ba, blen, width * 0.50 * (1.0 - 0.5 * t), depth - 1)
            px, py = nx, ny
        # fork at the tip
        if depth > 1:
            for side in (-1, 1):
                fern(px, py, ang + side * math.radians(r.uniform(24, 34)),
                     length * r.uniform(0.22, 0.32), width * 0.50, depth - 1)

    # growth points along the border, longer and denser toward the corners
    seeds = []
    for _ in range(40):
        seeds.append((r.uniform(-0.02, 1.02) * W, r.uniform(-0.03, 0.01) * H, math.pi / 2))
        seeds.append((r.uniform(-0.02, 1.02) * W, r.uniform(0.99, 1.03) * H, -math.pi / 2))
    for _ in range(22):
        seeds.append((r.uniform(-0.02, 0.01) * W, r.uniform(-0.02, 1.02) * H, 0.0))
        seeds.append((r.uniform(0.99, 1.02) * W, r.uniform(-0.02, 1.02) * H, math.pi))
    for sx, sy, a in seeds:
        # how close is this seed to a corner (0 = corner, 1 = middle of an edge)
        nx_, ny_ = min(sx, W - sx) / (W * 0.5), min(sy, H - sy) / (H * 0.5)
        nearness = 1.0 - min(1.0, math.hypot(nx_, ny_))
        boost = 0.62 + 0.95 * nearness ** 1.6
        depth = 3 + int(round(3 * nearness))
        fern(sx, sy, a + r.uniform(-0.6, 0.6), r.uniform(0.035, 0.075) * W * boost,
             r.uniform(1.4, 2.2) * S, depth)
        # small side sprout, so the border does not read as evenly spaced
        fern(sx + r.uniform(-40, 40), sy + r.uniform(-40, 40), a + r.uniform(-1.1, 1.1),
             r.uniform(0.015, 0.035) * W, r.uniform(1.0, 1.6) * S, 2)

    # nucleation plates: little hexagonal ice chips scattered where frost starts
    plates = Image.new('L', (W, H), 0)
    dp = ImageDraw.Draw(plates)
    for _ in range(110):
        ed = r.random() ** 3  # biased to the border
        side = r.randrange(4)
        if side == 0:
            px, py = r.uniform(0, W), ed * H * 0.34
        elif side == 1:
            px, py = r.uniform(0, W), H - ed * H * 0.34
        elif side == 2:
            px, py = ed * W * 0.16, r.uniform(0, H)
        else:
            px, py = W - ed * W * 0.16, r.uniform(0, H)
        rr = r.uniform(2.5, 6.5) * S
        rot = r.uniform(0, math.pi)
        pts = [(px + math.cos(rot + math.pi / 3 * k) * rr, py + math.sin(rot + math.pi / 3 * k) * rr) for k in range(6)]
        dp.polygon(pts, fill=int(r.uniform(70, 150)))
    lines = np.asarray(ImageChops.lighter(buf, plates).resize((w, h), Image.LANCZOS),
                       np.float32) / 255

    x, y = grid(w, h)
    ex = (1 - np.abs(x)) / (h / w)
    ey = 1 - np.abs(y)
    edge = np.minimum(ex, ey)  # distance to the nearest border, in height units
    haze_noise = fbm(w, h, 5, 5, 77)
    haze = smoothstep(0.20, -0.02, edge + (haze_noise - 0.5) * 0.10) ** 1.25 * 0.62
    corner_ness = np.sqrt(np.clip(1 - np.abs(x), 0, 1) ** 2 + np.clip(1 - np.abs(y), 0, 1) ** 2)
    corner = smoothstep(0.62, 0.02, corner_ness) ** 1.5 * (0.42 * haze_noise + 0.18)
    # fade the whole thing out toward the middle so it never crowds the play area
    fade = smoothstep(0.34, 0.02, edge)
    fern_a = np.clip(lines * fade * 1.15, 0, 1)
    alpha = np.clip(np.maximum(fern_a, haze * (0.5 + 0.45 * lines) + corner * fade), 0, 1)
    alpha = np.clip(alpha + glow_of(fern_a, 1.6) * 0.22, 0, 1)

    t = np.clip(lines * 1.5, 0, 1)[..., None]
    haze_c = np.array([188, 220, 252], np.float32)
    line_c = np.array([255, 255, 255], np.float32)
    save_rgba('frost_vignette', haze_c * (1 - t) + line_c * t, alpha)


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for f in (soft_dot, snow_dot, sparkle, ring, streak_tex, white, snowflake, crystal_shard, feather, magic_circle, mist, frost_vignette):
        f()
        print('generated', f.__name__)

# Generates Mai's two 48x48 buff icons in the style of the game's buff icons:
# a skewed, torn paper scrap with a 1px outline, mottled fill, a soft drop shadow,
# and a motif drawn in 1px lines. Nothing is copied from game assets.
#
# Usage: python scripts/mai/gen_buff_icons.py [out_dir]

import math
import os
import random
import sys

from PIL import Image

SIZE = 48

PARCHMENT = dict(base=(188, 170, 134), light=(198, 182, 144), dark=(176, 158, 122),
                 stain=(162, 140, 106), outline=(118, 88, 62), ink=(122, 90, 62))
ICE_PAPER = dict(base=(172, 202, 210), light=(186, 214, 220), dark=(158, 190, 200),
                 stain=(144, 176, 190), outline=(62, 88, 112), ink=(50, 78, 112))
FROST = (232, 244, 248)
ICE_LINE = (84, 128, 160)


def paper_polygon(rng):
    """Skewed quad like the game's scraps, with 1px torn notches along the edges."""
    corners = [(5, 4), (34, 2), (38, 44), (8, 45)]
    pts = []
    for (x0, y0), (x1, y1) in zip(corners, corners[1:] + corners[:1]):
        steps = int(max(abs(x1 - x0), abs(y1 - y0)))
        for i in range(steps):
            t = i / steps
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            if i % 5 == 0 and 0 < i < steps - 2:
                # push the edge inward by one pixel for a torn notch
                nx, ny = -(y1 - y0), x1 - x0
                n = math.hypot(nx, ny)
                x, y = x + nx / n * rng.choice((0.9, 1.4)), y + ny / n * rng.choice((0.9, 1.4))
            pts.append((x, y))
    return pts


def inside(poly, x, y):
    c = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, yi = poly[i]
        xj, yj = poly[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            c = not c
        j = i
    return c


def value_noise(rng, cell):
    grid = {}

    def at(ix, iy):
        if (ix, iy) not in grid:
            grid[(ix, iy)] = rng.random()
        return grid[(ix, iy)]

    def sample(x, y):
        fx, fy = x / cell, y / cell
        ix, iy = int(fx), int(fy)
        tx, ty = fx - ix, fy - iy
        tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
        a = at(ix, iy) * (1 - tx) + at(ix + 1, iy) * tx
        b = at(ix, iy + 1) * (1 - tx) + at(ix + 1, iy + 1) * tx
        return a * (1 - ty) + b * ty

    return sample


def paper(pal, seed):
    rng = random.Random(seed)
    img = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    poly = paper_polygon(rng)
    mask = [[inside(poly, x + 0.5, y + 0.5) for x in range(SIZE)] for y in range(SIZE)]
    blotch = value_noise(rng, 7)
    fine = value_noise(rng, 2.5)

    def is_paper(x, y):
        return 0 <= x < SIZE and 0 <= y < SIZE and mask[y][x]

    for y in range(SIZE):
        for x in range(SIZE):
            if is_paper(x, y):
                edge = not all(is_paper(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
                if edge:
                    img.putpixel((x, y), pal['outline'] + (255,))
                    continue
                v = blotch(x, y) * 0.7 + fine(x, y) * 0.3
                key = 'stain' if v < 0.22 else 'dark' if v < 0.40 else 'base' if v < 0.75 else 'light'
                img.putpixel((x, y), pal[key] + (255,))
            elif is_paper(x, y - 1) or is_paper(x, y - 2):
                img.putpixel((x, y), (0, 0, 0, 120))  # drop shadow under the scrap
    return img, is_paper


def line(img, p0, p1, color):
    (x0, y0), (x1, y1) = p0, p1
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        img.putpixel((x0, y0), color + (255,))
        if (x0, y0) == (x1, y1):
            return
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def poly_line(img, pts, color):
    for a, b in zip(pts, pts[1:]):
        line(img, a, b, color)


def dot(img, x, y, color):
    img.putpixel((x, y), color + (255,))


def thick(img, pts, color):
    """2px stroke, matching the weight of the game's main motif lines."""
    poly_line(img, pts, color)
    poly_line(img, [(x + 1, y) for x, y in pts], color)


def snowflake(img, cx, cy, r, color, tip=None):
    """Eight clean pixel arms (orthogonal and diagonal) with V barbs near the tips."""
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        line(img, (cx, cy), (cx + dx * r, cy + dy * r), color)
        bx, by = cx + dx * (r - 2), cy + dy * (r - 2)
        for s in (-1, 1):
            # barb points outward along the arm and sideways
            dot(img, bx + dy * s + dx, by + dx * s + dy, color)
        if tip:
            dot(img, cx + dx * r, cy + dy * r, tip)
    d = max(1, r - 2)
    for dx, dy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        line(img, (cx, cy), (cx + dx * d, cy + dy * d), color)
    if tip:
        dot(img, cx, cy, tip)


def sparkle(img, x, y, color):
    for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
        dot(img, x + dx, y + dy, color)


def reward_icon():
    """冰晶特调: an ice crystal hovering over a coupe glass, on ice-blue paper."""
    img, _ = paper(ICE_PAPER, seed=11)
    ink = ICE_PAPER['ink']
    # coupe bowl, 2px rim and sides
    thick(img, [(13, 24), (32, 24)], ink)
    thick(img, [(13, 24), (14, 26), (16, 28), (19, 30), (22, 31)], ink)
    thick(img, [(32, 24), (31, 26), (29, 28), (26, 30), (23, 31)], ink)
    # drink with an ice cube floating in it
    poly_line(img, [(16, 26), (29, 26)], ICE_LINE)
    for x in range(21, 25):
        for y in range(27, 29):
            dot(img, x, y, FROST)
    # stem and foot
    thick(img, [(22, 32), (22, 38)], ink)
    thick(img, [(17, 40), (19, 39), (26, 39), (28, 40)], ink)
    # glass highlight
    line(img, (16, 25), (17, 26), FROST)
    # crystal above the glass, with sparkles
    snowflake(img, 23, 13, 6, ink, tip=FROST)
    sparkle(img, 13, 12, FROST)
    sparkle(img, 33, 16, FROST)
    dot(img, 31, 7, FROST)
    return img


def punishment_icon():
    """冰封酒宴: a bowl and chopsticks sealed inside an ice block, frost creeping over the paper."""
    img, is_paper = paper(PARCHMENT, seed=23)
    ink = PARCHMENT['ink']
    block = [(11, 17), (34, 15), (36, 39), (13, 41)]
    # translucent ice tint inside the block
    for y in range(SIZE):
        for x in range(SIZE):
            if inside(block, x + 0.5, y + 0.5):
                r, g, b, a = img.getpixel((x, y))
                img.putpixel((x, y), (int(r * 0.6 + 170 * 0.4), int(g * 0.6 + 206 * 0.4), int(b * 0.6 + 224 * 0.4), a))
    # bowl: rim, body and foot
    thick(img, [(15, 29), (31, 28)], ink)
    thick(img, [(15, 29), (16, 32), (18, 34), (21, 35), (26, 35), (29, 33), (31, 30)], ink)
    thick(img, [(21, 36), (26, 36)], ink)
    # food mound
    poly_line(img, [(17, 28), (19, 26), (22, 25), (26, 25), (29, 27)], ink)
    # chopsticks resting across the bowl
    thick(img, [(17, 23), (32, 20)], ink)
    poly_line(img, [(18, 25), (33, 23)], ink)
    # ice block outline with highlights and a crack
    poly_line(img, block + [block[0]], ICE_LINE)
    line(img, (13, 19), (18, 18), FROST)
    line(img, (13, 19), (13, 23), FROST)
    line(img, (33, 36), (34, 32), FROST)
    poly_line(img, [(26, 16), (25, 19), (27, 21)], ICE_LINE)
    # snowflake in the corner
    snowflake(img, 33, 8, 4, ICE_LINE, tip=FROST)
    # frost creeping in from the corners of the paper
    rng = random.Random(5)
    for (cx, cy) in ((7, 6), (36, 42), (9, 43)):
        for _ in range(26):
            r = rng.random() ** 1.6 * 7
            a = rng.random() * math.tau
            x, y = round(cx + math.cos(a) * r), round(cy + math.sin(a) * r)
            if is_paper(x, y) and img.getpixel((x, y))[:3] != PARCHMENT['outline']:
                img.putpixel((x, y), FROST + (255,))
    return img


def main():
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(root, 'Build', 'Mai')
    os.makedirs(out, exist_ok=True)
    reward_icon().save(os.path.join(out, 'buff_reward.png'))
    punishment_icon().save(os.path.join(out, 'buff_punishment.png'))
    print('icons written to', os.path.abspath(out))


if __name__ == '__main__':
    main()

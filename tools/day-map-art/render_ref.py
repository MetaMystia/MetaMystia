"""Render AllDayMaps_Mirror (format v2, artOnly) maps to PNG for art reference.

usage: python render_ref.py <mirror_dir> <out_dir> <scale px/unit> [map_name_substring ...] [--fx]
Mesh sprites are drawn by fitting one affine (uv->local) per sprite; cells/objects use their 2x3 transform.
Special shaders: default skipped (like the editor); --fx approximates shadow(multiply) and daylight(screen).
"""
import json, sys, os, math
import numpy as np
from PIL import Image, ImageDraw

DEBUG = '--debug' in sys.argv
SORT_LAYERS = ['Default', 'Background', 'BelowCharacter', 'Character', 'Overlay', 'OnTop', 'EffectOverlay', 'UI']

def layer_rank(name):
    return SORT_LAYERS.index(name) if name in SORT_LAYERS else 2

class Sprites:
    def __init__(self, root, tiles, scale):
        self.root, self.scale = root, scale
        self.tiles = {t['key']: t for t in tiles}
        self.images, self.cache = {}, {}

    def atlas(self, path):
        if path not in self.images:
            self.images[path] = Image.open(os.path.join(self.root, path)).convert('RGBA')
        return self.images[path]

    def get(self, key):
        """Return (image, ox, oy): image in local space at self.scale, (ox, oy) = local coords of image top-left (units)."""
        if key in self.cache: return self.cache[key]
        t = self.tiles[key]
        img = self.atlas(t['image'])
        W, H = img.size
        ppu = t['pixelsPerUnit']
        mesh = t.get('mesh')
        if mesh:
            ratio = mesh['pixelsPerUnit'] / ppu
            verts = np.array(mesh['vertices'], float) * ratio
            uvs = np.array(mesh['uvs'], float)
            tris = mesh['triangles']
        else:
            l, b, w, h = t['rect']
            px, py = t['pivot']
            x0, y0 = -px * w / ppu, -py * h / ppu
            verts = np.array([[x0, y0], [x0 + w/ppu, y0], [x0 + w/ppu, y0 + h/ppu], [x0, y0 + h/ppu]])
            uvs = np.array([[l/W, b/H], [(l+w)/W, b/H], [(l+w)/W, (b+h)/H], [l/W, (b+h)/H]])
            tris = [0, 1, 2, 0, 2, 3]
        s = self.scale
        minx, maxx = verts[:, 0].min(), verts[:, 0].max()
        miny, maxy = verts[:, 1].min(), verts[:, 1].max()
        ow, oh = max(1, int(math.ceil((maxx - minx) * s - 1e-6))), max(1, int(math.ceil((maxy - miny) * s - 1e-6)))
        # destination pixel coords (top-left origin) and source atlas pixel coords
        dst = np.stack([(verts[:, 0] - minx) * s, (maxy - verts[:, 1]) * s], 1)
        src = np.stack([uvs[:, 0] * W, (1 - uvs[:, 1]) * H], 1)
        out = Image.new('RGBA', (ow, oh), (0, 0, 0, 0))
        mask = Image.new('L', (ow, oh), 0)
        md = ImageDraw.Draw(mask)
        for i in range(0, len(tris), 3):
            md.polygon([tuple(dst[j]) for j in tris[i:i+3]], fill=255)
        # fit dst -> src affine
        A = np.hstack([dst, np.ones((len(dst), 1))])
        coef, *_ = np.linalg.lstsq(A, src, rcond=None)
        res = np.abs(A @ coef - src).max() if len(dst) >= 3 else 0
        if res < 0.75:
            warped = img.transform((ow, oh), Image.AFFINE, tuple(coef[:, 0]) + tuple(coef[:, 1]), resample=Image.NEAREST if s >= ppu * 0.99 else Image.BILINEAR)
            out.paste(warped, (0, 0), mask)
        else:  # per-triangle
            for i in range(0, len(tris), 3):
                idx = tris[i:i+3]
                A3 = np.hstack([dst[idx], np.ones((3, 1))])
                try: c3 = np.linalg.solve(A3, src[idx])
                except np.linalg.LinAlgError: continue
                tm = Image.new('L', (ow, oh), 0)
                ImageDraw.Draw(tm).polygon([tuple(dst[j]) for j in idx], fill=255)
                warped = img.transform((ow, oh), Image.AFFINE, tuple(c3[:, 0]) + tuple(c3[:, 1]), resample=Image.NEAREST)
                out.paste(warped, (0, 0), tm)
        r = (out, minx, maxy)
        self.cache[key] = r
        return r

def tint(im, color):
    r, g, b, a = (color + [1, 1, 1, 1])[:4]
    if (r, g, b, a) == (1, 1, 1, 1): return im
    arr = np.asarray(im).astype(np.float32)
    arr[..., 0] *= r; arr[..., 1] *= g; arr[..., 2] *= b; arr[..., 3] *= a
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))

def place(canvas, spr, item, wx, wy, bounds, s, mode='normal'):
    im, ox, oy = spr
    T = item.get('transform') or [1, 0, 0, 1, 0, 0]
    a, b, c, d, tx, ty = T
    sc = item.get('scale') or [1, 1]
    a, b, c, d = a * sc[0], b * sc[0], c * sc[1], d * sc[1]
    color = item.get('color') or [1, 1, 1, 1]
    im = tint(im, color)
    bx0, by1 = bounds[0], bounds[3]
    if (a, b, c, d) == (1, 0, 0, 1):
        px = int(round((wx + tx + ox - bx0) * s)); py = int(round((by1 - (wy + ty + oy)) * s))
        blend(canvas, im, px, py, mode)
        return
    # general affine: local(u units) -> world. image pixel (i,j) -> local (ox + i/s, oy - j/s)
    w, h = im.size
    corners = [(0, 0), (w, 0), (0, h), (w, h)]
    wpts = []
    for (i, j) in corners:
        lx, ly = ox + i / s, oy - j / s
        wpts.append((a * lx + c * ly + tx + wx, b * lx + d * ly + ty + wy))
    xs = [p[0] for p in wpts]; ys = [p[1] for p in wpts]
    X0, Y1 = min(xs), max(ys)
    OW, OH = int(math.ceil((max(xs) - X0) * s)) + 1, int(math.ceil((Y1 - min(ys)) * s)) + 1
    if OW <= 0 or OH <= 0 or OW > 20000 or OH > 20000: return
    # inverse map: out pixel (p,q) -> world (X0 + p/s, Y1 - q/s) -> local via inverse of [[a c][b d]] -> image px
    det = a * d - b * c
    if abs(det) < 1e-9: return
    ia, ib, ic, idd = d / det, -b / det, -c / det, a / det
    # world -> local: lx = ia*(X - tx - wx) + ic*(Y - ty - wy) ; ly = ib*(X-..) + idd*(Y-..)
    # image px: i = (lx - ox)*s ; j = (oy - ly)*s
    # X = X0 + p/s ; Y = Y1 - q/s
    k0x, k0y = X0 - tx - wx, Y1 - ty - wy
    # lx = ia*(k0x + p/s) + ic*(k0y - q/s)
    A_ = ia, -ic, (ia * k0x + ic * k0y - ox) * s
    # i = lx*s - ox*s -> coefficients in p,q: (ia)*p + (-ic)*q + s*(ia*k0x + ic*k0y - ox)
    B_ = -ib, idd, (oy - (ib * k0x + idd * k0y)) * s
    # j = (oy - ly)*s ; ly = ib*(k0x+p/s) + idd*(k0y - q/s) -> j = -ib*p + idd*q + s*(oy - ib*k0x - idd*k0y)
    warped = im.transform((OW, OH), Image.AFFINE, (A_[0], A_[1], A_[2], B_[0], B_[1], B_[2]), resample=Image.NEAREST)
    px = int(round((X0 - bx0) * s)); py = int(round((by1 - Y1) * s))
    blend(canvas, warped, px, py, mode)

def blend(canvas, im, px, py, mode):
    if mode == 'normal':
        canvas.alpha_composite(im, (px, py)) if px >= 0 and py >= 0 else canvas.paste(Image.alpha_composite(canvas.crop((px, py, px + im.width, py + im.height)), im), (px, py))
        return
    box = (px, py, px + im.width, py + im.height)
    base = np.asarray(canvas.crop(box)).astype(np.float32) / 255
    top = np.asarray(im).astype(np.float32) / 255
    al = top[..., 3:4]
    if mode == 'multiply':
        rgb = base[..., :3] * (1 - al) + base[..., :3] * top[..., :3] * al
    else:  # screen
        rgb = 1 - (1 - base[..., :3]) * (1 - top[..., :3] * al)
    out = np.concatenate([rgb, base[..., 3:4]], 2)
    canvas.paste(Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8)), box)

def render(root, m, s, fx, outpath, crop=None):
    spr = Sprites(root, m['tiles'], s)
    items = []  # (rank, order, sub, kind, data)
    pts = []
    for L in m['layers']:
        if L.get('isHeight') or not L.get('active', True) or not L['cells']: continue
        shader = L.get('shader', 'Sprites/Default')
        mode = 'normal'
        if shader != 'Sprites/Default':
            if not fx: continue
            mode = 'multiply' if 'Fill' in shader or 'Shadow' in L['name'] else 'screen'
        lc = L.get('color') or [1, 1, 1, 1]
        for cell in L['cells']:
            cc = cell.get('color') or [1, 1, 1, 1]
            col = [cc[i] * lc[i] for i in range(4)]
            it = dict(cell); it['color'] = col
            items.append(((layer_rank(L['sortingLayer']), L['sortingOrder'], 0, -cell['y'], cell['x']), it, cell['x'], cell['y'], mode))
            pts.append((cell['x'], cell['y']))
    for o in m['objects']:
        if not o.get('active', True): continue
        shader = o.get('shader', 'Sprites/Default')
        mode = 'normal'
        if shader != 'Sprites/Default':
            if not fx or 'Outline' in shader: continue
            mode = 'multiply' if 'Fill' in shader else 'screen'
        order = int(-32 * o['y']) if o.get('sortByY') else o.get('sortingOrder', 0)
        items.append(((layer_rank(o.get('sortingLayer', 'Character')), order, 1, -o['y'], o['x']), o, o['x'], o['y'], mode))
        pts.append((o['x'], o['y']))
    if not pts: return None
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    pad = 3
    bounds = (min(xs) - pad, min(ys) - pad, max(xs) + pad + 1, max(ys) + pad + 1)
    if crop:
        bounds = crop
        mg = 12  # big sprites can reach in from outside
        items = [t for t in items if crop[0] - mg <= t[2] <= crop[2] + mg and crop[1] - mg <= t[3] <= crop[3] + mg]
    W, H = int((bounds[2] - bounds[0]) * s), int((bounds[3] - bounds[1]) * s)
    canvas = Image.new('RGBA', (W, H), (24, 24, 32, 255))
    items.sort(key=lambda t: t[0])
    for key, it, x, y, mode in items:
        try:
            place(canvas, spr.get(it['tile']), it, x, y, bounds, s, mode)
        except Exception as e:
            print('  skip', it.get('tile'), e)
    if DEBUG:
        draw_debug(canvas, m, bounds, s, spr)
    canvas.convert('RGB').save(outpath)
    return bounds, (W, H)

def draw_debug(canvas, m, bounds, s, spr=None):
    ov = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)
    def P(x, y):
        return ((x - bounds[0]) * s, (bounds[3] - y) * s)
    # height cells: arrow showing walking-right direction
    for L in m['layers']:
        if not L.get('isHeight'): continue
        for c in L['cells']:
            x0, y0 = c['x'], c['y']
            sl = 0.0
            try:
                im = spr.get(c['tile'])[0]
                px = im.getpixel((im.width // 2, im.height // 2))
                sl = px[0] / 255 * (1 if px[1] > 127 else -1)
            except Exception:
                pass
            col = (60, 160, 255, 110) if sl > 0 else (255, 120, 60, 110)
            dr.rectangle([P(x0, y0 + 1), P(x0 + 1, y0)], fill=col, outline=(255, 255, 255, 90))
            dr.line([P(x0 + 0.1, y0 + 0.5 - sl * 0.4), P(x0 + 0.9, y0 + 0.5 + sl * 0.4)], fill=(255, 255, 255, 230), width=2)
            dr.text(P(x0 + 0.05, y0 + 0.95), f'{sl:+.2f}', fill=(255, 255, 255, 255))
    for c in (m.get('height') or {}).get('cells', []):
        x0, y0, sl = c['x'], c['y'], c['slope']
        col = (60, 160, 255, 110) if sl > 0 else (255, 120, 60, 110)
        dr.rectangle([P(x0, y0 + 1), P(x0 + 1, y0)], fill=col)
        dr.line([P(x0 + 0.1, y0 + 0.5 - sl * 0.4), P(x0 + 0.9, y0 + 0.5 + sl * 0.4)], fill=(255, 255, 255, 230), width=2)
    for col in m.get('nativeColliders') or []:
        if not col.get('enabled', True) or not col.get('active', True): continue
        M = col['matrix']
        def T(x, y):
            return (M[0] * x + M[1] * y + M[3], M[4] * x + M[5] * y + M[7])
        ox, oy = col.get('offset') or [0, 0]
        if col.get('camera'): rgb = (0, 255, 255)
        elif col.get('isTrigger'): rgb = (255, 220, 0)
        else: rgb = (255, 40, 60)
        typ = col['type']
        polys = []
        if typ == 'BoxCollider2D' and col.get('size'):
            w, h = col['size']
            polys.append([T(ox - w/2, oy - h/2), T(ox + w/2, oy - h/2), T(ox + w/2, oy + h/2), T(ox - w/2, oy + h/2)])
        elif typ == 'CircleCollider2D' and col.get('radius'):
            r = col['radius']
            polys.append([T(ox + r * math.cos(a / 16 * math.pi), oy + r * math.sin(a / 16 * math.pi)) for a in range(32)])
        else:
            for path in col.get('paths') or []:
                if typ == 'CompositeCollider2D':
                    polys.append([T(px, py) for px, py in path])  # composite paths are already offset in local space
                else:
                    polys.append([T(px + ox, py + oy) for px, py in path])
        width = 3 if not col.get('isTrigger') else 2
        for poly in polys:
            if len(poly) < 2: continue
            pts = [P(*q) for q in poly]
            closed = typ != 'EdgeCollider2D'
            if col.get('usedByComposite'): continue
            fill = rgb + (40,) if (closed and not col.get('isTrigger') and not col.get('camera')) else None
            if fill and typ != 'CompositeCollider2D': dr.polygon(pts, fill=fill)
            dr.line(pts + ([pts[0]] if closed else []), fill=rgb + (230,), width=width)
    for b in m.get('collisions') or []:
        x0, y0 = P(b['x'] - b['width'] / 2, b['y'] + b['height'] / 2)
        x1, y1 = P(b['x'] + b['width'] / 2, b['y'] - b['height'] / 2)
        dr.rectangle([x0, y0, x1, y1], fill=(255, 40, 60, 50), outline=(255, 40, 60, 200))
    cb = (m.get('camera') or {}).get('bounds') or []
    if len(cb) == 4:
        dr.rectangle([P(cb[0], cb[3]), P(cb[2], cb[1])], outline=(0, 255, 255, 255), width=3)
    for sp in m.get('spawnMarkers') or []:
        x, y = P(sp['x'], sp['y'])
        dr.ellipse([x - 6, y - 6, x + 6, y + 6], outline=(0, 255, 120, 255), width=3)
    canvas.alpha_composite(ov)

if __name__ == '__main__':
    root, out, s = sys.argv[1], sys.argv[2], float(sys.argv[3])
    args = [a for a in sys.argv[4:] if not a.startswith('--')]
    fx = '--fx' in sys.argv
    crop = None
    tag = ''
    for a in sys.argv:
        if a.startswith('--crop='):
            crop = tuple(float(v) for v in a[7:].split(','))
            tag = '_c' + '_'.join(str(int(v)) for v in crop)
    os.makedirs(out, exist_ok=True)
    d = json.load(open(os.path.join(root, 'ResourceEx.json')))
    for m in d['dayMaps']:
        if args and not any(a in m['name'] for a in args): continue
        name = m['name'].replace('_Mirror', '')
        p = os.path.join(out, f'{name}_{int(s)}{"_fx" if fx else ""}{"_dbg" if DEBUG else ""}{tag}.png')
        r = render(root, m, s, fx, p, crop)
        print(name, r, flush=True)

"""三途川（生图版）组装：定稿地面图 + 物件精灵 → 分层、可走区域与碰撞、坡度、文字、光效 → 资源包。

  python -m maps.sanzu.assemble <工作目录> <输出目录> --font <12 像素字体.ttf>

工作目录需含 plate_fine.png（3456×2304 地面图）与 spr/*.png（游戏尺寸的物件精灵，见 sprites.py）。
输出：SanzuRiver.zip、preview.png（1:1）、preview_half.png、overview.png、debug.png、layer_*.png、report.txt。
"""
import argparse
import json
import os
import zipfile

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

from dayart.collide import WalkGrid
from dayart.core import Frame, PPU, paste, poly_mask, rgb, to_image, value_noise
from dayart.pack import Atlas, camera_bounds, slice_layer, sprite_tile, synth_bgm, write_zip
from dayart.sprite import Sprite
from dayart.validate import validate_map
from maps.sanzu import layout as L
from maps.sanzu import place as Q

MAP_ID = 1073742300
FOLDER = 'map/sanzu'
SPAWNS = [('FromChuuu', -0.6, -21.6, 'Up'), ('Pier', -1.2, 8.2, 'Up'), ('Kawara', -13.4, -2.2, 'Down'),
          ('Market', 26.6, -1.6, 'Down'), ('Knoll', -29.6, -3.0, 'Down')]


# ---------------------------------------------------------------- 颜色
def hsv(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx, mn = a.max(-1), a.min(-1)
    d = mx - mn + 1e-6
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    s = np.where(mx > 0, (mx - mn) / (mx + 1e-6), 0)
    return h, s, mx


def grade(rgbv, sat=0.86, val=0.92, tint='#a9bcc0', amt=0.12):
    """物件精灵配色向地面图的阴天光线靠：降一点饱和度与明度，叠一层冷灰。"""
    lum = (rgbv * np.array([0.3, 0.59, 0.11], np.float32)).sum(-1, keepdims=True)
    out = lum + (rgbv - lum) * sat
    out = out * val
    return np.clip(out * (1 - amt) + out * rgb(tint) * amt * 1.6, 0, 1)


# ---------------------------------------------------------------- 精灵
def draw_text(arr, cx, cy, s, color, font, vertical=False, k=1):
    """在 RGBA float 数组上写像素字（12 像素字体，按字形墨迹居中到 12×12 格）。"""
    f = ImageFont.truetype(font, 12)
    n = len(s)
    w, h = (12, 12 * n) if vertical else (12 * n, 12)
    m = Image.new('L', (w, h), 0)
    d = ImageDraw.Draw(m)
    d.fontmode = '1'
    for i, ch in enumerate(s):
        x, y = (0, 12 * i) if vertical else (12 * i, 0)
        bb = d.textbbox((0, 0), ch, font=f)
        d.text((x + (12 - (bb[2] - bb[0])) // 2 - bb[0], y + (12 - (bb[3] - bb[1])) // 2 - bb[1]), ch, font=f, fill=255)
    if k > 1:
        m = m.resize((w * k, h * k), Image.Resampling.NEAREST)
    mask = np.asarray(m) > 127
    H, W = arr.shape[:2]
    x0, y0 = int(round(cx - mask.shape[1] / 2)), int(round(cy - mask.shape[0] / 2))
    ys, xs = np.nonzero(mask)
    ys, xs = ys + y0, xs + x0
    ok = (ys >= 0) & (ys < H) & (xs >= 0) & (xs < W)
    c = rgb(color)
    arr[ys[ok], xs[ok], :3] = c
    arr[ys[ok], xs[ok], 3] = 1


def load_sprite(path, scale=1.0, flip=False, texts=(), font=None):
    im = Image.open(path).convert('RGBA')
    if scale != 1.0:
        im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.Resampling.BOX)
    a = np.asarray(im).astype(np.float32) / 255
    a[..., 3] = (a[..., 3] > 0.5).astype(np.float32)
    a[..., :3] = grade(a[..., :3])
    for (cx, cy), s, color, vertical, k in texts:
        if font:
            draw_text(a, cx, cy, s, color, font, vertical, k)
    if flip:
        a = a[:, ::-1].copy()
    op = a[..., 3] > 0.5
    rows = np.nonzero(op.any(1))[0]
    bottom = rows.max() + 1
    foot = op[max(0, bottom - 3):bottom].any(0)
    xs = np.nonzero(foot)[0]
    spr = Sprite(a.shape[1], a.shape[0], ((xs.min() + xs.max() + 1) / 2, bottom))
    spr.a = a
    base = op[max(0, bottom - 8):bottom].any(0)
    bx = np.nonzero(base)[0]
    spr.foot_w = (bx.max() - bx.min() + 1) / PPU
    return spr


# ---------------------------------------------------------------- 地形
class Terrain:
    def __init__(self, plate):
        self.F = Frame(L.X0, L.Y0, L.X1, L.Y1)
        self.X, self.Y = self.F.grid()
        self.plate = plate
        self.h, self.s, self.v = hsv(plate)

    def poly(self, pts):
        return poly_mask(self.F, pts)

    def rect(self, x0, y0, x1, y1):
        return (self.X >= x0) & (self.X < x1) & (self.Y >= y0) & (self.Y < y1)

    def segment(self):
        h, s, v, X, Y = self.h, self.s, self.v, self.X, self.Y
        water = (h >= 165) & (h <= 215) & (s >= 0.22) & (v >= 0.25)
        water = ndi.binary_opening(water, iterations=2)
        water = ndi.binary_closing(water, iterations=3)
        lab, n = ndi.label(water)
        sizes = ndi.sum(water, lab, range(1, n + 1))
        self.water = np.isin(lab, np.nonzero(sizes > 6 * PPU * PPU)[0] + 1)

        can = (h >= 70) & (h <= 168) & (s >= 0.15) & (v <= 0.5) & (Y < -5.4)
        can |= (v <= 0.2) & (Y < -6.5) & ~self.water
        can = ndi.binary_closing(can, iterations=6)
        can = ndi.binary_opening(can, iterations=4)
        can = ndi.binary_fill_holes(can) & ~self.water
        # 林冠连成大片并接到地图边缘；场地中间的深色草斑面积小、不接边，排除
        lab, n = ndi.label(can)
        sizes = ndi.sum(can, lab, range(1, n + 1))
        border = np.zeros_like(can)
        border[-PPU:, :] = border[:, :PPU] = border[:, -PPU:] = True
        touch = set(np.unique(lab[border & can]))
        keep = [i + 1 for i in range(n) if sizes[i] > 20 * PPU * PPU or (i + 1 in touch and sizes[i] > 2 * PPU * PPU)]
        self.canopy = np.isin(lab, keep)

    def canopy_block(self, allow=0.8, side=0.25):
        """林冠阻挡：从林冠上缘往里 allow 格可以走（人物被林冠遮住），侧缘留 side 格。"""
        c = self.canopy
        depth = np.zeros(c.shape, np.int32)
        run = np.zeros(c.shape[1], np.int32)
        for r in range(c.shape[0]):
            run = np.where(c[r], run + 1, 0)
            depth[r] = run
        block = c & (depth > int(allow * PPU))
        return ndi.binary_erosion(block, iterations=int(side * PPU))

    def walkable(self):
        X, Y = self.X, self.Y
        near_water = ndi.binary_dilation(self.water, iterations=int(0.3 * PPU))
        walk = ~near_water & ~self.canopy_block()
        walk &= self.rect(L.X0 + 0.8, L.Y0 + 0.25, L.X1 - 0.8, L.Y1 - 0.8)
        stairs = self.poly(Q.STAIRS)
        wall = self.rect(*Q.WALL) & ~stairs
        walk &= ~wall
        terrace = self.poly(Q.TERRACE)
        edge = terrace & ~ndi.binary_erosion(terrace, iterations=int(0.35 * PPU)) & (Y > Q.WALL[3] + 0.2)
        walk &= ~edge
        walk |= self.rect(*Q.PIER_STEM) | self.rect(*Q.PIER_HEAD) | self.rect(*Q.JETTY)
        walk |= self.bridge_band()
        self.stairs, self.wall, self.terrace = stairs, wall, terrace
        self.walk = walk

    def bridge_band(self):
        X, Y = self.X, self.Y
        x0, x1 = Q.BRIDGE_X
        yc = np.vectorize(Q.bridge_center)(np.clip(X, x0, x1))
        return (X >= x0) & (X < x1) & (np.abs(Y - yc) <= Q.BRIDGE_HALF)

    def slope_cells(self):
        cells = []
        band = self.bridge_band()
        for cx, s in Q.BRIDGE_SLOPES.items():
            for cy in range(-1, 5):
                sub = band[int((L.Y1 - cy - 1) * PPU):int((L.Y1 - cy) * PPU), int((cx - L.X0) * PPU):int((cx + 1 - L.X0) * PPU)]
                if sub.mean() > 0.05:
                    cells.append(dict(x=cx, y=cy, slope=s))
        st = self.stairs
        for cx in range(-30, -20):
            for cy in range(-12, -7):
                sub = st[int((L.Y1 - cy - 1) * PPU):int((L.Y1 - cy) * PPU), int((cx - L.X0) * PPU):int((cx + 1 - L.X0) * PPU)]
                if sub.mean() >= 0.3:
                    cells.append(dict(x=cx, y=cy, slope=Q.STAIRS_SLOPE))
        return cells

    def bridge_rail(self):
        """拱桥南侧栏杆与桥身侧面：从地面图切出来做成按 Y 排序的物件，走在桥上的人会被它挡住下半身。"""
        X, Y, h, s, v = self.X, self.Y, self.h, self.s, self.v
        x0, x1 = Q.BRIDGE_X
        yc = np.vectorize(Q.bridge_center)(np.clip(X, x0, x1))
        foot = yc - Q.BRIDGE_HALF - 0.25                      # 南栏杆脚线
        region = (X >= x0 - 0.15) & (X < x1 + 0.15) & (Y >= foot - 0.7) & (Y <= foot + 1.15)
        red = ((h <= 22) | (h >= 340)) & (s >= 0.35)
        dark = (v <= 0.28)
        m = region & (red | dark)
        m = ndi.binary_closing(m, iterations=1) & region
        ys, xs = np.nonzero(m)
        y0, y1, xa, xb = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        a = np.zeros((y1 - y0, xb - xa, 4), np.float32)
        a[..., :3] = self.plate[y0:y1, xa:xb]
        a[..., 3] = m[y0:y1, xa:xb]
        pivot_y_world = float(foot.min() + 0.05)
        py = (L.Y1 - pivot_y_world) * PPU - y0
        mid_px = ((Q.BRIDGE_MID - L.X0) * PPU) - xa
        spr = Sprite(a.shape[1], a.shape[0], (mid_px, py))
        spr.a = a
        return spr, Q.BRIDGE_MID, pivot_y_world


# ---------------------------------------------------------------- 光与雾
def glow_layer(F, glows):
    acc_a = np.zeros(F.shape, np.float32)
    acc_c = np.zeros(F.shape + (3,), np.float32)
    for x, y, r, color, a in glows:
        px, py = F.px(x, y)
        R = r * PPU
        x0, x1 = int(max(0, px - R)), int(min(F.W, px + R + 1))
        y0, y1 = int(max(0, py - R)), int(min(F.H, py + R + 1))
        yy, xx = np.mgrid[y0:y1, x0:x1]
        d = np.hypot(xx + 0.5 - px, yy + 0.5 - py) / R
        al = np.clip(1 - d, 0, 1) ** 2 * a
        sub_a = acc_a[y0:y1, x0:x1]
        tot = sub_a + al * (1 - sub_a)
        w = np.where(tot > 0, al / np.maximum(tot, 1e-6), 0)[..., None]
        acc_c[y0:y1, x0:x1] = acc_c[y0:y1, x0:x1] * (1 - w) + rgb(color) * w
        acc_a[y0:y1, x0:x1] = tot
    return np.dstack([acc_c, acc_a])


def mist_layer(T):
    """河面雾：越远越浓的柔和雾层，横向拉长的噪声让它成缕；只罩在水上。"""
    F, X, Y = T.F, T.X, T.Y
    n = ndi.zoom(value_noise((F.H // 8, F.W // 24), 6, 32, 2), (8, 24), order=1)[:F.H, :F.W]
    depth = np.clip((Y - 6.0) / 14.0, 0, 1)
    a = (0.05 + 0.20 * depth) * (0.55 + 0.9 * n) * depth ** 0.5
    over = ndi.binary_dilation(T.water, iterations=12) | (Y > 19)
    a = np.where(over, a, 0)
    a = ndi.gaussian_filter(a, 6)
    col = np.broadcast_to(rgb('#e3ecec'), F.shape + (3,))
    return np.dstack([col, np.clip(a, 0, 0.32)]).astype(np.float32)


def shadows(plate, F, objs):
    """物件脚下的接触投影烘进地面：柔边椭圆，冷灰相乘。"""
    m = Image.new('L', (F.W, F.H), 0)
    d = ImageDraw.Draw(m)
    for o in objs:
        if o['shadow'] <= 0:
            continue
        px, py = F.px(o['x'], o['y'])
        w = o['shadow'] * PPU
        h = min(0.6 * PPU, w * 0.32)
        d.ellipse([px - w / 2, py - h * 0.6, px + w / 2, py + h * 0.4], fill=255)
    a = ndi.gaussian_filter(np.asarray(m).astype(np.float32) / 255, 2.5)[..., None]
    shade = plate * np.array([0.6, 0.64, 0.7], np.float32)
    return plate * (1 - a * 0.55) + shade * (a * 0.55)


def patch_plate(plate, F, feather=10):
    out = plate.copy()
    for (x0, y0, x1, y1), (dx, dy) in Q.PLATE_PATCHES:
        px0, py0 = (int(v) for v in F.px(x0, y1))
        px1, py1 = (int(v) for v in F.px(x1, y0))
        ox, oy = int(dx * PPU), int(-dy * PPU)
        src = plate[py0 + oy:py1 + oy, px0 + ox:px1 + ox]
        m = np.zeros((py1 - py0, px1 - px0), np.float32)
        m[feather:-feather, feather:-feather] = 1
        m = ndi.gaussian_filter(m, feather / 2)[..., None]
        out[py0:py1, px0:px1] = out[py0:py1, px0:px1] * (1 - m) + src * m
    return out


# ---------------------------------------------------------------- 主流程
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('work')
    ap.add_argument('out')
    ap.add_argument('--font')
    ap.add_argument('--chunk', type=int, default=4)
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    plate = np.asarray(Image.open(os.path.join(args.work, 'plate_fine.png')).convert('RGB')).astype(np.float32) / 255
    plate = patch_plate(plate, Frame(L.X0, L.Y0, L.X1, L.Y1))
    T = Terrain(plate)
    F = T.F
    T.segment()
    T.walkable()

    # ---------------------------------------------------- 物件
    objs, cache = [], {}
    for i, p in enumerate(Q.P):
        key = (p['spr'], p.get('scale', 1.0), p.get('flip', False))
        if key not in cache:
            texts = Q.TEXT.get(p['spr'], ()) if p.get('scale', 1.0) == 1.0 else (
                Q.TEXT_SMALL_STELE if p['spr'] == 'Stele' else ())
            cache[key] = (load_sprite(os.path.join(args.work, 'spr', p['spr'] + '.png'), p.get('scale', 1.0), p.get('flip', False),
                                      texts, args.font), f'o{len(cache)}')
        spr, tkey = cache[key]
        name = f"{p['spr']}{i}"
        layer = p.get('layer', 'Character')
        sh = p.get('shadow', None)
        if sh is None:
            big = any(k in p['spr'] for k in ('Tree', 'Willow', 'Conifer'))
            sh = spr.w / PPU * 0.5 if big else spr.foot_w * 1.15
        objs.append(dict(name=name, spr=spr, tile=tkey, x=p['x'], y=p['y'], layer=layer, order=p.get('order', 0),
                         sort=layer == 'Character', alpha=p.get('alpha', 1.0), collide=p.get('collide'), shadow=sh,
                         glow=p.get('glow')))
    rail, rx, ry = T.bridge_rail()
    cache[('BridgeRail', 1.0, False)] = (rail, f'o{len(cache)}')
    objs.append(dict(name='BridgeRailSouth', spr=rail, tile=cache[('BridgeRail', 1.0, False)][1], x=rx, y=ry, layer='Character',
                     order=0, sort=True, alpha=1.0, collide=None, shadow=0, glow=None))

    ground = shadows(plate, F, objs)
    ground_rgba = np.dstack([ground, np.ones(F.shape, np.float32)])
    canopy = np.dstack([plate, T.canopy.astype(np.float32)])
    glows = [(o['x'] + o['glow'][0], o['y'] + o['glow'][1]) + tuple(o['glow'][2:]) for o in objs if o['glow']]
    glow = glow_layer(F, glows)
    mist = mist_layer(T)

    # ---------------------------------------------------- 碰撞与坡度
    g = WalkGrid(L.X0, L.Y0, L.X1, L.Y1, res=4)
    g.walk = g.from_pixels(T.walk, PPU, 0.6)
    for o in objs:
        if o['collide']:
            w, h = o['collide']
            g.rect_block(o['x'], o['y'] - 0.1 + h / 2, w, h)
    # 只保留与出生点连通的可走区域
    lab, n = ndi.label(g.walk)
    keep = {lab[g.cell_of(x, y)] for _, x, y, _ in SPAWNS if 0 <= g.cell_of(x, y)[0] < g.H}
    keep.discard(0)
    g.walk &= np.isin(lab, list(keep))
    rects = g.rectangles(only_near=1.5)
    slopes = T.slope_cells()
    issues = g.check([(n_, x, y) for n_, x, y, _ in SPAWNS], radius=0.3)
    bad_slopes = g.check_slopes(slopes)

    # ---------------------------------------------------- 切片与打包
    tiles, layers, files = [], [], {}
    atlas_g = Atlas('ground')
    gt, gc = slice_layer(to_image(ground_rgba), F, args.chunk, atlas_g, 'g', skip_empty=False)
    tiles += gt
    layers.append(dict(name='Ground', sortingLayer='Background', sortingOrder=-2000, cells=gc))
    files.update(atlas_g.finalize(gt, FOLDER))
    for name, arr, order, prefix in (('Canopy', canopy, 0, 'c'), ('Mist', mist, 10, 'f'), ('Glow', glow, 20, 'l')):
        at = Atlas(name.lower())
        t, c = slice_layer(to_image(arr), F, 4, at, prefix, skip_empty=True)
        tiles += t
        layers.append(dict(name=name, sortingLayer='Overlay', sortingOrder=order, cells=c))
        files.update(at.finalize(t, FOLDER))
    atlas_o = Atlas('objects')
    otiles, objects, done = [], [], set()
    for o in objs:
        spr = o['spr']
        if o['tile'] not in done:
            s2 = Sprite(spr.w, spr.h, spr.pivot)
            s2.a = spr.a.copy()
            if o['alpha'] < 1:
                s2.a[..., 3] *= o['alpha']
            otiles.append(sprite_tile(s2, atlas_o, o['tile']))
            done.add(o['tile'])
        objects.append(dict(name=o['name'], tile=o['tile'], x=round(float(o['x']), 4), y=round(float(o['y']), 4), scale=[1, 1],
                            sortByY=bool(o['sort']), sortingLayer=o['layer'], sortingOrder=o['order']))
    files.update(atlas_o.finalize(otiles, FOLDER))
    tiles += otiles
    files[f'{FOLDER}/sanzu_bgm.wav'] = synth_bgm()
    cam = camera_bounds(L.X0, L.Y0, L.X1, L.Y1)
    day_map = dict(
        id=MAP_ID, formatVersion=1, name='三途川',
        description='此岸的赛之河原与渡口。河上雾气终日不散，对岸是只能远眺的彼岸花原。',
        tiles=tiles, layers=layers, height=dict(cells=slopes), objects=objects, collisions=rects,
        spawnMarkers=[dict(name=n_, x=x, y=y, rotation=r) for n_, x, y, r in SPAWNS],
        defaultSpawnMarker='FromChuuu',
        camera=dict(shouldFollow=True, bounds=cam, position=[0, 0, -10]),
        mapBGM=dict(intro=f'{FOLDER}/sanzu_bgm.wav', loop=f'{FOLDER}/sanzu_bgm.wav'))
    config = dict(packInfo=dict(name='三途川', label='SanzuRiver', version='0.2.0', authors=['MetaMystia'],
                                description='三途川白天地图（生图美术版，不含 NPC 与出口）', dependencies=['CORE']),
                  dayMaps=[day_map])
    zpath = os.path.join(args.out, 'SanzuRiver.zip')
    write_zip(zpath, config, files)
    with zipfile.ZipFile(zpath) as z:
        issues += [f'加载校验: {e}' for e in validate_map(json.loads(z.read('ResourceEx.json'))['dayMaps'][0], z)]

    # ---------------------------------------------------- 预览
    img = ground_rgba.copy()

    def put(o):
        px, py = F.px(o['x'], o['y'])
        paste(img, o['spr'].a, px - o['spr'].pivot[0], py - o['spr'].pivot[1], o['alpha'])
    for o in [o for o in objs if o['layer'] == 'BelowCharacter']:
        put(o)
    for o in sorted([o for o in objs if o['layer'] == 'Character'], key=lambda o: -o['y']):
        put(o)
    noover = img.copy()
    paste(img, canopy, 0, 0)
    paste(img, mist, 0, 0)
    paste(img, glow, 0, 0)
    for o in sorted([o for o in objs if o['layer'] == 'Overlay'], key=lambda o: o['order']):
        put(o)
    img[..., 3] = 1
    prev = to_image(img).convert('RGB')
    prev.save(os.path.join(args.out, 'preview.png'))
    prev.resize((F.W // 2, F.H // 2), Image.LANCZOS).save(os.path.join(args.out, 'preview_half.png'))
    prev.resize((F.W // 4, F.H // 4), Image.LANCZOS).save(os.path.join(args.out, 'overview.png'))
    noover[..., 3] = 1
    to_image(noover).convert('RGB').resize((F.W // 2, F.H // 2), Image.LANCZOS).save(os.path.join(args.out, 'preview_noover_half.png'))

    dbg = prev.convert('RGBA')
    ov = Image.new('RGBA', dbg.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    P = F.px
    for r in rects:
        d.rectangle([P(r['x'] - r['width'] / 2, r['y'] + r['height'] / 2), P(r['x'] + r['width'] / 2, r['y'] - r['height'] / 2)],
                    fill=(255, 40, 60, 60), outline=(255, 40, 60, 150))
    for c in slopes:
        col = (60, 160, 255, 120) if c['slope'] > 0 else (255, 140, 40, 120)
        d.rectangle([P(c['x'], c['y'] + 1), P(c['x'] + 1, c['y'])], fill=col, outline=(255, 255, 255, 160))
        sl = c['slope']
        d.line([P(c['x'] + 0.1, c['y'] + 0.5 - sl * 0.4), P(c['x'] + 0.9, c['y'] + 0.5 + sl * 0.4)], fill=(255, 255, 255, 255), width=3)
    can_edge = T.canopy & ~ndi.binary_erosion(T.canopy, iterations=3)
    ce = np.zeros(F.shape + (4,), np.uint8)
    ce[can_edge] = (120, 255, 120, 200)
    ov.alpha_composite(Image.fromarray(ce, 'RGBA'))
    for n_, x, y, _ in SPAWNS:
        px_, py_ = P(x, y)
        d.ellipse([px_ - 10, py_ - 10, px_ + 10, py_ + 10], outline=(0, 255, 120, 255), width=4)
        d.text((px_ + 12, py_ - 8), n_, fill=(0, 255, 120, 255))
    d.rectangle([P(cam[0], cam[3]), P(cam[2], cam[1])], outline=(0, 255, 255, 255), width=4)
    dbg.alpha_composite(ov)
    dbg.convert('RGB').save(os.path.join(args.out, 'debug_full.png'))
    dbg.resize((F.W // 2, F.H // 2), Image.LANCZOS).convert('RGB').save(os.path.join(args.out, 'debug.png'))
    for name, arr in (('layer_canopy', canopy), ('layer_mist', mist), ('layer_glow', glow)):
        to_image(arr).resize((F.W // 4, F.H // 4), Image.NEAREST).save(os.path.join(args.out, name + '.png'))

    rep = [f'map id {MAP_ID}  size {F.W}x{F.H}px  ground chunk {args.chunk}',
           f'tiles {len(tiles)} (ground {len(gt)}, objects {len(otiles)})  cells {sum(len(l_["cells"]) for l_ in layers)}',
           f'objects {len(objects)}  collisions {len(rects)}  slope cells {len(slopes)}  camera {cam}',
           f'atlas files {len([k for k in files if k.endswith(".png")])}  zip {os.path.getsize(zpath)} bytes',
           'issues:'] + ['  ' + i for i in issues] + ([f'  坡度格不在可走区域: {bad_slopes}'] if bad_slopes else [])
    open(os.path.join(args.out, 'report.txt'), 'w').write('\n'.join(rep) + '\n')
    print('\n'.join(rep))


if __name__ == '__main__':
    main()

"""参考渲染：画布地图（formatVersion 3）资源包 → PNG。

用法：python render_ref.py <资源包.zip> <输出目录> [像素/格，默认 48] [--debug]
按游戏规则排序：排序层优先，同层按排序值；按 Y 排序的物件取 (int)(-32×Y)。
--debug 叠加碰撞（红：实心，橙：折线）、坡度（蓝正橙负）、出生点（绿）与相机中心范围（青）。
"""
import io
import json
import math
import os
import sys
import zipfile

import numpy as np
from PIL import Image, ImageDraw

SORTING_LAYERS = ['Default', 'Background', 'BelowCharacter', 'Character', 'Overlay', 'OnTop', 'EffectOverlay', 'UI']
CHUNK = 1024


def rank(layer):
    return SORTING_LAYERS.index(layer) if layer in SORTING_LAYERS else 0


class Pack:
    def __init__(self, path):
        self.z = zipfile.ZipFile(path)
        self.config = json.loads(self.z.read('ResourceEx.json'))
        self.label = (self.config.get('packInfo') or {}).get('label')

    def image(self, path):
        if path.startswith('rex://'):
            pack, _, path = path[6:].partition('/')
            if pack != self.label:
                raise ValueError(f'cross-package image not available offline: {path}')
        return Image.open(io.BytesIO(self.z.read(path))).convert('RGBA')


def tint(img, color):
    if not color or tuple(color) == (1, 1, 1, 1):
        return img
    arr = np.asarray(img, np.float32) * np.asarray(color, np.float32)
    return Image.fromarray(np.clip(arr + 0.5, 0, 255).astype(np.uint8), 'RGBA')


def compose(pack, image):
    """切块图片 → 完整 RGBA 图片。"""
    img = Image.new('RGBA', (image['width'], image['height']), (0, 0, 0, 0))
    for c, r in image['chunks']:
        img.paste(pack.image(f"{image['dir']}/{c}_{r}.png"), (c * CHUNK, r * CHUNK))
    return img


def draw_items(pack, m):
    """[(排序键, 图片, 左下角世界坐标, 每单位像素)]，按游戏绘制顺序。"""
    ppu = m.get('pixelsPerUnit', 48)
    items = []
    for i, layer in enumerate(m['layers']):
        img = tint(compose(pack, layer['image']), layer.get('color'))
        items.append(((rank(layer.get('sortingLayer', 'Background')), layer.get('sortingOrder', 0), 0, i), img, layer['x'] / ppu, layer['y'] / ppu, ppu))
    prefabs = {p['key']: p for p in m['prefabs']}
    images = {}
    for i, o in enumerate(m['objects']):
        p = prefabs[o['prefab']]
        if p['key'] not in images:
            images[p['key']] = compose(pack, p['image'])
        img = images[p['key']]
        sx, sy = o.get('scale', [1, 1])
        if sx < 0:
            img = img.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        if sy < 0:
            img = img.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        pp = p.get('pixelsPerUnit', 48) / max(abs(sx), abs(sy))
        px, py = p.get('pivot', [0.5, 0])
        px, py = (1 - px if sx < 0 else px), (1 - py if sy < 0 else py)
        order = int(o['y'] * -32) if p.get('sortByY', True) else p.get('sortingOrder', 0)
        items.append(((rank(p.get('sortingLayer', 'Character')), order, 1, i), img,
                      o['x'] - px * img.width / pp, o['y'] - py * img.height / pp, pp))
    items.sort(key=lambda t: t[0])
    return items


def extent(m):
    ppu = m.get('pixelsPerUnit', 48)
    xs, ys = [], []
    for layer in m['layers']:
        xs += [layer['x'] / ppu, (layer['x'] + layer['image']['width']) / ppu]
        ys += [layer['y'] / ppu, (layer['y'] + layer['image']['height']) / ppu]
    for o in m['objects']:
        xs.append(o['x'])
        ys.append(o['y'])
    xs += [p[0] for p in m['camera'].get('bounds') or []]
    ys += [p[1] for p in m['camera'].get('bounds') or []]
    return math.floor(min(xs)) - 2, math.floor(min(ys)) - 2, math.ceil(max(xs)) + 2, math.ceil(max(ys)) + 2


def render(pack, m, scale, debug):
    x0, y0, x1, y1 = extent(m)
    W, H = int((x1 - x0) * scale), int((y1 - y0) * scale)
    canvas = Image.new('RGBA', (W, H), (24, 24, 32, 255))
    for _, img, x, y, ppu in draw_items(pack, m):
        k = scale / ppu
        if abs(k - 1) > 1e-6:
            img = img.resize((max(1, round(img.width * k)), max(1, round(img.height * k))), Image.NEAREST if k > 1 else Image.BOX)
        px, py = round((x - x0) * scale), round((y1 - y) * scale) - img.height
        canvas.alpha_composite(img, (max(0, px), max(0, py)), (max(0, -px), max(0, -py)))
    if debug:
        draw_debug(pack, canvas, m, (x0, y1), scale)
    return canvas.convert('RGB')


def collider_polys(c, ox=0.0, oy=0.0, sx=1.0, sy=1.0):
    """碰撞 → [(点列, 是否闭合, 是否实心)]，坐标为世界单位。"""
    def P(x, y):
        return ox + x * sx, oy + y * sy
    if c['shape'] == 'polygon':
        return [([P(*p) for p in path], True, True) for path in c['paths']]
    if c['shape'] == 'edge':
        return [([P(*p) for p in c['points']], c.get('closed', False), False)]
    if c['shape'] == 'box':
        a = math.radians(c.get('rotation', 0))
        hw, hh = c['width'] / 2, c['height'] / 2
        pts = [(c['x'] + dx * math.cos(a) - dy * math.sin(a), c['y'] + dx * math.sin(a) + dy * math.cos(a))
               for dx, dy in ((-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh))]
        return [([P(*p) for p in pts], True, True)]
    if c['shape'] == 'circle':
        return [([P(c['x'] + c['radius'] * math.cos(t / 16 * math.pi), c['y'] + c['radius'] * math.sin(t / 16 * math.pi)) for t in range(32)], True, True)]
    return []


def draw_debug(pack, canvas, m, origin, scale):
    ov = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)

    def P(x, y):
        return (x - origin[0]) * scale, (origin[1] - y) * scale
    for cell in (m.get('height') or {}).get('cells', []):
        x, y, s = cell['x'], cell['y'], cell['slope']
        dr.rectangle([P(x, y + 1), P(x + 1, y)], fill=(60, 160, 255, 90) if s > 0 else (255, 140, 60, 90))
        dr.line([P(x + 0.1, y + 0.5 - s * 0.4), P(x + 0.9, y + 0.5 + s * 0.4)], fill=(255, 255, 255, 220), width=2)
    prefabs = {p['key']: p for p in m['prefabs']}
    shapes = [s for c in m['colliders'] for s in collider_polys(c)]
    for o in m['objects']:
        sx, sy = o.get('scale', [1, 1])
        shapes += [s for c in prefabs[o['prefab']].get('colliders', []) for s in collider_polys(c, o['x'], o['y'], sx, sy)]
    for pts, closed, solid in shapes:
        xy = [P(*p) for p in pts]
        if solid:
            dr.polygon(xy, fill=(255, 40, 60, 50))
        dr.line(xy + (xy[:1] if closed else []), fill=(255, 40, 60, 230) if solid else (255, 160, 0, 230), width=3)
    bounds = m['camera'].get('bounds')
    if bounds:
        xy = [P(*p) for p in bounds]
        dr.line(xy + xy[:1], fill=(0, 255, 255, 255), width=3)
    for s in m['spawnMarkers']:
        x, y = P(s['x'], s['y'])
        dr.ellipse([x - 7, y - 7, x + 7, y + 7], outline=(0, 255, 120, 255), width=3)
    canvas.alpha_composite(ov)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    if len(args) < 2:
        sys.exit(__doc__)
    path, out = args[0], args[1]
    scale = float(args[2]) if len(args) > 2 else 48
    debug = '--debug' in sys.argv
    os.makedirs(out, exist_ok=True)
    pack = Pack(path)
    for m in pack.config.get('dayMaps', []):
        target = os.path.join(out, f"{m['id']}_{int(scale)}{'_debug' if debug else ''}.png")
        render(pack, m, scale, debug).save(target)
        print(target)


if __name__ == '__main__':
    main()

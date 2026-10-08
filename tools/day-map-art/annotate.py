"""1:1 标注图：在地图预览上叠加坐标网格、碰撞、坡度、出生点、相机范围、物件与分区标注，右侧附图例。

  python tools/day-map-art/annotate.py <资源包.zip> <1:1 预览.png> <输出.png> --font <12 像素字体.ttf> [--labels <labels.json>]

碰撞、坡度、出生点、相机、物件位置全部从资源包的 ResourceEx.json 读取，与游戏实际加载的数据一致。
labels.json（可选）：zones 分区文字、names 物件显示名（按名称去掉数字后的前缀匹配）、overrides 按坐标改名、
extra 额外地点、dots 只画点不写字的重复小物件。
"""
import argparse
import json
import math
import re
import zipfile

from PIL import Image, ImageDraw, ImageFont

ML, MT, MB, MR = 96, 84, 84, 820          # 边距：左、上、下、右（右侧放图例）
C_COLL = (255, 50, 70)
C_UP, C_DOWN = (70, 160, 255), (255, 150, 40)
C_SPAWN = (40, 255, 130)
C_CAM = (0, 230, 255)
C_TEXT, C_STROKE = (255, 255, 255), (20, 22, 28)


class Fonts:
    def __init__(self, path):
        self.path = path
        self.cache = {}

    def __call__(self, k):
        if k not in self.cache:
            self.cache[k] = ImageFont.truetype(self.path, 12 * k)
        return self.cache[k]


def map_frame(m):
    tiles = {t['key']: t for t in m['tiles']}
    ground = next(l for l in m['layers'] if l['name'] == 'Ground')
    xs, ys, xe, ye = [], [], [], []
    for c in ground['cells']:
        t = tiles[c['tile']]
        w, h = t['rect'][2] / t['pixelsPerUnit'], t['rect'][3] / t['pixelsPerUnit']
        xs.append(c['x']); ys.append(c['y']); xe.append(c['x'] + w); ye.append(c['y'] + h)
    ppu = tiles[ground['cells'][0]['tile']]['pixelsPerUnit']
    return min(xs), min(ys), max(xe), max(ye), ppu


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('zip')
    ap.add_argument('preview')
    ap.add_argument('out')
    ap.add_argument('--font', required=True)
    ap.add_argument('--labels')
    a = ap.parse_args()
    with zipfile.ZipFile(a.zip) as z:
        cfg = json.loads(z.read('ResourceEx.json'))
    m = cfg['dayMaps'][0]
    lab = json.load(open(a.labels, encoding='utf-8')) if a.labels else {}
    X0, Y0, X1, Y1, P = map_frame(m)
    F = Fonts(a.font)

    base = Image.open(a.preview).convert('RGBA')
    W, H = base.size
    img = Image.new('RGBA', (ML + W + MR, MT + H + MB), (26, 28, 34, 255))
    img.paste(base, (ML, MT))

    def px(x, y):
        return ML + (x - X0) * P, MT + (Y1 - y) * P

    ov = Image.new('RGBA', img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    d.fontmode = '1'

    def text(xy, s, k=2, fill=C_TEXT, anchor='la', stroke=None, d_=None):
        (d_ or d).text(xy, s, font=F(k), fill=fill, anchor=anchor, stroke_width=stroke if stroke is not None else max(2, k), stroke_fill=C_STROKE)

    # ---------------------------------------------------- 网格与标尺
    for gx in range(int(math.ceil(X0)), int(X1) + 1):
        x, _ = px(gx, 0)
        major = gx % 4 == 0
        d.line([(x, MT), (x, MT + H)], fill=(255, 255, 255, 70 if major else 26), width=1)
        if gx % 2 == 0:
            for yy, anc in ((MT - 10, 'mb'), (MT + H + 10, 'mt')):
                text((x, yy), str(gx), 2 if major else 1, fill=(255, 235, 120) if major else (200, 200, 200), anchor=anc, d_=d)
    for gy in range(int(math.ceil(Y0)), int(Y1) + 1):
        _, y = px(0, gy)
        major = gy % 4 == 0
        d.line([(ML, y), (ML + W, y)], fill=(255, 255, 255, 70 if major else 26), width=1)
        if gy % 2 == 0:
            text((ML - 10, y), str(gy), 2 if major else 1, fill=(120, 230, 255) if major else (200, 200, 200), anchor='rm', d_=d)
            text((ML + W + 10, y), str(gy), 2 if major else 1, fill=(120, 230, 255) if major else (200, 200, 200), anchor='lm', d_=d)
    ox, oy = px(0, 0)
    d.line([(ox, MT), (ox, MT + H)], fill=(255, 235, 120, 120), width=2)
    d.line([(ML, oy), (ML + W, oy)], fill=(120, 230, 255, 120), width=2)
    text((ML - 10, MT - 10), 'y＼x', 1, anchor='rb')

    # ---------------------------------------------------- 碰撞
    for r in m['collisions']:
        a0 = px(r['x'] - r['width'] / 2, r['y'] + r['height'] / 2)
        a1 = px(r['x'] + r['width'] / 2, r['y'] - r['height'] / 2)
        d.rectangle([a0, (a1[0] - 1, a1[1] - 1)], fill=C_COLL + (62,), outline=C_COLL + (150,))

    # ---------------------------------------------------- 坡度
    for c in m['height']['cells']:
        s = c['slope']
        col = C_UP if s > 0 else C_DOWN
        a0, a1 = px(c['x'], c['y'] + 1), px(c['x'] + 1, c['y'])
        d.rectangle([a0, (a1[0] - 1, a1[1] - 1)], fill=col + (120,), outline=(255, 255, 255, 200), width=2)
        cx, cy = (a0[0] + a1[0]) / 2, (a0[1] + a1[1]) / 2
        d.line([(cx - 16, cy + 16 * s), (cx + 16, cy - 16 * s)], fill=(255, 255, 255, 255), width=3)
        text((cx, a1[1] - 4), f'{s:+.2f}', 1, anchor='mb')

    # ---------------------------------------------------- 相机
    cam = m['camera']['bounds']
    d.rectangle([px(cam[0], cam[3]), px(cam[2], cam[1])], outline=C_CAM + (255,), width=4)
    text((px(cam[0], cam[3])[0] + 8, px(cam[0], cam[3])[1] + 6), '相机跟随点范围（camera.bounds）', 2, fill=C_CAM)
    spawns = {s['name']: s for s in m['spawnMarkers']}
    sp = spawns[m['defaultSpawnMarker']]
    ccx = min(max(sp['x'], cam[0]), cam[2])
    ccy = min(max(sp['y'], cam[1]), cam[3]) + 0.5          # 相机跟随点受 bounds 限制，画面中心再上移 0.5 格
    hw, hh = 40 / 3, 7.5
    v0, v1 = px(ccx - hw, ccy + hh), px(ccx + hw, ccy - hh)
    for i in range(int(v0[0]), int(v1[0]), 24):
        d.line([(i, v0[1]), (min(i + 12, v1[0]), v0[1])], fill=(200, 255, 255, 230), width=3)
        d.line([(i, v1[1]), (min(i + 12, v1[0]), v1[1])], fill=(200, 255, 255, 230), width=3)
    for j in range(int(v0[1]), int(v1[1]), 24):
        d.line([(v0[0], j), (v0[0], min(j + 12, v1[1]))], fill=(200, 255, 255, 230), width=3)
        d.line([(v1[0], j), (v1[0], min(j + 12, v1[1]))], fill=(200, 255, 255, 230), width=3)
    text((v0[0] + 8, v1[1] - 8), f'默认出生点 {sp["name"]} 的画面范围（16:9，约 26.7×15 格）', 2, fill=(200, 255, 255), anchor='lb')

    img.alpha_composite(ov)
    ov = Image.new('RGBA', img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    d.fontmode = '1'

    # ---------------------------------------------------- 分区
    for z in lab.get('zones', []):
        k = z.get('size', 2)
        x, y = px(z['x'], z['y'])
        if z.get('vertical'):
            s = z['text'].replace(' · ', '·')
            for i, ch in enumerate(s):
                text((x, y + i * 12 * k), ch, k, fill=(255, 248, 220), anchor='mt', stroke=k + 1)
        else:
            text((x, y), z['text'], k, fill=(255, 248, 220), anchor='mm', stroke=k + 1)

    # ---------------------------------------------------- 物件
    names, dots = lab.get('names', {}), lab.get('dots', {})
    overrides = lab.get('overrides', [])
    boxes = []

    def free(b):
        if b[0] < ML or b[2] > ML + W or b[1] < MT or b[3] > MT + H:
            return False
        return all(b[2] <= o[0] or b[0] >= o[2] or b[3] <= o[1] or b[1] >= o[3] for o in boxes)

    def place(x, y, s, color, k=2):
        f = F(k)
        tw = d.textlength(s, font=f) + 2 * k
        th = 12 * k + 2 * k
        cands = [(14, -th / 2), (14, -th - 6), (-tw - 14, -th / 2), (-tw / 2, 12), (-tw / 2, -th - 30), (14, 10),
                 (-tw - 14, -th - 6), (-tw - 14, 10), (24, -th - 30), (-tw - 24, -th - 30), (-tw / 2, 40), (30, 30)]
        for dx, dy in cands:
            b = (x + dx, y + dy, x + dx + tw, y + dy + th)
            if free(b):
                break
        boxes.append(b)
        lx = b[0] if b[0] > x else (b[2] if b[2] < x else x)
        ly = (b[1] + b[3]) / 2 if not (b[0] <= x <= b[2]) else (b[1] if b[1] > y else b[3])
        if math.hypot(lx - x, ly - y) > 16:
            d.line([(x, y), (lx, ly)], fill=color + (200,), width=2)
        text((b[0] + k, b[1] + k), s, k, fill=color, stroke=k)

    counts = {}
    labeled = []
    for o in m['objects']:
        kind = re.sub(r'\d+$', '', o['name'])
        x, y = px(o['x'], o['y'])
        dot = next((v for p, v in dots.items() if kind.startswith(p)), None)
        name = next((ov_['text'] for ov_ in overrides if abs(ov_['x'] - o['x']) < 0.05 and abs(ov_['y'] - o['y']) < 0.05), None)
        if name is None and dot:
            col = tuple(int(dot[1][i:i + 2], 16) for i in (1, 3, 5))
            r = 5
            d.ellipse([x - r, y - r, x + r, y + r], fill=col + (255,), outline=C_STROKE + (255,), width=2)
            counts[dot[0]] = counts.get(dot[0], 0) + 1
            continue
        name = name or names.get(kind, kind)
        labeled.append((x, y, name))
        d.ellipse([x - 7, y - 7, x + 7, y + 7], fill=(255, 120, 200, 255), outline=C_STROKE + (255,), width=2)
    for e in lab.get('extra', []):
        x, y = px(e['x'], e['y'])
        d.polygon([(x, y - 8), (x + 8, y), (x, y + 8), (x - 8, y)], fill=(255, 210, 90, 255), outline=C_STROKE + (255,))
        labeled.append((x, y, e['text']))

    # ---------------------------------------------------- 出生点（先占位，标注避让它们）
    arrows = {'Up': (0, -1), 'Down': (0, 1), 'Left': (-1, 0), 'Right': (1, 0)}
    sp_labels = []
    for s in m['spawnMarkers']:
        x, y = px(s['x'], s['y'])
        d.ellipse([x - 16, y - 16, x + 16, y + 16], outline=C_SPAWN + (255,), width=5)
        d.ellipse([x - 5, y - 5, x + 5, y + 5], fill=C_SPAWN + (255,))
        ax, ay = arrows.get(s['rotation'], (0, 0))
        tip = (x + ax * 40, y + ay * 40)
        d.line([(x + ax * 16, y + ay * 16), tip], fill=C_SPAWN + (255,), width=5)
        d.polygon([tip, (tip[0] - ay * 9 - ax * 12, tip[1] - ax * 9 - ay * 12), (tip[0] + ay * 9 - ax * 12, tip[1] + ax * 9 - ay * 12)],
                  fill=C_SPAWN + (255,))
        boxes.append((x - 20, y - 20, x + 20, y + 20))
        sp_labels.append((x, y, s))
    for x, y, s in sp_labels:
        star = '★' if s['name'] == m['defaultSpawnMarker'] else ''
        place(x, y, f"{star}出生点 {s['name']} ({s['x']:g}, {s['y']:g})", C_SPAWN, 2)
    for x, y, name in sorted(labeled, key=lambda t: (t[1], t[0])):
        place(x, y, name, (255, 200, 235), 2)

    # 比例尺
    sx, sy = ML, MT + H + 52
    for i in range(5):
        d.rectangle([sx + i * P, sy, sx + (i + 1) * P - 1, sy + 10], fill=(255, 255, 255, 255) if i % 2 == 0 else (90, 90, 90, 255))
    text((sx + 5 * P + 12, sy + 5), f'5 格（1 格 = {P} 像素，人物约 1 格高）', 1, anchor='lm')

    img.alpha_composite(ov)

    # ---------------------------------------------------- 图例
    d = ImageDraw.Draw(img)
    d.fontmode = '1'
    lx, ly = ML + W + 70, MT
    lw = MR - 100

    def line(s, k=2, fill=C_TEXT, gap=10):
        nonlocal ly
        d.text((lx, ly), s, font=F(k), fill=fill)
        ly += 12 * k + gap

    def swatch(fill, outline, s, shape='rect'):
        nonlocal ly
        if shape == 'rect':
            d.rectangle([lx, ly + 2, lx + 34, ly + 22], fill=fill, outline=outline, width=2)
        elif shape == 'dot':
            d.ellipse([lx + 9, ly + 4, lx + 25, ly + 20], fill=fill, outline=outline, width=2)
        elif shape == 'ring':
            d.ellipse([lx + 3, ly, lx + 27, ly + 24], outline=fill, width=4)
        elif shape == 'diamond':
            d.polygon([(lx + 17, ly + 2), (lx + 29, ly + 12), (lx + 17, ly + 22), (lx + 5, ly + 12)], fill=fill, outline=outline)
        elif shape == 'box':
            d.rectangle([lx, ly + 2, lx + 34, ly + 22], outline=fill, width=4)
        d.text((lx + 48, ly), s, font=F(2), fill=C_TEXT)
        ly += 36

    pi = cfg['packInfo']
    line(f"{lab.get('title', m['name'])}", 4, (255, 248, 220), 16)
    line(f"地图 ID {m['id']}（formatVersion {m['formatVersion']}）", 2)
    line(f"资源包 {pi['label']} v{pi['version']}", 2)
    line(f"范围 x {X0:g}~{X1:g}，y {Y0:g}~{Y1:g}（{X1 - X0:g}×{Y1 - Y0:g} 格）", 2)
    line(f"本图 1:1：{P} 像素 = 1 格，{W}×{H} 像素", 2, gap=30)

    line('图例', 3, (255, 235, 120), 14)
    swatch(C_COLL + (110,), C_COLL, f"碰撞矩形（{len(m['collisions'])} 个）")
    swatch(C_UP + (200,), (255, 255, 255), '坡度格，正值（向右走上升）')
    swatch(C_DOWN + (200,), (255, 255, 255), '坡度格，负值（向右走下降）')
    swatch(C_CAM, None, '相机跟随点范围（画面中心再上移 0.5 格）', 'box')
    swatch((200, 255, 255), None, '默认出生点的实际画面范围（虚线）', 'box')
    swatch(C_SPAWN, None, '出生点，箭头为朝向；★为默认', 'ring')
    swatch((255, 120, 200), C_STROKE, '物件脚点（按 Y 排序的锚点）', 'dot')
    swatch((255, 210, 90), C_STROKE, '地面图上的地标（非物件）', 'diamond')
    for dot_name, col in dots.values():
        c = tuple(int(col[i:i + 2], 16) for i in (1, 3, 5))
        n = counts.get(dot_name, 0)
        if n:
            swatch(c, C_STROKE, f'{dot_name} ×{n}', 'dot')
    ly += 10
    d.line([(lx, ly), (lx + 60, ly)], fill=(255, 255, 255), width=2)
    d.text((lx + 72, ly - 12), '网格：细线 1 格，粗线 4 格', font=F(2), fill=C_TEXT)
    ly += 44

    line('出生点', 3, (255, 235, 120), 14)
    rot = {'Up': '上', 'Down': '下', 'Left': '左', 'Right': '右'}
    for s in m['spawnMarkers']:
        star = ' ★默认' if s['name'] == m['defaultSpawnMarker'] else ''
        line(f"{s['name']}  ({s['x']:g}, {s['y']:g})  朝{rot.get(s['rotation'], s['rotation'])}{star}", 2, C_SPAWN, 8)
    ly += 20

    line('图层', 3, (255, 235, 120), 14)
    tiles = {t['key']: t for t in m['tiles']}
    for l in m['layers']:
        line(f"{l['name']}：{l['sortingLayer']} / {l.get('sortingOrder', 0)}，{len(l['cells'])} 块", 2, gap=8)
    n_sort = sum(1 for o in m['objects'] if o.get('sortByY'))
    line(f"物件 {len(m['objects'])} 个（按 Y 排序 {n_sort} 个）", 2, gap=8)
    line(f"切片 {len(tiles)} 个，坡度格 {len(m['height']['cells'])} 个", 2, gap=30)

    line('说明', 3, (255, 235, 120), 14)
    for s in ['碰撞、坡度、出生点、相机、物件位置',
              '均读自资源包，与游戏加载的一致。',
              '碰撞只有轴对齐矩形，斜岸处为台阶状。',
              '坡度只改变横向移动：y += 坡度 × x。',
              '树冠在顶层，人物走入林缘会被遮住。',
              '人魂、流灯不挡路；告示与碑文为像素字。']:
        line(s, 2, (210, 210, 210), 6)

    img.convert('RGB').save(a.out, optimize=True)
    print(img.size, len(labeled), 'labels', counts)


if __name__ == '__main__':
    main()

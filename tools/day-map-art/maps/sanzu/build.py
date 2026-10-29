"""三途川地图构建：绘制 → 切片打包 → 碰撞与检查 → 资源包 ZIP 与预览图。

用法：python build.py <输出目录> [--font <像素字体.ttf>]
输出：SanzuRiver.zip、preview.png（1:1 合成）、overview.png（1/4）、debug.png（碰撞/坡度/出生点）、report.txt
"""
import argparse
import json
import os
import sys
import zipfile

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE.rsplit('/maps/', 1)[0])
sys.path.insert(0, HERE)
import layout as L
from paint import SanzuMap
from dayart.collide import WalkGrid
from dayart.core import paste, to_image
from dayart.pack import FORMAT_VERSION, Prefabs, camera_bounds, canvas_layer, sprite_pivot, synth_bgm, write_zip
from dayart.validate import validate_map

MAP_ID = 1073742300       # 非托管 ID 段（≥1073741824），无需签名
FOLDER = 'map/sanzu'


def composite(m, with_overlay=True):
    """按游戏的层级合成预览：地面 → 物件（按脚点 Y 从上到下）→ 前景层物件 → 树冠 → 雾 → 光。"""
    img = m.ground.copy()
    objs = sorted([o for o in m.objects if o.layer == 'Character'], key=lambda o: -o.y)
    for o in objs:
        px, py = m.px(o.x, o.y)
        paste(img, o.spr.a, px - o.spr.pivot[0], py - o.spr.pivot[1], o.alpha)
    if with_overlay:
        for layer in (m.canopy,):
            paste(img, layer, 0, 0)
        for o in sorted([o for o in m.objects if o.layer == 'Overlay' and o.order < 10], key=lambda o: o.order):
            px, py = m.px(o.x, o.y)
            paste(img, o.spr.a, px - o.spr.pivot[0], py - o.spr.pivot[1], o.alpha)
        paste(img, m.fog, 0, 0)
        paste(img, m.glow, 0, 0)
        for o in sorted([o for o in m.objects if o.layer == 'Overlay' and o.order >= 10], key=lambda o: o.order):
            px, py = m.px(o.x, o.y)
            paste(img, o.spr.a, px - o.spr.pivot[0], py - o.spr.pivot[1], o.alpha)
    img[..., 3] = 1
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('out')
    ap.add_argument('--font')
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    m = SanzuMap(font=args.font).run()
    F = m.F

    # ---------------------------------------------------- 碰撞
    g = WalkGrid(L.X0, L.Y0, L.X1, L.Y1, res=4)
    g.walk = g.from_pixels(m.walk, F.ppu, 0.6)
    for o in m.objects:
        for dx, dy, w, h in o.collide:
            g.rect_block(o.x + dx, o.y + dy, w, h)
    rects = g.rectangles(only_near=1.5)
    slopes = m.slope_cells()
    spawns = [(n, x, y) for n, x, y, _ in L.SPAWNS]
    issues = g.check(spawns, radius=0.3)
    bad_slopes = g.check_slopes(slopes)

    # ---------------------------------------------------- 图层与物件模板
    left, bottom = round(F.x0 * F.ppu), round(F.y0 * F.ppu)
    layers, files = [], {}
    for name, arr, layer, order in (('Ground', m.ground, 'Background', -2000), ('Canopy', m.canopy, 'Overlay', 0),
                                    ('Fog', m.fog, 'Overlay', 10), ('Glow', m.glow, 'Overlay', 20)):
        cfg, chunks = canvas_layer(name, to_image(arr), left, bottom, f'{FOLDER}/{name.lower()}', layer, order)
        if cfg:
            layers.append(cfg)
            files.update(chunks)
    prefabs = Prefabs(f'{FOLDER}/prefabs')
    objects = []
    for o in m.objects:
        spr = o.spr
        if o.alpha < 1:
            spr.a[..., 3] *= o.alpha
        key = prefabs.add(o.name, spr.image(), sprite_pivot(spr), o.layer, 0 if o.sort else o.order, bool(o.sort))
        objects.append(dict(name=o.name, prefab=key, x=round(float(o.x), 4), y=round(float(o.y), 4), scale=[1, 1]))
    files.update(prefabs.files)
    files[f'{FOLDER}/sanzu_bgm.wav'] = synth_bgm()

    cam = camera_bounds(L.X0, L.Y0, L.X1, L.Y1)
    day_map = dict(
        id=MAP_ID, formatVersion=FORMAT_VERSION, name='三途川',
        description='此岸的赛之河原与渡口。河上浓雾终日不散，雾里时有微光闪烁；对岸是只能远眺的彼岸花原。',
        pixelsPerUnit=F.ppu, layers=layers, prefabs=prefabs.items, objects=objects, colliders=rects, height=dict(cells=slopes),
        spawnMarkers=[dict(name=n, x=x, y=y, rotation=r) for n, x, y, r in L.SPAWNS],
        defaultSpawnMarker=L.DEFAULT_SPAWN,
        camera=dict(shouldFollow=True, bounds=cam, position=[0, 0, -10]),
        mapBGM=dict(intro=f'{FOLDER}/sanzu_bgm.wav', loop=f'{FOLDER}/sanzu_bgm.wav'))
    config = dict(packInfo=dict(name='三途川', label='SanzuRiver', version='0.1.0', authors=['MetaMystia'],
                                description='三途川白天地图（美术与碰撞首版，不含 NPC 与出口）', dependencies=['CORE']),
                  dayMaps=[day_map])
    zpath = os.path.join(args.out, 'SanzuRiver.zip')
    write_zip(zpath, config, files)
    with zipfile.ZipFile(zpath) as z:
        issues += [f'加载校验: {e}' for e in validate_map(json.loads(z.read('ResourceEx.json'))['dayMaps'][0], z)]

    # ---------------------------------------------------- 预览
    prev = to_image(composite(m))
    prev.convert('RGB').save(os.path.join(args.out, 'preview.png'))
    prev.resize((F.W // 4, F.H // 4), Image.LANCZOS).convert('RGB').save(os.path.join(args.out, 'overview.png'))
    prev.resize((F.W // 2, F.H // 2), Image.LANCZOS).convert('RGB').save(os.path.join(args.out, 'preview_half.png'))
    to_image(composite(m, False)).convert('RGB').save(os.path.join(args.out, 'preview_noover.png'))
    dbg = prev.convert('RGBA')
    ov = Image.new('RGBA', dbg.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    P = m.px
    for r in rects:
        d.rectangle([P(r['x'] - r['width'] / 2, r['y'] + r['height'] / 2), P(r['x'] + r['width'] / 2, r['y'] - r['height'] / 2)],
                    fill=(255, 40, 60, 60), outline=(255, 40, 60, 150))
    for c in slopes:
        col = (60, 160, 255, 120) if c['slope'] > 0 else (255, 140, 40, 120)
        d.rectangle([P(c['x'], c['y'] + 1), P(c['x'] + 1, c['y'])], fill=col, outline=(255, 255, 255, 160))
        s = c['slope']
        d.line([P(c['x'] + 0.1, c['y'] + 0.5 - s * 0.4), P(c['x'] + 0.9, c['y'] + 0.5 + s * 0.4)], fill=(255, 255, 255, 255), width=3)
    for n, x, y, _ in L.SPAWNS:
        px_, py_ = P(x, y)
        d.ellipse([px_ - 10, py_ - 10, px_ + 10, py_ + 10], outline=(0, 255, 120, 255), width=4)
        d.text((px_ + 12, py_ - 8), n, fill=(0, 255, 120, 255))
    d.line([P(x, y) for x, y in cam + cam[:1]], fill=(0, 255, 255, 255), width=4)
    dbg.alpha_composite(ov)
    dbg.resize((F.W // 2, F.H // 2), Image.LANCZOS).convert('RGB').save(os.path.join(args.out, 'debug.png'))
    for name, arr in (('layer_ground', m.ground), ('layer_canopy', m.canopy), ('layer_fog', m.fog), ('layer_glow', m.glow)):
        to_image(arr).resize((F.W // 4, F.H // 4), Image.NEAREST).save(os.path.join(args.out, name + '.png'))

    rep = [f'map id {MAP_ID}  size {F.W}x{F.H}px',
           f'layers {len(layers)} ({sum(len(l["image"]["chunks"]) for l in layers)} chunks)  prefabs {len(prefabs.items)}',
           f'objects {len(objects)}  colliders {len(rects)}  slope cells {len(slopes)}  camera {cam}',
           f'png files {len([k for k in files if k.endswith(".png")])}',
           'issues:'] + ['  ' + i for i in issues] + ([f'  坡度格不在可走区域: {bad_slopes}'] if bad_slopes else [])
    open(os.path.join(args.out, 'report.txt'), 'w').write('\n'.join(rep) + '\n')
    print('\n'.join(rep))


if __name__ == '__main__':
    main()

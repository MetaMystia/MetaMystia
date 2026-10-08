"""离线校验资源包里的 dayMaps，规则与 Mod 的 DayMapRegistry.Validate（formatVersion 1）一致。

用法：python -m dayart.validate <资源包.zip>
只证明配置能通过加载校验；不能证明可达性、遮挡和手感，这些要在游戏里走一遍。
"""
import io
import json
import math
import sys
import wave
import zipfile

from PIL import Image

SORTING_LAYERS = {'Default', 'Background', 'BelowCharacter', 'Character', 'Overlay', 'OnTop', 'EffectOverlay'}


def finite(*v):
    return all(isinstance(x, (int, float)) and math.isfinite(x) for x in v)


def coord(x, y):
    return finite(x, y) and abs(x) <= 4096 and abs(y) <= 4096


def validate_map(c, z):
    err = []
    names = set(z.namelist())
    if c.get('formatVersion') != 1 or not str(c.get('name', '')).strip():
        return ['formatVersion/name invalid']
    tiles, layers, objects = c['tiles'], c['layers'], c['objects']
    cols, spawns = c['collisions'], c['spawnMarkers']
    if len(tiles) > 4096 or len(layers) > 32 or len(objects) > 4096 or len(cols) > 4096 or len(spawns) > 256:
        err.append('map capacity exceeded')
    sizes, keys = {}, set()
    for t in tiles:
        if not t.get('key') or t['key'] in keys:
            err.append(f"duplicate/empty tile key {t.get('key')}")
        keys.add(t['key'])
        img = t['image']
        if img not in names:
            err.append(f'image missing: {img}')
            continue
        if img not in sizes:
            sizes[img] = Image.open(io.BytesIO(z.read(img))).size
        W, H = sizes[img]
        r = t['rect']
        if len(r) != 4 or r[0] < 0 or r[1] < 0 or r[2] <= 0 or r[3] <= 0 or r[0] + r[2] > W or r[1] + r[3] > H:
            err.append(f"tile rect out of bounds: {t['key']}")
        p = t.get('pivot', [0, 0])
        if len(p) != 2 or not finite(*p) or any(v < 0 or v > 1 for v in p) or not (t.get('pixelsPerUnit', 48) > 0):
            err.append(f"tile geometry invalid: {t['key']}")
    total = 0
    for L in layers:
        if L.get('sortingLayer', 'Background') not in SORTING_LAYERS or not -32768 <= L.get('sortingOrder', -2000) <= 32767:
            err.append(f"layer settings invalid: {L.get('name')}")
        total += len(L['cells'])
        seen = set()
        for cell in L['cells']:
            if cell['tile'] not in keys or not coord(cell['x'], cell['y']) or (cell['x'], cell['y']) in seen:
                err.append(f"cell invalid in {L.get('name')}: {cell}")
                break
            seen.add((cell['x'], cell['y']))
    if total > 100000:
        err.append('cell limit exceeded')
    for o in objects:
        sc = o.get('scale', [1, 1])
        if o['tile'] not in keys or not coord(o['x'], o['y']) or len(sc) != 2 or any(v <= 0 for v in sc) \
                or o.get('sortingLayer', 'Character') not in SORTING_LAYERS or (o.get('sortByY', True) and abs(o['y']) > 1023):
            err.append(f"object settings invalid: {o.get('name')}")
    h = c.get('height')
    if h is not None:
        seen = set()
        for cell in h.get('cells', []):
            if not coord(cell['x'], cell['y']) or not finite(cell['slope']) or abs(cell['slope']) > 1 or (cell['x'], cell['y']) in seen:
                err.append(f'height cell invalid: {cell}')
            seen.add((cell['x'], cell['y']))
    for b in cols:
        if not coord(b['x'], b['y']) or not finite(b['width'], b['height']) or b['width'] <= 0 or b['height'] <= 0:
            err.append(f'collision invalid: {b}')
    sn = set()
    for m in spawns:
        if not m.get('name') or m['name'] in sn or not coord(m['x'], m['y']) or m.get('rotation', 'Down') not in ('Down', 'Up', 'Left', 'Right'):
            err.append(f"spawn invalid: {m.get('name')}")
        sn.add(m['name'])
        for b in cols:
            if abs(m['x'] - b['x']) <= b['width'] / 2 and abs(m['y'] - b['y']) <= b['height'] / 2:
                err.append(f"spawn inside collision: {m['name']}")
                break
    if c.get('defaultSpawnMarker') not in sn:
        err.append('defaultSpawnMarker missing')
    cam = c.get('camera', {})
    if len(cam.get('position', [])) != 3:
        err.append('camera position invalid')
    b = cam.get('bounds') or []
    if cam.get('shouldFollow', True) and (len(b) != 4 or b[0] >= b[2] or b[1] >= b[3]):
        err.append('camera bounds invalid')
    for k in ('intro', 'loop'):
        path = (c.get('mapBGM') or {}).get(k)
        if path not in names:
            err.append(f'BGM {k} missing')
            continue
        with wave.open(io.BytesIO(z.read(path))) as w:
            if w.getsampwidth() not in (1, 2, 3, 4) or w.getnframes() == 0:
                err.append(f'BGM {k} unsupported')
    return err


def main(path):
    with zipfile.ZipFile(path) as z:
        cfg = json.loads(z.read('ResourceEx.json'))
        ok = True
        for m in cfg.get('dayMaps', []):
            errs = validate_map(m, z)
            print(f"dayMaps[{m['id']}] {m['name']}: " + ('OK' if not errs else f'{len(errs)} errors'))
            for e in errs[:30]:
                print('  ', e)
            ok &= not errs
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv[1]))

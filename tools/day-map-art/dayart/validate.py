"""离线校验资源包里的 dayMaps，规则与 Mod 的 DayMapRegistry.Validate（formatVersion 3）一致。

用法：python -m dayart.validate <资源包.zip>
只证明配置能通过加载校验；不能证明可达性、遮挡和手感，这些要在游戏里走一遍。
原版 BGM 地址（mapBGM.native）只能在游戏内确认，这里只检查写法。
"""
import io
import json
import math
import struct
import sys
import wave
import zipfile

SORTING_LAYERS = {'Default', 'Background', 'BelowCharacter', 'Character', 'Overlay', 'OnTop', 'EffectOverlay', 'UI'}
ROTATIONS = {'Down', 'Left', 'Up', 'Right'}
CHUNK, IMAGE_LIMIT, POINT_LIMIT = 1024, 16384, 4096


def finite(*v):
    return all(isinstance(x, (int, float)) and not isinstance(x, bool) and math.isfinite(x) for x in v)


def coord(x, y):
    return finite(x, y) and abs(x) <= 4096 and abs(y) <= 4096


def numbers(v, n):
    return isinstance(v, list) and len(v) == n and finite(*v)


def points(v, minimum):
    return isinstance(v, list) and minimum <= len(v) <= POINT_LIMIT and all(numbers(p, 2) and coord(*p) for p in v)


def order(v):
    return isinstance(v, int) and -32768 <= v <= 32767


def png_size(z, path):
    """只读 PNG 头部的宽高。"""
    if path not in z.namelist():
        return None
    head = z.open(path).read(24)
    if len(head) < 24 or head[:4] != b'\x89PNG' or head[12:16] != b'IHDR':
        return None
    return struct.unpack('>II', head[16:24])


def collider_error(c):
    shape = c.get('shape') if isinstance(c, dict) else None
    ok = (shape == 'polygon' and isinstance(c.get('paths'), list) and 0 < len(c['paths']) <= 64 and all(points(p, 3) for p in c['paths'])) \
        or (shape == 'edge' and points(c.get('points'), 2)) \
        or (shape == 'box' and coord(c.get('x', 0), c.get('y', 0)) and finite(c.get('width'), c.get('height'), c.get('rotation', 0)) and c['width'] > 0 and c['height'] > 0) \
        or (shape == 'circle' and coord(c.get('x', 0), c.get('y', 0)) and finite(c.get('radius')) and c['radius'] > 0)
    return None if ok else f"collider invalid: {c.get('name') or shape if isinstance(c, dict) else c}"


def inside_path(path, x, y):
    inside = False
    j = len(path) - 1
    for i in range(len(path)):
        (xi, yi), (xj, yj) = path[i], path[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            inside = not inside
        j = i
    return inside


def contains(c, x, y):
    if c['shape'] == 'polygon':
        return sum(inside_path(p, x, y) for p in c['paths']) % 2 == 1
    if c['shape'] == 'circle':
        return (x - c.get('x', 0)) ** 2 + (y - c.get('y', 0)) ** 2 <= c['radius'] ** 2
    if c['shape'] == 'box':
        a = -math.radians(c.get('rotation', 0))
        dx, dy = x - c.get('x', 0), y - c.get('y', 0)
        return abs(dx * math.cos(a) - dy * math.sin(a)) <= c['width'] / 2 and abs(dx * math.sin(a) + dy * math.cos(a)) <= c['height'] / 2
    return False


def chunk_path(image, chunk):
    return f"{image['dir']}/{chunk[0]}_{chunk[1]}.png"


def image_error(image, z):
    if not (isinstance(image, dict) and isinstance(image.get('width'), int) and isinstance(image.get('height'), int)
            and 0 < image['width'] <= IMAGE_LIMIT and 0 < image['height'] <= IMAGE_LIMIT and image.get('dir') and isinstance(image.get('chunks'), list)):
        return 'image size/dir invalid'
    cols, rows = -(-image['width'] // CHUNK), -(-image['height'] // CHUNK)
    seen = set()
    for ch in image['chunks']:
        if not (isinstance(ch, list) and len(ch) == 2 and 0 <= ch[0] < cols and 0 <= ch[1] < rows) or tuple(ch) in seen:
            return f'chunk index invalid: {ch}'
        seen.add(tuple(ch))
        expected = (min(CHUNK, image['width'] - ch[0] * CHUNK), min(CHUNK, image['height'] - ch[1] * CHUNK))
        if png_size(z, chunk_path(image, ch)) != expected:
            return f'chunk missing or size mismatch: {chunk_path(image, ch)}'
    return None


def validate_map(c, z):
    if c.get('formatVersion') != 3 or not str(c.get('name', '')).strip():
        return ['formatVersion/name invalid']
    ppu = c.get('pixelsPerUnit', 48)
    if not isinstance(ppu, int) or ppu <= 0:
        return ['pixelsPerUnit invalid']
    layers, prefab_list, objects = c.get('layers'), c.get('prefabs'), c.get('objects')
    colliders, spawns, camera, bgm = c.get('colliders'), c.get('spawnMarkers'), c.get('camera'), c.get('mapBGM')
    if None in (layers, prefab_list, objects, colliders, spawns, camera, bgm):
        return ['required collection/camera/mapBGM is null']
    err = []
    if len(layers) > 32 or len(prefab_list) > 4096 or len(objects) > 8192 or len(colliders) > 4096 or len(spawns) > 256:
        err.append('map capacity exceeded')
    for layer in layers:
        name = layer.get('name')
        if layer.get('sortingLayer', 'Background') not in SORTING_LAYERS or not order(layer.get('sortingOrder', 0)) \
                or ('color' in layer and not numbers(layer['color'], 4)) or not all(isinstance(layer.get(k, 0), int) for k in 'xy') \
                or not coord(layer.get('x', 0) / ppu, layer.get('y', 0) / ppu):
            err.append(f'layer settings invalid: {name}')
        if (e := image_error(layer.get('image'), z)) is not None:
            err.append(f'{e} in layer {name}')
    prefabs = {}
    for p in prefab_list:
        key = p.get('key')
        if not key or key in prefabs:
            err.append(f'duplicate/empty prefab key {key}')
            continue
        prefabs[key] = p
        if (e := image_error(p.get('image'), z)) is not None:
            err.append(f'{e} in prefab {key}')
        if not numbers(p.get('pivot', [0.5, 0]), 2) or not finite(p.get('pixelsPerUnit', 48)) or p.get('pixelsPerUnit', 48) <= 0 \
                or p.get('sortingLayer', 'Character') not in SORTING_LAYERS or not order(p.get('sortingOrder', 0)):
            err.append(f'prefab settings invalid: {key}')
        err += [f'{e} in prefab {key}' for e in map(collider_error, p.get('colliders', [])) if e]
    for o in objects:
        p = prefabs.get(o.get('prefab'))
        scale = o.get('scale', [1, 1])
        if p is None or not coord(o.get('x', 0), o.get('y', 0)) or not numbers(scale, 2) or 0 in scale \
                or (p.get('sortByY', True) and abs(o.get('y', 0)) > 1023):
            err.append(f"object invalid: {o.get('name') or o.get('prefab')}")
    err += [e for e in map(collider_error, colliders) if e]
    h = c.get('height')
    if h is not None:
        cells = h.get('cells')
        seen = set()
        if not isinstance(cells, list) or len(cells) > 100000:
            err.append('height cells missing or capacity exceeded')
        else:
            for cell in cells:
                key = (cell.get('x'), cell.get('y'))
                if not all(isinstance(v, int) for v in key) or not coord(*key) or not finite(cell.get('slope')) or abs(cell['slope']) > 1 or key in seen:
                    err.append(f'height cell position/slope invalid: {cell}')
                    break
                seen.add(key)
    if err:
        return err
    names = set()
    for m in spawns:
        if not m.get('name') or m['name'] in names or not coord(m.get('x', 0), m.get('y', 0)) or m.get('rotation', 'Down') not in ROTATIONS:
            err.append(f"spawn marker invalid: {m.get('name')}")
            continue
        names.add(m['name'])
        x, y = m.get('x', 0), m.get('y', 0)
        blocked = any(contains(b, x, y) for b in colliders) or any(
            contains(b, (x - o.get('x', 0)) / o.get('scale', [1, 1])[0], (y - o.get('y', 0)) / o.get('scale', [1, 1])[1])
            for o in objects for b in prefabs[o['prefab']].get('colliders', []))
        if blocked:
            err.append(f"spawn inside collision: {m['name']}")
    if c.get('defaultSpawnMarker') not in names:
        err.append('defaultSpawnMarker missing')
    if not numbers(camera.get('position', [0, 0, -10]), 3):
        err.append('camera position invalid')
    if camera.get('shouldFollow', True) and not points(camera.get('bounds'), 3):
        err.append('camera bounds invalid')
    if bgm.get('native') is not None:
        if bgm.get('intro') is not None or bgm.get('loop') is not None:
            err.append('mapBGM native and intro/loop are exclusive')
    else:
        for k in ('intro', 'loop'):
            path = bgm.get(k)
            if path not in z.namelist():
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

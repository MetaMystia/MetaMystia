"""把 mirror.extract 导出的原版地图场景转换为画布地图（formatVersion 3）资源包。

用法（在 tools/day-map-art 目录下）：
    python -m mirror.build <场景目录> <输出.zip> --id 1073742303 --label HakureiFestivalMirror --name 博丽大祭 [--dependency DLC3]

对应关系：
- 每个使用默认材质的 Tilemap（含 BinaryTilemapDecompressor 解压的格子）→ 一张画布层，保留排序层与顺序；
- 不在 SortingGroup 内的 SpriteRenderer、最外层 SortingGroup → 物件；组内按原排序合成为一张图，
  带 LayerSortingController 的按脚点 Y 排序，其余保留固定顺序；
- CompositeCollider2D 的轮廓 → 闭合折线；独立的实心碰撞 → 物件或地图碰撞；CameraCollider → 相机多边形；
- HEIGHT_MAP → 坡度格（要求每格颜色一致）；SpawnMarker → 出生点；hideClock 与地图 BGM（引用原版 LoopedBGMPackage）。
角色、出口、剧情演出与特殊材质（光照、阴影滤镜）不转换，数量写进报告。
"""
import argparse
import collections
import json
import math
import os

import numpy as np
from PIL import Image

from dayart.pack import FORMAT_VERSION, Prefabs, canvas_layer, write_zip

PPU = 48
# m_SortingLayer 是排序层在游戏 TagManager 中的序号。
SORTING_LAYERS = ['Default', 'Background', 'BelowCharacter', 'Character', 'Overlay', 'OnTop', 'EffectOverlay', 'UI']
DEFAULT_SHADERS = {'Sprites/Default', 'Custom/DefaultSprite'}
EXCLUDED = ('/Interactables/NPCs', '/Interactables/MapTransition', '/ForStory')
ROTATIONS = ['Down', 'Left', 'Up', 'Right']
SORT_STEP = 32  # LayerSortingController.IsometricRangePerYUnit


def r6(v):
    v = round(float(v), 6)
    return 0.0 if v == 0 else v


class Scene:
    def __init__(self, folder):
        self.folder = folder
        data = json.load(open(os.path.join(folder, 'scene.json'), encoding='utf-8'))
        self.root = data['nodes'][0]['path']
        self.nodes = [n for n in data['nodes'] if n['active'] and not any(n['path'].startswith(self.root + e) for e in EXCLUDED)]
        self.by_path = {n['path']: n for n in self.nodes}
        self.sprites = data['sprites']
        self._images = {}
        self.skipped = collections.Counter()

    @staticmethod
    def comp(node, kind):
        return next((c for c in node['components'] if c['type'] == kind), None)

    def parent(self, node):
        head, sep, _ = node['path'].rpartition('/')
        return self.by_path.get(head) if sep else None

    def ancestors(self, node):
        while (node := self.parent(node)) is not None:
            yield node

    def image(self, key):
        if key not in self._images:
            self._images[key] = Image.open(os.path.join(self.folder, 'sprites', self.sprites[key]['file'])).convert('RGBA')
        return self._images[key]

    def drawable(self, renderer):
        if not renderer['m_Enabled'] or renderer.get('sprite') is None:
            return False
        shader = (renderer['materials'][0] or {}).get('shader') if renderer['materials'] else None
        if shader not in DEFAULT_SHADERS:
            self.skipped[f'renderer:{shader}'] += 1
            return False
        return True


def affine(m4):
    """4×4 世界矩阵 → 2D 仿射 3×3。"""
    m = np.asarray(m4, float)
    return np.array([[m[0, 0], m[0, 1], m[0, 3]], [m[1, 0], m[1, 1], m[1, 3]], [0, 0, 1]])


def translate(x, y):
    return np.array([[1, 0, x], [0, 1, y], [0, 0, 1]], float)


class Draw:
    """一个精灵的绘制：世界仿射矩阵（作用于 Sprite 局部坐标）与颜色。"""

    def __init__(self, scene, sprite, matrix, color=(1, 1, 1, 1), flip=(False, False)):
        s = scene.sprites[sprite]
        self.sprite = sprite
        self.image = scene.image(sprite)
        w, h = self.image.size
        ppu, (px, py) = s['ppu'], s['pivot']
        # 图片像素 (i, j)（左上原点）→ Sprite 局部坐标（以 pivot 为原点，单位为世界单位）
        local = np.array([[1 / ppu, 0, -px * w / ppu], [0, -1 / ppu, (h - py * h) / ppu], [0, 0, 1]])
        local = np.diag([-1 if flip[0] else 1, -1 if flip[1] else 1, 1]) @ local
        self.to_world = matrix @ local
        self._pivot_px = np.array([px * w, h - py * h])
        self.color = color

    def pivot_world(self):
        """Sprite 锚点的世界坐标。"""
        return self.to_world[:2, :2] @ self._pivot_px + self.to_world[:2, 2]

    def tinted(self):
        if tuple(self.color) == (1, 1, 1, 1):
            return self.image
        arr = np.asarray(self.image, np.float32) * np.asarray(self.color, np.float32)
        return Image.fromarray(np.clip(arr + 0.5, 0, 255).astype(np.uint8), 'RGBA')

    def corners(self):
        w, h = self.image.size
        return (self.to_world @ np.array([[0, w, 0, w], [0, 0, h, h], [1, 1, 1, 1]]))[:2].T


def bounds(draws):
    pts = np.concatenate([d.corners() for d in draws])
    return pts.min(0), pts.max(0)


def composite(canvas, img, x, y):
    """把 img 以左上角 (x, y) 叠到 canvas，超出部分裁掉。"""
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(canvas.width, x + img.width), min(canvas.height, y + img.height)
    if x0 < x1 and y0 < y1:
        canvas.alpha_composite(img, (x0, y0), (x0 - x, y0 - y, x1 - x, y1 - y))


def render(draws, left, top, w, h):
    """按顺序把 draws 合成到世界像素矩形（left、top 为左上角的世界像素坐标，y 向上）。"""
    canvas = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    to_canvas = np.array([[PPU, 0, -left], [0, -PPU, top], [0, 0, 1]], float)
    for d in draws:
        img = d.tinted()
        a = to_canvas @ d.to_world
        if np.allclose(a[:2, :2], np.eye(2), atol=1e-4):
            composite(canvas, img, int(round(a[0, 2])), int(round(a[1, 2])))
            continue
        # 一般仿射：在变换后的包围盒内按画布像素反算图片像素，最近邻取样保持像素画边缘。
        pts = (a @ np.array([[0, img.width, 0, img.width], [0, 0, img.height, img.height], [1, 1, 1, 1]]))[:2]
        bx, by = math.floor(pts[0].min()), math.floor(pts[1].min())
        bw, bh = math.ceil(pts[0].max()) - bx, math.ceil(pts[1].max()) - by
        inv = np.linalg.inv(translate(-bx, -by) @ a)
        composite(canvas, img.transform((bw, bh), Image.AFFINE, tuple(inv[0]) + tuple(inv[1]), resample=Image.NEAREST), bx, by)
    return canvas


def render_region(draws, pad=0):
    """渲染到包住全部 draws 的整像素矩形，返回 (图片, 左下角世界像素坐标)。"""
    lo, hi = bounds(draws)
    left, bottom = math.floor(lo[0] * PPU + 1e-6) - pad, math.floor(lo[1] * PPU + 1e-6) - pad
    right, top = math.ceil(hi[0] * PPU - 1e-6) + pad, math.ceil(hi[1] * PPU - 1e-6) + pad
    return render(draws, left, top, right - left, top - bottom), (left, bottom)


# ---------------------------------------------------------------- 图层

def tilemap_draws(scene, node, records):
    tilemap = scene.comp(node, 'Tilemap')
    world = affine(node['world'])
    ax, ay = tilemap['anchor']['x'], tilemap['anchor']['y']
    tint = np.array([tilemap['color'][k] for k in 'rgba'])
    draws = []
    for cell in tilemap['cells']:
        m = np.asarray(cell['matrix'], float).reshape(4, 4)
        color = tint * np.array([cell['color'][k] for k in 'rgba'])
        draws.append(Draw(scene, cell['sprite'], world @ translate(cell['x'] + ax, cell['y'] + ay) @ affine(m), color))
    for rec in records:
        c = rec['matrix']  # float3x4 按列存储
        m = np.array([[c[0], c[3], c[9]], [c[1], c[4], c[10]], [0, 0, 1]])
        draws.append(Draw(scene, rec['sprite'], world @ translate(rec['x'] + ax, rec['y'] + ay) @ m, tint))
    return draws


def build_layers(scene, folder):
    binary = collections.defaultdict(list)
    for node in scene.nodes:
        if (d := scene.comp(node, 'MB:BinaryTilemapDecompressor')) is not None:
            for rec in d['data']['records']:
                binary[d['data']['tilemaps'][rec['tilemap']]].append(rec)
    layers, files = [], {}
    for node in scene.nodes:
        tilemap, renderer = scene.comp(node, 'Tilemap'), scene.comp(node, 'TilemapRenderer')
        if tilemap is None or renderer is None or not renderer['m_Enabled']:
            continue
        shader = (renderer['materials'][0] or {}).get('shader')
        draws = tilemap_draws(scene, node, binary[tilemap['id']])
        if not draws:
            continue
        if shader not in DEFAULT_SHADERS:
            scene.skipped[f'tilemap:{node["name"]}:{shader}'] += len(draws)
            continue
        img, (left, bottom) = render_region(draws)
        layer, layer_files = canvas_layer(node['name'], img, left, bottom, f'{folder}/layers/{len(layers)}',
                                          SORTING_LAYERS[renderer['m_SortingLayer']], renderer['m_SortingOrder'])
        if layer:
            layers.append(layer)
            files.update(layer_files)
    return layers, files


# ---------------------------------------------------------------- 物件

def sort_order(scene, node, sorting):
    if scene.comp(node, 'MB:LayerSortingController') is not None:
        return int(node['world'][1][3] * -SORT_STEP)  # C# 强制转换向零截断
    return sorting['m_SortingOrder']


def group_draws(scene, group):
    """按 SortingGroup 内部排序展开成绘制序列；嵌套组作为整体参与排序。"""
    items = []
    renderer = scene.comp(group, 'SpriteRenderer')
    if renderer and scene.drawable(renderer):
        items.append(((renderer['m_SortingLayer'], renderer['m_SortingOrder']), [sprite_draw(scene, group, renderer)]))
    prefix = group['path'] + '/'
    for node in scene.nodes:
        if not node['path'].startswith(prefix):
            continue
        owner = next(a for a in scene.ancestors(node) if a is group or scene.comp(a, 'SortingGroup'))
        if owner is not group:
            continue
        if (g := scene.comp(node, 'SortingGroup')) is not None:
            if g['m_Enabled']:
                items.append(((g['m_SortingLayer'], sort_order(scene, node, g)), group_draws(scene, node)))
        elif (r := scene.comp(node, 'SpriteRenderer')) is not None and scene.drawable(r):
            items.append(((r['m_SortingLayer'], sort_order(scene, node, r)), [sprite_draw(scene, node, r)]))
    items.sort(key=lambda item: item[0])  # 稳定排序，同序按层级顺序
    return [d for _, ds in items for d in ds]


def sprite_draw(scene, node, r):
    color = tuple(r['m_Color'][k] for k in 'rgba')
    if r['m_DrawMode'] != 0:
        scene.skipped['renderer:sliced/tiled'] += 1
    return Draw(scene, r['sprite'], affine(node['world']), color, (r['m_FlipX'], r['m_FlipY']))


def sorting_units(scene):
    """最外层 SortingGroup，以及不属于任何组的 SpriteRenderer。"""
    for node in scene.nodes:
        if any(scene.comp(a, 'SortingGroup') for a in scene.ancestors(node)):
            continue
        group, renderer = scene.comp(node, 'SortingGroup'), scene.comp(node, 'SpriteRenderer')
        if group is not None and group['m_Enabled']:
            draws = group_draws(scene, node)
            sorting = group
        elif renderer is not None and group is None and scene.drawable(renderer):
            draws = [sprite_draw(scene, node, renderer)]
            sorting = renderer
        else:
            continue
        if draws:
            yield node, sorting, draws


# ---------------------------------------------------------------- 碰撞

def collider_shapes(comp, matrix):
    """碰撞组件 → v3 碰撞列表（坐标已乘 matrix）。"""
    def pt(x, y):
        p = matrix @ np.array([x, y, 1.0])
        return [r6(p[0]), r6(p[1])]
    ox, oy = comp.get('m_Offset', {}).get('x', 0), comp.get('m_Offset', {}).get('y', 0)
    kind = comp['type']
    linear = matrix[:2, :2]
    if kind == 'PolygonCollider2D':
        return [dict(shape='polygon', paths=[[pt(p['x'] + ox, p['y'] + oy) for p in path] for path in comp['m_Points']['m_Paths']])]
    if kind == 'EdgeCollider2D':
        return [dict(shape='edge', points=[pt(p['x'] + ox, p['y'] + oy) for p in comp['m_Points']])]
    if kind == 'BoxCollider2D':
        w, h = comp['m_Size']['x'], comp['m_Size']['y']
        if abs(linear[0, 1]) < 1e-6 and abs(linear[1, 0]) < 1e-6:
            cx, cy = pt(ox, oy)
            return [dict(shape='box', x=cx, y=cy, width=r6(abs(linear[0, 0]) * w), height=r6(abs(linear[1, 1]) * h), rotation=0)]
        return [dict(shape='polygon', paths=[[pt(ox + sx * w / 2, oy + sy * h / 2) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]])]
    if kind == 'CircleCollider2D':
        cx, cy = pt(ox, oy)
        return [dict(shape='circle', x=cx, y=cy, radius=r6(comp['m_Radius'] * math.sqrt(abs(np.linalg.det(linear)))))]
    if kind == 'CompositeCollider2D':
        paths = [[pt(p['x'], p['y']) for p in path] for path in comp['m_CompositePaths']['m_Paths']]
        if comp['m_GeometryType'] == 0:  # Outlines：只有轮廓阻挡
            return [dict(shape='edge', points=path, closed=True) for path in paths]
        return [dict(shape='polygon', paths=paths)]
    return []


def solid_colliders(scene):
    """(节点, 组件)：启用的实心碰撞。并入 CompositeCollider2D 的成员由合成轮廓代表，不单独输出。"""
    for node in scene.nodes:
        for c in node['components']:
            if c['type'] not in ('PolygonCollider2D', 'EdgeCollider2D', 'BoxCollider2D', 'CircleCollider2D', 'CompositeCollider2D', 'CapsuleCollider2D'):
                continue
            if node['name'] == 'CameraCollider':
                continue
            if not c['m_Enabled'] or c['m_IsTrigger'] or c.get('m_UsedByComposite'):
                scene.skipped[f"collider:{'trigger' if c['m_IsTrigger'] else 'composite member' if c.get('m_UsedByComposite') else 'disabled'}"] += 1
                continue
            if c['type'] == 'CapsuleCollider2D':
                scene.skipped['collider:capsule'] += 1
                continue
            yield node, c


# ---------------------------------------------------------------- 组装

def build(scene_dir, out, map_id, label, name, dependencies):
    scene = Scene(scene_dir)
    folder = f'dayMaps/{map_id}'
    layers, files = build_layers(scene, folder)

    prefabs = Prefabs(f'{folder}/prefabs')
    units = {}
    for node, sorting, draws in sorting_units(scene):
        x, y = node['world'][0][3], node['world'][1][3]
        unit = dict(node=node, sorting=sorting, x=x, y=y, scale=(1, 1), colliders=[])
        single = draws[0] if len(draws) == 1 else None
        s = scene.sprites[single.sprite] if single else None
        if single and np.allclose(np.abs(single.to_world[:2, :2]), np.diag([1 / s['ppu'], 1 / s['ppu']]), atol=1e-6) \
                and np.allclose(single.pivot_world(), (x, y), atol=1e-6):
            # 单张精灵且只有平移与翻转：直接用原图、原锚点与原 PPU，翻转写进物件缩放。
            unit.update(img=single.tinted(), pivot=s['pivot'], ppu=s['ppu'],
                        scale=(int(np.sign(single.to_world[0, 0])), int(-np.sign(single.to_world[1, 1]))))
        else:
            img, (left, bottom) = render_region(draws)
            unit.update(img=img, pivot=((x * PPU - left) / img.width, (y * PPU - bottom) / img.height), ppu=PPU)
        units[node['path']] = unit

    map_colliders = []
    for node, comp in solid_colliders(scene):
        owner = next((units[n['path']] for n in [node, *scene.ancestors(node)] if n['path'] in units), None)
        matrix = affine(node['world'])
        if owner is None:
            shapes = collider_shapes(comp, matrix)
            label_name = node['name'] if comp['type'] != 'CompositeCollider2D' else f"{node['name']} outline"
            map_colliders += [dict(name=f'{label_name} {i}' if len(shapes) > 1 else label_name, **s) for i, s in enumerate(shapes)]
        else:
            sx, sy = owner['scale']
            owner['colliders'] += collider_shapes(comp, np.diag([sx, sy, 1]) @ translate(-owner['x'], -owner['y']) @ matrix)

    objects = []
    for unit in units.values():
        node, sorting = unit['node'], unit['sorting']
        sort_by_y = scene.comp(node, 'MB:LayerSortingController') is not None
        key = prefabs.add(node['name'], unit['img'], unit['pivot'], SORTING_LAYERS[sorting['m_SortingLayer']],
                          0 if sort_by_y else sorting['m_SortingOrder'], sort_by_y, unit['colliders'], unit['ppu'])
        objects.append(dict(name=node['name'], prefab=key, x=r6(unit['x']), y=r6(unit['y']), scale=list(unit['scale'])))
    files.update(prefabs.files)

    # 原版按格内像素采样坡度；这里要求每格颜色一致，逐格保存。
    height_cells = []
    hm = next((n for n in scene.nodes if n['name'] == 'HEIGHT_MAP' and scene.comp(n, 'Tilemap')), None)
    for cell in scene.comp(hm, 'Tilemap')['cells'] if hm else []:
        px = np.asarray(scene.image(cell['sprite']))
        values = {(int(p[0]), p[1] > 127) for p in px[px[..., 3] > 0]}
        if len(values) != 1:
            raise ValueError(f"height sprite is not uniform: {scene.sprites[cell['sprite']]['name']}")
        r, positive = values.pop()
        if r:
            height_cells.append(dict(x=cell['x'], y=cell['y'], slope=r6(r / 255 * (1 if positive else -1))))

    map_node = next(n for n in scene.nodes if any(c['type'].startswith('MB:') and 'mapBGM' in c.get('data', {}) for c in n['components']))
    map_data = next(c['data'] for c in map_node['components'] if 'mapBGM' in c.get('data', {}))
    camera_node = scene.by_path.get(f'{scene.root}/Grid/CameraCollider')
    camera = dict(shouldFollow=bool(map_data['shouldCameraFollow']), position=[r6(map_data['cameraDefaultPosition'][k]) for k in 'xyz'])
    if camera_node is not None:
        camera['bounds'] = collider_shapes(scene.comp(camera_node, 'PolygonCollider2D'), affine(camera_node['world']))[0]['paths'][0]

    spawns = []
    for node in scene.nodes:
        if (m := scene.comp(node, 'MB:SpawnMarker')) is not None:
            spawns.append(dict(name=m['data']['spawnMarkerName'], x=r6(node['world'][0][3]), y=r6(node['world'][1][3]),
                               rotation=ROTATIONS[m['data']['targetRotation']]))

    day_map = dict(id=map_id, formatVersion=FORMAT_VERSION, name=name, description=f'原版地图镜像：{scene.root}',
                   pixelsPerUnit=PPU, hideClock=bool(map_data['hideClock']), layers=layers, prefabs=prefabs.items,
                   objects=objects, colliders=map_colliders, height=dict(cells=height_cells), spawnMarkers=spawns,
                   defaultSpawnMarker=spawns[0]['name'], camera=camera, mapBGM=dict(native=map_data['mapBGM']))
    config = dict(packInfo=dict(label=label, name=f'{name}镜像', version='3.0.0', authors=['MetaMystia'],
                                description='由游戏 Addressables 转换的画布地图，仅供本地参考。', dependencies=dependencies),
                  dayMaps=[day_map])
    write_zip(out, config, files)
    report = dict(layers=[(l['name'], l['sortingLayer'], l['sortingOrder'], [l['x'], l['y'], l['image']['width'], l['image']['height']], len(l['image']['chunks']))
                          for l in layers],
                  prefabs=len(prefabs.items), objects=len(objects), objectColliders=sum(len(p['colliders']) for p in prefabs.items),
                  mapColliders=len(map_colliders), heightCells=len(height_cells), spawnMarkers=[s['name'] for s in spawns],
                  bgm=map_data['mapBGM'], skipped=dict(scene.skipped))
    return report


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('scene')
    ap.add_argument('out')
    ap.add_argument('--id', type=int, required=True)
    ap.add_argument('--label', required=True)
    ap.add_argument('--name', required=True)
    ap.add_argument('--dependency', action='append', default=[])
    a = ap.parse_args()
    report = build(a.scene, a.out, a.id, a.label, a.name, a.dependency)
    print(json.dumps(report, ensure_ascii=False, indent=1))


if __name__ == '__main__':
    main()

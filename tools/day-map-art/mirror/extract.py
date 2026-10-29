"""从游戏 Addressables 导出一张白天地图预制体：GameObject 树、世界矩阵、渲染器、瓦片、碰撞与 Sprite PNG。

用法（在 tools/day-map-art 目录下）：
    python -m mirror.extract <aa 目录> <预制体地址> <输出目录>
    例：python -m mirror.extract <游戏>/TouhouMystiaIzakaya_Data/StreamingAssets/aa DLC3_Map_HakureiFestival .tmp/mirror/HakureiFestival

输出 <输出目录>/scene.json、sprites/*.png，解密后的 bundle 缓存在 <输出目录>/bundles。
"""
import gzip
import json
import os
import struct
import sys

import numpy as np
import UnityPy
from UnityPy.classes import PPtr

from .addressables import Catalog

UnityPy.config.FALLBACK_UNITY_VERSION = '2021.3.28f1'
# 地图预制体依赖的字体包很大且与美术无关。
SKIP_BUNDLES = ('system_fontdata',)
COLLIDERS = {'PolygonCollider2D', 'BoxCollider2D', 'CircleCollider2D', 'EdgeCollider2D', 'CompositeCollider2D', 'CapsuleCollider2D'}


def obj_id(obj):
    return f'{obj.assets_file.name}:{obj.path_id}'


def deref(ptr, owner):
    if not ptr or ptr.get('m_PathID', 0) == 0:
        return None
    p = PPtr(m_FileID=ptr['m_FileID'], m_PathID=ptr['m_PathID'])
    p.assetsfile = owner.assets_file
    try:
        return p.deref()
    except (KeyError, IndexError, FileNotFoundError):
        return None


def local_matrix(t):
    x, y, z, w = (t['m_LocalRotation'][k] for k in 'xyzw')
    rotation = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                         [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                         [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
    m = np.eye(4)
    m[:3, :3] = rotation @ np.diag([t['m_LocalScale'][k] for k in 'xyz'])
    m[:3, 3] = [t['m_LocalPosition'][k] for k in 'xyz']
    return m


class SceneDumper:
    def __init__(self, out):
        self.out = out
        self.sprites = {}
        self.nodes = []
        os.makedirs(os.path.join(out, 'sprites'), exist_ok=True)

    def sprite(self, ptr, owner):
        o = deref(ptr, owner)
        if o is None:
            return None
        key = obj_id(o)
        if key not in self.sprites:
            tt = o.read_typetree()
            file = f'{len(self.sprites):05d}.png'
            o.read().image.save(os.path.join(self.out, 'sprites', file))
            r = tt['m_Rect']
            self.sprites[key] = {'name': tt['m_Name'], 'file': file, 'size': [r['width'], r['height']],
                                 'pivot': [tt['m_Pivot']['x'], tt['m_Pivot']['y']], 'ppu': tt['m_PixelsToUnits']}
        return key

    def material(self, ptr, owner):
        o = deref(ptr, owner)
        if o is None:
            return None
        mt = o.read_typetree()
        shader = deref(mt['m_Shader'], o)
        return {'name': mt['m_Name'], 'shader': shader.read_typetree()['m_ParsedForm']['m_Name'] if shader else None}

    def binary_records(self, profile):
        """BinaryTilemapProfile：gzip 后每 64 字节一格（int2 坐标、float3x4 矩阵、Sprite 索引、Tilemap 索引）。"""
        pt = profile.read_typetree()
        sprites = [self.sprite(p, profile) for p in pt['m_SpriteList']]
        script = deref(pt['m_BinaryTilemapData'], profile).read().m_Script
        data = gzip.decompress(script if isinstance(script, (bytes, bytearray)) else script.encode('utf-8', 'surrogateescape'))
        records = []
        for i in range(len(data) // 64):
            v = struct.unpack_from('<2i12f2i', data, i * 64)
            records.append({'x': v[0], 'y': v[1], 'matrix': list(v[2:14]), 'sprite': sprites[v[14]], 'tilemap': v[15]})
        return records

    def component(self, co, tt):
        t = co.type.name
        entry = {'type': t, 'id': obj_id(co)}
        if t == 'MonoBehaviour':
            script = deref(tt['m_Script'], co)
            entry['type'] = 'MB:' + (script.read().m_ClassName if script else '?')
            data = {k: v for k, v in tt.items() if k not in ('m_GameObject', 'm_Script', 'm_Name')}
            if entry['type'] == 'MB:BinaryTilemapDecompressor':
                data['records'] = self.binary_records(deref(tt['m_BinaryTilemapProfile'], co))
                data['tilemaps'] = [obj_id(deref(p, co)) for p in tt['m_TilemapList']]
            if 'mapBGM' in data:  # DaySceneMap 及其子类
                bgm = deref(data['mapBGM'], co)
                data['mapBGM'] = bgm.read().m_Name if bgm else None
            entry['data'] = data
        elif t == 'SpriteRenderer':
            entry.update({k: tt[k] for k in ('m_Enabled', 'm_SortingLayer', 'm_SortingOrder', 'm_Color', 'm_FlipX', 'm_FlipY', 'm_DrawMode', 'm_Size')})
            entry['sprite'] = self.sprite(tt['m_Sprite'], co)
            entry['materials'] = [self.material(m, co) for m in tt['m_Materials']]
        elif t == 'TilemapRenderer':
            entry.update({k: tt[k] for k in ('m_Enabled', 'm_SortingLayer', 'm_SortingOrder')})
            entry['materials'] = [self.material(m, co) for m in tt['m_Materials']]
        elif t == 'SortingGroup':
            entry.update({k: tt[k] for k in ('m_Enabled', 'm_SortingLayer', 'm_SortingOrder')})
        elif t == 'Tilemap':
            sprites = [self.sprite(s['m_Data'], co) for s in tt['m_TileSpriteArray']]
            matrices = [[m['m_Data'][f'e{r}{c}'] for r in range(4) for c in range(4)] for m in tt['m_TileMatrixArray']]
            colors = [m['m_Data'] for m in tt['m_TileColorArray']]
            entry.update({'color': tt['m_Color'], 'anchor': tt['m_TileAnchor'], 'cells': [
                {'x': pos['x'], 'y': pos['y'], 'sprite': sprites[info['m_TileSpriteIndex']],
                 'matrix': matrices[info['m_TileMatrixIndex']], 'color': colors[info['m_TileColorIndex']]}
                for pos, info in tt['m_Tiles']]})
        elif t in COLLIDERS:
            entry.update({k: v for k, v in tt.items() if k not in ('m_GameObject', 'm_Material', 'm_ColliderPaths')})
        elif t == 'Rigidbody2D':
            entry['bodyType'] = tt.get('m_BodyType')
        return entry

    def walk(self, go_obj, parent_world=np.eye(4), parent_path='', parent_active=True):
        go = go_obj.read_typetree()
        path = f"{parent_path}/{go['m_Name']}" if parent_path else go['m_Name']
        node = {'path': path, 'name': go['m_Name'], 'active': parent_active and bool(go['m_IsActive']), 'layer': go['m_Layer'], 'components': []}
        world, children = parent_world, []
        for c in go['m_Component']:
            co = deref(c['component'], go_obj)
            if co is None:
                continue
            tt = co.read_typetree()
            if co.type.name in ('Transform', 'RectTransform'):
                world = parent_world @ local_matrix(tt)
                children = [deref(ch, co) for ch in tt['m_Children']]
            else:
                node['components'].append(self.component(co, tt))
        node['world'] = world.tolist()
        self.nodes.append(node)
        for ch in children:
            if ch is not None:
                self.walk(deref(ch.read_typetree()['m_GameObject'], ch), world, path, node['active'])


def main(aa_dir, address, out):
    catalog = Catalog(aa_dir)
    prefab = next(i for i in catalog.internal_ids(address) if i.endswith('.prefab'))
    bundles = [p for p in catalog.fetch(address, os.path.join(out, 'bundles')) if not os.path.basename(p).startswith(SKIP_BUNDLES)]
    env = UnityPy.load(*bundles)
    root_name = os.path.splitext(os.path.basename(prefab))[0]
    root = next(o for o in env.objects if o.type.name == 'GameObject' and o.read().m_Name == root_name)
    dumper = SceneDumper(out)
    dumper.walk(root)
    with open(os.path.join(out, 'scene.json'), 'w', encoding='utf-8') as f:
        json.dump({'address': address, 'prefab': prefab, 'nodes': dumper.nodes, 'sprites': dumper.sprites}, f, ensure_ascii=False)
    print(f'{root_name}: {len(dumper.nodes)} nodes, {len(dumper.sprites)} sprites')


if __name__ == '__main__':
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    main(*sys.argv[1:])

"""读取游戏 Addressables：解析 catalog.bundle，找到某个地址依赖的全部 bundle，并解密（XOR 0x53）到缓存目录。"""
import base64
import json
import os
import struct

import numpy as np
import UnityPy

XOR_KEY = 0x53


def _read_catalog(aa_dir):
    env = UnityPy.load(os.path.join(aa_dir, 'catalog.bundle'))
    for obj in env.objects:
        if obj.type.name == 'TextAsset':
            script = obj.read().m_Script
            return json.loads(script if isinstance(script, str) else script.decode('utf-8'))
    raise ValueError('catalog TextAsset not found')


def _key_reader(data):
    b = base64.b64decode(data)

    def read(offset):
        kind = b[offset]
        offset += 1
        if kind in (0, 1):  # ASCII / UTF-16 字符串
            n = struct.unpack_from('<i', b, offset)[0]
            return b[offset + 4:offset + 4 + n].decode('ascii' if kind == 0 else 'utf-16-le')
        if kind == 2:
            return struct.unpack_from('<H', b, offset)[0]
        if kind in (3, 4):
            return struct.unpack_from('<i', b, offset)[0]
        return None
    return read


class Catalog:
    """Addressables 1.x ContentCatalogData：键 → 条目（内部 ID、依赖键）。"""

    def __init__(self, aa_dir):
        self.aa_dir = aa_dir
        c = _read_catalog(aa_dir)
        self.ids = c['m_InternalIds']
        read_key = _key_reader(c['m_KeyDataString'])
        bb = base64.b64decode(c['m_BucketDataString'])
        offset = 4
        self.buckets = []
        for _ in range(struct.unpack_from('<i', bb, 0)[0]):
            key_offset, count = struct.unpack_from('<ii', bb, offset)
            self.buckets.append((read_key(key_offset), struct.unpack_from(f'<{count}i', bb, offset + 8)))
            offset += 8 + 4 * count
        eb = base64.b64decode(c['m_EntryDataString'])
        self.entries = [struct.unpack_from('<7i', eb, 4 + 28 * i) for i in range(struct.unpack_from('<i', eb, 0)[0])]

    def internal_ids(self, address):
        return [self.ids[self.entries[e][0]] for key, es in self.buckets if key == address for e in es]

    def bundles(self, address):
        """地址所有条目的依赖 bundle 文件名。"""
        names = set()
        for key, es in self.buckets:
            if key != address:
                continue
            for e in es:
                dep = self.entries[e][2]
                if dep >= 0:
                    names.update(self.ids[self.entries[d][0]].replace('\\', '/').rsplit('/', 1)[-1] for d in self.buckets[dep][1])
        return sorted(names)

    def fetch(self, address, cache_dir):
        """把依赖 bundle 解密到 cache_dir，返回路径列表。"""
        os.makedirs(cache_dir, exist_ok=True)
        paths = []
        for name in self.bundles(address):
            dst = os.path.join(cache_dir, name)
            if not os.path.exists(dst):
                data = open(os.path.join(self.aa_dir, 'StandaloneWindows64', name), 'rb').read()
                if not data.startswith(b'UnityFS'):
                    data = (np.frombuffer(data, np.uint8) ^ XOR_KEY).tobytes()
                if not data.startswith(b'UnityFS'):
                    raise ValueError(f'not a UnityFS bundle after XOR: {name}')
                with open(dst, 'wb') as f:
                    f.write(data)
            paths.append(dst)
        return paths

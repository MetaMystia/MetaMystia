"""生成独立白天地图库测试包。用法：python create-day-map-package.py 输出.zip"""
import io
import json
import math
from pathlib import Path
import struct
import sys
import wave
import zipfile
import zlib

MAP_ID = 1073742000
SIZE = 48
WIDTH, HEIGHT = SIZE * 8, SIZE * 3
pixels = bytearray(WIDTH * HEIGHT * 4)


def pixel(x, y, color):
    offset = ((HEIGHT - 1 - y) * WIDTH + x) * 4
    pixels[offset:offset + 4] = bytes(color)


def rectangle(x, y, width, height, color):
    for py in range(y, y + height):
        for px in range(x, x + width):
            pixel(px, py, color)


# 图集坐标与 Unity 一致：左下角原点。
colors = [(43, 77, 76, 255), (34, 79, 110, 255), (139, 104, 78, 255),
          (116, 127, 123, 255), (208, 174, 104, 255)]
for index, color in enumerate(colors):
    rectangle(index * SIZE, 0, SIZE, SIZE, color)
    for y in range(3, SIZE, 12):
        for x in range(3, SIZE, 16):
            detail = tuple(min(255, c + 16) for c in color[:3]) + (255,)
            rectangle(index * SIZE + x, y, 7, 2, detail)
# 木板与栏杆。
for x in range(0, SIZE, 8):
    rectangle(2 * SIZE + x, 0, 1, SIZE, (78, 64, 63, 255))
rectangle(4 * SIZE, 12, SIZE, 5, (82, 64, 72, 255))
# 第六格是透明花丛。
for x, y in [(8, 8), (21, 17), (36, 6), (31, 30)]:
    rectangle(5 * SIZE + x, y, 2, 12, (55, 109, 81, 255))
    rectangle(5 * SIZE + x - 3, y + 10, 8, 5, (205, 74, 121, 255))
# 上两行是一棵有透明边缘的树，以树脚为排序基点。
rectangle(20, 48, 8, 43, (105, 75, 85, 255))
for y in range(75, 140):
    for x in range(1, 47):
        if ((x - 24) / 23) ** 2 + ((y - 110) / 32) ** 2 < 1:
            pixel(x, y, (97 + (x + y) % 15, 124, 126, 255))
# 坡面测试格：箭头表示向右走时的升降方向。
for index, direction in [(6, 1), (7, -1)]:
    rectangle(index * SIZE, 0, SIZE, SIZE, (100, 91, 109, 255))
    for x in range(8, 39):
        y = 16 + (x - 8) // 2 if direction > 0 else 32 - (x - 8) // 2
        rectangle(index * SIZE + x, y, 1, 3, (224, 196, 146, 255))
    tip = 31 if direction > 0 else 17
    rectangle(index * SIZE + 33, tip - 3, 6, 9, (224, 196, 146, 255))


def png_chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))


def encode_png(buffer, width, height):
    """RGBA 缓冲区（左上原点）→ PNG 字节。"""
    rows = b''.join(b'\0' + buffer[y * width * 4:(y + 1) * width * 4] for y in range(height))
    return (b'\x89PNG\r\n\x1a\n' + png_chunk(b'IHDR', struct.pack('>2I5B', width, height, 8, 6, 0, 0, 0))
            + png_chunk(b'IDAT', zlib.compress(rows)) + png_chunk(b'IEND', b''))


def atlas_region(x, y, width, height):
    """从图集（左下原点）取出一块，返回左上原点的 RGBA 缓冲区。"""
    out = bytearray(width * height * 4)
    for row in range(height):
        src = ((HEIGHT - 1 - (y + height - 1 - row)) * WIDTH + x) * 4
        out[row * width * 4:(row + 1) * width * 4] = pixels[src:src + width * 4]
    return out


def chunked_image(buffer, width, height, folder, files):
    """按 1024 像素切块，跳过全透明块，返回画布地图的图片配置。"""
    chunks = []
    for row in range((height + 1023) // 1024):
        for column in range((width + 1023) // 1024):
            w, h = min(1024, width - column * 1024), min(1024, height - row * 1024)
            block = bytearray()
            for y in range(row * 1024, row * 1024 + h):
                start = (y * width + column * 1024) * 4
                block += buffer[start:start + w * 4]
            if any(block[3::4]):
                files[f'{folder}/{column}_{row}.png'] = encode_png(block, w, h)
                chunks.append([column, row])
    return dict(width=width, height=height, dir=folder, chunks=chunks)


X0, Y0, COLS, ROWS = -18, -10, 36, 20
LAYER_WIDTH, LAYER_HEIGHT = COLS * SIZE, ROWS * SIZE
keys = ['bank', 'water', 'bridge', 'path', 'rail', 'flowers', 'slope-up', 'slope-down']


def paint_layer(cells):
    """{(x, y): 图集格名} → 整张图层缓冲区；每格 48 像素，透明像素不覆盖。"""
    buffer = bytearray(LAYER_WIDTH * LAYER_HEIGHT * 4)
    for (x, y), key in cells.items():
        tile = atlas_region(keys.index(key) * SIZE, 0, SIZE, SIZE)
        left, top = (x - X0) * SIZE, (Y0 + ROWS - 1 - y) * SIZE
        for row in range(SIZE):
            for col in range(SIZE):
                src = (row * SIZE + col) * 4
                if tile[src + 3]:
                    dst = ((top + row) * LAYER_WIDTH + left + col) * 4
                    buffer[dst:dst + 4] = tile[src:src + 4]
    return buffer


audio = io.BytesIO()
with wave.open(audio, 'wb') as wav:
    wav.setparams((1, 2, 22050, 0, 'NONE', 'not compressed'))
    wav.writeframes(b''.join(struct.pack('<h', int(300 * math.sin(2 * math.pi * 220 * n / 22050)))
                             for n in range(22050 * 2)))

ground = {}
for y in range(Y0, Y0 + ROWS):
    for x in range(X0, X0 + COLS):
        tile = 'water' if -3 <= x < 3 else 'bank'
        if -1 <= y < 2:
            tile = 'bridge' if -3 <= x < 3 else 'path'
        if -7 <= y < -2 and -14 <= x < -10:
            tile = 'slope-up'
        if -7 <= y < -2 and 8 <= x < 12:
            tile = 'slope-down'
        ground[(x, y)] = tile
files = {'map/test.wav': audio.getvalue()}
# 地面宽 1728 像素，跨两个图块，用于在游戏中检查图块接缝。
layers = []
for name, cells, sorting_layer, order in (
        ('Ground', ground, 'Background', -2000),
        ('Flowers', {(x, y): 'flowers' for x in [-13, -9, -6, 5, 8, 12] for y in [-6, 5]}, 'Background', -1999),
        ('Foreground', {(x, 1): 'rail' for x in range(-3, 3)}, 'Overlay', 5)):
    image = chunked_image(paint_layer(cells), LAYER_WIDTH, LAYER_HEIGHT, f'map/{name.lower()}', files)
    layers.append(dict(name=name, sortingLayer=sorting_layer, sortingOrder=order, x=X0 * SIZE, y=Y0 * SIZE, image=image))
tree = dict(key='tree', image=chunked_image(atlas_region(0, 48, 48, 96), 48, 96, 'map/tree', files), pivot=[0.5, 0],
            colliders=[dict(shape='circle', x=0, y=0.25, radius=0.2)])
colliders = [
    # 北侧河道用多边形，南侧用矩形；外圈用闭合折线围出可走区域，与原版 CompositeCollider2D 轮廓相同。
    dict(name='river-north', shape='polygon', paths=[[[-3, 2], [3, 2], [3, 9.5], [-3, 9.5]]]),
    dict(name='river-south', shape='box', x=0, y=-5.5, width=6, height=9),
    dict(name='boundary', shape='edge', points=[[-17, -9], [17, -9], [17, 9], [-17, 9]], closed=True),
]
height_cells = [dict(x=x, y=y, slope=slope) for xs, slope in [(range(-14, -10), 0.5), (range(8, 12), -0.5)]
                for x in xs for y in range(-7, -2)]
config = dict(packInfo=dict(name='孤立地图测试', label='IsolatedMapTest', version='0.3.0',
                            authors=['MetaMystia'], dependencies=['CORE']),
              dayMaps=[dict(id=MAP_ID, name='三途川 · 试验', formatVersion=3, pixelsPerUnit=SIZE,
                            layers=layers, prefabs=[tree],
                            objects=[dict(name='Willow', prefab='tree', x=-7, y=3, scale=[2, 2])],
                            colliders=colliders, height=dict(cells=height_cells),
                            spawnMarkers=[dict(name='Entry', x=-6, y=0), dict(name='East', x=6, y=0),
                                          dict(name='SlopeUp', x=-13.5, y=-6), dict(name='SlopeDown', x=8.5, y=-4)],
                            defaultSpawnMarker='Entry',
                            camera=dict(bounds=[[-4.635417, -2.96875], [4.635417, -2.96875],
                                                [4.635417, 1.96875], [-4.635417, 1.96875]]),
                            mapBGM=dict(intro='map/test.wav', loop='map/test.wav'))])
output = Path(sys.argv[1])
output.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
    archive.writestr('ResourceEx.json', json.dumps(config, ensure_ascii=False, indent=2))
    for path, data in files.items():
        archive.writestr(path, data)
print(f'{output}: map ID {MAP_ID}, {len(layers)} layers, {sum(len(l["image"]["chunks"]) for l in layers)} chunks, '
      f'{len(colliders)} colliders')

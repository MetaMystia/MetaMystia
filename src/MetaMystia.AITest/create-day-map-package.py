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


def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))


rows = b''.join(b'\0' + pixels[y * WIDTH * 4:(y + 1) * WIDTH * 4] for y in range(HEIGHT))
png = (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>2I5B', WIDTH, HEIGHT, 8, 6, 0, 0, 0))
       + chunk(b'IDAT', zlib.compress(rows)) + chunk(b'IEND', b''))
audio = io.BytesIO()
with wave.open(audio, 'wb') as wav:
    wav.setparams((1, 2, 22050, 0, 'NONE', 'not compressed'))
    wav.writeframes(b''.join(struct.pack('<h', int(300 * math.sin(2 * math.pi * 220 * n / 22050)))
                             for n in range(22050 * 2)))

keys = ['bank', 'water', 'bridge', 'path', 'rail', 'flowers', 'slope-up', 'slope-down']
tiles = [dict(key=key, image='map/atlas.png', rect=[i * SIZE, 0, SIZE, SIZE]) for i, key in enumerate(keys)]
tiles.append(dict(key='tree', image='map/atlas.png', rect=[0, 48, 48, 96], pivot=[0.5, 0]))
ground = []
for y in range(-10, 10):
    for x in range(-18, 18):
        tile = 'water' if -3 <= x < 3 else 'bank'
        if -1 <= y < 2:
            tile = 'bridge' if -3 <= x < 3 else 'path'
        if -7 <= y < -2 and -14 <= x < -10:
            tile = 'slope-up'
        if -7 <= y < -2 and 8 <= x < 12:
            tile = 'slope-down'
        ground.append(dict(x=x, y=y, tile=tile))
flowers = [dict(x=x, y=y, tile='flowers') for x in [-13, -9, -6, 5, 8, 12] for y in [-6, 5]]
rail = [dict(x=x, y=1, tile='rail') for x in range(-3, 3)]
boxes = [dict(name='river-north', x=0, y=6, width=6, height=8),
         dict(name='river-south', x=0, y=-5.5, width=6, height=9),
         dict(name='west', x=-17.5, y=0, width=1, height=20),
         dict(name='east', x=17.5, y=0, width=1, height=20),
         dict(name='north', x=0, y=9.5, width=36, height=1),
         dict(name='south', x=0, y=-9.5, width=36, height=1)]
height_cells = [dict(x=x, y=y, slope=slope) for xs, slope in [(range(-14, -10), 0.5), (range(8, 12), -0.5)]
                for x in xs for y in range(-7, -2)]
config = dict(packInfo=dict(name='孤立地图测试', label='IsolatedMapTest', version='0.2.0',
                            authors=['MetaMystia'], dependencies=['CORE']),
              dayMaps=[dict(id=MAP_ID, name='三途川 · 试验', formatVersion=1, tiles=tiles,
                            layers=[dict(name='Ground', cells=ground),
                                    dict(name='Flowers', sortingOrder=-1999, cells=flowers),
                                    dict(name='Foreground', sortingLayer='Overlay', sortingOrder=5, cells=rail)],
                            objects=[dict(name='Willow', tile='tree', x=-7, y=3, scale=[2, 2])],
                            height=dict(cells=height_cells), collisions=boxes,
                            spawnMarkers=[dict(name='Entry', x=-6, y=0), dict(name='East', x=6, y=0),
                                          dict(name='SlopeUp', x=-13.5, y=-6), dict(name='SlopeDown', x=8.5, y=-4)],
                            defaultSpawnMarker='Entry',
                            camera=dict(bounds=[-4.635417, -2.96875, 4.635417, 1.96875]),
                            mapBGM=dict(intro='map/test.wav', loop='map/test.wav'))])
output = Path(sys.argv[1])
output.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
    archive.writestr('ResourceEx.json', json.dumps(config, ensure_ascii=False, indent=2))
    archive.writestr('map/atlas.png', png)
    archive.writestr('map/test.wav', audio.getvalue())
print(f'{output}: map ID {MAP_ID}, {len(ground)} ground cells, {len(boxes)} boxes')

"""从 1:1 预览裁出若干固定区域，便于每轮比对。用法：python crops.py <构建输出目录>"""
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import layout as L

REGIONS = {
    'ferry': (-9, -2, 11, 13), 'kawara': (-23, -9, -3, 5), 'road': (-11, -22, 9, -8), 'bridge': (10, -6, 30, 9),
    'knoll': (-36, -14, -18, 4), 'river': (-20, 8, 4, 24), 'market': (18, -4, 36, 13), 'higan': (6, 14, 30, 24),
    'tree': (-35, -2, -24, 9), 'skeleton': (5, -6, 17, 4),
}

out = sys.argv[1]
im = Image.open(os.path.join(out, 'preview.png'))
names = sys.argv[2:] or list(REGIONS)
for name in names:
    x0, y0, x1, y1 = REGIONS[name]
    box = (int((x0 - L.X0) * 48), int((L.Y1 - y1) * 48), int((x1 - L.X0) * 48), int((L.Y1 - y0) * 48))
    im.crop(box).save(os.path.join(out, f'c_{name}.png'))
print('ok', names)

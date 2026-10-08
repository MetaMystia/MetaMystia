"""生图重绘的前后处理：出布局底图；地面图分块细化，切窗口给生图模型重绘，再拼回整图。

  python -m maps.sanzu.refine draft  <build 输出目录> <draft.png>  # 程序化版的无前景预览 + 树冠，缩到 1/2 作重绘底图
  python -m maps.sanzu.refine split  <plate.png> <dir>          # 写 dir/win_XX.png 与 windows.json
  python -m maps.sanzu.refine stitch <plate.png> <dir> <out.png> [a,b]  # 读 dir/gen_XX.png，配准、校色、按最小差异接缝拼接；
                                                                  # 给出 a,b 时 y<a 用原图，a..b 渐变过渡

生成图任意尺寸，按窗口尺寸缩放。缺失的窗口保留原图。
"""
import json
import os
import sys

import numpy as np
from PIL import Image

from dayart import paintover as po

COLS, ROWS = 4, 5
WIN = (960, 640)


def draft(build_out, out):
    g = Image.open(os.path.join(build_out, 'preview_noover.png')).convert('RGBA')
    c = Image.open(os.path.join(build_out, 'layer_canopy.png')).convert('RGBA').resize(g.size, Image.Resampling.NEAREST)
    g.alpha_composite(c)
    g.convert('RGB').resize((g.width // 2, g.height // 2), Image.Resampling.BOX).save(out)


def split(plate, d):
    os.makedirs(d, exist_ok=True)
    img = Image.open(plate).convert('RGB')
    W, H = img.size
    wins = po.windows(W, H, WIN[0], WIN[1], COLS, ROWS)
    for i, b in enumerate(wins):
        img.crop(b).save(os.path.join(d, f'win_{i:02d}.png'))
    json.dump({'size': [W, H], 'windows': wins}, open(os.path.join(d, 'windows.json'), 'w'))
    print(len(wins), 'windows')


def stitch(plate, d, out, keep_top=None):
    base = po.load_rgb(plate)
    meta = json.load(open(os.path.join(d, 'windows.json')))
    canvas = base.copy()
    filled = np.zeros(base.shape[:2], bool)
    log = []
    for i, (x0, y0, x1, y1) in enumerate(meta['windows']):
        p = os.path.join(d, f'gen_{i:02d}.png')
        ref = base[y0:y1, x0:x1]
        if not os.path.exists(p):
            log.append((i, 'missing'))
            patch = ref
        else:
            g = po.load_rgb(p, (x1 - x0, y1 - y0))
            dx, dy = po.register(ref, g)
            g = po.shift_img(g, dx, dy)
            g = po.color_match(g, ref, sigma=24)
            err = float(np.abs(g - ref).mean())
            log.append((i, dx, dy, round(err, 4)))
            patch = g
        po.quilt(canvas, filled, patch, (x0, y0, x1, y1))
    if keep_top:
        # 远景条保留原图：远处本该更虚，也避免细化把远岸画成近景
        a, b = keep_top
        w = np.clip((np.arange(canvas.shape[0]) - a) / max(1, b - a), 0, 1)[:, None, None]
        canvas = base * (1 - w) + canvas * w
    po.save_rgb(canvas, out)
    for row in log:
        print(*row)


if __name__ == '__main__':
    cmd = sys.argv[1]
    if cmd == 'draft':
        draft(sys.argv[2], sys.argv[3])
    elif cmd == 'split':
        split(sys.argv[2], sys.argv[3])
    else:
        kt = tuple(int(v) for v in sys.argv[5].split(',')) if len(sys.argv) > 5 else None
        stitch(sys.argv[2], sys.argv[3], sys.argv[4], kt)
